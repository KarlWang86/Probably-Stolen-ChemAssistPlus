using System;
using System.Collections.Generic;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        const double MergeConcentrationPoints = 5;

        static GameItem ParkSpareRawBottle(GameItem bottle, Recipe recipe)
        {
            if (WaterHelper.GetTotalVolume(bottle) != 0) return null;
            var machine = Scan().Where(x => x.IsID("evaporator") && MachineView.Walk(x).Container != null
                    && AcceptsEvaporatorRoute(x, recipe)
                    && MachineView.Walk(x).Container.childItem == null
                    && GraphUtils.CanAccept(MachineView.Walk(x).Container, bottle))
                .OrderBy(x => EvaporatorRoutePriority(x, recipe)).ThenByDescending(Charged).ThenBy(x => x.uniqueId).FirstOrDefault();
            if (machine != null) MovePot(bottle, MachineView.Walk(machine).Container);
            return machine;
        }

        static GameItem FindDryFinisher(GameItem pot, Recipe recipe)
        {
            return Scan().Where(x => x.IsID("chem_finisher") && MachineView.Walk(x).Container != null
                    && ((MachineView.Walk(x).Container.childItem == null && GraphUtils.CanAccept(MachineView.Walk(x).Container, pot))
                        || CanReceiveEmptyFinisher(x, pot)))
                .OrderBy(x => x.GetTagReadonly(MachineChemFinisher.SELECTED_CHEM_ID_TAG).GetString() == recipe.ProductId ? 0
                    : string.IsNullOrEmpty(x.GetTagReadonly(MachineChemFinisher.SELECTED_CHEM_ID_TAG).GetString()) ? 1 : 2)
                .ThenBy(x => x.uniqueId).FirstOrDefault();
        }

        static bool CanMergeDryFinisher(GameItem finisher, GameItem source, Recipe recipe)
        {
            var target = MachineView.Walk(finisher).Container?.childItem;
            if (finisher.GetTagReadonly(MachineChemFinisher.SELECTED_CHEM_ID_TAG).GetString() != recipe.ProductId) return false;
            if (target == null || target == source || !Owned(target) || !Owned(source)) return false;
            if (!source.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) || !target.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)) return false;
            if (source.IsTag(WaterHelper.FILTER_CONTAINER_TAG) || target.IsTag(WaterHelper.FILTER_CONTAINER_TAG)) return false;
            if (!ExtractedProduct(source, recipe) || !ExtractedProduct(target, recipe)) return false;
            if (NativeChem.WetTotal(source) > WaterHelper.DRY_TOLERANCE_UL
                || NativeChem.WetTotal(target) > WaterHelper.DRY_TOLERANCE_UL) return false;
            return WaterHelper.GetTotalVolume(source) <= WaterHelper.GetFreeCapacity(target);
        }

        // Stage dry material only at day boundaries, never from a preview or during another job.
        // Native dry tolerance includes all evaporative components, not just alcohol and water.
        static string StageDryEvaporatorBottles()
        {
            if (!autoProcessing || busy || !PlayerStore.IsInstanceExist()) return "";
            var messages = new List<string>();
            foreach (var machine in Scan().Where(x => x.IsID("evaporator")).OrderBy(x => x.uniqueId))
            {
                var pot = MachineView.Walk(machine).Container?.childItem;
                if (pot == null || !Owned(pot) || NativeChem.WetTotal(pot) > WaterHelper.DRY_TOLERANCE_UL) continue;
                var recipe = Detect(pot, null);
                if (!ExtractedProduct(pot, recipe)) continue;
                RememberEvaporatorRoute(machine, recipe);
                var finisher = Scan().Where(x => x.IsID("chem_finisher") && CanMergeDryFinisher(x, pot, recipe))
                    .OrderBy(x => x.uniqueId).FirstOrDefault() ?? FindDryFinisher(pot, recipe);
                int reportPotId = pot.uniqueId;
                string message;
                if (finisher == null)
                    message = "蒸发器 #" + machine.uniqueId + "：干料瓶 #" + pot.uniqueId + "（"
                        + ChemNames.ProductName(recipe.ProductId) + "）保留原位，没有可整瓶合并的同类干料瓶或可接收的空闲制剂机。";
                else
                {
                    try
                    {
                        message = MoveDryBottle(machine, finisher, pot, recipe, ref reportPotId);
                    }
                    catch (Exception error)
                    {
                        message = "干料移瓶中止：蒸发器 #" + machine.uniqueId + "，瓶 #" + pot.uniqueId
                            + " 当前位于 " + SceneSnapshot.Where(pot) + "；" + error.Message;
                        log?.Error(error.ToString());
                    }
                }
                messages.Add(message);
                log?.Log("[干料移瓶] " + message);
                foreach (var batch in nightDetails.Where(x => x.PotId == reportPotId)) batch.Detail += "\n" + message;
                nightDetails.Add(new BatchDisplay { Title = "蒸发器 #" + machine.uniqueId + " · 干料移瓶", Detail = message });
            }
            return string.Join("\n", messages);
        }

        // 返回日志正文；ref 保留转液成功后即更新报告瓶号的时机（后续记录仍可能异常）。
        static string MoveDryBottle(GameItem machine, GameItem finisher, GameItem pot, Recipe recipe, ref int reportPotId)
        {
            if (!Present(machine) || !Present(finisher) || MachineView.Walk(machine).Container?.childItem != pot
                || !ExtractedProduct(pot, recipe) || NativeChem.WetTotal(pot) > WaterHelper.DRY_TOLERANCE_UL)
                throw new InvalidOperationException("设备或干燥条件已变化，取消移瓶。");
            bool matched = finisher.GetTagReadonly(MachineChemFinisher.SELECTED_CHEM_ID_TAG).GetString() == recipe.ProductId;
            var target = MachineView.Walk(finisher).Container?.childItem;
            if (CanSwapLargerWorkBottle(pot, MachineView.Walk(finisher).Container))
            {
                SwapLargerWorkBottle(pot, MachineView.Walk(finisher).Container);
                finisher.ModifyTag(MachineChemFinisher.SELECTED_CHEM_ID_TAG, s => s.SetString(recipe.ProductId), false);
                nightDidWork = true;
                return "干料大瓶转入制剂机：" + Name(machine) + " #" + machine.uniqueId + " → 制剂机 #" + finisher.uniqueId
                    + "；工作瓶 #" + pot.uniqueId + "，有效成分 " + Vol(Amount(pot, recipe.ProductLiquid)) + "；下游空瓶换回原机器槽，待玩家制剂。";
            }
            if (target != null)
            {
                bool emptyReceiver = CanReceiveEmptyFinisher(finisher, pot);
                if (!emptyReceiver && !CanMergeDryFinisher(finisher, pot, recipe))
                    throw new InvalidOperationException("制剂机同类干料或容量条件已变化，取消合并。");
                int volume = WaterHelper.GetTotalVolume(pot);
                TransferWholeSolution(pot, target);
                if (emptyReceiver) finisher.ModifyTag(MachineChemFinisher.SELECTED_CHEM_ID_TAG, s => s.SetString(recipe.ProductId), false);
                nightDidWork = true;
                reportPotId = target.uniqueId;
                foreach (var batch in nightDetails.Where(x => x.PotId == pot.uniqueId)) batch.PotId = target.uniqueId;
                if (runPotId == pot.uniqueId) runPotId = target.uniqueId;
                return (emptyReceiver ? "干料转入制剂机空接收瓶：" : "干料合并至制剂机：") + Name(machine) + " #" + machine.uniqueId + " 的瓶 #" + pot.uniqueId
                    + " → 制剂机 #" + finisher.uniqueId + " 的瓶 #" + target.uniqueId
                    + "；" + ChemNames.ProductName(recipe.ProductId) + "，整瓶转移 " + Vol(volume)
                    + "，合并后有效成分 " + Vol(Amount(target, recipe.ProductLiquid))
                    + "；源空瓶留在原机器槽备用，待玩家制剂。";
            }
            MovePot(pot, MachineView.Walk(finisher).Container);
            nightDidWork = true;
            // A dry bottle is parked for the player to produce through the game's controls.
            finisher.ModifyTag(MachineChemFinisher.SELECTED_CHEM_ID_TAG, s => s.SetString(recipe.ProductId), false);
            return "干料转入制剂机：" + Name(machine) + " #" + machine.uniqueId + " → 制剂机 #" + finisher.uniqueId
                + "；工作瓶 #" + pot.uniqueId + "，" + ChemNames.ProductName(recipe.ProductId)
                + " " + Vol(Amount(pot, recipe.ProductLiquid)) + "，挥发液剩 " + Vol(NativeChem.WetTotal(pot))
                + (matched ? "；沿用已设置配方。" : "；已设置对应配方。") + "原机器容器槽已腾空，待玩家制剂。";
        }

        sealed class ConsolidationPlan
        {
            public GameItem SourceMachine, TargetMachine, Source, Target, WorkPot;
            public Recipe Recipe;
            public Recipe IncomingRecipe;
            public long IncomingVolume;
        }

        static bool CanConsolidate(ConsolidationPlan plan)
        {
            if (!Present(plan.SourceMachine) || !Present(plan.TargetMachine) || !Owned(plan.Source) || !Owned(plan.Target)
                || !Charged(plan.SourceMachine) || !Charged(plan.TargetMachine)) return false;
            // 两瓶必须仍在原机器，且不能占用本批工作瓶。
            if (MachineView.Walk(plan.SourceMachine).Container?.childItem != plan.Source
                || MachineView.Walk(plan.TargetMachine).Container?.childItem != plan.Target
                || plan.Source == plan.Target || plan.Source == plan.WorkPot || plan.Target == plan.WorkPot) return false;
            if (!plan.Source.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) || !plan.Target.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)
                || plan.Source.IsTag(WaterHelper.FILTER_CONTAINER_TAG) || plan.Target.IsTag(WaterHelper.FILTER_CONTAINER_TAG)) return false;
            // 同类湿原液及设备分工分别复核。
            if (!ExtractedProduct(plan.Source, plan.Recipe) || !ExtractedProduct(plan.Target, plan.Recipe)
                || !AcceptsEvaporatorRoute(plan.SourceMachine, plan.IncomingRecipe)
                || !AcceptsEvaporatorRoute(plan.TargetMachine, plan.Recipe)) return false;
            if (NativeChem.WetTotal(plan.Source) <= WaterHelper.DRY_TOLERANCE_UL
                || NativeChem.WetTotal(plan.Target) <= WaterHelper.DRY_TOLERANCE_UL
                || WaterHelper.GetTotalVolume(plan.Source) > WaterHelper.GetFreeCapacity(plan.Target)) return false;
            // Keep the emptied source bottle installed as the next batch's receiver.
            return plan.IncomingVolume > 0 && WaterHelper.GetTotalCapacity(plan.Source) >= plan.IncomingVolume;
        }

        static ConsolidationPlan PlanConsolidation(GameItem workPot, long incomingVolume, Recipe incomingRecipe)
        {
            var all = Scan();
            var machines = all.Where(x => x.IsID("evaporator") && Charged(x)).OrderBy(x => x.uniqueId).ToList();
            foreach (var sourceMachine in machines.Where(x => MachineView.Walk(x).Container?.childItem != null)
                .OrderBy(x => WaterHelper.GetTotalVolume(MachineView.Walk(x).Container.childItem)))
            {
                var source = MachineView.Walk(sourceMachine).Container?.childItem;
                if (source == null || source == workPot) continue;
                var recipe = Detect(source, null);
                if (!ExtractedProduct(source, recipe)) continue;
                foreach (var targetMachine in machines.Where(x => x != sourceMachine))
                {
                    var plan = new ConsolidationPlan { SourceMachine = sourceMachine, TargetMachine = targetMachine,
                        Source = source, Target = MachineView.Walk(targetMachine).Container?.childItem,
                        WorkPot = workPot, Recipe = recipe, IncomingRecipe = incomingRecipe, IncomingVolume = incomingVolume };
                    if (CanConsolidate(plan)) return plan;
                }
            }
            return null;
        }

        static string ConsolidationSummary(ConsolidationPlan plan) => "同类存量集中：蒸发器 #" + plan.SourceMachine.uniqueId
            + " 的瓶 #" + plan.Source.uniqueId + " → 蒸发器 #" + plan.TargetMachine.uniqueId + " 的瓶 #" + plan.Target.uniqueId
            + "（" + ChemNames.ProductName(plan.Recipe.ProductId) + "；允许浓度不同）；源空瓶留在原液槽，"
            + "容量 " + Vol(WaterHelper.GetTotalCapacity(plan.Source)) + "，供下一批预计 " + Vol(plan.IncomingVolume) + " 原液接收。";

        static void ConsolidateEvaporators(ConsolidationPlan plan)
        {
            if (!CanConsolidate(plan)) throw new InvalidOperationException("同类存量集中条件变化，尚未转液或搅拌，请重新预检。");
            int volume = WaterHelper.GetTotalVolume(plan.Source);
            TransferWholeSolution(plan.Source, plan.Target);
            RememberEvaporatorRoute(plan.SourceMachine, plan.Recipe);
            RememberEvaporatorRoute(plan.TargetMachine, plan.Recipe);
            // Redirect old batch snapshots before the emptied receiver is reused for another product.
            foreach (var batch in nightDetails.Where(x => x.PotId == plan.Source.uniqueId))
            {
                batch.PotId = plan.Target.uniqueId;
                batch.Detail += "\n" + ConsolidationSummary(plan);
            }
            if (runPotId == plan.Source.uniqueId) runPotId = plan.Target.uniqueId;
            if (MachineView.Walk(plan.SourceMachine).Container.childItem != plan.Source || WaterHelper.GetTotalVolume(plan.Source) != 0)
                throw new InvalidOperationException("同类原液已集中，但原液槽空接收瓶状态异常；尚未搅拌。");
            Trace(ConsolidationSummary(plan) + " 实际转液 " + Vol(volume) + "，接收瓶现有 " + Vol(WaterHelper.GetTotalVolume(plan.Target)) + "。");
        }

        static double ProductConcentration(GameItem pot, Recipe recipe) =>
            100d * Amount(pot, recipe.ProductLiquid) / Math.Max(1, WaterHelper.GetTotalVolume(pot));

        static bool ExtractedProduct(GameItem pot, Recipe recipe) => pot != null && recipe != null
            && Detect(pot, null) == recipe && AlreadyExtracted(pot, recipe);

        static bool CanReceiveBatch(GameItem machine, GameItem pot, Recipe recipe, long volume, bool planning)
        {
            if (!AcceptsEvaporatorRoute(machine, recipe)) return false;
            var slot = MachineView.Walk(machine).Container;
            if (pot == null || slot == null) return false;
            var target = slot.childItem;
            if (target == null || target == pot) return target == pot || GraphUtils.CanAccept(slot, pot);
            if (CanSwapLargerWorkBottle(pot, slot, volume)) return true;
            if (!Owned(target) || !target.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)
                || target.IsTag(WaterHelper.FILTER_CONTAINER_TAG) || volume <= 0
                || WaterHelper.GetFreeCapacity(target) < volume) return false;
            // A real empty bottle already installed in the input slot can also receive the batch.
            if (WaterHelper.GetTotalVolume(target) == 0) return true;
            if (!ExtractedProduct(target, recipe) || NativeChem.WetTotal(target) <= WaterHelper.DRY_TOLERANCE_UL) return false;
            // Purification changes concentration. Preflight only admits a possible same-product receiver;
            // the final transfer rechecks the actual purified liquid, capacity and power synchronously.
            return planning || (ExtractedProduct(pot, recipe)
                && Math.Abs(ProductConcentration(target, recipe) - ProductConcentration(pot, recipe)) <= MergeConcentrationPoints + 1e-9);
        }

        static GameItem FindEvaporator(GameItem pot, Recipe recipe = null, long volume = -1, bool planning = false)
        {
            recipe = recipe ?? Detect(pot, null);
            if (volume < 0) volume = pot == null ? 0 : WaterHelper.GetTotalVolume(pot);
            return Scan().Where(x => x.IsID("evaporator") && Charged(x) && CanReceiveBatch(x, pot, recipe, volume, planning))
                .OrderBy(x => EvaporatorRoutePriority(x, recipe))
                .ThenByDescending(x => MachineView.Walk(x).Container.childItem != null)
                .ThenBy(x => x.uniqueId).FirstOrDefault();
        }

        static string EvaporatorBlockReason(GameItem pot, Recipe recipe = null, long volume = -1, bool planning = false)
        {
            recipe = recipe ?? Detect(pot, null);
            if (volume < 0) volume = pot == null ? 0 : WaterHelper.GetTotalVolume(pot);
            var machines = Scan().Where(x => x.IsID("evaporator")).ToList();
            if (machines.Count == 0) return "缺蒸发器。";
            var routed = machines.Where(x => AcceptsEvaporatorRoute(x, recipe)).ToList();
            if (routed.Count == 0) return "没有对应配方或尚未分配的蒸发器；其他设备保留原料记忆。请在空槽或空瓶蒸发器标题栏点击“原液分工”设置用途，或手动放入对应单一原液调整用途。";
            var compatible = machines.Where(x => CanReceiveBatch(x, pot, recipe, volume, planning)).ToList();
            if (compatible.Count == 0 && routed.All(x => MachineView.Walk(x).Container?.childItem != null))
            {
                string bottles = string.Join("；", routed.Select(x =>
                {
                    var bottle = MachineView.Walk(x).Container.childItem;
                    return "蒸发器 #" + x.uniqueId + " / 瓶 #" + bottle.uniqueId + "：容量 "
                        + Vol(WaterHelper.GetTotalCapacity(bottle)) + "，剩余 " + Vol(WaterHelper.GetFreeCapacity(bottle));
                }));
                return "本批" + (planning ? "保守接收预留 " : "实际接收量 ") + Vol(volume) + "；" + bottles
                    + "。接收瓶须空间足够；非空瓶还须为可合并的同配方湿料。可更换大空瓶，或取走空瓶后由工作瓶整瓶入槽；原液分工可在蒸发器标题栏调整。";
            }
            if (compatible.Count == 0) return planning
                ? "对应蒸发器没有可用空槽或整批接收瓶；保留其他设备的原料记忆。请等待原批次完成，或检查本配方接收瓶容量、电池及同类存量集中条件。"
                : "对应蒸发器没有可用空槽或可整批接收的容器；不借用其他原料设备。合并须同种已提取湿原液、浓度差≤5个百分点且空间足够。";
            if (compatible.All(x => NativeChem.FindBattery(x) == null)) return "蒸发器缺电池，请装入电池。";
            if (!compatible.Any(Charged)) return "蒸发器电池没电或电量不足以启动一次，请更换/充电。";
            return "蒸发器状态已变化，暂时不能接收原液。";
        }

        static GameItem DeliverToEvaporator(GameItem machine, GameItem source, Recipe recipe)
        {
            int volume = WaterHelper.GetTotalVolume(source);
            if (!Present(machine) || !Charged(machine) || !CanReceiveBatch(machine, source, recipe, volume, false))
                throw new InvalidOperationException("蒸发器接收条件变化，原液保留在工作瓶。");
            var slot = MachineView.Walk(machine).Container;
            var target = slot.childItem;
            if (CanSwapLargerWorkBottle(source, slot))
            {
                SwapLargerWorkBottle(source, slot);
                RememberEvaporatorRoute(machine, recipe);
                return source;
            }
            if (target == null || target == source)
            {
                MovePot(source, slot);
                RememberEvaporatorRoute(machine, recipe);
                MarkAutoWork();
                return source;
            }
            double sourceConcentration = ProductConcentration(source, recipe);
            double targetConcentration = ProductConcentration(target, recipe);
            TransferWholeSolution(source, target);
            RememberEvaporatorRoute(machine, recipe);
            var spareMachine = ParkSpareRawBottle(source, recipe);
            Trace("  同类合并：蒸发器 #" + machine.uniqueId + "；源瓶 #" + source.uniqueId + " → 接收瓶 #" + target.uniqueId
                + "；" + ChemNames.ProductName(recipe.ProductId) + "，本批 " + Vol(volume)
                + "；有效成分浓度 " + sourceConcentration.ToString("0.###") + "% / " + targetConcentration.ToString("0.###")
                + "% → " + ProductConcentration(target, recipe).ToString("0.###") + "%；源空瓶"
                + (spareMachine != null ? "放入蒸发器 #" + spareMachine.uniqueId + " 原液槽，作为备用接收瓶。" : "留在搅拌机（其他原液槽均已占用）。"));
            return target;
        }

        static void TransferWholeSolution(GameItem source, GameItem target)
        {
            TransferSolution(source, target, WaterHelper.GetTotalVolume(source));
        }

        static void TransferSolution(GameItem source, GameItem target, int volume)
        {
            // Native transfer/reaction APIs read modifiedState. Refresh it only when
            // executing a real transfer, never from a preview or a planning snapshot.
            source.ValidateShapeState(); target.ValidateShapeState();
            int sourceVolume = WaterHelper.GetTotalVolume(source);
            if (source == target || volume <= 0 || volume > sourceVolume || WaterHelper.GetFreeCapacity(target) < volume)
                throw new InvalidOperationException("整瓶转液容量或来源变化，尚未转液。");
            double temperature = SceneSnapshot.Temp(source);
            int targetVolume = WaterHelper.GetTotalVolume(target);
            var before = WaterHelper.GetLiquidParts(target);
            // No yield while liquid is in transit. Recover only components not actually added if a native call fails.
            var taken = WaterHelper.RemoveAndReturn(source, volume);
            if (taken.Any(x => x.Value > 0)) MarkAutoWork();
            try { AddSolution(target, taken, temperature); }
            finally
            {
                var untransferred = new List<KeyValuePair<string, int>>();
                foreach (var part in taken)
                {
                    int index = Liquid.Liquids.FindIndex(x => x.identifier == part.Key);
                    int delivered = Math.Max(0, Amount(target, part.Key) - (index < 0 ? 0 : before[index]));
                    int remaining = Math.Max(0, part.Value - delivered);
                    if (remaining > 0) untransferred.Add(new KeyValuePair<string, int>(part.Key, remaining));
                }
                AddSolution(source, untransferred, temperature);
                WaterHelper.UpdateContainerGoodType(source);
                WaterHelper.UpdateContainerGoodType(target);
            }
            if (WaterHelper.GetTotalVolume(source) != sourceVolume - volume || WaterHelper.GetTotalVolume(target) != targetVolume + volume)
                throw new InvalidOperationException("合并转移未完整完成，已停止；请检查源瓶和蒸发器内接收瓶。");
        }
    }
}

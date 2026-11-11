using System;
using System.Collections.Generic;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        const double MergeConcentrationPoints = 5;

        static double ProductConcentration(GameItem pot, Recipe recipe) =>
            100d * Amount(pot, recipe.ProductLiquid) / Math.Max(1, WaterHelper.GetTotalVolume(pot));

        static bool ExtractedProduct(GameItem pot, Recipe recipe) => pot != null && recipe != null
            && Detect(pot, null) == recipe && AlreadyExtracted(pot, recipe);

        static bool CanReceiveBatch(GameItem machine, GameItem pot, Recipe recipe, long volume, bool planning)
        {
            var slot = MachineView.Walk(machine).Container;
            if (pot == null || slot == null) return false;
            var target = slot.childItem;
            if (target == null || target == pot) return target == pot || GraphUtils.CanAccept(slot, pot);
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
                .OrderByDescending(x => MachineView.Walk(x).Container.childItem != null)
                .ThenBy(x => x.uniqueId).FirstOrDefault();
        }

        static string EvaporatorBlockReason(GameItem pot, Recipe recipe = null, long volume = -1, bool planning = false)
        {
            recipe = recipe ?? Detect(pot, null);
            if (volume < 0) volume = pot == null ? 0 : WaterHelper.GetTotalVolume(pot);
            var machines = Scan().Where(x => x.IsID("evaporator")).ToList();
            if (machines.Count == 0) return "缺蒸发器。";
            var compatible = machines.Where(x => CanReceiveBatch(x, pot, recipe, volume, planning)).ToList();
            if (compatible.Count == 0) return planning
                ? "蒸发器没有可用空槽，也没有同类已提取湿半成品或空瓶能容纳预计整批原液。请空出原液槽或换大瓶。"
                : "蒸发器没有可用空槽或可整批接收的容器；合并仅限同种已提取湿半成品、有效成分浓度差≤5个百分点且空间足够，干料和不同配方不混合。";
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
            if (target == null || target == source)
            {
                MovePot(source, slot);
                MarkAutoWork();
                return source;
            }
            double sourceConcentration = ProductConcentration(source, recipe);
            double targetConcentration = ProductConcentration(target, recipe);
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
            if (WaterHelper.GetTotalVolume(source) != 0 || WaterHelper.GetTotalVolume(target) != targetVolume + volume)
                throw new InvalidOperationException("合并转移未完整完成，已停止；请检查源瓶和蒸发器内接收瓶。");
            Trace("  同类合并：蒸发器 #" + machine.uniqueId + "；源瓶 #" + source.uniqueId + " → 接收瓶 #" + target.uniqueId
                + "；" + ChemNames.ProductName(recipe.ProductId) + "，本批 " + Vol(volume)
                + "；有效成分浓度 " + sourceConcentration.ToString("0.###") + "% / " + targetConcentration.ToString("0.###")
                + "% → " + ProductConcentration(target, recipe).ToString("0.###") + "%；源空瓶留在搅拌机。");
            return target;
        }
    }
}

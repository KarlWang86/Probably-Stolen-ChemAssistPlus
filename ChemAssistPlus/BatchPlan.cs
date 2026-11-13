using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class BatchPlan
        {
            public int[] Full, Kept;
            public int KeepVolume, TotalCount;
            public bool Limited;
            public long Peak, RawPeak, SolutionPeak, TabletReserve;
            public ConsolidationPlan Consolidation;
            public readonly List<GameItem> Selected = new List<GameItem>();
            public string Solvent;
            public long FullNeed, Stock;
            public readonly List<string> Errors = new List<string>(), Lines = new List<string>();
            public readonly List<SupplySnapshot> Supplies = new List<SupplySnapshot>();
        }
        static string Good(string text) => "<color=#BCE89A>" + text + "</color>";
        static string Warn(string text) => "<color=#FFD36B>" + text + "</color>";
        static string Bad(string text) => "<color=#FF8585>" + text + "</color>";
        static string SupplyName(string id) => id == "alcohol" ? "酒精" : id == "acid" ? "酸液" : id == "base" ? "碱液"
            : id == "lipobond_agent" ? "脂合剂" : id == "reducing_agent" ? "还原剂" : LiquidName(id);
        static void SelectWorkingBlender()
        {
            if (busy || contextBlender != null) return;
            var candidates = Scan().Where(x => x.IsID("blender")).ToList();
            selectedBlender = candidates.FirstOrDefault(x => FirstKind(MachineView.Walk(x).Input).Count > 0)
                ?? candidates.FirstOrDefault(x => MachineView.Walk(x).Container?.childItem != null)
                ?? candidates.FirstOrDefault();
        }
        static int Part(int[] parts, string id)
        {
            int index = Liquid.Liquids.FindIndex(x => x.identifier == id);
            return index < 0 ? 0 : parts[index];
        }
        static List<GameItem> FirstKind(GameInventory input)
        {
            var all = NativeChem.Blendables(input);
            var first = all.FirstOrDefault();
            return first == null ? all : all.Where(x => x.IsID(first.identifier)).ToList();
        }
        static bool LooseBottle(GameItem item)
        {
            if (item == null || !(item.parentInventory is GameGridInventory)
                || !item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) || item.IsTag(WaterHelper.FILTER_CONTAINER_TAG)) return false;
            var parent = item.parentInventory.GetParentItem();
            for (int depth = 0; parent != null && depth < 24; depth++, parent = parent.parentInventory?.GetParentItem())
                if (parent.IsTag("MACHINE")) return false;
            return true;
        }
        static int[] BatchAmounts(NativeChem.ProbeResult p, IEnumerable<GameItem> selected = null)
        {
            double retained = 1 - MachineBlender.GetLoss(p.Blender) / 100d;
            return Liquid.Liquids.Select(l => checked(Amount(p.Pot, l.identifier) + (selected ?? p.Solids).Sum(x =>
                checked((int)Math.Round(Amount(x, l.identifier) * retained, MidpointRounding.AwayFromZero))))).ToArray();
        }
        static int SolventNeed(Recipe recipe, int[] parts)
        {
            if (recipe.FeedLiquids.Length < 2) return 0;
            string solvent = recipe.FeedLiquids[1];
            int feed = Part(parts, recipe.FeedLiquids[0]);
            long target = SolventTargetForParts(recipe, feed, solvent, Part(parts, "stabilizing_agent"));
            return checked((int)Math.Max(0, target - Part(parts, solvent)));
        }
        static BatchPlan PlanWholeMaterials(NativeChem.ProbeResult p, int limit)
        {
            var plan = new BatchPlan { TotalCount = p.Solids.Count,
                Solvent = p.Recipe.FeedLiquids.Length > 1 ? p.Recipe.FeedLiquids[1] : null };
            plan.Stock = plan.Solvent == null ? 0 : Sources(plan.Solvent, p.Pot).Sum(x => (long)EffectiveAmount(x, plan.Solvent));
            plan.FullNeed = SolventNeed(p.Recipe, BatchAmounts(p));
            plan.Selected.AddRange(p.Solids);
            if (plan.Solvent == "alcohol" && plan.FullNeed > plan.Stock)
            {
                plan.Selected.Clear();
                foreach (var material in p.Solids)
                {
                    var candidate = plan.Selected.Concat(new[] { material });
                    if (SolventNeed(p.Recipe, BatchAmounts(p, candidate)) > plan.Stock) break;
                    plan.Selected.Add(material);
                }
                plan.Limited = plan.Selected.Count < p.Solids.Count;
                if (plan.Selected.Count == 0 && p.Solids.Count > 0)
                    plan.Errors.Add("酒精不足以处理一整份原料，固体原料保留；补充后再处理。");
            }
            if (plan.Selected.Count > limit) plan.Selected.RemoveRange(limit, plan.Selected.Count - limit);
            plan.Limited = plan.Selected.Count < p.Solids.Count;
            plan.Full = plan.Kept = BatchAmounts(p, plan.Selected);
            plan.KeepVolume = plan.Kept.Sum();
            return plan;
        }
        static BatchPlan BuildBatchPlan(NativeChem.ProbeResult p)
        {
            var plan = BuildBatchPlanForCount(p, p.Solids.Count);
            // Recompute real-concentration pours and reagent space for each whole-item prefix.
            while (p.Pot != null && plan.Peak > WaterHelper.GetTotalCapacity(p.Pot) && plan.Selected.Count > 1)
                plan = BuildBatchPlanForCount(p, plan.Selected.Count - 1);
            return plan;
        }
        // 只更新预估数组：扣除来源份额，并逐项累计目标瓶成分；溢出规则与实际规划一致。
        static void ApplyProjectedPour(int[] remaining, int[] projected, int[] taken)
        {
            for (int i = 0; i < remaining.Length; i++)
            {
                remaining[i] -= taken[i];
                projected[i] = checked(projected[i] + taken[i]);
            }
        }

        static BatchPlan BuildBatchPlanForCount(NativeChem.ProbeResult p, int limit)
        {
            var plan = PlanWholeMaterials(p, limit);
            var projected = (int[])plan.Kept.Clone();
            var remainingSources = new Dictionary<GameItem, int[]>();
            long addedVolume = 0;
            int Estimate(string id, int need)
            {
                int missing = need, volume = 0;
                long stock = Sources(id, p.Pot).Sum(x => (long)EffectiveAmount(x, id));
                long stockVolume = Sources(id, p.Pot).Sum(x => (long)WaterHelper.GetTotalVolume(x));
                foreach (var source in Sources(id, p.Pot))
                {
                    if (!remainingSources.TryGetValue(source, out var parts)) remainingSources[source] = parts = WaterHelper.GetLiquidParts(source);
                    for (int attempt = 0; missing > 0 && attempt < 3; attempt++)
                    {
                        int active = EffectivePart(parts, id), total = parts.Sum();
                        if (active == 0 || total == 0) break;
                        int pour = (int)Math.Min(total, DoseMath.RequiredSolution(missing, active, total));
                        var taken = WaterHelper.SplitProportionally(parts, pour);
                        int delivered = EffectivePart(taken, id);
                        ApplyProjectedPour(parts, projected, taken);
                        volume += pour; missing -= delivered;
                        if (delivered <= 0) break;
                    }
                    if (missing <= 0) break;
                }
                addedVolume += volume;
                plan.Supplies.Add(new SupplySnapshot { Id = id, Name = SupplyName(id), Available = stockVolume,
                    Required = missing <= 0 ? volume : stock > 0 ? need * (double)stockVolume / stock : 0,
                    Known = need == 0 || stock > 0, Short = missing > 0, Limited = id == "alcohol" && plan.Limited });
                string line = LiquidName(id) + "：约 " + Vol(volume) + " 原溶液（有效 " + Vol(need) + "；库存有效 " + Vol(stock) + "）";
                plan.Lines.Add(missing > 0 ? Bad(LiquidName(id) + "：需有效 " + Vol(need) + "；库存有效 " + Vol(stock) + "，不足 " + Vol(missing)) : Good(line));
                if (missing > 0) plan.Errors.Add(LiquidName(id) + "不足 " + Vol(missing));
                return missing;
            }
            if (plan.Solvent != null) Estimate(plan.Solvent, SolventNeed(p.Recipe, plan.Kept));
            else plan.Lines.Add("此配方无需提取溶剂。");
            long protein = DoseMath.ProteinNeed(Part(projected, "protein"), Part(projected, "acid"), Part(projected, "base"));
            string reagent = ProteinReagent(p.Pot, Part(projected, "acid"), Part(projected, "base"), protein);
            Estimate(reagent, checked((int)protein));
            Estimate("lipobond_agent", Math.Max(0, (Part(projected, "fat") + 7) / 8 - Part(projected, "lipobond_agent")));
            var tabletItems = Tablets();
            int tablets = tabletItems.Count, bottledTablets = tabletItems.Count(InTabletBottle);
            // Reactions and single-pass filtration run before dosing; raw components cannot predict final demand.
            plan.Supplies.Add(new SupplySnapshot { Id = "water_tablet", Name = "净水药片", Available = tablets,
                Tablets = true });
            plan.Lines.Add("净水药片：过滤后按需使用；库存 " + tablets + " 片（散片 " + (tablets - bottledTablets)
                + "，瓶内 " + bottledTablets + "；散片优先）。实际用量及未投片原因见处理结果。");
            if (p.Pot == null) plan.Errors.Add("请准备工作瓶。");
            else
            {
                long rawPeak = WaterHelper.GetTotalVolume(p.Pot) + plan.Selected.Sum(x => (long)ChemicalHelper.GetTotalVolume(x));
                plan.RawPeak = rawPeak;
                plan.SolutionPeak = plan.KeepVolume + addedVolume;
                plan.TabletReserve = tablets > 0 ? 15000 : 0;
                long peak = Math.Max(rawPeak, plan.SolutionPeak + plan.TabletReserve);
                plan.Peak = peak;
                if (peak > WaterHelper.GetTotalCapacity(p.Pot)) plan.Errors.Add("工作瓶容量不足：" + Name(p.Pot) + " #" + p.Pot.uniqueId
                    + "，容量 " + Vol(WaterHelper.GetTotalCapacity(p.Pot)) + "；预计至少 " + Vol(peak) + "，请换大瓶。");
            }
            if (p.Recipe.ProductId == "fanta_pill" && Part(plan.Kept, "methylol") <= Part(plan.Kept, "stabilizing_agent")
                && Part(plan.Kept, p.Recipe.ProductLiquid) == 0) plan.Errors.Add("稳定剂不低于前体，原生反应不会启动。");
            if (p.Recipe.High > 0 && (p.Solids.Count > 0 || ReactableFeed(p.Pot, p.Recipe) > 0))
            {
                var plate = FindPlate(p.Recipe);
                if (plate == null) plan.Errors.Add("缺空闲且关闭的加热板。");
                else if (MachineBlender.GetUpgradeCount(plate) < 2 && !HeatingHookReady()) plan.Errors.Add("控温补丁未加载。");
                if (p.Pot != null && SceneSnapshot.Temp(p.Pot) > p.Recipe.High) plan.Errors.Add("原液温度过高，请先冷却。");
            }
            long distillationVolume = plan.KeepVolume + addedVolume + (tablets > 0 ? 15000 : 0);
            if (p.Pot != null)
            {
                var receiver = FindEvaporator(p.Pot, p.Recipe, distillationVolume, true);
                if (receiver == null)
                {
                    plan.Consolidation = PlanConsolidation(p.Pot, distillationVolume, p.Recipe);
                    if (plan.Consolidation == null) plan.Errors.Add(EvaporatorBlockReason(p.Pot, p.Recipe, distillationVolume, true));
                }
                else
                {
                    plan.Lines.Add(EvaporatorRouteSummary(receiver, p.Recipe));
                    if (MachineView.Walk(receiver).Container.childItem != null
                        && MachineView.Walk(receiver).Container.childItem != p.Pot)
                        plan.Lines.Add("蒸发器 #" + receiver.uniqueId + " 可接收同类原液；净化后复核浓度差≤5个百分点及实际容量，不合格则保留本瓶。");
                }
            }
            string filterKey = PreferredFilterKey();
            if (filterKey == null) plan.Errors.Add(NoFilterReason());
            else plan.Lines.Add(PlannedFilterName(filterKey));
            return plan;
        }
        static string PlanSummary(NativeChem.ProbeResult p, BatchPlan plan)
        {
            var text = new StringBuilder();
            text.AppendLine(ChemNames.ProductName(p.Recipe.ProductId) + (p.Solids.Count > 0 ? " · " + p.Solids.Count + " 份" + Name(p.Solid) : " · 半成品"));
            if (p.Pot != null)
                text.AppendLine("工作瓶：" + Name(p.Pot) + " #" + p.Pot.uniqueId + " · 容量 " + Vol(WaterHelper.GetTotalCapacity(p.Pot))
                    + (p.AutoPot ? "（拟自动装入空容器）" : "（容器槽）"));
            if (plan.Limited)
            {
                text.AppendLine(Warn("按酒精库存及瓶容量限量 · 本次 " + plan.Selected.Count + "/" + plan.TotalCount + " 份，其余固体留在输入格。"));
            }
            if (plan.Errors.Count > 0) text.AppendLine(Bad("暂不能开工：" + string.Join("；", plan.Errors)));
            if (plan.Consolidation != null) text.AppendLine(Warn("开工前拟" + ConsolidationSummary(plan.Consolidation)));
            if (p.Pot != null && plan.Peak > WaterHelper.GetTotalCapacity(p.Pot))
                text.AppendLine("容量估算：原料瞬时 " + Vol(plan.RawPeak) + "；搅拌液及试剂 " + Vol(plan.SolutionPeak)
                    + " + 药片预留 " + Vol(plan.TabletReserve) + "，取较大值。");
            return text.ToString();
        }
    }
}

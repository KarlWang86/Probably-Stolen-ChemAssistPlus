using System;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static string ProteinReagent(GameItem pot, long acid, long alkali, long need)
        {
            if (acid != alkali) return acid > alkali ? "acid" : "base";
            var candidates = new[] { "acid", "base" }.Select(id => new { Id = id, Items = Sources(id, pot).ToList() }).ToList();
            return candidates.OrderByDescending(x => x.Items.Sum(i => (long)EffectiveAmount(i, x.Id)) >= need)
                .ThenByDescending(x => x.Items.Count == 0 ? 0 : (double)EffectiveAmount(x.Items[0], x.Id) / Math.Max(1, WaterHelper.GetTotalVolume(x.Items[0])))
                .First().Id;
        }
        public static string Preflight(Recipe chain, GameItem solid, GameItem pot, bool prep, bool overnight)
        {
            contextPot = pot; contextChain = chain;
            if (!prep)
            {
                string problem = null;
                var finisher = FindId("chem_finisher");
                var recipe = ChemFinisherRecipe.GetRecipeById(chain.ProductId);
                if (solid != null || !AlreadyExtracted(pot, chain)) problem = "请放入已提取的干料瓶。";
                else if (NativeChem.WetTotal(pot) > WaterHelper.DRY_TOLERANCE_UL) problem = "尚未干燥，请继续过夜蒸发。";
                else if (finisher == null || !SlotFree(finisher) || !NativeControls.HasProduceAction(finisher)) problem = "缺可用成型机。";
                else if (recipe == null || !recipe.CanOutput(pot)) problem = "原液不足以制作一件成品。";
                else if (!NativeControls.CanFitProduct(finisher, chain.ProductId)) problem = "成型机输出空间不足。";
                lastReport = problem == null ? Good("原液已干燥，无需添加溶剂。") : Bad(problem);
                return problem;
            }
            var blender = contextMachine ?? selectedMachine;
            var solids = solid == null ? new System.Collections.Generic.List<GameItem>() : FirstKind(MachineView.Walk(blender).Input);
            var p = new NativeChem.ProbeResult { Machine = blender, Pot = pot, Recipe = chain, Solid = solids.FirstOrDefault(), Solids = solids,
                Parts = MachineView.Walk(blender), AutoPot = pot != null && MachineView.Walk(blender).Container?.childItem != pot };
            var plan = BuildBatchPlan(p);
            lastCheckedPlan = plan;
            lastReport = PlanSummary(p, plan);
            string message = "[预检] " + ChemNames.ProductName(chain.ProductId)
                + (IsFanta(chain) ? "；加热器 #" + blender.uniqueId + " 直接处理前体液，不搅拌"
                    : "；本次原料 " + plan.Selected.Count + "/" + solids.Count + " 份（余料保留固体）")
                + (pot == null ? "" : "；工作瓶 " + Name(pot) + " #" + pot.uniqueId + "，容量 " + Vol(WaterHelper.GetTotalCapacity(pot))
                    + (p.AutoPot ? "（拟自动装入）" : "（容器槽）") + "；原料瞬时 " + Vol(plan.RawPeak)
                    + "，本瓶及试剂 " + Vol(plan.SolutionPeak) + "，药片预留 " + Vol(plan.TabletReserve))
                + (plan.Consolidation == null ? "" : "；开工前拟" + ConsolidationSummary(plan.Consolidation))
                + "；" + (plan.Errors.Count == 0 ? "通过" : string.Join("；", plan.Errors));
            if (message != lastPreflightLog) { log?.Log(message); lastPreflightLog = message; }
            return plan.Errors.Count == 0 ? null : string.Join("；", plan.Errors);
        }
    }
}

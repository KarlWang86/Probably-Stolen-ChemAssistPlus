using System;
using System.Linq;
using System.Text;
using HarmonyLib;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static bool showingNightResult;
        static string lastNightResult;
        static void RefreshInventoryReport()
        {
            if (busy || !PlayerStore.IsInstanceExist()) return;
            showingNightResult = false;
            previewDetails.Clear(); displayNightDetails = false;
            lastFailure = null;
            try
            {
                foreach (var blender in Scan().Where(x => x.IsID("blender") && HasPendingBatch(x)).OrderBy(x => x.uniqueId))
                {
                    selectedBlender = contextBlender = blender; contextPot = null; contextChain = null;
                    if (WaitingForDistillation(blender))
                    {
                        previewDetails.Add(new BatchDisplay { Title = Name(blender) + " #" + blender.uniqueId,
                            Detail = "已有批次待蒸发。" });
                        continue;
                    }
                    var p = Probe();
                    if (p.Recipe == null || p.Pot == null)
                    {
                        previewDetails.Add(new BatchDisplay { Title = Name(blender) + " #" + blender.uniqueId,
                            Detail = Bad(p.Block ?? "请准备原料与工作瓶。") });
                        continue;
                    }
                    PrepareRoute(p.Recipe); contextPot = p.Pot; contextChain = p.Recipe;
                    if (p.Solid == null && AlreadyExtracted(p.Pot, p.Recipe)
                        && NativeChem.WetTotal(p.Pot) <= WaterHelper.DRY_TOLERANCE_UL)
                    {
                        Preflight(p.Recipe, null, p.Pot, false, false);
                        var stages = new StageSnapshot[StageCount];
                        stages[4] = Snapshot("实际", p.Recipe.ProductLiquid, WaterHelper.GetLiquidParts(p.Pot));
                        previewDetails.Add(new BatchDisplay { Title = Name(blender) + " #" + blender.uniqueId,
                            Detail = lastReport, Stages = stages });
                    }
                    else PreviewComposition(p, BuildBatchPlan(p));
                }
                SetReport(previewDetails.Count > 0 ? "各搅拌机独立预估；结束当天按顺序重新检查库存。" : "没有待处理原料。\n" + EvaporatorContents());
            }
            catch (Exception error) { SetReport(Bad("用量检查失败：" + error.Message)); log?.Error(error.ToString()); }
            finally { contextBlender = contextPot = null; contextChain = null; }
        }

        static string EvaporatorContents()
        {
            var text = new StringBuilder();
            foreach (var machine in Scan().Where(x => x.IsID("evaporator")))
            {
                var pot = MachineView.Walk(machine).Container?.childItem;
                if (pot == null) continue;
                foreach (var recipe in RecipeCatalog.Recipes)
                    if (Amount(pot, recipe.ProductLiquid) > 0)
                    {
                        int wet = NativeChem.WetTotal(pot);
                        text.AppendLine("蒸发器 #" + machine.uniqueId + " · 原液槽工作瓶 #" + pot.uniqueId + "：" + LiquidName(recipe.ProductLiquid) + " " + Vol(Amount(pot, recipe.ProductLiquid))
                            + (wet <= WaterHelper.DRY_TOLERANCE_UL ? "，已干燥，可取瓶成型。" : "；挥发液剩 " + Vol(wet) + "，继续过夜。"));
                    }
            }
            return text.ToString();
        }
        internal static void ReportAfterOvernight()
        {
            try
            {
                if (!PlayerStore.IsInstanceExist()) return;
                SchedulePowerReminder();
                string report = EvaporatorContents();
                if (report.Length > 0) log?.Log("[过夜产物核对]\n" + report);
                if (nightDidWork && nightReport != null)
                {
                    CaptureOvernightStage();
                    lastNightResult = nightReport;
                    nightDidWork = false;
                    if (awaitingMorningReport && showMorningPopup)
                    {
                        morningReportAt = UnityEngine.Time.unscaledTime + 1.5f;
                        morningReportReady = true;
                    }
                }
            }
            catch (Exception error) { log?.Error("过夜产物核对失败（只读）：" + error); }
        }
    }
    [HarmonyPatch(typeof(PlayerStore), "BeginDay")]
    internal static class OvernightReportHook
    {
        static void Postfix() => Plus.ReportAfterOvernight();
    }
}

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static bool autoProcessing = true;
        static string autoWaitReason, lastPreflightLog;
        static EndOfDayUIManager pendingEndDay;
        static PlayerStore pendingEndDayStore;
        static bool resumingEndDay;
        static bool awaitingMorningReport, morningReportReady;
        static float morningReportAt;
        static string nightReport;
        static bool nightDidWork;
        // Each source machine gets one turn per night; fanta plates keep their own bottle.
        static readonly Queue<GameItem> nightMachines = new Queue<GameItem>();
        static readonly List<string> nightBatchReports = new List<string>();
        static string nightBatchLabel;

        static bool HasPendingBatch(GameItem blender)
        {
            var parts = MachineView.Walk(blender);
            var pot = parts.Container?.childItem;
            if (blender.IsID("heating_plate")) return HasFantaMaterial(pot);
            return NativeChem.FirstBlendable(parts.Input) != null
                || (pot != null && NativeChem.WetTotal(pot) > WaterHelper.DRY_TOLERANCE_UL
                    && RecipeCatalog.Recipes.Any(c => Amount(pot, c.FeedLiquids[0]) > 0 || Amount(pot, c.ProductLiquid) > 0));
        }

        static void ClearNightQueue()
        {
            nightMachines.Clear(); nightBatchReports.Clear(); nightBatchLabel = null;
        }

        static IEnumerable<GameItem> PendingMachines() => Scan()
            .Where(x => (x.IsID("blender") || x.IsID("heating_plate")) && HasPendingBatch(x))
            .OrderBy(x => x.IsID("heating_plate") ? 0 : 1).ThenBy(x => x.uniqueId);

        static string NightSummary() => "本晚多机处理（共 " + nightBatchReports.Count + " 批）\n"
            + string.Join("\n\n", nightBatchReports);

        static bool StartNextNightBatch()
        {
            while (nightMachines.Count > 0)
            {
                var blender = nightMachines.Dequeue();
                contextMachine = contextPot = null; contextChain = null;
                string label = Name(blender) + " #" + blender.uniqueId;
                if (!Present(blender)) { nightBatchReports.Add(label + "：设备已离开库存，跳过。"); continue; }
                if (!HasPendingBatch(blender)) continue;
                if (WaitingForDistillation(blender))
                {
                    nightBatchReports.Add(label + "：已有批次待蒸发，继续原生过夜。");
                    nightDetails.Add(new BatchDisplay { Title = label, Detail = "已有批次待蒸发，继续原生过夜。" });
                    continue;
                }
                selectedMachine = blender;
                runDetail = null; lastCheckedPlan = null;
                Run(true, true, false);
                if (busy)
                {
                    nightBatchLabel = label;
                    log?.Log("[多机队列] 开始 " + label + "；剩余 " + nightMachines.Count + " 台；芬太原位处理，其他配方借用空闲加热板，不同配方不混合。");
                    return true;
                }
                nightDetails.Add(new BatchDisplay { Title = label, Detail = lastReport,
                    Supplies = lastCheckedPlan?.Supplies.ToArray() ?? Array.Empty<SupplySnapshot>() });
                nightBatchReports.Add(label + "：未开工，原料保留。\n" + lastReport);
            }
            return false;
        }
        static bool showMorningPopup = true;

        static void SetMorningPopup(bool enabled)
        {
            showMorningPopup = enabled;
            if (!enabled) awaitingMorningReport = morningReportReady = false;
            log?.Log("[次日弹窗] " + (enabled ? "开启" : "关闭"));
        }

        static void QueueMorningReport(string report)
        {
            if (!nightDidWork)
            {
                log?.Log("[结束当天] 未执行自动处理步骤，不安排次日弹窗。" + report);
                return;
            }
            nightReport = report;
            awaitingMorningReport = showMorningPopup;
            morningReportReady = false;
        }

        public static void TickMorningReport()
        {
            if (!showMorningPopup || !morningReportReady || Time.unscaledTime < morningReportAt || !ReadyForUi()) return;
            morningReportReady = awaitingMorningReport = false;
            RestoreRunDisplay();
            SetReport("本晚处理结果（按设备排列）");
            OpenPanel(false); // Keep the nightly result instead of replacing it with a new preflight.
        }

        static void SetAutoProcessing(bool enabled)
        {
            autoProcessing = enabled;
            autoWaitReason = null;
            window?.Get("auto_processing")?.SetBool(enabled, false);
            log?.Log("[自动处理] " + (enabled ? "已开启待命；仅点击结束当天时处理，送入蒸发器后继续原生过夜。" : "已关闭。"));
            if (!enabled) CancelJob("已关闭自动处理；已完成操作不回退，原液保留。");
            AutoWaiting(enabled ? "已待命；放入原料不会开工，点击结束当天时才处理。" : "已关闭");
        }

        static void AutoWaiting(string reason)
        {
            window?.Get("auto_status")?.SetText(!autoProcessing ? "自动处理已关闭" : busy ? "正在处理" : "结束当天自动处理");
            if (autoWaitReason == reason) return;
            autoWaitReason = reason;
            if (autoProcessing) log?.Log("[自动等待] " + reason);
        }

        // Defer the native sleep button before its blackscreen/EndNight callback.
        // Normal game ticks keep heating/reactions running while that request waits.
        internal static bool BeforeEndDay(EndOfDayUIManager manager)
        {
            if (resumingEndDay) return true;
            UpdatePowerWarning(false);
            if (pendingEndDay != null) return false;
            if (busy) { AutoWaiting("已有操作在运行，请等完成后再结束当天。"); return false; }
            // A new night cannot inherit an unread report or work flag from a previous night.
            nightDidWork = false;
            awaitingMorningReport = morningReportReady = false;
            nightReport = null;
            if (!autoProcessing || !ReadyForUi()) return true;
            try
            {
                ClearNightQueue();
                nightDetails.Clear(); runDetail = null;
                lastFailure = null; RenderFailureBanner();
                RememberMachineBottles();
                string dryTransfers = StageDryEvaporatorBottles();
                string refilled = RefillMachineBottles();
                if (refilled.Length > 0) dryTransfers = (dryTransfers + "\n" + refilled).Trim();
                if (dryTransfers.Length > 0) nightBatchReports.Add(dryTransfers);
                foreach (var blender in PendingMachines())
                    nightMachines.Enqueue(blender);
                if (nightMachines.Count == 0) { QueueMorningReport(dryTransfers + "\n本晚没有待处理批次，已正常过夜。"); return true; }
                log?.Log("[结束当天] 收到请求；逐台处理 " + nightMachines.Count + " 台设备（含芬太前体加热器），暂缓原生过夜。");
                StartNextNightBatch();
                if (busy)
                {
                    pendingEndDay = manager;
                    pendingEndDayStore = PlayerStore.Instance;
                    manager.CloseUI(false);
                    window?.Hide();
                    AutoWaiting("结束当天前正在处理；送入蒸发器后自动继续过夜。重复点击不会重复开工。");
                }
                else
                {
                    RestoreRunDisplay();
                    SetReport("本晚检查结束；各批结果如下。");
                    QueueMorningReport(NightSummary());
                    ClearNightQueue();
                    return true;
                }
                return false;
            }
            catch (Exception error)
            {
                log?.Error(error.ToString());
                RecordFailure("自动检查异常：" + error.Message);
                CancelJob("本晚检查异常，自动开关仍开启：" + error.Message);
                QueueMorningReport("本晚检查异常：" + error.Message + "\n本次已停止处理，自动开关仍开启。");
                return true;
            }
        }

        static void ResumeEndDay()
        {
            var manager = pendingEndDay;
            var store = pendingEndDayStore;
            pendingEndDay = null; pendingEndDayStore = null;
            string report = NightSummary();
            ClearNightQueue();
            RestoreRunDisplay();
            SetReport("本晚处理结束；各批结果如下。");
            ContinueNativeEndDay(manager, store, report);
        }

        static void ContinueNativeEndDay(EndOfDayUIManager manager, PlayerStore store, string report)
        {
            if (manager == null || !PlayerStore.IsInstanceExist() || PlayerStore.Instance != store) return;
            QueueMorningReport(report);
            AutoWaiting(nightDidWork ? "本晚处理结束，继续原生过夜；第二天显示结果。" : "没有执行处理步骤，正常过夜且不弹窗。");
            log?.Log("[结束当天] 本次处理已结束；恢复原生过夜一次，次日弹窗=" + (nightDidWork && showMorningPopup));
            window?.Hide();
            resumingEndDay = true;
            try { manager.OnEndDayButtonClick(); }
            finally { resumingEndDay = false; }
        }

        // An extracted bottle is not a new raw-material batch. Leave it waiting
        // when evaporators are in use, without reporting an occupied-slot failure.
        static bool WaitingForDistillation(GameItem blender)
        {
            if (blender.IsID("heating_plate")) return false;
            var parts = MachineView.Walk(blender);
            if (FirstKind(parts.Input).Count > 0) return false;
            var pot = parts.Container?.childItem;
            if (HasFantaMaterial(pot)) return false;
            if (pot != null && RecipeCatalog.Recipes.Any(c => c.FeedLiquids[0] != c.ProductLiquid && ReactableFeed(pot, c) > 0)) return false;
            var machines = Scan().Where(x => x.IsID("evaporator")).ToList();
            return machines.Count > 0 && machines.All(x => MachineView.Walk(x).Container?.childItem != null)
                && FindEvaporator(pot) == null;
        }

        static void MarkAutoWork()
        {
            if (pendingEndDay != null) nightDidWork = true;
        }
    }

    [HarmonyPatch(typeof(EndOfDayUIManager), "OnEndDayButtonClick")]
    internal static class EndDayProcessingHook
    {
        static bool Prefix(EndOfDayUIManager __instance) => Plus.BeforeEndDay(__instance);
    }
}

using System;
using System.Linq;
using UnityEngine;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static string powerWarning, lastPowerNotice;
        static float powerNoticeAt;

        static void SchedulePowerReminder()
        {
            lastPowerNotice = null;
            powerNoticeAt = Time.unscaledTime + 2f;
            UpdatePowerWarning(false);
        }

        // Monitor existing wet batches too: a night without new raw materials must not hide a stalled machine.
        static void UpdatePowerWarning(bool notify = true)
        {
            var warnings = Scan().Where(x => x.IsID("evaporator")).Select(machine =>
            {
                var pot = MachineView.Walk(machine).Container?.childItem;
                if (pot == null || NativeChem.WetTotal(pot) <= WaterHelper.DRY_TOLERANCE_UL || Charged(machine)) return null;
                if (!RecipeCatalog.Recipes.Any(c => Amount(pot, c.ProductLiquid) > 0 || Amount(pot, c.FeedLiquids[0]) > 0)) return null;
                string reason = NativeChem.FindBattery(machine) == null ? "缺电池" : "电池没电或电量不足以启动";
                return "蒸发器 #" + machine.uniqueId + "：" + reason + "，工作瓶 #" + pot.uniqueId + " 尚未蒸干。";
            }).Where(x => x != null).ToList();
            string current = string.Join("\n", warnings);
            if (current != powerWarning)
            {
                powerWarning = current;
                log?.Log(current.Length == 0 ? "[蒸发器供电] 无待处理缺电问题。" : "[蒸发器供电] " + current);
                RenderFailureBanner();
            }
            if (current.Length == 0) { lastPowerNotice = null; return; }
            if (!notify || current == lastPowerNotice || Time.unscaledTime < powerNoticeAt || !ReadyForUi() || StoreUIManager.Instance == null) return;
            try
            {
                StoreUIManager.Instance.Notify(current + " 请更换/充电；F10查看。", "default");
                lastPowerNotice = current;
            }
            catch (Exception e) { powerNoticeAt = Time.unscaledTime + 10f; log?.Log("[供电提醒] " + e.Message); }
        }
    }
}

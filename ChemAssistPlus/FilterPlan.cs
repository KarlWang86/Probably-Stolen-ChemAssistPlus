using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static bool IsPitcher(GameItem item) => item != null && item.IsID("water_pitcher") && item.IsTag(WaterHelper.FILTER_CONTAINER_TAG);
        sealed class FilterSetup
        {
            public GameItem Pitcher, Material, Existing;
            public bool Change => Material != Existing;
        }
        static bool MaterialMatches(GameItem f, string key) => f != null
            && (key == "cloth" ? f.IsID("kotton_fabric") : f.IsID("water_filter") || f.IsID("water_filter_adv"))
            && DurabilityHelper.GetRealDurability(f) > 0;
        static GameSlotInventory FilterSlot(GameItem p) => MachineView.Find<GameSlotInventory>(p?.contentWindow?.childElement);
        static bool CanInstall(GameSlotInventory slot, GameItem material)
        {
            if (slot == null || !slot.MayHaveValidInventorySlot(material)) return false;
            var marker = new SlotMarker { item = material, inventory = slot, itemGridShape = material.modifiedShape, numTransfer = material.unitCount };
            return material.CheckIsStateValid(slot, marker, material.modifiedShape, material.modifiedState);
        }
        static FilterSetup PlanFilter(string key)
        {
            var all = Scan();
            var pitchers = all.Where(x => x != contextPot && IsPitcher(x));
            var options = new List<FilterSetup>();
            foreach (var pitcher in pitchers.Where(x => WaterHelper.IsEmpty(x) && FilterSlot(x) != null))
            {
                var slot = FilterSlot(pitcher);
                var existing = slot.childItem;
                if (MaterialMatches(existing, key)) options.Add(new FilterSetup { Pitcher = pitcher, Material = existing, Existing = existing });
                foreach (var spare in all.Where(x => MaterialMatches(x, key)))
                {
                    // Loose stock only; never remove another pitcher's installed material.
                    // Reserve real free space before removing the old material. No swap-buffer mutations during inspection.
                    if (!(spare.parentInventory is GameGridInventory storage) || storage.GetParentItem()?.IsTag(WaterHelper.FILTER_CONTAINER_TAG) == true
                        || !CanInstall(slot, spare) || (existing != null && !GraphUtils.CanAccept(storage, existing))) continue;
                    options.Add(new FilterSetup { Pitcher = pitcher, Material = spare, Existing = existing });
                }
            }
            return options.OrderBy(x => x.Change).ThenBy(x => DurabilityHelper.GetRealDurability(x.Material))
                .ThenByDescending(x => WaterHelper.GetTotalCapacity(x.Pitcher)).ThenBy(x => x.Material.uniqueId).FirstOrDefault();
        }
        static GameItem EnsureFilter(string key, StringBuilder report)
        {
            var plan = PlanFilter(key); // Recheck ownership, empty pitcher, durability and storage immediately before mutation.
            if (plan == null) return null;
            if (!plan.Change) return plan.Pitcher;
            var slot = FilterSlot(plan.Pitcher);
            var storage = plan.Material.parentInventory;
            if (plan.Existing != null)
            {
                GraphUtils.TryAcceptAll(storage, plan.Existing);
                if (slot.childItem != null) throw new InvalidOperationException("拆下滤材未能安全入库，停止过滤；原液保留。");
                report.AppendLine("拆下 " + Name(plan.Existing) + "，已归还 " + SceneSnapshot.Where(plan.Existing));
            }
            try
            {
                if (!GraphUtils.CanAccept(slot, plan.Material)) throw new InvalidOperationException("原生槽位拒绝备用滤材。");
                GraphUtils.TryAcceptAll(slot, plan.Material);
                if (WaterHelper.GetFilterItem(plan.Pitcher) != plan.Material)
                    throw new InvalidOperationException("安装滤材未完成。");
            }
            catch
            {
                if (slot.childItem == null && plan.Existing != null && Owned(plan.Existing) && GraphUtils.CanAccept(slot, plan.Existing))
                    GraphUtils.TryAcceptAll(slot, plan.Existing);
                throw; // Execution failures stop even in non-strict mode; never pour through an uncertain setup.
            }
            report.AppendLine("自动安装滤材：" + Name(plan.Pitcher) + " ← " + Name(plan.Material));
            return plan.Pitcher;
        }
        static string PreferredFilterKey() => PlanFilter("fine") != null ? "fine" : PlanFilter("cloth") != null ? "cloth" : null;
        static string NoFilterReason() => "单道过滤不可用（滤芯优先，棉布备用）：\n" + FilterReason("fine") + "\n" + FilterReason("cloth");
        static string PlannedFilterName(string key)
        {
            var p = PlanFilter(key);
            return p == null ? FilterReason(key) : (key == "fine" ? "单道过滤：优先滤芯；" : "单道过滤：无可用滤芯，改用棉布；") + Name(p.Pitcher) + " / " + Name(p.Material) + (p.Change ? "（将自动换装；旧滤材归还仓库）" : "（已装好）");
        }
        static string FilterReason(string key)
        {
            var all = Scan();
            string stage = key == "cloth" ? "备用棉布" : "优先滤芯";
            var pitchers = all.Where(x => x != contextPot && IsPitcher(x)).ToList();
            if (pitchers.Count == 0) return stage + "：[缺容器] 没有可操作的过滤水罐。";
            if (!pitchers.Any(WaterHelper.IsEmpty)) return stage + "：[容器占用] 请先清空所选水罐。";
            if (!all.Any(x => MaterialMatches(x, key))) return stage + "：[缺滤材] 未找到有耐久的" + (key == "cloth" ? "棉布。" : "滤芯。");
            return stage + "：[无法换装] 滤材已装在其他水罐，或槽位不接受，或旧滤材没有安全存放空间；请在备用滤材所在库存留出空位。";
        }
    }
}


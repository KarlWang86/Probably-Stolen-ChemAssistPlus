using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Bound each raw portion by the current filter's real durability. A portion passes
        // each native filter stage once; already-filtered liquid is buffered until that stage ends.
        static bool FilterPortionFits(int[] parts, GameItem filter)
        {
            long cost = 0; int lastCost = 0;
            int size = filter.GetTagReadonly("FILTER_SIZE_TAG").GetInt();
            for (int i = 0; i < parts.Length; i++)
            {
                var liquid = Liquid.Liquids[i];
                if (parts[i] <= 0 || liquid.identifier == "microbe" || size > liquid.particleSize || liquid.filterVolumeRemoved <= 0) continue;
                int floor = (int)(parts[i] * liquid.filterMinimumPurity);
                long actions = ((long)parts[i] - floor + liquid.filterVolumeRemoved - 1) / liquid.filterVolumeRemoved;
                if (actions > 10000) return false;
                if (actions > 0) { cost += actions * liquid.filterDurabilityUsage; lastCost = liquid.filterDurabilityUsage; }
            }
            return cost == 0 || cost - lastCost < DurabilityHelper.GetRealDurability(filter);
        }
        public static void FilterBatch(GameItem pot, StringBuilder report, List<string> warnings)
        {
            foreach (string key in new[] { "cloth", "fine" })
            {
                var buffered = new List<KeyValuePair<string, int>>();
                int chunks = 0, original = WaterHelper.GetTotalVolume(pot);
                double temperature = SceneSnapshot.Temp(pot);
                try
                {
                    while (WaterHelper.GetTotalVolume(pot) > 0)
                    {
                        if (++chunks > 4096) throw new InvalidOperationException("过滤分批达到上限，剩余原液保留。");
                        var pitcher = EnsureFilter(key, report);
                        if (pitcher == null) throw new InvalidOperationException(FilterReason(key) + "剩余原液保留。");
                        if (pitcher == pot) throw new InvalidOperationException("工作瓶不能同时作为过滤水壶。");
                        var material = WaterHelper.GetFilterItem(pitcher);
                        int portion = FilterPortionVolume(pot, pitcher, material);
                        int durability = DurabilityHelper.GetRealDurability(material);
                        var taken = WaterHelper.RemoveAndReturn(pot, portion).ToList();
                        int start = buffered.Count;
                        buffered.AddRange(taken); // Keep every removed component recoverable even if a native call fails.
                        MarkAutoWork();
                        FilterComponents(pot, material, taken, buffered, start);
                        report.AppendLine((key == "cloth" ? "滤布" : "滤芯") + " #" + material.uniqueId + "：处理 " + Vol(portion)
                            + "，耐久 " + durability + " → " + DurabilityHelper.GetRealDurability(material));
                    }
                }
                finally
                {
                    AddSolution(pot, buffered, temperature);
                    WaterHelper.UpdateContainerGoodType(pot);
                }
                report.AppendLine((key == "cloth" ? "滤布完成" : "精滤完成") + "：" + chunks + "批，" + Vol(original) + " → " + Vol(WaterHelper.GetTotalVolume(pot)));
            }
        }
        // 二分查找本次滤材耐久与水壶容量都能处理的最大份额。
        static int FilterPortionVolume(GameItem pot, GameItem pitcher, GameItem material)
        {
            var parts = WaterHelper.GetLiquidParts(pot);
            int low = 0, high = Math.Min(parts.Sum(), WaterHelper.GetTotalCapacity(pitcher));
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (FilterPortionFits(WaterHelper.SplitProportionally(parts, middle), material)) low = middle;
                else high = middle - 1;
            }
            if (low <= 0) throw new InvalidOperationException("滤材当前耐久不足以处理最小份额，原液保留。");
            return low;
        }

        // 调用前已缓冲全部原液；逐项替换成功结果，异常时仍可回收未处理部分。
        static void FilterComponents(GameItem pot, GameItem material, List<KeyValuePair<string, int>> taken,
            List<KeyValuePair<string, int>> buffered, int start)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                var component = taken[i];
                int filtered = WaterHelper.HandleFilter(component.Key, component.Value, pot, material);
                buffered[start + i] = new KeyValuePair<string, int>(component.Key, filtered);
            }
        }
    }
}

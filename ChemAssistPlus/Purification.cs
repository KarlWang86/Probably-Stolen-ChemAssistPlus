using System;
using System.Collections.Generic;
using System.Linq;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static string tabletResult;
        static string uvResult;
        static void PurifyWithUV(GameItem pot)
        {
            int microbes = Amount(pot, "microbe");
            if (microbes == 0)
            {
                uvResult = "紫外线：无微生物，跳过。";
                Trace(uvResult);
                return;
            }
            // OnUVUsed does not check for a lamp itself. Require an owned native UVFilter.
            var lamp = Scan().Where(x => x.IsID("uv_filter")).OrderBy(x => x.uniqueId).FirstOrDefault();
            if (lamp == null)
            {
                uvResult = "紫外线：未找到玩家拥有的紫外线灯，继续按药片规则净化。";
                Trace(uvResult);
                return;
            }
            // Match UVFilter's native target predicate, without imposing a water requirement.
            if (WaterHelper.IsEmpty(pot) || !pot.IsTag(WaterHelper.LIQUID_CONTAINER_TAG))
            {
                uvResult = "紫外线：目标不是非空液体容器，跳过。";
                Trace(uvResult);
                return;
            }
            int water = Amount(pot, "water");
            WaterHelper.OnUVUsed(pot);
            pot.ValidateShapeState();
            MarkAutoWork();
            uvResult = "紫外线：微生物 " + Vol(microbes) + " → " + Vol(Amount(pot, "microbe")) + "。";
            Trace(uvResult + " 使用 " + Name(lamp) + " #" + lamp.uniqueId + "；工作瓶 #" + pot.uniqueId
                + "；水 " + Vol(water) + " → " + Vol(Amount(pot, "water")) + "；原生一次处理，无耗电或耐久消耗。");
        }
        // SceneSnapshot descends into containers, including water_tablet_container.
        static bool InTabletBottle(GameItem item)
        {
            var parent = item.parentInventory?.GetParentItem();
            for (int depth = 0; parent != null && depth < 24; depth++, parent = parent.parentInventory?.GetParentItem())
                if (parent.IsID("water_tablet_container")) return true;
            return false;
        }
        // Count real tablets only; consume loose tablets before opening bottle contents.
        static List<GameItem> Tablets() => Scan().Where(x => x.IsID("water_tablet") && Amount(x, "chlorine") > 0)
            .OrderBy(InTabletBottle).ThenBy(x => x.uniqueId).ToList();
        // Native HandleChlorine uses single-precision total * 0.007f, including all components.
        static int ChlorineFloor(int total) => (int)(total * 0.007f);
        static bool ChlorineUseful(GameItem pot) => Amount(pot, "water") > 0
            && (Amount(pot, "microbe") > 0 || Amount(pot, "chemical_contaminant") > ChlorineFloor(WaterHelper.GetTotalVolume(pot)));
        static void SettleChlorine(GameItem pot)
        {
            for (int tick = 0; tick < 128 && Amount(pot, "chlorine") > 0; tick++)
            {
                int before = Amount(pot, "chlorine");
                ChemicalHelper.HandleChlorine(pot);
                pot.ValidateShapeState();
                if (Amount(pot, "chlorine") >= before) break;
                MarkAutoWork();
            }
        }
        static void PurifyBatch(GameItem pot)
        {
            int before = Amount(pot, "chemical_contaminant"), microbes = Amount(pot, "microbe"), count = 0;
            var inventory = Tablets();
            int bottled = inventory.Count(InTabletBottle);
            Trace("[净水药片库存] 散片 " + (inventory.Count - bottled) + " 片；瓶内 " + bottled + " 片；合计 " + inventory.Count + " 片，优先散片。");
            string reason = null;
            if (Amount(pot, "water") == 0 && before > 0) runNotes.Add("无水时药片不反应，未消耗药片。");
            SettleChlorine(pot);
            while (ChlorineUseful(pot) && count < 64)
            {
                var tablet = Tablets().FirstOrDefault();
                if (tablet == null) { reason = "缺净水药片，残余污染保留"; break; }
                if (ChemicalHelper.GetTotalVolume(tablet) > WaterHelper.GetFreeCapacity(pot))
                { reason = "工作瓶空间不足，未投更多药片"; break; }
                int itemId = tablet.uniqueId;
                string origin = InTabletBottle(tablet) ? "瓶内单片" : "散片";
                ChemicalHelper.DissolveInto(tablet, pot);
                pot.ValidateShapeState();
                MarkAutoWork(); count++;
                SettleChlorine(pot);
                Trace("[净水药片] 消耗" + origin + " #" + itemId + "；化学污染剩 " + Vol(Amount(pot, "chemical_contaminant")));
            }
            usedTablets += count;
            if (reason == null && ChlorineUseful(pot)) reason = "达到单批64片上限，残余保留";
            if (reason == null) reason = Amount(pot, "water") == 0 ? "无水，原生药片反应不生效"
                : Amount(pot, "chemical_contaminant") == 0 && Amount(pot, "microbe") == 0 ? "无需净化"
                : "化学污染 " + Vol(Amount(pot, "chemical_contaminant")) + " ≤ 原生下限 "
                    + Vol(ChlorineFloor(WaterHelper.GetTotalVolume(pot))) + "，投片无法继续降低";
            tabletResult = "净水药片：消耗 " + count + " 片；" + reason + "。";
            Trace(tabletResult + " 化学污染 " + Vol(before) + " → " + Vol(Amount(pot, "chemical_contaminant"))
                + "；当前下限 " + Vol(ChlorineFloor(WaterHelper.GetTotalVolume(pot))));
            if (count > 0 || before != Amount(pot, "chemical_contaminant") || microbes != Amount(pot, "microbe"))
                Trace("[药片净水] 共 " + count + " 片；化学污染 " + Vol(before) + " → " + Vol(Amount(pot, "chemical_contaminant"))
                    + "；微生物 " + Vol(microbes) + " → " + Vol(Amount(pot, "microbe")) + "；保留原生净化下限。");
        }
    }
}

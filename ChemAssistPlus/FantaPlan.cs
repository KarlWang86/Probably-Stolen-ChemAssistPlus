using System;
using System.Collections.Generic;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class FantaBudget
        {
            public string Reagent;
            public readonly Dictionary<string, int> Limits = new Dictionary<string, int>();
            public readonly Dictionary<string, long> Volumes = new Dictionary<string, long>();
            public readonly Dictionary<string, int> Used = new Dictionary<string, int>();
            public readonly HashSet<int> LoggedSources = new HashSet<int>();
        }

        static bool IsFanta(Recipe recipe) => recipe?.ProductId == "fanta_pill";
        const int FantaPurificationWater = 200000;
        static string FantaModuleWarning(GameItem plate) => plate != null && MachineBlender.GetUpgradeCount(plate) < 2
            ? "警告：前体液加热器 #" + plate.uniqueId + " 仅有 " + MachineBlender.GetUpgradeCount(plate)
                + " 个模组（建议至少2个）；允许继续，助手沿用低配板控温，实际温度以日志为准。" : null;
        // Read actual state in previews too: manual pours can leave modifiedState stale.
        static int FantaAmount(GameItem pot, string id) => LiveLiquidAmount(pot, id);
        static int[] FantaParts(GameItem pot) => Liquid.Liquids.Select(x => FantaAmount(pot, x.identifier)).ToArray();
        static int FantaEffective(GameItem pot, string id) => EffectivePart(FantaParts(pot), id);
        // Signed balance is required at the receiving bottle: adding acid to existing
        // base reduces a negative balance even when max(0, acid-base) stays at zero.
        static int FantaDoseValue(GameItem pot, string id) => id == "acid" ? FantaAmount(pot, "acid") - FantaAmount(pot, "base")
            : id == "base" ? FantaAmount(pot, "base") - FantaAmount(pot, "acid") : FantaAmount(pot, id);
        static bool HasFantaMaterial(GameItem pot) => FantaAmount(pot, "methylol") > 0
            || FantaAmount(pot, "fanta") > 0 || FantaAmount(pot, "stabilizing_agent") > 0;
        static readonly string[] FantaResiduals = { "methylol", "stabilizing_agent", "reducing_agent", "acid", "base" };
        static string FantaResidue(GameItem pot) => string.Join("；", FantaResiduals.Where(id => FantaAmount(pot, id) > 0)
            .Select(id => LiquidName(id) + " " + Vol(FantaAmount(pot, id))));
        static bool FantaReady(GameItem pot) => FantaAmount(pot, "fanta") > 0 && FantaResiduals.All(id => FantaAmount(pot, id) == 0);

        static NativeChem.ProbeResult ProbeFantaPlate(NativeChem.ProbeResult p)
        {
            p.Pot = p.Parts?.Container?.childItem;
            p.Recipe = RecipeCatalog.Recipes.First(x => IsFanta(x));
            if (!HasFantaMaterial(p.Pot)) p.Block = "加热器槽中没有芬太前体液或对应半成品。";
            else if (Detect(p.Pot, null) != p.Recipe) p.Block = "加热器工作瓶含其他配方或没有前体/产物，请分开处理。";
            else if (!Owned(p.Pot) || !p.Pot.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)
                || p.Pot.IsTag(WaterHelper.FILTER_CONTAINER_TAG)) p.Block = "加热器工作瓶不是可处理的普通液体容器。";
            contextPot = p.Pot; contextChain = p.Recipe;
            return p;
        }

        // Ordinary impurities travel with the reagent. Other synthesis reagents must not
        // hitchhike in a dose and invalidate the bounded reducing-agent tail calculation.
        static IEnumerable<GameItem> FantaSources(string id, GameItem pot) => SceneSnapshot.Run().LiquidContainers
            .Where(x => x != pot && Owned(x) && FantaEffective(x, id) > 0
                && !RecipeCatalog.Recipes.Any(c => (c.FeedLiquids[0] != id && FantaAmount(x, c.FeedLiquids[0]) > 0)
                    || (c.ProductLiquid != id && FantaAmount(x, c.ProductLiquid) > 0)))
            .Where(x => FantaAmount(x, "stabilizing_agent") == 0
                && new[] { "acid", "base", "reducing_agent", "lipobond_agent" }
                    .All(other => other == id || ((id == "acid" || id == "base") && (other == "acid" || other == "base"))
                        || FantaAmount(x, other) == 0))
            .Where(x => id != "water" || Liquid.Liquids.All(liquid => liquid.identifier == "water"
                || liquid.identifier == "heavy_metal" || liquid.identifier == "chemical_contaminant"
                || liquid.identifier == "physical_contaminant" || liquid.identifier == "mineral"
                || liquid.identifier == "microbe" || FantaAmount(x, liquid.identifier) == 0))
            .OrderByDescending(x => id == "water" ? FantaEffective(x, id) / (double)Math.Max(1, FantaParts(x).Sum()) : 0)
            .ThenByDescending(x => preferredSources.TryGetValue(id, out var preferred) && x == preferred)
            .ThenByDescending(x => FantaEffective(x, id) / (double)Math.Max(1, FantaParts(x).Sum()))
            .ThenBy(x => x.uniqueId);

        static BatchPlan BuildFantaPlan(NativeChem.ProbeResult p)
        {
            var plan = new BatchPlan { Full = FantaParts(p.Pot), Solvent = "reducing_agent", Fanta = new FantaBudget() };
            plan.Kept = (int[])plan.Full.Clone(); plan.KeepVolume = plan.Full.Sum();
            var budget = plan.Fanta;
            int feed = FantaAmount(p.Pot, "methylol"), stabilizer = FantaAmount(p.Pot, "stabilizing_agent");
            if (p.Machine == null || !p.Machine.IsID("heating_plate") || p.Parts?.Container?.childItem != p.Pot)
                plan.Errors.Add("请将芬太前体液容器直接放入加热器槽，无需搅拌。");
            else if (!PlateAvailable(p.Machine)) plan.Errors.Add("请先关闭前体液所在加热器，助手只接管关闭的设备。");
            else if (feed > 0 && MachineBlender.GetUpgradeCount(p.Machine) < 2 && !HeatingHookReady())
                plan.Errors.Add("低配板控温补丁未加载。");
            var moduleWarning = FantaModuleWarning(p.Machine);
            if (moduleWarning != null) plan.Lines.Add(Warn(moduleWarning));
            if (Detect(p.Pot, null) != p.Recipe) plan.Errors.Add("工作瓶混有其他配方，不能处理。");
            if (feed == 0 && FantaAmount(p.Pot, "fanta") == 0) plan.Errors.Add("没有前体或目标产物。");
            if (SceneSnapshot.Temp(p.Pot) > p.Recipe.High) plan.Errors.Add("工作瓶温度过高，请先冷却。");
            if (feed > 0 && (stabilizer > 0 || FantaAmount(p.Pot, "protein") > 0 || FantaAmount(p.Pot, "fat") > 0)
                && SceneSnapshot.Temp(p.Pot) >= 110) plan.Errors.Add("前处理需要低于110℃，请先关闭加热器并冷却。");
            if (feed == 0 && FantaAmount(p.Pot, "reducing_agent") > 0)
                plan.Errors.Add("前体已耗尽但还原剂仍有残余，现有过滤/蒸发无法清除，请人工处理。");
            int existingReducer = FantaAmount(p.Pot, "reducing_agent");
            if (feed > 0 && (existingReducer <= 1000 ? FantaFeedForDose(existingReducer) > feed : existingReducer * 2L > feed))
                plan.Errors.Add("已有还原剂超过可无残余处理的保守范围，暂不追加试剂或加热，请先人工处理。");

            // Reserve an upper bound without predicting native competing reactions. Every
            // runtime dose is capped by both effective amount and solution volume below.
            var projected = (int[])plan.Full.Clone();
            ReserveFantaSupply(plan, p.Pot, projected, "reducing_agent", Math.Max(0, feed - FantaAmount(p.Pot, "reducing_agent")));
            int fat = Part(projected, "fat");
            ReserveFantaSupply(plan, p.Pot, projected, "lipobond_agent", fat > 0 ? checked(fat + 1000) : 0);
            long reactive = (long)Part(projected, "stabilizing_agent") + Part(projected, "protein")
                + Part(projected, "organic_waste") + Part(projected, "microbe");
            int acid = FantaAmount(p.Pot, "acid"), alkali = FantaAmount(p.Pot, "base");
            budget.Reagent = acid != alkali ? acid > alkali ? "acid" : "base"
                : new[] { "acid", "base" }.OrderByDescending(id => FantaSources(id, p.Pot).Sum(x => (long)FantaEffective(x, id))).First();
            // Leave a bounded retry reserve on the original side too: impurities in
            // the opposite solution may consume it before neutralization takes place.
            int reserve = reactive > 0 ? checked((int)(reactive + 1000)) : acid != alkali ? 1000 : 0;
            ReserveFantaSupply(plan, p.Pot, projected, budget.Reagent, reserve);
            string opposite = budget.Reagent == "acid" ? "base" : "acid";
            int neutralizer = checked((int)Math.Abs((long)Part(projected, "acid") - Part(projected, "base")));
            // Covers at most a 1 mL top-up/rounding tail as well as the conservative reserve.
            ReserveFantaSupply(plan, p.Pot, projected, opposite, neutralizer > 0 ? checked(neutralizer + 1000) : 0);
            // Existing water can evaporate during synthesis. Reserve a full post-reaction top-up.
            ReserveFantaSupply(plan, p.Pot, projected, "water", feed > 0 ? FantaPurificationWater
                : Math.Max(0, FantaPurificationWater - FantaAmount(p.Pot, "water")));
            plan.RawPeak = plan.KeepVolume;
            plan.SolutionPeak = checked(plan.KeepVolume + budget.Volumes.Values.Sum());
            plan.TabletReserve = Tablets().Count > 0 ? 15000 : 0;
            int tablets = Tablets().Count;
            plan.Supplies.Add(new SupplySnapshot { Id = "water_tablet", Name = "净水药片", Available = tablets,
                Required = 1, Tablets = true, Known = true, Short = tablets == 0 });
            if (tablets == 0) plan.Errors.Add("芬太后处理缺净水药片，请准备散片或瓶内真实药片。");
            plan.Lines.Add("芬太后处理：关火后从真实水源将水补足200 mL，再棉布→滤芯过滤、按需消毒和投净水药片；药片按原生反应实际需要消耗，随后送蒸发器过夜。");
            plan.Peak = plan.SolutionPeak + plan.TabletReserve;
            if (plan.Peak > WaterHelper.GetTotalCapacity(p.Pot))
                plan.Errors.Add("工作瓶容量不足：保守储备峰值 " + Vol(plan.Peak) + "，当前容量 " + Vol(WaterHelper.GetTotalCapacity(p.Pot)) + "。请换大瓶。");
            PlanFiltration(plan);
            long wetUpper = Liquid.Liquids.Select((liquid, i) => liquid.isEvaporative ? (long)projected[i] : 0).Sum()
                + Part(projected, "microbe");
            if (wetUpper > WaterHelper.DRY_TOLERANCE_UL)
            {
                var receiver = FindEvaporator(p.Pot, p.Recipe, plan.Peak, true);
                if (receiver == null) plan.Errors.Add(EvaporatorBlockReason(p.Pot, p.Recipe, plan.Peak, true));
                else
                {
                    plan.Lines.Add(EvaporatorRouteSummary(receiver, p.Recipe));
                    if (CanSwapLargerWorkBottle(p.Pot, MachineView.Walk(receiver).Container, plan.Peak))
                        plan.Lines.Add("当前工作瓶更大，完成后整瓶移入蒸发器；下游空瓶换回加热器槽，不受小空瓶容量限制。");
                }
            }
            else plan.Lines.Add("当前储备未预计产生超容差挥发液，无需为合成本身准备蒸发器；完成后仍按真实干湿复核。");
            plan.Lines.Add("加热器直接处理，不搅拌、不自动拆包或取前体。试剂用量为保守储备上限，实际按成分分次投料；达到储备边界则停止保料。");
            plan.Lines.Add("残余前体、稳定剂、还原剂及酸碱须为0才移交。净化后按实际干湿分流，无接收位置则留在加热器瓶中。");
            return plan;
        }

        static void ReserveFantaSupply(BatchPlan plan, GameItem pot, int[] projected, string id, int need)
        {
            var sources = FantaSources(id, pot).ToList();
            long stock = sources.Sum(x => (long)FantaEffective(x, id));
            long stockVolume = sources.Sum(x => (long)FantaParts(x).Sum());
            int missing = need; long volume = 0;
            foreach (var source in sources)
            {
                var parts = FantaParts(source);
                for (int attempt = 0; missing > 0 && attempt < 3; attempt++)
                {
                    int active = EffectivePart(parts, id), total = parts.Sum();
                    if (active <= 0 || total <= 0) break;
                    int pour = (int)Math.Min(total, DoseMath.RequiredSolution(missing, active, total));
                    var taken = WaterHelper.SplitProportionally(parts, pour);
                    int delivered = EffectivePart(taken, id);
                    ApplyProjectedPour(parts, projected, taken); volume += pour; missing -= delivered;
                    if (delivered <= 0) break;
                }
                if (missing <= 0) break;
            }
            plan.Fanta.Limits[id] = need;
            // Repeated small proportional pours may each add a rounding microlitre per component.
            long rounding = need > 0 ? checked((long)(need / 1000 + 64) * Liquid.Liquids.Count) : 0;
            plan.Fanta.Volumes[id] = volume + rounding;
            plan.Supplies.Add(new SupplySnapshot { Id = id, Name = SupplyName(id) + "储备上限", Available = stockVolume,
                Required = volume, Known = missing <= 0, Short = missing > 0 });
            if (missing > 0) plan.Errors.Add(LiquidName(id) + "储备不足：需有效 " + Vol(need) + "，可用 " + Vol(stock));
        }
    }
}

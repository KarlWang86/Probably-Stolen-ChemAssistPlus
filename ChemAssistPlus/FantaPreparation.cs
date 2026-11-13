using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class FantaDraw
        {
            public GameItem Source;
            public int Volume, Delivered;
            public int[] Before;
        }

        static void CheckFantaPlate(NativeChem.ProbeResult p, bool cold = false)
        {
            CheckPot(p, p.Machine);
            if (!Present(p.Machine) || !Owned(p.Machine) || Detect(p.Pot, null) != p.Recipe)
                throw new InvalidOperationException("芬太批次的设备、所有权或配方已变化。");
            p.Pot.ValidateShapeState();
            double temperature = SceneSnapshot.Temp(p.Pot);
            if (temperature > p.Recipe.High + 0.1 || (cold && temperature >= 110))
                throw new InvalidOperationException(cold ? "前处理温度达到110℃，请先冷却；原液保留。" : "芬太工作瓶超温，已停止。");
            if (activePlate == null && p.Machine.GetTagReadonly(MachineHeatingPlate.HEATING_PLATE_STATE).GetInt() != 0)
                throw new InvalidOperationException("前处理期间加热器被开启，助手已让出控制。");
        }

        // Plan the entire dose before removing anything. Bound rounding overshoot so a
        // reducing-agent tail cannot be created by a proportional transfer itself.
        static void DoseFanta(NativeChem.ProbeResult p, FantaBudget budget, string id, int need)
        {
            if (need <= 0) return;
            CheckFantaPlate(p);
            budget.Used.TryGetValue(id, out int used);
            budget.Limits.TryGetValue(id, out int limit);
            if ((long)used + need > limit) throw new InvalidOperationException(LiquidName(id) + "达到本批储备上限，停止补料；请重新检查实际成分。");
            var draws = PlanFantaDose(p.Pot, id, need);
            long volume = draws.Sum(x => (long)x.Volume);
            actualUsage.TryGetValue(id, out long consumed);
            if (!budget.Volumes.TryGetValue(id, out long allowance) || consumed + volume > allowance)
                throw new InvalidOperationException(LiquidName(id) + "来源浓度/杂质变化，所需原溶液超过容量预算；尚未投本次料。");
            if (volume > WaterHelper.GetFreeCapacity(p.Pot))
                throw new InvalidOperationException("工作瓶装不下本次整份试剂，尚未投本次料。");
            foreach (var draw in draws)
            {
                if (!Owned(draw.Source) || !Present(draw.Source)
                    || !FantaParts(draw.Source).SequenceEqual(draw.Before))
                    throw new InvalidOperationException("试剂来源已变化，停止投料。");
                int before = FantaDoseValue(p.Pot, id), beforeVolume = WaterHelper.GetTotalVolume(p.Pot);
                try { TransferSolution(draw.Source, p.Pot, draw.Volume); }
                finally
                {
                    int delivered = Math.Max(0, FantaDoseValue(p.Pot, id) - before);
                    budget.Used.TryGetValue(id, out used); budget.Used[id] = checked(used + delivered);
                    actualUsage.TryGetValue(id, out consumed);
                    actualUsage[id] = consumed + Math.Max(0, WaterHelper.GetTotalVolume(p.Pot) - beforeVolume);
                }
                if (FantaDoseValue(p.Pot, id) - before != draw.Delivered)
                    throw new InvalidOperationException("同比投料结果与预检不同，停止保留原液。");
                preferredSources[id] = draw.Source;
                if (budget.LoggedSources.Add(draw.Source.uniqueId))
                    Trace("  芬太试剂来源：" + Name(draw.Source) + " #" + draw.Source.uniqueId + "（" + LiquidName(id) + "，水和杂质同比转移）；实际总用量在阶段结束记录。");
            }
        }

        static List<FantaDraw> PlanFantaDose(GameItem pot, string id, int need)
        {
            var draws = new List<FantaDraw>(); int missing = need;
            foreach (var source in FantaSources(id, pot))
            {
                var parts = FantaParts(source);
                for (int attempt = 0; missing > 0 && attempt < 8; attempt++)
                {
                    int active = EffectivePart(parts, id), total = parts.Sum();
                    if (active <= 0 || total <= 0) break;
                    int pour = (int)Math.Min(total, DoseMath.RequiredSolution(missing, active, total));
                    var taken = WaterHelper.SplitProportionally(parts, pour);
                    // Native integer distribution can over-deliver by a few microlitres.
                    for (int adjust = 0; EffectivePart(taken, id) > missing && pour > 0 && adjust < Liquid.Liquids.Count + 2; adjust++)
                        taken = WaterHelper.SplitProportionally(parts, --pour);
                    int delivered = EffectivePart(taken, id);
                    if (delivered <= 0 || delivered > missing) break;
                    draws.Add(new FantaDraw { Source = source, Before = (int[])parts.Clone(), Volume = pour, Delivered = delivered });
                    for (int i = 0; i < parts.Length; i++) parts[i] -= taken[i];
                    missing -= delivered;
                    if (missing == 0) return draws;
                }
            }
            throw new InvalidOperationException("[缺试剂] 本次需要" + LiquidName(id) + "有效 " + Vol(need)
                + "，当前来源无法精确满足，还差 " + Vol(missing) + "；尚未投本次料。");
        }

        static void FantaReactionTick(NativeChem.ProbeResult p)
        {
            CheckFantaPlate(p);
            p.Pot.ValidateShapeState();
            ChemicalHelper.HandleTemperatureReaction(p.Pot);
            MarkAutoWork();
            p.Pot.ValidateShapeState();
        }

        static IEnumerator SettleFanta(NativeChem.ProbeResult p, FantaBudget budget)
        {
            int stagnant = 0; long previous = long.MaxValue;
            for (int tick = 0; tick < 2400; tick++)
            {
                CheckFantaPlate(p, FantaAmount(p.Pot, "methylol") > 0);
                int stabilizer = FantaAmount(p.Pot, "stabilizing_agent"), protein = FantaAmount(p.Pot, "protein"), fat = FantaAmount(p.Pot, "fat");
                long remaining = (long)stabilizer + protein + fat;
                if (remaining == 0) yield break;
                if (remaining >= previous) stagnant++; else stagnant = 0;
                if (stagnant > 64) throw new InvalidOperationException("芬太前处理连续64拍没有进展，原液保留。");
                previous = remaining;
                if (stabilizer > 0 || protein > 0)
                {
                    int net = FantaEffective(p.Pot, budget.Reagent);
                    DoseFanta(p, budget, budget.Reagent, Math.Max(0, Math.Min(1000, checked(stabilizer + protein)) - net));
                }
                if (fat > 0)
                    DoseFanta(p, budget, "lipobond_agent", Math.Max(0, (fat + 7) / 8 - FantaAmount(p.Pot, "lipobond_agent")));
                CheckFantaPlate(p, FantaAmount(p.Pot, "methylol") > 0);
                FantaReactionTick(p);
                if (tick % 32 == 31)
                {
                    Status("芬太前处理：稳定剂 " + Vol(FantaAmount(p.Pot, "stabilizing_agent")) + "；蛋白 " + Vol(FantaAmount(p.Pot, "protein")) + "；脂肪 " + Vol(FantaAmount(p.Pot, "fat")));
                    yield return null;
                }
            }
            throw new InvalidOperationException("芬太前处理达到2400拍上限，原液保留。");
        }

        static IEnumerator NeutralizeFanta(NativeChem.ProbeResult p, FantaBudget budget)
        {
            int stagnant = 0; long previous = long.MaxValue;
            for (int tick = 0; tick < 512; tick++)
            {
                CheckFantaPlate(p);
                int acid = FantaAmount(p.Pot, "acid"), alkali = FantaAmount(p.Pot, "base");
                if (acid == 0 && alkali == 0) yield break;
                long remaining = (long)acid + alkali;
                stagnant = remaining >= previous ? stagnant + 1 : 0; previous = remaining;
                if (stagnant > 32) throw new InvalidOperationException("酸碱残余处理无进展，停止补料并保留原液。");
                // Process already-mixed acid/base first. Top up the opposite reagent only
                // when one side has actually run out, rather than chasing a moving ratio.
                if (acid == 0) DoseFanta(p, budget, "acid", Math.Min(1000, alkali));
                else if (alkali == 0) DoseFanta(p, budget, "base", Math.Min(1000, acid));
                FantaReactionTick(p);
                if (tick % 32 == 31) { Status("芬太酸碱残余：" + FantaResidue(p.Pot)); yield return null; }
            }
            throw new InvalidOperationException("酸碱处理达到512拍上限，残余保留。");
        }

        static IEnumerator PrepareFantaBatch(NativeChem.ProbeResult p)
        {
            string blocked = Preflight(p.Recipe, null, p.Pot, true, true);
            if (blocked != null) throw new InvalidOperationException("芬太开工条件变化，尚未投料：" + blocked);
            var budget = lastCheckedPlan.Fanta;
            CheckFantaPlate(p);
            Trace("芬太步骤1 · 加热器 #" + p.Machine.uniqueId + " 的工作瓶 #" + p.Pot.uniqueId + "；直接处理前体液，不搅拌。初始：" + Composition(p.Pot));
            CaptureRunStage(1, "实际（未搅拌）", "methylol", p.Pot);
            yield return null;
            Trace("芬太步骤2 · 先按真实成分处理稳定剂、蛋白及脂肪，再处理残余酸碱。");
            var settling = SettleFanta(p, budget);
            using (settling as IDisposable) while (settling.MoveNext()) yield return settling.Current;
            var neutralizing = NeutralizeFanta(p, budget);
            using (neutralizing as IDisposable) while (neutralizing.MoveNext()) yield return neutralizing.Current;
            var synthesis = SynthesizeFanta(p, budget);
            using (synthesis as IDisposable) while (synthesis.MoveNext()) yield return synthesis.Current;
            // Reducing-agent solution can introduce ordinary impurities of its own.
            settling = SettleFanta(p, budget);
            using (settling as IDisposable) while (settling.MoveNext()) yield return settling.Current;
            neutralizing = NeutralizeFanta(p, budget);
            using (neutralizing as IDisposable) while (neutralizing.MoveNext()) yield return neutralizing.Current;
            if (!FantaReady(p.Pot)) throw new InvalidOperationException("芬太反应未完成或存在不可清除残余：" + FantaResidue(p.Pot));
            foreach (var usage in actualUsage) Trace("  芬太实际耗料：" + LiquidName(usage.Key) + "原溶液 " + Vol(usage.Value)
                + "；有效投入 " + Vol(budget.Used.TryGetValue(usage.Key, out int amount) ? amount : 0));
            CaptureRunStage(2, "实际", "fanta", p.Pot);
            FinishPreparedBatch(p);
        }

        static void DeliverFantaDry(NativeChem.ProbeResult p)
        {
            CheckFantaPlate(p);
            var finisher = Scan().Where(x => x.IsID("chem_finisher") && CanMergeDryFinisher(x, p.Pot, p.Recipe))
                .OrderBy(x => x.uniqueId).FirstOrDefault() ?? FindDryFinisher(p.Pot, p.Recipe);
            if (finisher == null)
            {
                compactOutcome = Warn("芬太干料已净化；无可接收制剂机，工作瓶 #" + p.Pot.uniqueId + " 留在加热器 #" + p.Machine.uniqueId + "，待玩家安排。");
                Trace(compactOutcome);
                return;
            }
            int reportId = p.Pot.uniqueId;
            string message = MoveDryBottle(p.Machine, finisher, p.Pot, p.Recipe, ref reportId);
            runPotId = reportId;
            compactOutcome = Good("芬太干料 → 制剂机 #" + finisher.uniqueId + " · 接收瓶 #" + reportId + "；待玩家制剂。");
            Trace("芬太步骤4 · 无需蒸发。" + message);
        }
    }
}

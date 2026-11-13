using System;
using System.Collections;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Cooperative main-thread work: never split a native dose/reaction itself.
        // A single native operation may exceed the budget; yield immediately after it.
        sealed class FantaWorkSlice : IDisposable
        {
            readonly System.Diagnostics.Stopwatch wall = System.Diagnostics.Stopwatch.StartNew();
            readonly System.Diagnostics.Stopwatch work = System.Diagnostics.Stopwatch.StartNew();
            readonly string stage;
            readonly int machineId;
            readonly long initialScans;
            int ticks, sliceTicks, yields;
            double activeMs, maxSliceMs, nextProgressMs;
            public FantaWorkSlice(string stage, NativeChem.ProbeResult p)
            {
                this.stage = stage;
                machineId = p.Machine.uniqueId; initialScans = SceneSnapshot.TotalScans;
            }
            public bool EndTick()
            {
                ticks++; sliceTicks++;
                return work.Elapsed.TotalMilliseconds >= 4 || sliceTicks >= 32;
            }
            public bool ProgressDue()
            {
                if (wall.Elapsed.TotalMilliseconds < nextProgressMs) return false;
                nextProgressMs = wall.Elapsed.TotalMilliseconds + 250;
                return true;
            }
            public void Pause()
            {
                work.Stop();
                double elapsed = work.Elapsed.TotalMilliseconds;
                activeMs += elapsed; maxSliceMs = Math.Max(maxSliceMs, elapsed);
                work.Reset(); // Disposal while suspended must not count this slice twice.
            }
            public void Resume()
            {
                yields++; sliceTicks = 0; work.Restart();
            }
            public void Dispose()
            {
                Pause();
                if (ticks == 0) return;
                Trace("[芬太阶段耗时] " + stage + "；加热器 #" + machineId + "；原生反应 " + ticks
                    + " 拍；让帧 " + yields + " 次；库存扫描 " + (SceneSnapshot.TotalScans - initialScans)
                    + " 次；工作 " + activeMs.ToString("0.0") + " ms；总历时 " + wall.Elapsed.TotalMilliseconds.ToString("0.0")
                    + " ms；最大连续工作 " + maxSliceMs.ToString("0.0") + " ms（每段目标4 ms，单次原生操作不可拆分）。");
            }
        }

        // With at most 1000 uL reducing agent, the native reaction always consumes
        // q=min(feed, reagent) per tick at either valid temperature rate. Calculate
        // its complete integer tail before dosing; execute only the native reaction.
        static int FantaFeedForDose(int reagent)
        {
            int total = 0;
            while (reagent > 0)
            {
                total = checked(total + reagent);
                reagent -= (int)Math.Round(reagent * 0.75);
            }
            return total;
        }

        static int SafeFantaDose(int feed)
        {
            int low = 0, high = Math.Min(1000, feed);
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (FantaFeedForDose(middle) <= feed) low = middle;
                else high = middle - 1;
            }
            return low;
        }

        static IEnumerator SynthesizeFanta(NativeChem.ProbeResult p, FantaBudget budget)
        {
            CheckFantaPlate(p);
            if (FantaAmount(p.Pot, "methylol") == 0) yield break;
            if (FantaAmount(p.Pot, "stabilizing_agent") != 0)
                throw new InvalidOperationException("稳定剂尚未处理完，禁止启动芬太合成。");
            var plate = p.Machine; var c = p.Recipe;
            originalTarget = plate.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt();
            activeTarget = c.Target; activeModules = MachineBlender.GetUpgradeCount(plate); heatingFailure = null;
            activePlate = plate;
            try
            {
                plate.ModifyTag(MachineHeatingPlate.CURRENT_TARGET_TEMP, s => s.SetInt(c.Target), false);
                plate.ModifyTag(MachineHeatingPlate.HEATING_PLATE_STATE, s => s.SetInt(2), false);
                Trace("芬太步骤3 · 原位控温合成：加热器 #" + plate.uniqueId + "，工作瓶 #" + p.Pot.uniqueId
                    + "，目标 " + c.Target + "℃；还原剂按原生整数尾量分次补充，不按一次理论总量过投。");
                using (var slice = new FantaWorkSlice("合成", p))
                {
                    int stagnant = 0, previousFeed = FantaAmount(p.Pot, "methylol");
                    for (int tick = 0; tick < 16384; tick++)
                    {
                        CheckFantaPlate(p);
                        if (!ControlHeating(p.Parts.Container, plate)) throw new InvalidOperationException(heatingFailure);
                        int feed = FantaAmount(p.Pot, "methylol");
                        if (feed == 0)
                        {
                            if (FantaAmount(p.Pot, "reducing_agent") > 0)
                                throw new InvalidOperationException("前体耗尽但还原剂有残余，不能靠过滤或蒸发清除：" + FantaResidue(p.Pot));
                            Trace("  芬太合成结束：前体及还原剂均为0，目标成分 " + Vol(FantaAmount(p.Pot, "fanta")) + "；已关闭加热并恢复原目标。");
                            yield break;
                        }
                        if (FantaAmount(p.Pot, "stabilizing_agent") > 0)
                            throw new InvalidOperationException("反应中重新检测到稳定剂，停止合成。");
                        if (FantaAmount(p.Pot, "reducing_agent") == 0)
                            DoseFanta(p, budget, "reducing_agent", SafeFantaDose(feed));
                        MachineHeatingPlate.HandleHeating(p.Parts.Container, 2, plate);
                        MarkAutoWork();
                        if (heatingFailure != null) throw new InvalidOperationException(heatingFailure);
                        FantaReactionTick(p);
                        int remaining = FantaAmount(p.Pot, "methylol");
                        stagnant = remaining < previousFeed || SceneSnapshot.Temp(p.Pot) < 110 ? 0 : stagnant + 1;
                        previousFeed = remaining;
                        if (stagnant > 180) throw new InvalidOperationException("芬太合成连续180拍无进展，已关火保留原液。");
                        if (slice.EndTick())
                        {
                            if (slice.ProgressDue())
                                Status("芬太合成：加热器 #" + plate.uniqueId + "；" + SceneSnapshot.Temp(p.Pot).ToString("0.0")
                                    + "℃；前体剩 " + Vol(remaining) + "；产物 " + Vol(FantaAmount(p.Pot, "fanta")));
                            slice.Pause();
                            yield return null;
                            slice.Resume();
                        }
                    }
                    throw new InvalidOperationException("芬太合成达到16384拍上限，已关火保留原液。");
                }
            }
            finally { StopPlate(); }
        }
    }
}

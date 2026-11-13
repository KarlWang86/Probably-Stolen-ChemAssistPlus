using System;
using System.Collections;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
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
                    CheckFantaPlate(p);
                    FantaReactionTick(p);
                    int remaining = FantaAmount(p.Pot, "methylol");
                    stagnant = remaining < previousFeed || SceneSnapshot.Temp(p.Pot) < 110 ? 0 : stagnant + 1;
                    previousFeed = remaining;
                    if (stagnant > 180) throw new InvalidOperationException("芬太合成连续180拍无进展，已关火保留原液。");
                    if (tick % 32 == 31)
                    {
                        Status("芬太合成：加热器 #" + plate.uniqueId + "；" + SceneSnapshot.Temp(p.Pot).ToString("0.0")
                            + "℃；前体剩 " + Vol(remaining) + "；产物 " + Vol(FantaAmount(p.Pot, "fanta")));
                        yield return null;
                    }
                }
                throw new InvalidOperationException("芬太合成达到16384拍上限，已关火保留原液。");
            }
            finally { StopPlate(); }
        }
    }
}

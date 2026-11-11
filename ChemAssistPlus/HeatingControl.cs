using System;
using System.Linq;
using HarmonyLib;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        static GameItem activePlate;
        static int activeTarget, originalTarget, activeModules;
        static void StopPlate()
        {
            // 所有正常/取消/异常出口关闭本批板并恢复借用前目标。
            if (activePlate == null) return;
            var plate = activePlate;
            bool stopped = false;
            try { plate.ModifyTag(MachineHeatingPlate.HEATING_PLATE_STATE, s => s.SetInt(0), false); stopped = true; }
            finally
            {
                try { plate.ModifyTag(MachineHeatingPlate.CURRENT_TARGET_TEMP, s => s.SetInt(originalTarget), false); }
                finally { if (stopped) { activePlate = null; activeTarget = 0; } }
            }
            log?.Log("[加热清理] " + Name(plate) + " #" + plate.uniqueId + " 已关火；恢复原目标 " + originalTarget + "℃。");
        }

        static string heatingFailure;
        static bool HeatingHookReady()
        {
            var method = AccessTools.Method(typeof(MachineHeatingPlate), "HandleHeating", new[] { typeof(GameSlotInventory), typeof(int), typeof(GameItem) });
            return method != null && Harmony.GetPatchInfo(method)?.Prefixes.Any(p => p.PatchMethod.DeclaringType == typeof(HeatingControl)) == true;
        }
        internal static bool ControlHeating(GameSlotInventory slot, GameItem machine)
        {
            if (!busy || job == null || machine != activePlate) return true;
            try
            {
                if (heatingFailure != null) throw new InvalidOperationException(heatingFailure);
                if (slot?.childItem != contextPot || !Owned(machine) || !Owned(contextPot)
                    || MachineBlender.GetUpgradeCount(machine) != activeModules
                    || machine.GetTagReadonly(MachineHeatingPlate.HEATING_PLATE_STATE).GetInt() == 0)
                    throw new InvalidOperationException("本批加热条件改变（瓶子、模块、所有权或开关），已停止。");
                if (activeModules < 2)
                    machine.ModifyTag(MachineHeatingPlate.CURRENT_TARGET_TEMP, s => s.SetInt(activeTarget), false);
                else if (machine.GetTagReadonly(MachineHeatingPlate.CURRENT_TARGET_TEMP).GetInt() != activeTarget)
                    throw new InvalidOperationException("玩家修改了目标温度，自动流程已让出控制。");
                if (contextChain == null || ChemAssistPlus.Core.SceneSnapshot.Temp(contextPot) > contextChain.High + 0.1)
                    throw new InvalidOperationException("工作瓶超过本配方安全温区。");
                return true;
            }
            catch (Exception e)
            {
                heatingFailure = "自动控温中断：" + e.Message;
                try { StopPlate(); } catch (Exception stop) { heatingFailure += "；关闭加热失败：" + stop.Message; }
                return false; // Never let a patch exception escape into the native game tick.
            }
        }
    }

    [HarmonyPatch(typeof(MachineHeatingPlate), "HandleHeating", new[] { typeof(GameSlotInventory), typeof(int), typeof(GameItem) })]
    internal static class HeatingControl
    {
        static bool Prefix(GameSlotInventory __0, GameItem __2) => Plus.ControlHeating(__0, __2);
    }
}


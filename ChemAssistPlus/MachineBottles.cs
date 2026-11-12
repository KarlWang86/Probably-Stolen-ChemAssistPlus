using System;
using System.Collections.Generic;
using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        const string PreferredMachineBottleTag = "CHEMASSISTPLUS_PREFERRED_BOTTLE_V1";
        static bool BottleMachine(GameItem machine) => machine.IsID("blender") || machine.IsID("evaporator") || machine.IsID("chem_finisher");
        static bool EmptyMachineBottle(GameItem bottle) => bottle != null && Owned(bottle)
            && bottle.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) && !bottle.IsTag(WaterHelper.FILTER_CONTAINER_TAG)
            && LiveLiquidVolume(bottle) == 0 && WaterHelper.GetTotalVolume(bottle) == 0;

        static void RememberMachineBottles()
        {
            // Capture the player's setup before any nightly dry staging or batch moves.
            foreach (var machine in Scan().Where(BottleMachine))
            {
                var bottle = MachineView.Walk(machine).Container?.childItem;
                if (bottle == null || !Owned(bottle) || bottle.uniqueId <= 0) continue;
                if (machine.GetTagReadonly(PreferredMachineBottleTag).GetInt() != bottle.uniqueId)
                    machine.ModifyTag(PreferredMachineBottleTag, tag => tag.SetInt(bottle.uniqueId), false);
            }
        }

        static string RefillMachineBottles()
        {
            if (!autoProcessing || busy || !PlayerStore.IsInstanceExist()) return "";
            var machines = Scan().Where(BottleMachine).OrderBy(x => x.uniqueId).ToList();
            var messages = new List<string>();
            // Restore every available original first, before another machine can borrow it by size.
            for (int pass = 0; pass < 2; pass++)
            foreach (var machine in machines)
            {
                var slot = MachineView.Walk(machine).Container;
                if (!Present(machine) || slot == null || slot.childItem != null) continue;
                int originalId = machine.GetTagReadonly(PreferredMachineBottleTag).GetInt();
                var bottle = Scan().Where(x => LooseBottle(x) && EmptyMachineBottle(x) && GraphUtils.CanAccept(slot, x)
                        && (pass != 0 || x.uniqueId == originalId))
                    .OrderByDescending(WaterHelper.GetTotalCapacity).ThenBy(x => x.uniqueId).FirstOrDefault();
                if (bottle == null) continue;
                try
                {
                    if (slot.childItem != null || !LooseBottle(bottle) || !EmptyMachineBottle(bottle)) continue;
                    MovePot(bottle, slot);
                    nightDidWork = true;
                    string message = Name(machine) + " #" + machine.uniqueId + "："
                        + (bottle.uniqueId == originalId ? "放回原空瓶" : "补入可用大空瓶") + " #" + bottle.uniqueId
                        + "，容量 " + Vol(WaterHelper.GetTotalCapacity(bottle)) + "。";
                    messages.Add(message); log?.Log("[过夜补瓶] " + message);
                    nightDetails.Add(new BatchDisplay { Title = "机器 #" + machine.uniqueId + " · 补瓶", Detail = message });
                }
                catch (Exception error)
                {
                    log?.Error("[过夜补瓶] 机器 #" + machine.uniqueId + "，瓶 #" + bottle.uniqueId + "：" + error);
                    messages.Add("机器 #" + machine.uniqueId + " 补瓶中止，瓶子当前位置：" + SceneSnapshot.Where(bottle));
                }
            }
            foreach (var machine in machines.Where(x => MachineView.Walk(x).Container != null && MachineView.Walk(x).Container.childItem == null))
            {
                string message = Name(machine) + " #" + machine.uniqueId + "：没有可用散放空瓶，保留空槽。";
                messages.Add(message); log?.Log("[过夜补瓶] " + message);
            }
            if (messages.Count > 0) InvalidateMaterialIcons();
            return string.Join("\n", messages);
        }

        static bool CanReceiveEmptyFinisher(GameItem finisher, GameItem source)
        {
            var target = MachineView.Walk(finisher).Container?.childItem;
            return target != source && EmptyMachineBottle(target) && source != null && Owned(source)
                && WaterHelper.GetTotalVolume(source) <= WaterHelper.GetFreeCapacity(target);
        }
    }
}

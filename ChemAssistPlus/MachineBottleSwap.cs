using System;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Same rule checks as a native slot insertion, without changing the occupied slot.
        // The swap itself is delegated to GraphUtils; previews never move either bottle.
        static bool BottleFitsSwapSlot(GameItem item, GameSlotInventory slot)
        {
            if (!slot.MayHaveValidInventorySlot(item) || item.MaxNumRemove() < 1) return false;
            var shape = new GridShapeBuilder(item.modifiedShape).SetTransform(0, 0, false, slot.defaultOrientation);
            var marker = new SlotMarker { item = item, inventory = slot, itemGridShape = shape, numTransfer = 1 };
            return item.CheckIsStateValid(slot, marker, shape, item.modifiedState)
                && slot.CheckChangeParentShapeStateModifier(item, marker, shape, item.modifiedState)
                && item.MaxNumAdd(marker) >= 1;
        }

        static bool CanSwapLargerWorkBottle(GameItem source, GameSlotInventory targetSlot, long volume = -1)
        {
            var target = targetSlot?.childItem;
            if (source == null || !Owned(source) || !EmptyMachineBottle(target) || source == target
                || source.unitCount != 1 || target.unitCount != 1
                || !(source.parentInventory is GameSlotInventory sourceSlot) || sourceSlot.childItem != source
                || !source.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) || source.IsTag(WaterHelper.FILTER_CONTAINER_TAG)) return false;
            var sourceMachine = sourceSlot.GetParentItem();
            if (sourceMachine == null || !Owned(sourceMachine) || (!BottleMachine(sourceMachine) && !sourceMachine.IsID("heating_plate"))
                || MachineView.Walk(sourceMachine).Container != sourceSlot) return false;
            var targetMachine = targetSlot.GetParentItem();
            if (targetMachine == null || !Owned(targetMachine) || !BottleMachine(targetMachine)
                || MachineView.Walk(targetMachine).Container != targetSlot) return false;
            var buffer = EmporiumEntry.Instance?.swapBufferElement;
            if (buffer == null || buffer.childItem != null || !GraphUtils.CanAccept(buffer, source)) return false;
            return WaterHelper.GetTotalCapacity(source) > WaterHelper.GetTotalCapacity(target)
                && (volume < 0 || volume <= WaterHelper.GetTotalCapacity(source))
                && BottleFitsSwapSlot(source, targetSlot) && BottleFitsSwapSlot(target, sourceSlot);
        }

        static void SwapLargerWorkBottle(GameItem source, GameSlotInventory targetSlot)
        {
            source.ValidateShapeState(); targetSlot.childItem?.ValidateShapeState();
            if (!CanSwapLargerWorkBottle(source, targetSlot))
                throw new InvalidOperationException("大工作瓶与下游空瓶的交换条件变化，尚未移交。");
            var target = targetSlot.childItem;
            var sourceSlot = (GameSlotInventory)source.parentInventory;
            var sourceMachine = sourceSlot.GetParentItem();
            var targetMachine = targetSlot.GetParentItem();
            bool swapped;
            try { swapped = GraphUtils.SwapItemsInPlace(source, target); }
            catch (Exception error)
            {
                throw new InvalidOperationException("原生换瓶异常，已停止；工作瓶 #" + source.uniqueId + " 位于 "
                    + SceneSnapshot.Where(source) + "；空瓶 #" + target.uniqueId + " 位于 " + SceneSnapshot.Where(target), error);
            }
            if (!swapped || targetSlot.childItem != source || sourceSlot.childItem != target)
                throw new InvalidOperationException("原生换瓶未完成，已停止；工作瓶 #" + source.uniqueId + " 位于 "
                    + SceneSnapshot.Where(source) + "；空瓶 #" + target.uniqueId + " 位于 " + SceneSnapshot.Where(target));
            sourceMachine.ModifyTag(PreferredMachineBottleTag, tag => tag.SetInt(target.uniqueId), false);
            targetMachine.ModifyTag(PreferredMachineBottleTag, tag => tag.SetInt(source.uniqueId), false);
            Trace("[大瓶移交] " + Name(sourceMachine) + " #" + sourceMachine.uniqueId + " → " + Name(targetMachine)
                + " #" + targetMachine.uniqueId + "；工作瓶 #" + source.uniqueId + "（容量 " + Vol(WaterHelper.GetTotalCapacity(source))
                + "，实际原液 " + Vol(WaterHelper.GetTotalVolume(source)) + "）整瓶移交；空瓶 #" + target.uniqueId
                + "（容量 " + Vol(WaterHelper.GetTotalCapacity(target)) + "）换回原机器容器槽。");
            MarkAutoWork();
        }
    }
}

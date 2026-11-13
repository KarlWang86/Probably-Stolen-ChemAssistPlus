using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    // Native graph edges are changed synchronously on Unity's main thread. Only
    // invalidate here: never traverse inventory or invoke gameplay from a patch.
    [HarmonyPatch]
    internal static class InventoryGraphChanges
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            // Keep game types literal so the native scanner can verify each target.
            yield return AccessTools.Method(typeof(GameItem), "AddParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameItem), "RemoveParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameSlotInventory), "AddParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameSlotInventory), "RemoveParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameGridInventory), "AddParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameGridInventory), "RemoveParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameGridScrollableInventory), "AddParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameGridScrollableInventory), "RemoveParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameCharacterRaidInventory), "AddParent", new[] { typeof(GraphNodeStorage) });
            yield return AccessTools.Method(typeof(GameCharacterRaidInventory), "RemoveParent", new[] { typeof(GraphNodeStorage) });
        }

        internal static bool Ready() => TargetMethods().All(method => method != null
            && Harmony.GetPatchInfo(method)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(InventoryGraphChanges)) == true);

        static void Postfix(bool __result)
        {
            if (__result) SceneSnapshot.Invalidate();
        }
    }
}

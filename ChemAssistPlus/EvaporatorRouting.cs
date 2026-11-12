using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Saved by the game's ordinary machine tag serialization, not a cross-save static cache.
        const string EvaporatorRecipeMemoryTag = "CHEMASSISTPLUS_EVAPORATOR_RECIPE_V1";

        static Recipe EvaporatorRoute(GameItem machine)
        {
            var bottle = MachineView.Walk(machine).Container?.childItem;
            var actual = bottle == null ? null : Detect(bottle, null);
            if (actual != null) return actual;
            string remembered = machine.GetTagReadonly(EvaporatorRecipeMemoryTag).GetString();
            return RecipeCatalog.Recipes.FirstOrDefault(x => x.ProductId == remembered);
        }

        static bool AcceptsEvaporatorRoute(GameItem machine, Recipe recipe)
        {
            if (recipe == null) return false;
            var route = EvaporatorRoute(machine);
            return route == null || route == recipe;
        }

        static int EvaporatorRoutePriority(GameItem machine, Recipe recipe) =>
            EvaporatorRoute(machine) == recipe ? 0 : 1;

        static void RememberEvaporatorRoute(GameItem machine, Recipe recipe)
        {
            if (recipe == null || !Owned(machine) || !machine.IsID("evaporator")) return;
            if (machine.GetTagReadonly(EvaporatorRecipeMemoryTag).GetString() == recipe.ProductId) return;
            machine.ModifyTag(EvaporatorRecipeMemoryTag, tag => tag.SetString(recipe.ProductId), false);
            log?.Log("[蒸发器原料记忆] 蒸发器 #" + machine.uniqueId + " → " + ChemNames.ProductName(recipe.ProductId)
                + "；取走原液或留下空瓶后保留，随游戏正常存档保存。");
        }

        static void RememberOccupiedEvaporators()
        {
            // Actual single-recipe contents establish the initial layout or an intentional manual reassignment.
            // Empty or mixed bottles never erase/overwrite a known route. Preflight never calls this writer.
            foreach (var machine in Scan().Where(x => x.IsID("evaporator")))
            {
                var bottle = MachineView.Walk(machine).Container?.childItem;
                if (bottle != null) RememberEvaporatorRoute(machine, Detect(bottle, null));
            }
        }

        static string EvaporatorRouteSummary(GameItem machine, Recipe recipe) =>
            "蒸发器 #" + machine.uniqueId + "（" + (EvaporatorRoute(machine) == recipe ? "沿用" : "首次分配")
                + ChemNames.ProductName(recipe.ProductId) + "原液位置）";
    }
}

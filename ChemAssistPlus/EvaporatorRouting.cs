using System.Linq;
using ChemAssistPlus.Core;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        // Saved by the game's ordinary machine tag serialization, not a cross-save static cache.
        const string EvaporatorRecipeMemoryTag = "CHEMASSISTPLUS_EVAPORATOR_RECIPE_V1";
        static CustomUIWindow routeWindow;

        static void OpenEvaporatorRoute(GameItem machine)
        {
            if (!ReadyForUi() || !Present(machine)) return;
            routeWindow?.Close();
            var route = EvaporatorRoute(machine);
            var bottle = MachineView.Walk(machine).Container?.childItem;
            bool editable = !busy && (bottle == null || EmptyMachineBottle(bottle));
            var ui = CustomUIManager.Window(Id + ".evaporator_route", "蒸发器 #" + machine.uniqueId + " · 原液分工", CustomUIManager.LAYER_PAPER)
                .UseMetaPrefabs(false).UseMachineryStyle(true).SetSize(450, 540).SetDraggable(true).SetCloseOnEscape(true);
            ui.AddLabel("当前分工：" + (route == null ? "尚未分配" : ChemNames.ProductName(route.ProductId))
                + "\n原液槽：" + (bottle == null ? "空槽" : "瓶 #" + bottle.uniqueId + "，容量 " + Vol(WaterHelper.GetTotalCapacity(bottle))))
                .AddLabel(editable ? "选择用途后保存记忆；不会移动瓶子或液体。" : "仅在助手空闲且原液槽为空或装有空瓶时可更改分工。");
            if (editable)
                foreach (var recipe in RecipeCatalog.Recipes)
                {
                    var selected = recipe;
                    ui.AddButton(ChemNames.ProductName(selected.ProductId), () => SetEvaporatorRoute(machine, selected));
                }
            ui.AddButton("关闭", () => routeWindow?.Close());
            routeWindow = ui.Show();
        }

        static void SetEvaporatorRoute(GameItem machine, Recipe recipe)
        {
            if (busy || !Present(machine)) { routeWindow?.Close(); return; }
            var bottle = MachineView.Walk(machine).Container?.childItem;
            if (bottle == null || EmptyMachineBottle(bottle)) RememberEvaporatorRoute(machine, recipe);
            OpenEvaporatorRoute(machine);
        }

        static Recipe EvaporatorRoute(GameItem machine)
        {
            var bottle = MachineView.Walk(machine).Container?.childItem;
            var actual = LiveLiquidRecipe(bottle);
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
                if (bottle != null) RememberEvaporatorRoute(machine, LiveLiquidRecipe(bottle));
            }
        }

        static string EvaporatorRouteSummary(GameItem machine, Recipe recipe) =>
            "蒸发器 #" + machine.uniqueId + "（" + (EvaporatorRoute(machine) == recipe ? "沿用" : "首次分配")
                + ChemNames.ProductName(recipe.ProductId) + "原液位置）";
    }
}

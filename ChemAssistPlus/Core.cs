using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChemAssistPlus.Core
{
    // 游戏适配层：本文件连接原生配方、库存图和机器 UI 槽位；不负责自动执行。
    // 游戏升级后优先核对这些接口，以及 Workflow.NativeControls 中的原生按钮定位。
    public sealed class Recipe
    {
        public string ProductId, ProductLiquid;
        public string[] FeedLiquids;
        public int Low, High, Target;
        public bool ManualOnly;
        public string ManualNote;
        public Recipe(string product, string active, string feed, string solvent, int low, int high, int target)
        { ProductId = product; ProductLiquid = active; FeedLiquids = solvent == null ? new[] { feed } : new[] { feed, solvent }; Low = low; High = high; Target = target; }
    }
    public static class RecipeCatalog
    {
        // 温度/投料参数是本 Mod 的路线配置；实际化学反应仍由游戏自己的 tick 推进。
        // 咖啡路线会修改共享 Recipe，切换路线后须先 ApplyRoute 再预检/执行。
        public static readonly List<Recipe> Recipes = new List<Recipe>
        {
            new Recipe("dream_dust", "dream_cap_extract", "dream_cap_mush", "alcohol", 40, 49, 45),
            new Recipe("caffeine_pill", "caffeine", "coffee_mush", "alcohol", 50, 78, 75),
            new Recipe("nightmare_dust", "nightmare_extract", "nightmare_cap_mush", "alcohol", 40, 49, 45),
            new Recipe("fanta_pill", "fanta", "methylol", "reducing_agent", 120, 129, 125),
            new Recipe("zyanide_pill", "zyanide", "zyanide", null, 0, 0, 0),
            new Recipe("birth_control_pill", "contraceptive", "contraceptive", null, 0, 0, 0),
            new Recipe("pink_alt_injector", "nomore", "nomore", null, 0, 0, 0)
        };
        public static void ApplyRoute(Recipe c, string solvent)
        {
            if (c.ProductId != "caffeine_pill") return;
            c.FeedLiquids[1] = solvent;
            c.Low = solvent == "water" ? 80 : 50;
            c.High = solvent == "water" ? 98 : 78;
            c.Target = solvent == "water" ? 85 : 75;
        }
    }
    public sealed class SceneSnapshot
    {
        // 递归扫描玩家物品及嵌套容器；HashSet 防重复、深度上限防异常库存图循环。
        // 只读快照，不缓存跨存档 GameItem 引用，不修改磁盘存档。
        public List<GameItem> All = new List<GameItem>();
        public List<GameItem> LiquidContainers => All.Where(x => x.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)).ToList();
        public static SceneSnapshot Run()
        {
            var result = new SceneSnapshot();
            if (!PlayerStore.IsInstanceExist()) return result;
            var seen = new HashSet<GraphNodeStorage>();
            // Native roots include front/back storage, counter, main inventory, showcase,
            // trash, documents, hidden/afterhour storage and vending machines.
            // Descend further into bags, tablet bottles and machine inventories.
            foreach (var item in PlayerStore.Instance.FindAllItem(true)) Visit(item, result.All, seen, 0);
            return result;
        }
        static void Visit(GraphNodeStorage node, List<GameItem> found, HashSet<GraphNodeStorage> seen, int depth)
        {
            if (node == null || depth > 24 || !seen.Add(node)) return;
            if (node is GameItem item && GeneralHelper.IsItemOwned(item)) found.Add(item);
            foreach (var child in node.children.ToArray()) Visit(child, found, seen, depth + 1);
        }
        public static int Amount(GameItem item, string liquid)
        { return item == null ? 0 : Math.Max(0, item.GetTagReadonly(Liquid.GetCurrentPartFromId(liquid)).GetInt()); }
        public static double Temp(GameItem item) => item.GetTagReadonly(ChemicalHelper.CONTAINER_TEMP_TAG).GetDouble();
        public static string Where(GameItem item)
        {
            var owner = item?.parentInventory?.GetParentItem();
            return owner != null ? owner.name + " / #" + item.uniqueId : "店内 / #" + item?.uniqueId;
        }
    }
    public sealed class MachineSlots
    {
        public GameSlotInventory Container;
        public GameGridInventory Input, Output, Module;
    }
    public static class MachineView
    {
        // 从机器原生 PixelElement 树定位 container/input/output/module。
        // Find 不深入 GameItem，防止误把瓶子/物品内部槽位当成机器自己的槽位。
        public static MachineSlots Walk(GameItem item)
        {
            if (!(item?.contentWindow?.childElement is MachineryPanelRow row)) return new MachineSlots();
            return new MachineSlots
            {
                Container = Find<GameSlotInventory>(row.GetPanel("container")?.content),
                Input = Find<GameGridInventory>(row.GetPanel("input")?.content),
                Output = Find<GameGridInventory>(row.GetPanel("output")?.content),
                // GetModuleInv assumes a powered machine (module at body index 1).
                // Blender/heating plate/finisher put modules at index 0 instead.
                Module = Find<GameGridInventory>(row.GetPanel("module")?.content)
            };
        }
        public static T Find<T>(PixelElement element) where T : class
        {
            if (element == null) return null;
            if (element is T result) return result;
            if (element is MachineryIntake intake) return Find<T>(intake.content);
            if (element is MachineryPanel panel) return Find<T>(panel.content);
            if (element is MachineryStack stack)
                foreach (var child in stack.elements) { var match = Find<T>(child); if (match != null) return match; }
            if (element is GraphNodeStorage node)
            {
                var seen = new HashSet<GraphNodeStorage>();
                var queue = new Queue<GraphNodeStorage>(); queue.Enqueue(node);
                while (queue.Count > 0)
                {
                    var child = queue.Dequeue();
                    if (!seen.Add(child)) continue;
                    if (child is T typed) return typed;
                    // Do not inspect inventory contents while finding a machine's own slot.
                    if (child is GameItem) continue;
                    foreach (var grandchild in child.children) queue.Enqueue(grandchild);
                }
            }
            return null;
        }
    }
    public static class ChemNames
    {
        public static string Name(string id) => Liquid.Liquids?.FirstOrDefault(x => x.identifier == id)?.displayName ?? id;
        public static string ProductName(string id) => ChemFinisherRecipe.GetRecipeById(id)?.displayName ?? id;
    }
    public static class NativeChem
    {
        public sealed class ProbeResult
        {
            public GameItem Machine, Solid, Pot;
            public List<GameItem> Solids = new List<GameItem>();
            public List<GameItem> InputSolids = new List<GameItem>();
            public bool AutoPot;
            public MachineSlots Parts;
            public Recipe Recipe;
            public string Block;
            public bool Ok => Block == null;
        }
        public sealed class TakeLog
        {
            public List<string> Sources = new List<string>();
            public List<string> Emptied = new List<string>();
        }
        public static List<GameItem> Blendables(GameInventory inventory) => inventory?.childItems
            .Where(x => x.IsTag(ChemicalHelper.BLENDABLE_TAG) && ChemicalHelper.GetTotalVolume(x) > 0).ToList() ?? new List<GameItem>();
        public static GameItem FirstBlendable(GameInventory inventory) => Blendables(inventory).FirstOrDefault();
        public static Recipe ChainOfSolid(GameItem item) => RecipeCatalog.Recipes.FirstOrDefault(x => SceneSnapshot.Amount(item, x.FeedLiquids[0]) > 0);
        public static GameItem FindBattery(GameItem item) => MachineHelper.GetBatterySlot(item)?.childItem;
        public static string NameOf(GameItem item) => item?.name ?? "无";
        public static int WetTotal(GameItem item) => Liquid.Liquids.Where(x => x.isEvaporative).Sum(x => SceneSnapshot.Amount(item, x.identifier));
    }
}

namespace ChemAssistPlus
{
    public sealed class ModEntry : IMod
    {
        // 原生 ModLoader 唯一入口；生命周期转交 Plus，不依赖原“一键制作”Mod。
        public void Init(ModManifest manifest) => Plus.Init(manifest);
        public void OnEnable() => Plus.Enable();
        public void OnDisable() => Plus.Disable();
    }
}


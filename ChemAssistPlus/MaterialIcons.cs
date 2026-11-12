using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class MaterialIconKind
        {
            public string SpriteName, NameKey;
            public string[] Liquids;
            public Sprite Sprite;
            public MaterialIconKind(string sprite, string name, params string[] liquids)
            { SpriteName = sprite; NameKey = name; Liquids = liquids; }
        }

        // Actual harvested materials, not solvent bottles or synthetic reagent icons.
        // Native HandleNomoreReaction converts dream_cap_extract into nomore.
        static readonly MaterialIconKind[] materialIconKinds = {
            new MaterialIconKind("coffee_bean", "item_coffee_bean_name", "coffee_mush", "caffeine"),
            new MaterialIconKind("dream_cap", "item_dream_cap_name", "dream_cap_mush", "dream_cap_extract", "nomore"),
            new MaterialIconKind("nightmare_cap", "item_nightmare_cap_name", "nightmare_cap_mush", "nightmare_extract")
        };

        sealed class BottleMaterialIcons
        {
            public GameItemElement Item;
            public GameItem WorkPot;
            public int Mask;
            public readonly List<TreeNodeRender> Main = new List<TreeNodeRender>(), Drag = new List<TreeNodeRender>();
        }
        static readonly Dictionary<GameItemElement, BottleMaterialIcons> bottleMaterialIcons = new Dictionary<GameItemElement, BottleMaterialIcons>();
        static readonly Dictionary<GameItemElement, BottleMaterialIcons> machineMaterialIcons = new Dictionary<GameItemElement, BottleMaterialIcons>();
        static readonly Vector3[] materialIconCorners = new Vector3[4];
        static float nextMaterialIconScan;

        // Native batch transfers write state with skipValidate=true. GetTagReadonly reads
        // modifiedState, which can still describe the previous contents. Never mutate either here.
        static int LiveLiquidAmount(GameItem item, string id)
        {
            if (item?.state?.dict == null) return 0;
            return item.state.dict.TryGetValue(Liquid.GetCurrentPartFromId(id), out var tag) ? Math.Max(0, tag.GetInt()) : 0;
        }
        static long LiveLiquidVolume(GameItem item) => Liquid.Liquids.Sum(x => (long)LiveLiquidAmount(item, x.identifier));
        static Core.Recipe LiveLiquidRecipe(GameItem item)
        {
            if (item == null) return null;
            var matches = Core.RecipeCatalog.Recipes.Where(x => LiveLiquidAmount(item, x.FeedLiquids[0]) > 0
                || LiveLiquidAmount(item, x.ProductLiquid) > 0).ToList();
            return matches.Count == 1 ? matches[0] : null;
        }
        internal static void InvalidateMaterialIcons() { nextMaterialIconScan = 0; }

        static int MaterialMask(GameItem item)
        {
            if (item == null || !item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)) return 0;
            int mask = 0;
            for (int i = 0; i < materialIconKinds.Length; i++)
                if (materialIconKinds[i].Liquids.Any(id => LiveLiquidAmount(item, id) > 0)) mask |= 1 << i;
            return mask;
        }

        static Sprite MaterialSprite(int index)
        {
            var kind = materialIconKinds[index];
            if (kind.Sprite == null) kind.Sprite = RenderHandler.TryLoadFromAtlas("Items/items_hydroponic_plant", kind.SpriteName);
            return kind.Sprite;
        }

        static TreeNodeRender MaterialNode(List<TreeNodeRender> nodes, int index, TreeNodeRender parent)
        {
            while (nodes.Count <= index) nodes.Add(null);
            var node = nodes[index];
            if (node == null)
            {
                node = TreeNodeRender.Instantiate().SetParent(parent);
                node.gameObject.name = "ChemAssistPlus.MaterialIcon";
                node.image.raycastTarget = false;
                node.image.type = Image.Type.Simple;
                node.image.preserveAspect = true;
                var rect = node.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                nodes[index] = node;
            }
            else if (node.parent != parent) node.SetParent(parent);
            return node;
        }

        static void MaterialBounds(RectTransform rect, Transform parent, out Vector2 min, out Vector2 max)
        {
            rect.GetWorldCorners(materialIconCorners);
            min = max = parent.InverseTransformPoint(materialIconCorners[0]);
            for (int i = 1; i < materialIconCorners.Length; i++)
            {
                Vector2 point = parent.InverseTransformPoint(materialIconCorners[i]);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
        }

        static void HideMaterialNodes(List<TreeNodeRender> nodes)
        {
            foreach (var node in nodes) if (node != null) node.image.enabled = false;
        }

        static void DrawMaterialIcons(BottleMaterialIcons icons, bool dragging, bool machine)
        {
            var item = icons.Item;
            var parent = dragging ? item.fakeMiddle : item.middle;
            var background = dragging ? item.fakeBackground : item.background;
            var nodes = dragging ? icons.Drag : icons.Main;
            if (parent == null || background == null || background.image == null)
            {
                HideMaterialNodes(nodes);
                return;
            }
            int count = 0;
            for (int i = 0; i < materialIconKinds.Length; i++) if ((icons.Mask & (1 << i)) != 0) count++;
            if (count == 0) return;
            // Slots scale/center the native image independently of the inventory grid.
            // Use its actual rendered bounds, including rotation/flip, in the unrotated overlay parent.
            MaterialBounds(background.rectTransform, parent.rectTransform, out var min, out var max);
            for (Transform ancestor = background.rectTransform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var rectMask = ancestor.GetComponent<RectMask2D>();
                var mask = ancestor.GetComponent<Mask>();
                if ((rectMask != null && rectMask.isActiveAndEnabled) || (mask != null && mask.isActiveAndEnabled))
                {
                    MaterialBounds((RectTransform)ancestor, parent.rectTransform, out var clipMin, out var clipMax);
                    min = Vector2.Max(min, clipMin); max = Vector2.Min(max, clipMax);
                }
            }
            float width = max.x - min.x, height = max.y - min.y;
            if (width <= 4 || height <= 4) { HideMaterialNodes(nodes); return; }
            float grid = UISettings.current.gridSize;
            // Leave one cell (at most half the width) at the machine's top-right for status mods.
            float statusSpace = machine ? Mathf.Min(grid, width * 0.5f) : 0;
            float size = Mathf.Max(1, machine
                ? Mathf.Min(grid, height - 4, (width - 4 - statusSpace - (count - 1)) / count)
                : Mathf.Min(grid, width - 4, (height - 4 - (count - 1)) / count));
            float stackHeight = count * size + count - 1;
            var center = (min + max) * 0.5f;
            int row = 0;
            for (int i = 0; i < materialIconKinds.Length; i++)
            {
                if ((icons.Mask & (1 << i)) == 0) continue;
                var node = MaterialNode(nodes, row, parent);
                node.image.sprite = MaterialSprite(i);
                node.image.color = new Color(1, 1, 1, background.image.color.a);
                node.image.enabled = background.image.enabled && node.image.sprite != null;
                node.rectTransform.sizeDelta = new Vector2(size, size);
                node.rectTransform.localPosition = machine
                    ? new Vector3(min.x + 2 + size * 0.5f + row * (size + 1), max.y - 2 - size * 0.5f, 0)
                    : new Vector3(center.x, center.y + stackHeight * 0.5f - size * 0.5f - row * (size + 1), 0);
                node.rectTransform.SetAsLastSibling();
                row++;
            }
            for (int i = row; i < nodes.Count; i++) if (nodes[i] != null) nodes[i].image.enabled = false;
        }

        static void RemoveBottleMaterials(BottleMaterialIcons icons)
        {
            foreach (var node in icons.Main.Concat(icons.Drag))
                if (node != null) { node.image.enabled = false; UnityEngine.Object.Destroy(node.gameObject); }
        }

        static void ClearMaterialIcons()
        {
            foreach (var icons in bottleMaterialIcons.Values) RemoveBottleMaterials(icons);
            foreach (var icons in machineMaterialIcons.Values) RemoveBottleMaterials(icons);
            bottleMaterialIcons.Clear(); machineMaterialIcons.Clear(); nextMaterialIconScan = 0;
        }

        static int MachineMaterialMask(GameItemElement machine, GameItem pot)
        {
            int mask = MaterialMask(pot);
            // An empty remembered evaporator keeps its material marker so its assigned line is visible.
            if (mask == 0 && machine.IsID("evaporator") && (pot == null || LiveLiquidVolume(pot) == 0))
            {
                var route = EvaporatorRoute(machine);
                if (route != null)
                    for (int i = 0; i < materialIconKinds.Length; i++)
                        if (materialIconKinds[i].Liquids.Contains(route.FeedLiquids[0])
                            || materialIconKinds[i].Liquids.Contains(route.ProductLiquid)) mask |= 1 << i;
            }
            return mask;
        }

        static void RefreshMachineMaterials(GameItemElement machine, HashSet<GameItemElement> live)
        {
            var pot = Core.MachineView.Walk(machine).Container?.childItem;
            int mask = MachineMaterialMask(machine, pot);
            machineMaterialIcons.TryGetValue(machine, out var icons);
            if (mask == 0 || machine.middle == null || machine.IsDestroyed() || machine.isDebugMenu) return;
            live.Add(machine);
            if (icons == null) machineMaterialIcons.Add(machine, icons = new BottleMaterialIcons { Item = machine });
            icons.Mask = mask;
            icons.WorkPot = pot;
        }

        public static void TickMaterialIcons()
        {
            if (!PlayerStore.IsInstanceExist())
            {
                if (bottleMaterialIcons.Count > 0 || machineMaterialIcons.Count > 0) ClearMaterialIcons();
                return;
            }
            // Watch existing displays every frame, so swaps/composition changes do not wait
            // for periodic discovery. The scan also discovers previously empty/hidden bottles.
            foreach (var pair in bottleMaterialIcons)
                if (pair.Key.IsDestroyed() || MaterialMask(pair.Key) != pair.Value.Mask) nextMaterialIconScan = 0;
            foreach (var pair in machineMaterialIcons)
            {
                var pot = Core.MachineView.Walk(pair.Key).Container?.childItem;
                if (pair.Key.IsDestroyed() || pot != pair.Value.WorkPot || MachineMaterialMask(pair.Key, pot) != pair.Value.Mask)
                    nextMaterialIconScan = 0;
            }
            if (Time.unscaledTime >= nextMaterialIconScan)
            {
                nextMaterialIconScan = Time.unscaledTime + 0.25f;
                var all = Scan();
                var liveBottles = new HashSet<GameItemElement>();
                var liveMachines = new HashSet<GameItemElement>();
                foreach (var item in all)
                {
                    if (item is GameItemElement element && !element.IsDestroyed() && !item.isDebugMenu)
                    {
                        int mask = MaterialMask(item);
                        if (mask != 0 && element.middle != null)
                        {
                            liveBottles.Add(element);
                            if (!bottleMaterialIcons.TryGetValue(element, out var icons))
                                bottleMaterialIcons.Add(element, icons = new BottleMaterialIcons { Item = element });
                            icons.Mask = mask;
                        }
                    }
                    if (item is GameItemElement machine && item.IsTag("MACHINE")) RefreshMachineMaterials(machine, liveMachines);
                }
                foreach (var item in bottleMaterialIcons.Keys.Where(x => !liveBottles.Contains(x)).ToArray())
                { RemoveBottleMaterials(bottleMaterialIcons[item]); bottleMaterialIcons.Remove(item); }
                foreach (var item in machineMaterialIcons.Keys.Where(x => !liveMachines.Contains(x)).ToArray())
                { RemoveBottleMaterials(machineMaterialIcons[item]); machineMaterialIcons.Remove(item); }
            }
            // Mirror native real/drag visibility each frame, so moving a bottle leaves no ghost badge.
            foreach (var icons in bottleMaterialIcons.Values)
            {
                if (icons.Item.IsDestroyed()) continue;
                DrawMaterialIcons(icons, false, false); DrawMaterialIcons(icons, true, false);
            }
            foreach (var icons in machineMaterialIcons.Values)
            {
                if (icons.Item.IsDestroyed()) continue;
                DrawMaterialIcons(icons, false, true); DrawMaterialIcons(icons, true, true);
            }
        }
    }
}

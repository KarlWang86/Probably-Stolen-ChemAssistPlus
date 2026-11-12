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
            public int Mask;
            public readonly List<TreeNodeRender> Main = new List<TreeNodeRender>(), Drag = new List<TreeNodeRender>();
        }
        sealed class MachineMaterialIcons
        {
            public PixelWindow Window;
            public int Mask;
            public readonly List<MachineryButton> Buttons = new List<MachineryButton>();
        }

        static readonly Dictionary<GameItemElement, BottleMaterialIcons> bottleMaterialIcons = new Dictionary<GameItemElement, BottleMaterialIcons>();
        static readonly Dictionary<GameItem, MachineMaterialIcons> machineMaterialIcons = new Dictionary<GameItem, MachineMaterialIcons>();
        static float nextMaterialIconScan;

        static int MaterialMask(GameItem item)
        {
            if (item == null || !item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG)) return 0;
            int mask = 0;
            for (int i = 0; i < materialIconKinds.Length; i++)
                if (materialIconKinds[i].Liquids.Any(id => Amount(item, id) > 0)) mask |= 1 << i;
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
                node.image.preserveAspect = true;
                var rect = node.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                nodes[index] = node;
            }
            else if (node.parent != parent) node.SetParent(parent);
            return node;
        }

        static void DrawBottleMaterials(BottleMaterialIcons icons, bool dragging)
        {
            var item = icons.Item;
            var parent = dragging ? item.fakeMiddle : item.middle;
            var background = dragging ? item.fakeBackground : item.background;
            var nodes = dragging ? icons.Drag : icons.Main;
            if (parent == null || background == null || background.image == null)
            {
                foreach (var node in nodes) if (node != null) node.image.enabled = false;
                return;
            }
            int count = 0;
            for (int i = 0; i < materialIconKinds.Length; i++) if ((icons.Mask & (1 << i)) != 0) count++;
            if (count == 0) return;
            float grid = UISettings.current.gridSize;
            float width = item.modifiedShape.width * grid, height = item.modifiedShape.height * grid;
            // Stay upright at the top-right of the current rotated footprint; fit all icons vertically.
            float size = Mathf.Max(1, Mathf.Min(grid, width - 4, (height - 4 - (count - 1)) / count));
            int row = 0;
            for (int i = 0; i < materialIconKinds.Length; i++)
            {
                if ((icons.Mask & (1 << i)) == 0) continue;
                var node = MaterialNode(nodes, row, parent);
                node.image.sprite = MaterialSprite(i);
                node.image.color = new Color(1, 1, 1, background.image.color.a);
                node.image.enabled = background.image.enabled && node.image.sprite != null;
                node.rectTransform.sizeDelta = new Vector2(size, size);
                node.rectTransform.anchoredPosition = new Vector2(width - size - 2, -2 - row * (size + 1));
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

        static void RemoveMachineMaterials(MachineMaterialIcons icons)
        {
            foreach (var button in icons.Buttons)
            {
                // RemoveHeaderButton detaches but does not destroy its render tree.
                if (button.handler == null) continue;
                if (icons.Window?.handler != null) icons.Window.RemoveHeaderButton(button);
                UnityEngine.Object.Destroy(button.handler.gameObject);
            }
        }

        static void ClearMaterialIcons()
        {
            foreach (var icons in bottleMaterialIcons.Values) RemoveBottleMaterials(icons);
            foreach (var icons in machineMaterialIcons.Values) RemoveMachineMaterials(icons);
            bottleMaterialIcons.Clear(); machineMaterialIcons.Clear(); nextMaterialIconScan = 0;
        }

        static void RefreshMachineMaterials(GameItem machine, HashSet<GameItem> live)
        {
            var nativeWindow = machine.contentWindow;
            var pot = Core.MachineView.Walk(machine).Container?.childItem;
            int mask = MaterialMask(pot);
            // An empty remembered evaporator keeps its material marker so its assigned line is visible.
            if (mask == 0 && machine.IsID("evaporator") && (pot == null || WaterHelper.GetTotalVolume(pot) == 0))
            {
                var route = EvaporatorRoute(machine);
                if (route != null)
                    for (int i = 0; i < materialIconKinds.Length; i++)
                        if (materialIconKinds[i].Liquids.Contains(route.FeedLiquids[0])
                            || materialIconKinds[i].Liquids.Contains(route.ProductLiquid)) mask |= 1 << i;
            }
            machineMaterialIcons.TryGetValue(machine, out var icons);
            if (icons != null && (icons.Window != nativeWindow || icons.Mask != mask))
            {
                RemoveMachineMaterials(icons); machineMaterialIcons.Remove(machine); icons = null;
            }
            if (mask == 0 || nativeWindow?.handler == null) return;
            live.Add(machine);
            if (icons != null) return;
            icons = new MachineMaterialIcons { Window = nativeWindow, Mask = mask };
            machineMaterialIcons.Add(machine, icons);
            // Native header layout places controls right-to-left; reverse insertion preserves
            // the same coffee / dream cap / nightmare cap order as the bottle's vertical list.
            for (int i = materialIconKinds.Length - 1; i >= 0; i--)
            {
                if ((mask & (1 << i)) == 0) continue;
                var sprite = MaterialSprite(i);
                if (sprite == null) continue;
                var button = new MachineryButton(sprite, sprite, sprite, sprite, null).SetSize(16, 16);
                button.SetDescription(LocHelper.GetLocalizedItem(materialIconKinds[i].NameKey));
                icons.Buttons.Add(button);
                nativeWindow.AddHeaderButton(button);
                // Display only; do not intercept the machine's dragging or click targets.
                foreach (var graphic in button.handler.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                    if (graphic is Image image) { image.type = Image.Type.Simple; image.preserveAspect = true; }
                }
            }
        }

        public static void TickMaterialIcons()
        {
            if (!PlayerStore.IsInstanceExist())
            {
                if (bottleMaterialIcons.Count > 0 || machineMaterialIcons.Count > 0) ClearMaterialIcons();
                return;
            }
            if (Time.unscaledTime >= nextMaterialIconScan)
            {
                nextMaterialIconScan = Time.unscaledTime + 0.25f;
                var all = Scan();
                var liveBottles = new HashSet<GameItemElement>();
                var liveMachines = new HashSet<GameItem>();
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
                    if (item.IsTag("MACHINE") && item.contentWindow != null) RefreshMachineMaterials(item, liveMachines);
                }
                foreach (var item in bottleMaterialIcons.Keys.Where(x => !liveBottles.Contains(x)).ToArray())
                { RemoveBottleMaterials(bottleMaterialIcons[item]); bottleMaterialIcons.Remove(item); }
                foreach (var item in machineMaterialIcons.Keys.Where(x => !liveMachines.Contains(x)).ToArray())
                { RemoveMachineMaterials(machineMaterialIcons[item]); machineMaterialIcons.Remove(item); }
            }
            // Mirror native real/drag visibility each frame, so moving a bottle leaves no ghost badge.
            foreach (var icons in bottleMaterialIcons.Values)
            {
                if (icons.Item.IsDestroyed()) continue;
                DrawBottleMaterials(icons, false); DrawBottleMaterials(icons, true);
            }
        }
    }
}

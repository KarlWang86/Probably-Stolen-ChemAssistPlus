using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemAssistPlus
{
    // 纯显示模块：按 rootCanvas 转到窗口父坐标后的可用尺寸自适应、居中和限制溢出。
    // 统一原生按钮/下拉框/输入框布局与灰底白字；不参与配方或库存操作。
    // TMP 顶点色与字体面色都会影响最终文字，更新报告后也要 RefreshPanelText。
    public static partial class Plus
    {
        static Vector2 lastCanvasSize;
        static int lastScreenWidth, lastScreenHeight;
        static float nextLayoutCheck;
        static readonly Color PanelInk = Color.white;
        static readonly Color ControlBackground = new Color(0.38f, 0.38f, 0.38f, 1f);

        static Vector2 AvailableCanvasSize()
        {
            var canvas = window.Rect.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (canvasRect != null && window.Rect.parent != null)
            {
                var corners = new Vector3[4];
                canvasRect.GetWorldCorners(corners);
                var low = window.Rect.parent.InverseTransformPoint(corners[0]);
                var high = window.Rect.parent.InverseTransformPoint(corners[2]);
                return new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y));
            }
            var parent = window.Rect.parent as RectTransform;
            return parent != null && parent.rect.width > 0 && parent.rect.height > 0
                ? parent.rect.size : new Vector2(Screen.width, Screen.height);
        }

        public static void TickPanelLayout()
        {
            if (window?.IsAlive != true || !window.IsVisible || Time.unscaledTime < nextLayoutCheck) return;
            nextLayoutCheck = Time.unscaledTime + 0.5f;
            var size = AvailableCanvasSize();
            if (size != lastCanvasSize || Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) FitPanel(true);
            foreach (var scroll in window.ContentRoot.GetComponentsInChildren<ScrollRect>(true)) FitScrollbar(scroll);
        }
        static LayoutElement Layout(GameObject root)
        {
            var element = root.GetComponent<LayoutElement>();
            return element != null ? element : root.AddComponent<LayoutElement>();
        }
        static void FlexibleWidth(GameObject root)
        {
            var element = Layout(root);
            element.layoutPriority = 2;
            element.minWidth = 0;
            element.preferredWidth = 0;
            element.flexibleWidth = 1;
        }
        static void ControlHeight(GameObject root, float height)
        {
            FlexibleWidth(root);
            var element = Layout(root);
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0;
            root.transform.localScale = Vector3.one;
            var fitter = root.GetComponent<ContentSizeFitter>();
            if (fitter != null) { fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained; }
        }
        static void StyleText(TMP_Text text, bool wrap)
        {
            var bar = text.GetComponentInParent<CompositionBarView>(true);
            bool barText = bar != null && bar.IsBarLabel(text);
            if (barText) wrap = false;
            text.transform.localScale = Vector3.one;
            text.fontSize = barText ? 12.5f : 13;
            text.enableAutoSizing = false;
            text.color = PanelInk;
            text.faceColor = new Color32(255, 255, 255, 255);
            text.enableVertexGradient = false;
            text.richText = true;
            text.overrideColorTags = false;
            text.enableWordWrapping = wrap;
            text.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.lineSpacing = 0;
            text.margin = new Vector4(2, 2, 2, 2);
        }
        static void StyleSelectable(Selectable selectable)
        {
            if (selectable.targetGraphic is Image image)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }
            selectable.transition = Selectable.Transition.ColorTint;
            var colors = selectable.colors;
            colors.normalColor = ControlBackground;
            colors.highlightedColor = new Color(0.46f, 0.46f, 0.46f, 1);
            colors.pressedColor = new Color(0.28f, 0.28f, 0.28f, 1);
            colors.selectedColor = new Color(0.42f, 0.42f, 0.42f, 1);
            colors.disabledColor = new Color(0.22f, 0.22f, 0.22f, 1);
            colors.colorMultiplier = 1;
            selectable.colors = colors;
        }
        static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }
        static void FitPanel(bool center)
        {
            if (window?.IsAlive != true) return;
            lastCanvasSize = AvailableCanvasSize();
            lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
            // Use canvas units; screen pixels would apply the game's UI scale twice.
            float scale = Mathf.Max(0.1f, Mathf.Min(1, (lastCanvasSize.x - 32) / 450f, (lastCanvasSize.y - 40) / 720f));
            float width = 450 * scale, height = 720 * scale;
            window.Rect.anchorMin = window.Rect.anchorMax = window.Rect.pivot = new Vector2(0.5f, 0.5f);
            window.SetSize(width, height);
            if (center)
            {
                window.Center();
                var canvas = window.Rect.GetComponentInParent<Canvas>();
                var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
                if (canvasRect != null)
                {
                    var corners = new Vector3[4]; canvasRect.GetWorldCorners(corners);
                    window.Rect.position = (corners[0] + corners[2]) * 0.5f;
                }
            }
            var content = window.ContentRoot;
            foreach (var group in content.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
            {
                FlexibleWidth(group.gameObject);
                group.childControlWidth = true;
                group.childControlHeight = true;
                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;
                group.childAlignment = TextAnchor.UpperLeft;
                group.spacing = 2;
                group.transform.localScale = Vector3.one;
            }
            foreach (var text in content.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, true);
            foreach (var button in content.GetComponentsInChildren<Button>(true))
            {
                // Buttons inside a dropdown template retain their native item layout.
                if (button.GetComponentInParent<TMP_Dropdown>() != null) continue;
                ControlHeight(button.gameObject, 34);
                StyleSelectable(button);
                var buttonLayout = button.GetComponent<HorizontalLayoutGroup>();
                if (buttonLayout != null) { buttonLayout.childAlignment = TextAnchor.MiddleCenter; buttonLayout.childForceExpandHeight = true; }
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    StyleText(text, false);
                    text.color = Color.white;
                    text.faceColor = new Color32(255, 255, 255, 255);
                    text.enableVertexGradient = false;
                    text.overrideColorTags = true;
                    text.alignment = TextAlignmentOptions.Center;
                }
            }
            foreach (var dropdown in content.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                ControlHeight(dropdown.gameObject, 34);
                StyleSelectable(dropdown);
                if (dropdown.captionText != null)
                {
                    StyleText(dropdown.captionText, false);
                    dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
                    Stretch(dropdown.captionText.rectTransform, 8, 2, 30, 2);
                }
                if (dropdown.itemText != null) StyleText(dropdown.itemText, false);
                if (dropdown.template != null)
                {
                    dropdown.template.anchorMin = Vector2.zero;
                    dropdown.template.anchorMax = new Vector2(1, 0);
                    dropdown.template.pivot = new Vector2(0.5f, 1);
                    dropdown.template.anchoredPosition = Vector2.zero;
                    dropdown.template.sizeDelta = new Vector2(0, Mathf.Min(190, height * 0.45f));
                    dropdown.template.localScale = Vector3.one;
                    var popupBackground = dropdown.template.GetComponent<Image>();
                    if (popupBackground != null) { popupBackground.sprite = null; popupBackground.type = Image.Type.Simple; popupBackground.color = new Color(0.24f, 0.24f, 0.24f, 1); }
                    foreach (var itemToggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
                    {
                        StyleSelectable(itemToggle);
                        var itemRect = itemToggle.transform as RectTransform;
                        if (itemRect != null)
                        {
                            itemRect.localScale = Vector3.one;
                            itemRect.anchorMin = new Vector2(0, 0.5f);
                            itemRect.anchorMax = new Vector2(1, 0.5f);
                            itemRect.sizeDelta = new Vector2(0, 30);
                        }
                    }
                    if (dropdown.itemText != null)
                    {
                        dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
                        Stretch(dropdown.itemText.rectTransform, 24, 2, 6, 2);
                    }
                }
            }
            foreach (var input in content.GetComponentsInChildren<TMP_InputField>(true))
            {
                ControlHeight(input.gameObject, 34);
                StyleSelectable(input);
                Stretch(input.textViewport, 8, 3, 8, 3);
                if (input.textComponent != null) { StyleText(input.textComponent, false); input.textComponent.alignment = TextAlignmentOptions.MidlineLeft; }
                if (input.placeholder is TMP_Text placeholder) { StyleText(placeholder, false); placeholder.color = new Color(0.72f, 0.76f, 0.78f, 1); }
            }
            foreach (var toggle in content.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.GetComponentInParent<TMP_Dropdown>() != null) continue;
                ControlHeight(toggle.gameObject, 34);
                var box = toggle.targetGraphic;
                if (box != null && box.transform != toggle.transform)
                {
                    var layoutGroup = toggle.GetComponent<LayoutGroup>(); if (layoutGroup != null) layoutGroup.enabled = false;
                    LeftCheckbox(box.rectTransform);
                    if (toggle.graphic != null)
                    {
                        if (toggle.graphic.transform.parent == box.transform) Stretch(toggle.graphic.rectTransform, 2, 2, 2, 2);
                        else LeftCheckbox(toggle.graphic.rectTransform);
                    }
                    foreach (var label in toggle.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Stretch(label.rectTransform, 25, 0, 0, 0); StyleText(label, false); label.fontSize = 12;
                        label.alignment = TextAlignmentOptions.MidlineLeft; label.margin = Vector4.zero;
                    }
                }
            }
            foreach (var scroll in content.GetComponentsInChildren<ScrollRect>(true))
            {
                if (scroll.GetComponentInParent<TMP_Dropdown>() != null) continue;
                ControlHeight(scroll.gameObject, Mathf.Max(48, height - 166));
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                if (scroll.viewport != null)
                {
                    Stretch(scroll.viewport, 0, 0, 14, 0);
                    if (scroll.viewport.GetComponent<RectMask2D>() == null) scroll.viewport.gameObject.AddComponent<RectMask2D>();
                }
                FitScrollbar(scroll);
                if (scroll.content != null)
                {
                    scroll.content.anchorMin = new Vector2(0, 1);
                    scroll.content.anchorMax = Vector2.one;
                    scroll.content.pivot = new Vector2(0.5f, 1);
                    scroll.content.sizeDelta = new Vector2(0, scroll.content.sizeDelta.y);
                    var fitter = scroll.content.GetComponent<ContentSizeFitter>() ?? scroll.content.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
            RenderCompositionWidgets();
            RefreshPanelText();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
        static void LeftCheckbox(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(20, 20);
        }
        static void FitScrollbar(ScrollRect scroll)
        {
            var scrollbar = scroll.verticalScrollbar;
            if (scrollbar == null || scroll.viewport == null || scroll.content == null) return;
            bool overflow = scroll.content.rect.height > scroll.viewport.rect.height + 1;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollbar.gameObject.SetActive(overflow);
            var rect = scrollbar.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
                rect.offsetMin = new Vector2(-6, 0); rect.offsetMax = Vector2.zero;
            }
            var background = scrollbar.GetComponent<Image>();
            if (background != null) { background.sprite = null; background.type = Image.Type.Simple; background.color = new Color(0.2f, 0.23f, 0.27f, 0.9f); }
            if (scrollbar.targetGraphic is Image track)
            { track.sprite = null; track.type = Image.Type.Simple; track.color = new Color(0.2f, 0.23f, 0.27f, 0.9f); }
            if (scrollbar.handleRect != null)
            {
                if (scrollbar.handleRect.parent is RectTransform sliding)
                {
                    sliding.offsetMin = new Vector2(0, sliding.offsetMin.y);
                    sliding.offsetMax = new Vector2(0, sliding.offsetMax.y);
                }
                scrollbar.handleRect.offsetMin = new Vector2(0, scrollbar.handleRect.offsetMin.y);
                scrollbar.handleRect.offsetMax = new Vector2(0, scrollbar.handleRect.offsetMax.y);
                var handle = scrollbar.handleRect.GetComponent<Image>();
                if (handle != null) { handle.sprite = null; handle.type = Image.Type.Simple; handle.color = new Color(0.58f, 0.62f, 0.65f, 0.9f); }
            }
            Stretch(scroll.viewport, 0, 0, overflow ? 14 : 0, 0);
        }
        static void RefreshPanelText()
        {
            if (window?.IsAlive != true) return;
            float width = Mathf.Max(40, window.Rect.rect.width - 64);
            foreach (var text in window.ContentRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.enabled) continue;
                if (text.GetComponentInParent<CompositionBarView>(true) != null) continue;
                if (text.GetComponentInParent<Selectable>() != null) continue;
                StyleText(text, true);
                FlexibleWidth(text.gameObject);
                var element = Layout(text.gameObject);
                element.minHeight = 22;
                float measuredWidth = text.rectTransform.rect.width > 1 ? Mathf.Min(width, text.rectTransform.rect.width) : width;
                element.preferredHeight = Mathf.Max(22, text.GetPreferredValues(text.text, measuredWidth, Mathf.Infinity).y + 6);
                element.flexibleHeight = 0;
            }
            LayoutRebuilder.MarkLayoutForRebuild(window.ContentRoot);
        }
    }
}

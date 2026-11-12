using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChemAssistPlus
{
    public static partial class Plus
    {
        sealed class MixRectState
        {
            readonly RectTransform rect;
            readonly Transform parent;
            readonly int sibling;
            readonly Vector2 min, max, pivot, size;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            public MixRectState(RectTransform target)
            {
                rect = target; parent = target.parent; sibling = target.GetSiblingIndex();
                min = target.anchorMin; max = target.anchorMax; pivot = target.pivot;
                size = target.sizeDelta; position = target.anchoredPosition3D;
                scale = target.localScale; rotation = target.localRotation;
            }
            public void Restore()
            {
                if (rect == null) return;
                rect.SetParent(parent, false); rect.SetSiblingIndex(sibling);
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = size; rect.anchoredPosition3D = position;
                rect.localScale = scale; rect.localRotation = rotation;
            }
        }

        sealed class MixDialogView
        {
            public readonly MixRequest Request;
            readonly TextMeshProUGUI body;
            readonly MixRectState panelRect, bodyRect, yesRect, noRect;
            readonly bool autosize, wrap, richText, raycast;
            readonly float fontSize, fontMin, fontMax;
            readonly Vector4 margin;
            readonly TextAlignmentOptions alignment;
            readonly TextOverflowModes overflow;
            readonly Color color;
            GameObject viewportObject;

            public MixDialogView(MixRequest request)
            {
                Request = request; body = request.Dialog.bodyTMP;
                panelRect = new MixRectState(request.Dialog.panel.GetComponent<RectTransform>());
                bodyRect = new MixRectState(body.rectTransform);
                yesRect = new MixRectState(request.Dialog.confirmButton.GetComponent<RectTransform>());
                noRect = new MixRectState(request.Dialog.cancelButton.GetComponent<RectTransform>());
                autosize = body.enableAutoSizing; wrap = body.enableWordWrapping; richText = body.richText;
                raycast = body.raycastTarget; fontSize = body.fontSize; fontMin = body.fontSizeMin; fontMax = body.fontSizeMax;
                margin = body.margin; alignment = body.alignment; overflow = body.overflowMode; color = body.color;
            }

            static RectTransform NewRect(string name, Transform parent)
            {
                var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false); return rect;
            }

            public void Apply()
            {
                var dialog = Request.Dialog;
                var panel = dialog.panel.GetComponent<RectTransform>();
                var canvas = panel.GetComponentInParent<Canvas>();
                var parent = panel.parent as RectTransform;
                var available = parent != null ? parent.rect.size : new Vector2(Screen.width, Screen.height);
                if ((available.x <= 0 || available.y <= 0) && canvas != null)
                    available = ((RectTransform)canvas.rootCanvas.transform).rect.size;
                // The native button text is already legible in the player's screenshot.
                // Match its scale rather than assuming one canvas unit is one screen pixel.
                float readableFont = Mathf.Max(24, dialog.confirmButtonTMP.fontSize * 0.9f);
                float uiScale = readableFont / 24;
                float width = Mathf.Min(680 * uiScale, available.x * 0.94f);
                body.enableAutoSizing = false; body.fontSize = body.fontSizeMin = body.fontSizeMax = readableFont;
                body.enableWordWrapping = true; body.richText = true; body.raycastTarget = false;
                body.margin = Vector4.zero; body.alignment = TextAlignmentOptions.TopLeft;
                body.overflowMode = TextOverflowModes.Overflow; body.color = new Color32(238, 236, 207, 255);
                float preferred = body.GetPreferredValues(Request.Body, width - 48 * uiScale, float.PositiveInfinity).y;
                float height = Mathf.Min(Mathf.Max(420 * uiScale, preferred + 132 * uiScale), available.y * 0.9f);
                panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
                panel.sizeDelta = new Vector2(width, height); panel.anchoredPosition = Vector2.zero;

                var viewport = NewRect("ChemAssistPlus.MixViewport", panel);
                viewportObject = viewport.gameObject;
                viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
                viewport.offsetMin = new Vector2(24, 92) * uiScale; viewport.offsetMax = new Vector2(-24, -24) * uiScale;
                var image = viewportObject.AddComponent<Image>(); image.color = new Color(0, 0, 0, 0.001f);
                viewportObject.AddComponent<RectMask2D>();
                var content = NewRect("ChemAssistPlus.MixText", viewport);
                content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(0, Mathf.Max(preferred + 4 * uiScale, height - 116 * uiScale));
                body.rectTransform.SetParent(content, false);
                body.rectTransform.anchorMin = Vector2.zero; body.rectTransform.anchorMax = Vector2.one;
                body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;
                body.rectTransform.localScale = Vector3.one;
                var scroll = viewportObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.inertia = false; scroll.scrollSensitivity = 30 * uiScale;
                scroll.verticalNormalizedPosition = 1;
                float buttonWidth = Mathf.Min(190 * uiScale, (width - 68 * uiScale) * 0.5f);
                PlaceButton(dialog.confirmButton, panel, -(buttonWidth + 18 * uiScale) * 0.5f, buttonWidth, uiScale);
                PlaceButton(dialog.cancelButton, panel, (buttonWidth + 18 * uiScale) * 0.5f, buttonWidth, uiScale);
            }

            static void PlaceButton(GameObject button, RectTransform panel, float x, float width, float uiScale)
            {
                var rect = button.GetComponent<RectTransform>();
                rect.SetParent(panel, false); rect.SetAsLastSibling();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 0); rect.sizeDelta = new Vector2(width, 52 * uiScale);
                rect.anchoredPosition = new Vector2(x, 22 * uiScale);
            }

            public void Restore()
            {
                // Move the native text out before destroying our temporary viewport.
                bodyRect.Restore(); yesRect.Restore(); noRect.Restore(); panelRect.Restore();
                if (body != null)
                {
                    body.enableAutoSizing = autosize; body.fontSizeMin = fontMin; body.fontSizeMax = fontMax; body.fontSize = fontSize;
                    body.enableWordWrapping = wrap; body.richText = richText; body.raycastTarget = raycast;
                    body.margin = margin; body.alignment = alignment; body.overflowMode = overflow; body.color = color;
                }
                if (viewportObject != null) { viewportObject.SetActive(false); Object.Destroy(viewportObject); }
            }
        }

        static MixDialogView mixDialogView;
        static void RestoreMixDialogView()
        {
            var view = mixDialogView; mixDialogView = null; view?.Restore();
        }
        static void ShowReadableMixDialog(MixRequest request)
        {
            mixDialogView = new MixDialogView(request);
            mixDialogView.Apply();
        }
    }
}

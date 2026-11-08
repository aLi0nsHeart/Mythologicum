using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>A temporary name for standalone controls; its graphics never intercept pointer input.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        private readonly Vector3[] corners = new Vector3[4];
        private Button button;
        private Text sourceLabel, caption;
        private RectTransform popup, canvasRect;
        private PointerEventData pointer;
        private string overrideCaption;
        private float showAt;
        private bool hovered;

        public bool IsVisible => popup != null && popup.gameObject.activeSelf;
        public string Caption => !string.IsNullOrWhiteSpace(overrideCaption) ? overrideCaption
            : sourceLabel != null && !string.IsNullOrWhiteSpace(sourceLabel.text) ? sourceLabel.text : gameObject.name;

        public static UIHoverTooltip Attach(Button target, string text = null)
        {
            if (target == null) return null;
            var tooltip = target.GetComponent<UIHoverTooltip>() ?? target.gameObject.AddComponent<UIHoverTooltip>();
            tooltip.button = target; tooltip.overrideCaption = text;
            foreach (Transform child in target.transform)
                if (child.TryGetComponent<Text>(out var label)) { tooltip.sourceLabel = label; break; }
            return tooltip;
        }

        private void LateUpdate()
        {
            if (!hovered) return;
            if (!PointerStillTargetsControl()) { Hide(); return; }
            if (Time.unscaledTime < showAt) return;
            Show();
        }

        private bool PointerStillTargetsControl()
        {
            if (pointer == null || EventSystem.current == null || button == null || !button.isActiveAndEnabled) return false;
            hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
            foreach (var hit in hits)
                if (hit.module is GraphicRaycaster)
                    return hit.gameObject != null && hit.gameObject.GetComponentInParent<Button>() == button;
            return false;
        }

        private void Show()
        {
            if (popup == null && !CreatePopup()) return;
            caption.text = Caption;
            var layout = UILayoutSettings.Current;
            float paddingX = Mathf.Max(0,layout.tooltipPadding.x), paddingY = Mathf.Max(0,layout.tooltipPadding.y);
            float margin = Mathf.Max(0,layout.tooltipScreenMargin);
            float maximum = Mathf.Max(1,Mathf.Min(layout.tooltipMaximumWidth, canvasRect.rect.width - margin*2));
            float minimum = Mathf.Clamp(layout.tooltipMinimumWidth,1,maximum);
            float width = Mathf.Clamp(caption.preferredWidth + paddingX*2, minimum, maximum);
            popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            caption.rectTransform.anchoredPosition = new Vector2(paddingX,-paddingY);
            caption.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1,width - paddingX*2));
            float height = caption.preferredHeight + paddingY*2;
            caption.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1,height - paddingY*2));
            popup.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var source = (RectTransform)transform;
            source.GetWorldCorners(corners);
            Vector3 topLeft = canvasRect.InverseTransformPoint(corners[1]);
            Rect area = canvasRect.rect;
            popup.anchoredPosition = new Vector2(Mathf.Clamp(topLeft.x+layout.tooltipOffset.x, area.xMin + margin, area.xMax - width - margin),
                Mathf.Clamp(topLeft.y + layout.tooltipOffset.y, area.yMin + margin, Mathf.Max(area.yMin+margin,area.yMax - height - margin)));
            var theme = UITheme.Current;
            if(theme!=null)
            {
                var background=popup.GetComponent<Image>();
                background.sprite=theme.tooltipFrame!=null?theme.tooltipFrame:theme.windowFrame;
                background.color=theme.frameTint;
            }
            popup.gameObject.SetActive(true); popup.SetAsLastSibling();
        }

        private bool CreatePopup()
        {
            var canvas = GetComponentInParent<Canvas>(true);
            if (canvas == null) return false;
            canvasRect = (RectTransform)canvas.rootCanvas.transform;
            var box = ManagementWidgets.Box(canvasRect, "HUD tooltip", Vector2.zero, new Vector2(200, 46));
            popup = (RectTransform)box.transform;
            popup.anchorMin = popup.anchorMax = canvasRect.pivot; popup.pivot = Vector2.zero;
            var image = box.GetComponent<Image>(); image.raycastTarget = false;
            var theme = UITheme.Current;
            if (theme != null)
            {
                image.sprite = theme.tooltipFrame!=null?theme.tooltipFrame:theme.windowFrame; image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 4; image.color = Color.white;
            }
            UIThemeElement.Mark(box, UIThemeRole.Ignore);
            var group = box.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            caption = ManagementWidgets.Label(popup, "Tooltip text", new Vector2(13, 9), new Vector2(174, 28), 17);
            caption.horizontalOverflow = HorizontalWrapMode.Wrap; caption.verticalOverflow = VerticalWrapMode.Overflow;
            return true;
        }

        public void Hide()
        {
            hovered = false; pointer = null;
            if (popup != null) popup.gameObject.SetActive(false);
        }

        public void RefreshVisible()
        {
            if (IsVisible && hovered) Show();
        }

        public void OnPointerEnter(PointerEventData data)
        { pointer = data; hovered = true; showAt = Time.unscaledTime + Mathf.Max(0,UILayoutSettings.Current.tooltipDelay); }
        public void OnPointerExit(PointerEventData data) => Hide();
        public void OnPointerDown(PointerEventData data) => Hide();
        private void OnDisable() => Hide();
        private void OnDestroy() { if (popup != null) Destroy(popup.gameObject); }
    }
}

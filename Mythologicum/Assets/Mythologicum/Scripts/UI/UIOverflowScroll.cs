using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>A scroll view is stationary until its visible content actually exceeds the viewport.</summary>
    [DefaultExecutionOrder(30001)]
    [DisallowMultipleComponent]
    public sealed class UIOverflowScroll : MonoBehaviour
    {
        private ScrollRect scroll;
        private readonly Vector3[] corners = new Vector3[4];
        public bool HasOverflow { get; private set; }

        public static UIOverflowScroll Attach(ScrollRect target)
        {
            var overflow = target.GetComponent<UIOverflowScroll>() ?? target.gameObject.AddComponent<UIOverflowScroll>();
            overflow.scroll = target;
            overflow.EnsureScrollbar();
            return overflow;
        }

        private void EnsureScrollbar()
        {
            if (scroll.verticalScrollbar != null || scroll.viewport == null) return;
            var track = new GameObject("Content scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(scroll.transform, false);
            var rect = (RectTransform)track.transform;
            rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(9, 0);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(track.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            var bar = track.GetComponent<Scrollbar>();
            bar.handleRect = handleRect; bar.targetGraphic = handle.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            // A stable gutter avoids a wrap/scrollbar feedback loop when content just fits.
            var maximum = scroll.viewport.offsetMax;
            maximum.x -= 14f; scroll.viewport.offsetMax = maximum;
        }

        public void Refresh()
        {
            if (scroll == null) scroll = GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            EnsureScrollbar();
            float height = MeasureHeight(scroll.content);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            HasOverflow = height > scroll.viewport.rect.height + .5f;
            scroll.horizontal = false;
            scroll.vertical = HasOverflow;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            if (scroll.verticalScrollbar != null)
            {
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbar.gameObject.SetActive(HasOverflow);
            }
            if (!HasOverflow)
            {
                scroll.StopMovement();
                Vector2 position = scroll.content.anchoredPosition;
                position.y = 0f; scroll.content.anchoredPosition = position;
            }
            else
            {
                Vector2 position = scroll.content.anchoredPosition;
                position.y = Mathf.Clamp(position.y, 0f, height - scroll.viewport.rect.height);
                scroll.content.anchoredPosition = position;
            }
        }

        private float MeasureHeight(RectTransform content)
        {
            var text = content.GetComponent<Text>();
            if (text != null) return Mathf.Max(0f, text.preferredHeight);
            float height = 0f;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                child.GetWorldCorners(corners);
                float top = float.MinValue, bottom = float.MaxValue;
                foreach (var corner in corners)
                {
                    float y = content.InverseTransformPoint(corner).y;
                    top = Mathf.Max(top, y); bottom = Mathf.Min(bottom, y);
                }
                var label = child.GetComponent<Text>();
                if (label != null) bottom = top - label.preferredHeight * child.localScale.y;
                height = Mathf.Max(height, content.rect.yMax - bottom);
            }
            return Mathf.Max(0f, height);
        }

        private void LateUpdate() => Refresh();
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>An illustrated panel keeps the source image's proportions; only its content is laid out.</summary>
    [DisallowMultipleComponent]
    public sealed class UIFramedPanel : MonoBehaviour
    {
        private RectTransform rect, content, header, lining;
        private Image artwork;
        private bool vertical;
        private float preferredWidth;
        [SerializeField, Tooltip("Layout preset key. Generated panels choose their stable screen name automatically.")]
        private string layoutKey;
        private RectTransform logicalContent;
        private Vector2 logicalSize;
        private readonly Vector3[] canvasCorners = new Vector3[4];
        private Vector2 preferredPosition, appliedPosition;
        private bool hasPosition;
        private bool appliedAnchorOverride, appliedPositionOverride;
        private Vector2 originalAnchorMin, originalAnchorMax, originalPivot, originalPosition;
        public RectTransform Content => content;
        public RectTransform Header => header;
        public bool Vertical => vertical;
        public Sprite Artwork => artwork != null ? artwork.sprite : null;
        public string LayoutKey => layoutKey;

        public static UIFramedPanel Attach(GameObject panel, bool vertical, float preferredWidth)
        {
            var frame = panel.GetComponent<UIFramedPanel>() ?? panel.AddComponent<UIFramedPanel>();
            frame.vertical = vertical;
            frame.preferredWidth = Mathf.Max(100f, preferredWidth);
            if (string.IsNullOrEmpty(frame.layoutKey))
                frame.layoutKey = panel.name == "Window" && panel.transform.parent != null ? panel.transform.parent.name : panel.name;
            frame.Initialize();
            frame.Refresh();
            return frame;
        }

        private void Initialize()
        {
            rect = (RectTransform)transform;
            var hitArea = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            UIThemeElement.Mark(gameObject, UIThemeRole.Ignore);
            if (content != null) return;
            lining = Child("Panel lining");
            var background = lining.gameObject.AddComponent<Image>();
            background.color = UITheme.Current != null ? UITheme.Current.panelLiningColor : new Color(.055f, .035f, .018f, .985f);
            background.raycastTarget = false;
            UIThemeElement.Mark(background.gameObject, UIThemeRole.Ignore);
            var art = Child("Panel artwork");
            artwork = art.gameObject.AddComponent<Image>();
            artwork.raycastTarget = false;
            UIThemeElement.Mark(art.gameObject, UIThemeRole.Ignore);
            content = Child("Panel content");
            header = Child("Panel heading");
        }

        private RectTransform Child(string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(transform, false);
            return (RectTransform)child.transform;
        }

        /// <summary>Fits an existing logical layout without distorting the illustrated frame.</summary>
        public void SetLogicalContent(RectTransform target, Vector2 size)
        {
            logicalContent = target;
            logicalSize = size;
            if (target != null) target.SetParent(content, false);
            Refresh();
        }

        public void Refresh()
        {
            if (rect == null) return;
            UILayoutSettings layout = UILayoutSettings.Current;
            UIPanelLayout preset = layout.Panel(layoutKey);
            ApplyPreset(preset);
            UITheme theme = UITheme.Current;
            Sprite sprite = theme != null ? (vertical ? theme.verticalInfoFrame : theme.horizontalInfoFrame) : null;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : vertical ? 438f / 627f : 885f / 500f;
            float width = preset != null && preset.preferredWidth > 0f ? preset.preferredWidth : preferredWidth;
            width = Mathf.Max(100f, width * (preset != null ? Mathf.Clamp(preset.widthScale, .1f, 3f) : 1f));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width / aspect);
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) FitWithinCanvas(canvas);
            ApplyArtwork(artwork, sprite);
            if (theme != null)
            {
                artwork.color = theme.frameTint;
                lining.GetComponent<Image>().color = theme.panelLiningColor;
            }
            Inset((RectTransform)artwork.transform, 0, 0, 0, 0);
            // Source-space safe rectangles keep text clear of the crest and curved corners.
            Inset(content, vertical ? layout.verticalBodyInsets : layout.horizontalBodyInsets);
            Inset(lining, vertical ? layout.verticalLiningInsets : layout.horizontalLiningInsets);
            Inset(header, vertical ? layout.verticalHeadingInsets : layout.horizontalHeadingInsets);
            if (logicalContent != null)
            {
                logicalContent.anchorMin = logicalContent.anchorMax = logicalContent.pivot = new Vector2(.5f, .5f);
                logicalContent.anchoredPosition = Vector2.zero;
                logicalContent.sizeDelta = logicalSize;
                float scale = Mathf.Min(1f, content.rect.width / Mathf.Max(1f, logicalSize.x),
                    content.rect.height / Mathf.Max(1f, logicalSize.y));
                logicalContent.localScale = Vector3.one * scale;
            }
        }

        private void ApplyPreset(UIPanelLayout preset)
        {
            if (preset != null && preset.overrideAnchor)
            {
                if (!appliedAnchorOverride)
                {
                    originalAnchorMin = rect.anchorMin; originalAnchorMax = rect.anchorMax; originalPivot = rect.pivot;
                    appliedAnchorOverride = true;
                }
                rect.anchorMin = rect.anchorMax = new Vector2(Mathf.Clamp01(preset.anchor.x), Mathf.Clamp01(preset.anchor.y));
                rect.pivot = new Vector2(Mathf.Clamp01(preset.pivot.x), Mathf.Clamp01(preset.pivot.y));
            }
            else if (appliedAnchorOverride)
            {
                rect.anchorMin = originalAnchorMin; rect.anchorMax = originalAnchorMax; rect.pivot = originalPivot;
                appliedAnchorOverride = false;
            }
            if (preset != null && preset.overridePosition)
            {
                if (!appliedPositionOverride)
                {
                    originalPosition = hasPosition ? preferredPosition : rect.anchoredPosition;
                    appliedPositionOverride = true;
                }
                rect.anchoredPosition = preset.position;
            }
            else if (appliedPositionOverride)
            {
                rect.anchoredPosition = originalPosition; hasPosition = false;
                appliedPositionOverride = false;
            }
        }

        private void FitWithinCanvas(Canvas canvas)
        {
            var canvasRect = (RectTransform)canvas.rootCanvas.transform;
            canvasRect.GetWorldCorners(canvasCorners);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in canvasCorners)
            {
                Vector2 point = transform.parent.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point);
            }
            float edgeMargin = Mathf.Max(0f, UILayoutSettings.Current.canvasMargin);
            Vector2 margin = new Vector2(
                Mathf.Abs(transform.parent.InverseTransformVector(canvasRect.TransformVector(Vector3.right * edgeMargin)).x),
                Mathf.Abs(transform.parent.InverseTransformVector(canvasRect.TransformVector(Vector3.up * edgeMargin)).y));
            margin = Vector2.Min(margin, (maximum - minimum) * .25f);
            minimum += margin; maximum -= margin;
            // Preserve authored positions when resizing, while still allowing callers to move a panel.
            if (!hasPosition || rect.anchoredPosition != appliedPosition)
            {
                preferredPosition = rect.anchoredPosition; hasPosition = true;
            }
            rect.anchoredPosition = preferredPosition;
            Vector2 pivotPoint = rect.localPosition;
            float width = Capacity(pivotPoint.x, minimum.x, maximum.x, rect.pivot.x);
            float height = Capacity(pivotPoint.y, minimum.y, maximum.y, rect.pivot.y);
            // Very small windows can put an authored popup origin outside the canvas entirely.
            Vector2 correction = Vector2.zero;
            if (width < 1f) correction.x = Mathf.Lerp(minimum.x, maximum.x, rect.pivot.x) - pivotPoint.x;
            if (height < 1f) correction.y = Mathf.Lerp(minimum.y, maximum.y, rect.pivot.y) - pivotPoint.y;
            rect.anchoredPosition += correction; pivotPoint += correction;
            width = Capacity(pivotPoint.x, minimum.x, maximum.x, rect.pivot.x);
            height = Capacity(pivotPoint.y, minimum.y, maximum.y, rect.pivot.y);
            float scale = Mathf.Min(1f, width / rect.rect.width, height / rect.rect.height);
            rect.localScale = Vector3.one * Mathf.Max(0f, scale);
            appliedPosition = rect.anchoredPosition;
        }

        private static float Capacity(float position, float minimum, float maximum, float pivot)
        {
            float result = float.PositiveInfinity;
            if (pivot > .0001f) result = Mathf.Min(result, (position - minimum) / pivot);
            if (pivot < .9999f) result = Mathf.Min(result, (maximum - position) / (1f - pivot));
            return result;
        }

        private static void Inset(RectTransform target, float left, float bottom, float right, float top)
        {
            target.anchorMin = new Vector2(left, bottom);
            target.anchorMax = new Vector2(1f - right, 1f - top);
            target.offsetMin = target.offsetMax = Vector2.zero;
            target.localScale = Vector3.one;
        }

        private static void Inset(RectTransform target, UIFrameInsets value)
        {
            value = value.Clamped();
            Inset(target, value.left, value.bottom, value.right, value.top);
        }

        public static void ApplyArtwork(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private void LateUpdate() => Refresh();
    }
}

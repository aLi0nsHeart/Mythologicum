using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Keeps a control's artwork consistent while its gameplay state changes.</summary>
    [DisallowMultipleComponent]
    public sealed class UIThemeElement : MonoBehaviour
    {
        [SerializeField] private UIThemeRole role;
        private Graphic graphic;
        private Color sourceColor, assignedColor;
        private bool applied;
        private int originalFontSize;
        private Image hintBacking;
        public UIThemeRole Role { get => role; set { role = value; applied = false; } }

        public static UIThemeElement Mark(GameObject target, UIThemeRole role)
        {
            var element = target.GetComponent<UIThemeElement>() ?? target.AddComponent<UIThemeElement>();
            element.Role = role; return element;
        }
        public void Initialize(Graphic value)
        {
            if (graphic == value && applied) return;
            graphic = value; sourceColor = value.color;
            if (value is Text text) originalFontSize = text.fontSize;
            if (role == UIThemeRole.Auto) role = Guess(value);
        }
        public void Apply(UITheme theme)
        {
            if (theme == null || graphic == null || role == UIThemeRole.Ignore) return;
            if (!applied || graphic.color != assignedColor) sourceColor = graphic.color;
            if (graphic is Text text) ApplyText(text, theme);
            else if (graphic is Image image) ApplyImage(image, theme);
            assignedColor = graphic.color; applied = true;
        }
        private void ApplyText(Text text, UITheme theme)
        {
            Font font = role == UIThemeRole.Title ? theme.titleFont : role == UIThemeRole.Heading ? theme.headingFont : theme.bodyFont;
            if (font != null && text.font != font) text.font = font;
            var button = text.GetComponentInParent<Button>(true);
            var selectable = text.GetComponentInParent<Selectable>(true);
            if (button != null && text.transform.parent == button.transform && button.GetComponent<UIIcon>()?.IconOnly == true)
            {
                text.enabled = false;
                return;
            }
            Color color = role == UIThemeRole.Title || role == UIThemeRole.Heading ? theme.headingColor : theme.textColor;
            if (IsDanger(sourceColor)) color = theme.dangerColor;
            else if (sourceColor.g > sourceColor.r * 1.25f && sourceColor.g > .4f) color = theme.accentColor;
            if (selectable != null && !selectable.interactable) color = theme.mutedColor;
            text.color = color;
            if (button != null && text.GetComponentInParent<InputField>(true) == null)
            {
                // Imported button rims need room around labels; long descriptions can still wrap.
                if (text.transform.parent == button.transform)
                {
                    var rect = text.rectTransform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    var symbol = button.GetComponent<UIIcon>();
                    rect.offsetMin = new Vector2(symbol != null ? symbol.LabelPadding : 10f, 4);
                    rect.offsetMax = new Vector2(-10, -4);
                }
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = Mathf.Max(11, originalFontSize - 4);
                text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, originalFontSize);
            }
            if (text.GetComponent<Shadow>() == null)
            {
                var shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.025f, .012f, .006f, .8f);
                shadow.effectDistance = new Vector2(1, -1); shadow.useGraphicAlpha = true;
            }
            ApplyHintBacking(text, theme);
        }
        private void ApplyHintBacking(Text text, UITheme theme)
        {
            if (text.name != "Construction status" && !text.name.StartsWith("WASD /")) return;
            if (hintBacking == null)
            {
                var backing = new GameObject("Hint backing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backing.transform.SetParent(text.transform.parent, false);
                backing.transform.SetSiblingIndex(text.transform.GetSiblingIndex());
                hintBacking = backing.GetComponent<Image>(); hintBacking.raycastTarget = false;
                Mark(backing, UIThemeRole.Ignore);
            }
            hintBacking.gameObject.SetActive(text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text));
            hintBacking.sprite = theme.windowFrame; hintBacking.type = Image.Type.Sliced;
            hintBacking.pixelsPerUnitMultiplier = 4f; hintBacking.color = new Color(1, 1, 1, .93f);
            var source = text.rectTransform; var target = hintBacking.rectTransform;
            target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax;
            target.pivot = source.pivot; target.anchoredPosition = source.anchoredPosition;
            target.localScale = source.localScale; target.localRotation = source.localRotation;
            var size = new Vector2(Mathf.Min(source.rect.width, text.preferredWidth) + 26f,
                Mathf.Min(source.rect.height, text.preferredHeight) + 14f);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }
        private void OnDestroy()
        {
            if (hintBacking != null) Destroy(hintBacking.gameObject);
        }
        private void ApplyImage(Image image, UITheme theme)
        {
            Sprite sprite = null; Color tint = Color.white;
            switch (role)
            {
                case UIThemeRole.Button:
                    var button = image.GetComponent<Button>();
                    bool selected = sourceColor.r > sourceColor.g * 1.1f && sourceColor.g > sourceColor.b * 1.35f;
                    sprite = selected ? theme.buttonPressed : theme.buttonNormal;
                    if (button != null)
                    {
                        if (button.transition != Selectable.Transition.SpriteSwap)
                        {
                            button.transition = Selectable.Transition.SpriteSwap;
                            image.CrossFadeColor(Color.white, 0f, true, true);
                        }
                        button.spriteState = new SpriteState { highlightedSprite = theme.buttonHover, pressedSprite = theme.buttonPressed,
                            selectedSprite = theme.buttonHover, disabledSprite = theme.buttonDisabled };
                        if (!button.interactable) tint = new Color(.47f, .45f, .42f, .8f);
                    }
                    image.pixelsPerUnitMultiplier = 4f;
                    break;
                case UIThemeRole.Window: sprite = theme.windowFrame; image.pixelsPerUnitMultiplier = 2f; break;
                case UIThemeRole.Panel: sprite = theme.panelFrame; image.pixelsPerUnitMultiplier = 2f; break;
                case UIThemeRole.Input: sprite = theme.inputField; image.pixelsPerUnitMultiplier = 3f; break;
                case UIThemeRole.Track:
                    sprite = image.GetComponentInParent<Scrollbar>(true) != null ? theme.scrollbarTrack : theme.sliderTrack;
                    image.pixelsPerUnitMultiplier = 3f; break;
                case UIThemeRole.Fill: sprite = theme.sliderFill; image.pixelsPerUnitMultiplier = 3f; break;
                case UIThemeRole.Thumb:
                    sprite = image.GetComponentInParent<Scrollbar>(true) != null ? theme.scrollbarHandle : theme.sliderHandle;
                    image.pixelsPerUnitMultiplier = 3f; break;
                case UIThemeRole.Badge: sprite = theme.inputField; tint = new Color(.85f, .27f, .17f); image.pixelsPerUnitMultiplier = 5f; break;
                case UIThemeRole.Overlay: image.sprite = null; tint = new Color(.055f, .033f, .018f, .93f); break;
                case UIThemeRole.Icon: return;
            }
            if (sprite != null)
            {
                if (image.sprite != sprite) image.sprite = sprite;
                image.type = Image.Type.Sliced; image.fillCenter = true;
            }
            image.color = tint;
        }
        private static bool IsDanger(Color color) => color.r > .6f && color.r > color.g * 1.4f && color.r > color.b * 1.4f;
        private static UIThemeRole Guess(Graphic graphic)
        {
            if (graphic is Text text)
            {
                if (text.GetComponentInParent<Selectable>(true) != null) return UIThemeRole.Text;
                if (text.fontSize >= 34) return UIThemeRole.Title;
                return text.fontSize >= 23 && !text.text.Contains("\n") ? UIThemeRole.Heading : UIThemeRole.Text;
            }
            if (!(graphic is Image image)) return UIThemeRole.Ignore;
            string name = image.name.ToLowerInvariant();
            if (image.GetComponent<Button>() != null) return UIThemeRole.Button;
            if (image.GetComponent<InputField>() != null || image.GetComponent<Dropdown>() != null) return UIThemeRole.Input;
            if (image.GetComponent<Slider>() != null || image.GetComponent<Scrollbar>() != null) return UIThemeRole.Track;
            var slider = image.GetComponentInParent<Slider>(true);
            if (slider != null)
            {
                if (slider.fillRect == image.rectTransform) return UIThemeRole.Fill;
                if (slider.handleRect == image.rectTransform) return UIThemeRole.Thumb;
                return UIThemeRole.Track;
            }
            var bar = image.GetComponentInParent<Scrollbar>(true);
            if (bar != null) return bar.handleRect == image.rectTransform ? UIThemeRole.Thumb : UIThemeRole.Track;
            if (image.GetComponentInParent<Toggle>(true) is Toggle toggle)
                return toggle.graphic == image ? UIThemeRole.Icon : UIThemeRole.Input;
            if (name == "viewport" || image.color.a <= .02f) return UIThemeRole.Ignore;
            if (name.Contains("unread")) return UIThemeRole.Badge;
            if (name.Contains("overlay") || name.Contains("confirmation") || name == "confirm action" || name == "pause menu"
                || name == "park management window" || name == "new park setup" || name == "technology research" || name == "expedition world map"
                || name == "settings menu") return UIThemeRole.Overlay;
            if (image.sprite != null) return UIThemeRole.Icon;
            if (name == "window" || name.Contains("needs") || name.Contains("information") || name.Contains("overview") || name.Contains("inspection")) return UIThemeRole.Window;
            return UIThemeRole.Panel;
        }
    }
}

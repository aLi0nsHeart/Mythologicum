using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Keeps a control's artwork consistent while its gameplay state changes.</summary>
    [DisallowMultipleComponent]
    public sealed class UIThemeElement : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private UIThemeRole role;
        [SerializeField, Tooltip("Automatic chooses from the label's role and original size. Choose a category to style an authored label explicitly.")]
        private UITextCategory textCategory;
        [SerializeField, Tooltip("Local exceptions are suitable for authored scene/prefab labels. For generated interface labels, save exceptions in the UI theme asset instead.")]
        private bool localAppearanceEnabled;
        [SerializeField] private UITextAppearance localAppearance = new UITextAppearance();
        // Dropdowns clone their styled templates. Copy the captured design values too,
        // so a clone never applies a text scale for a second time to an already styled label.
        [SerializeField, HideInInspector] private Graphic graphic;
        [SerializeField, HideInInspector] private Color sourceColor, assignedColor;
        [SerializeField, HideInInspector] private bool applied;
        [SerializeField, HideInInspector] private int originalFontSize;
        [SerializeField, HideInInspector] private bool capturedOriginal;
        [SerializeField, HideInInspector] private FontStyle originalFontStyle;
        [SerializeField, HideInInspector] private TextAnchor originalAlignment;
        [SerializeField, HideInInspector] private float originalLineSpacing;
        [SerializeField, HideInInspector] private bool originalBestFit;
        [SerializeField, HideInInspector] private int originalMinimumSize;
        private Shadow themeShadow;
        private bool hovered, pressed, focused;
        private Image hintBacking;
        public bool FitToCanvas { get; set; }
        public UIThemeRole Role { get => role; set { role = value; applied = false; } }
        public bool LocalAppearanceEnabled { get => localAppearanceEnabled; set => localAppearanceEnabled = value; }
        public UITextAppearance LocalAppearance { get => localAppearance; set => localAppearance = value; }
        public UITextCategory TextCategory { get => textCategory; set => textCategory = value; }

        public static UIThemeElement Mark(GameObject target, UIThemeRole role)
        {
            var element = target.GetComponent<UIThemeElement>() ?? target.AddComponent<UIThemeElement>();
            element.Role = role; return element;
        }
        /// <summary>Creates a safe area inside an illustrated information frame.</summary>
        public static RectTransform InfoContent(GameObject panel, Vector2 minimum, Vector2 maximum)
        {
            var rect = (RectTransform)panel.transform;
            return UIFramedPanel.Attach(panel, rect.rect.height > rect.rect.width, rect.rect.width).Content;
        }
        public void Initialize(Graphic value)
        {
            if (graphic != value) capturedOriginal = false;
            if (graphic == value && capturedOriginal) return;
            graphic = value; sourceColor = value.color;
            if (value is Text text)
            {
                originalFontSize = text.fontSize; originalFontStyle = text.fontStyle;
                originalAlignment = text.alignment; originalLineSpacing = text.lineSpacing;
                originalBestFit = text.resizeTextForBestFit; originalMinimumSize = text.resizeTextMinSize;
            }
            capturedOriginal = true;
            if (role == UIThemeRole.Auto) role = Guess(value);
        }
        public void Apply(UITheme theme)
        {
            if (theme == null || graphic == null || role == UIThemeRole.Ignore) return;
            if (FitToCanvas && graphic.canvas != null)
            {
                var canvasRect = (RectTransform)graphic.canvas.rootCanvas.transform;
                var rect = graphic.rectTransform;
                float scale = Mathf.Min(1f, (canvasRect.rect.width - 30f) / Mathf.Max(1f, rect.rect.width),
                    (canvasRect.rect.height - 30f) / Mathf.Max(1f, rect.rect.height));
                rect.localScale = Vector3.one * Mathf.Max(.1f, scale);
            }
            if (!applied || graphic.color != assignedColor) sourceColor = graphic.color;
            if (graphic is Text text) ApplyText(text, theme);
            else if (graphic is Image image) ApplyImage(image, theme);
            assignedColor = graphic.color; applied = true;
        }
        private void ApplyText(Text text, UITheme theme)
        {
            var button = text.GetComponentInParent<Button>(true);
            var input = text.GetComponentInParent<InputField>(true);
            var selectable = text.GetComponentInParent<Selectable>(true);
            if (button != null && text.transform.parent == button.transform && button.GetComponent<UIIcon>()?.IconOnly == true)
            {
                text.enabled = false;
                return;
            }
            UITextCategory category = Category(text, button, input);
            Font font = category == UITextCategory.Title ? theme.titleFont
                : category == UITextCategory.Heading || category == UITextCategory.Hud ? theme.headingFont : theme.bodyFont;
            Color color = category == UITextCategory.Title || category == UITextCategory.Heading || category == UITextCategory.Hud
                ? theme.headingColor : theme.textColor;
            bool danger = IsDanger(sourceColor), accent = sourceColor.g > sourceColor.r * 1.25f && sourceColor.g > .4f;
            if (danger) color = theme.dangerColor;
            else if (accent) color = theme.accentColor;
            if (selectable != null && !selectable.interactable) color = theme.mutedColor;
            float size = originalFontSize, spacing = originalLineSpacing;
            FontStyle style = originalFontStyle;
            TextAnchor alignment = originalAlignment;
            UITextFitMode fit = originalBestFit ? UITextFitMode.ShrinkToFit : UITextFitMode.FixedSize;
            int minimum = originalMinimumSize, range = originalBestFit ? Mathf.Max(0, originalFontSize - originalMinimumSize) : 4;
            ApplyAppearance(theme.TextStyle(category), ref font, ref color, ref size, ref style, ref alignment, ref spacing, ref fit, ref minimum, ref range);
            ApplyAppearance(theme.TextOverride(transform), ref font, ref color, ref size, ref style, ref alignment, ref spacing, ref fit, ref minimum, ref range);
            if (localAppearanceEnabled)
                ApplyAppearance(localAppearance, ref font, ref color, ref size, ref style, ref alignment, ref spacing, ref fit, ref minimum, ref range);
            if (theme.preserveStatusColors)
            {
                if (danger) color = theme.dangerColor;
                else if (accent) color = theme.accentColor;
                if (selectable != null && !selectable.interactable) color = theme.mutedColor;
            }
            font = LocalizationFonts.For(font, text.text);
            if (font != null && text.font != font) text.font = font;
            text.color = color;
            text.fontSize = Mathf.Clamp(Mathf.RoundToInt(size * Mathf.Max(.1f, theme.globalTextScale)), 1, 300);
            text.fontStyle = style; text.alignment = alignment; text.lineSpacing = Mathf.Max(.1f, spacing);
            text.resizeTextForBestFit = fit == UITextFitMode.ShrinkToFit;
            if (text.resizeTextForBestFit)
            {
                text.resizeTextMaxSize = text.fontSize;
                text.resizeTextMinSize = Mathf.Clamp(Mathf.Max(minimum, text.fontSize - range), 1, text.fontSize);
            }
            if (button != null && input == null)
            {
                // Imported button rims need room around labels; long descriptions can still wrap.
                if (text.transform.parent == button.transform)
                {
                    var rect = text.rectTransform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    var symbol = button.GetComponent<UIIcon>();
                    rect.offsetMin = new Vector2(symbol != null ? symbol.LabelPadding : theme.buttonTextPadding.x, theme.buttonTextPadding.y);
                    rect.offsetMax = -theme.buttonTextPadding;
                }
            }
            if (themeShadow == null)
            {
                foreach (var effect in text.GetComponents<Shadow>())
                    if (!(effect is Outline)) { themeShadow = effect; break; }
                if (themeShadow == null && theme.textShadows)
                    themeShadow = text.gameObject.AddComponent<Shadow>();
            }
            if (themeShadow != null)
            {
                themeShadow.enabled = theme.textShadows; themeShadow.effectColor = theme.shadowColor;
                themeShadow.effectDistance = theme.shadowOffset; themeShadow.useGraphicAlpha = true;
            }
            bool useOutline = category == UITextCategory.Hud ? theme.hudOutlines : theme.textOutlines;
            if (useOutline && text.GetComponent<Outline>() == null) text.gameObject.AddComponent<Outline>();
            foreach (var outline in text.GetComponents<Outline>())
            {
                bool hud = category == UITextCategory.Hud;
                outline.enabled = hud ? theme.hudOutlines : theme.textOutlines;
                outline.effectColor = hud ? theme.hudOutlineColor : theme.outlineColor;
                outline.effectDistance = hud ? theme.hudOutlineOffset : theme.outlineOffset;
            }
            ApplyHintBacking(text, theme);
        }

        private UITextCategory Category(Text text, Button button, InputField input)
        {
            if (textCategory != UITextCategory.Automatic) return textCategory;
            // Unity reparents a dropdown's cloned option list beneath the canvas. Its
            // Toggle labels remain input text even though the Dropdown is no longer an ancestor.
            if (input != null || text.GetComponentInParent<Dropdown>(true) != null
                || text.GetComponentInParent<Toggle>(true) != null) return UITextCategory.Input;
            if (button != null) return UITextCategory.Button;
            if (text.name == "Park funds") return UITextCategory.Hud;
            if (role == UIThemeRole.Title) return UITextCategory.Title;
            if (role == UIThemeRole.Heading) return UITextCategory.Heading;
            return originalFontSize <= 17 ? UITextCategory.Small : UITextCategory.Body;
        }

        private static void ApplyAppearance(UITextAppearance appearance, ref Font font, ref Color color, ref float size,
            ref FontStyle style, ref TextAnchor alignment, ref float spacing, ref UITextFitMode fit, ref int minimum, ref int range)
        {
            if (appearance == null) return;
            if (appearance.font != null) font = appearance.font;
            if (appearance.fontSize > 0) size = appearance.fontSize;
            size *= Mathf.Max(.1f, appearance.sizeScale);
            if (appearance.overrideColor) color = appearance.color;
            if (appearance.overrideFontStyle) style = appearance.fontStyle;
            if (appearance.overrideAlignment) alignment = appearance.alignment;
            if (appearance.overrideLineSpacing) spacing = appearance.lineSpacing;
            if (appearance.fitMode == UITextFitMode.KeepOriginal) return;
            fit = appearance.fitMode; minimum = Mathf.Max(1, appearance.minimumFontSize); range = Mathf.Max(0, appearance.shrinkRange);
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
            hintBacking.pixelsPerUnitMultiplier = theme.buttonArtworkScale; hintBacking.color = theme.hintTint;
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
                    tint = theme.buttonTint;
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
                        if (!button.IsInteractable())
                        {
                            tint = theme.buttonDisabledTint;
                            sprite = theme.buttonDisabled != null ? theme.buttonDisabled : theme.buttonNormal;
                        }
                        else if (pressed && hovered) sprite = theme.buttonPressed;
                        else if (hovered || focused) sprite = theme.buttonHover;
                    }
                    if (sprite == null) sprite = theme.buttonNormal;
                    image.pixelsPerUnitMultiplier = Mathf.Max(.1f, theme.buttonArtworkScale);
                    break;
                case UIThemeRole.Window:
                    var frame = image.GetComponent<UIFramedPanel>();
                    if (frame != null) { frame.Refresh(); return; }
                    sprite = image.rectTransform.rect.height > image.rectTransform.rect.width
                        ? theme.verticalInfoFrame : theme.horizontalInfoFrame;
                    if (sprite == null) sprite = theme.windowFrame;
                    UIFramedPanel.ApplyArtwork(image, sprite);
                    return;
                case UIThemeRole.Panel:
                    // Row, list and tooltip backgrounds are quiet surfaces; the outer frame owns the ornament.
                    image.sprite = null;
                    tint = theme.surfaceColor;
                    break;
                case UIThemeRole.MapBackground:
                    UIFramedPanel.ApplyArtwork(image, theme.expeditionMapBackground);
                    return;
                case UIThemeRole.Input: sprite = theme.inputField; tint = theme.inputTint; image.pixelsPerUnitMultiplier = Mathf.Max(.1f, theme.controlArtworkScale); break;
                case UIThemeRole.Track:
                    sprite = image.GetComponentInParent<Scrollbar>(true) != null ? theme.scrollbarTrack : theme.sliderTrack;
                    tint = theme.trackTint; image.pixelsPerUnitMultiplier = Mathf.Max(.1f, theme.controlArtworkScale); break;
                case UIThemeRole.Fill: sprite = theme.sliderFill; tint = theme.fillTint; image.pixelsPerUnitMultiplier = Mathf.Max(.1f, theme.controlArtworkScale); break;
                case UIThemeRole.Thumb:
                    sprite = image.GetComponentInParent<Scrollbar>(true) != null ? theme.scrollbarHandle : theme.sliderHandle;
                    tint = theme.thumbTint; image.pixelsPerUnitMultiplier = Mathf.Max(.1f, theme.controlArtworkScale); break;
                case UIThemeRole.Badge: sprite = theme.inputField; tint = theme.badgeColor; image.pixelsPerUnitMultiplier = 5f; break;
                case UIThemeRole.Overlay: image.sprite = null; tint = theme.overlayColor; break;
                case UIThemeRole.Icon: return;
            }
            if (image.sprite != sprite) image.sprite = sprite;
            if (sprite != null)
            {
                image.type = Image.Type.Sliced; image.fillCenter = true;
            }
            else image.type = Image.Type.Simple;
            image.color = tint;
        }
        private static bool IsDanger(Color color) => color.r > .6f && color.r > color.g * 1.4f && color.r > color.b * 1.4f;

        public void OnPointerEnter(PointerEventData data) { hovered = true; Apply(UITheme.Current); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Apply(UITheme.Current); }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = true; Apply(UITheme.Current); } }
        public void OnPointerUp(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = false; Apply(UITheme.Current); } }
        public void OnSelect(BaseEventData data) { focused = true; Apply(UITheme.Current); }
        public void OnDeselect(BaseEventData data) { focused = false; Apply(UITheme.Current); }
        private void OnDisable() { hovered = pressed = focused = false; }
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
            // Unity creates a transparent full-canvas click catcher while a dropdown
            // is open. It must keep receiving outside clicks without button artwork.
            if (name == "blocker" && image.GetComponent<Button>() != null) return UIThemeRole.Ignore;
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

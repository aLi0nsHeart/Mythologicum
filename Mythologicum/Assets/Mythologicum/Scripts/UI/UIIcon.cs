using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>A decorative button symbol with the same hover, press and disabled states as its control.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIIcon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private UIIconKind kind;
        [SerializeField] private bool automatic = true;
        [SerializeField] private bool iconOnly;
        private Button button;
        private Text label;
        private Image icon;
        private bool hovered, pressed, selected;
        private Vector2 previousSize = new Vector2(-1, -1);
        private float labelPadding = 10f;
        private bool standalonePrepared;

        public UIIconKind Kind => kind;
        public Image IconImage => icon;
        public bool IconOnly => iconOnly;
        public float LabelPadding => iconOnly ? 0f : icon != null && icon.enabled && icon.sprite != null
            ? labelPadding : UITheme.Current != null ? UITheme.Current.buttonTextPadding.x : 10f;

        /// <summary>Assign a specific symbol; automatic name matching will leave this choice alone.</summary>
        public static UIIcon Attach(Button target, UIIconKind value)
        {
            if (target == null) return null;
            var symbol = target.GetComponent<UIIcon>() ?? target.gameObject.AddComponent<UIIcon>();
            symbol.button = target; symbol.automatic = false; symbol.kind = value; symbol.Refresh();
            return symbol;
        }

        /// <summary>Use the artwork itself as the complete control, with a delayed hover name.</summary>
        public static UIIcon AttachStandalone(Button target, UIIconKind value, string tooltip = null)
        {
            var symbol = Attach(target, value);
            if (symbol == null) return null;
            if (!symbol.iconOnly) symbol.previousSize = new Vector2(-1, -1);
            symbol.iconOnly = true;
            UIHoverTooltip.Attach(target, tooltip);
            symbol.Refresh();
            return symbol;
        }

        public static UIIcon AttachAutomatic(Button target)
        {
            if (target == null) return null;
            var existing = target.GetComponent<UIIcon>();
            if (existing != null) { existing.Refresh(); return existing; }
            var value = Infer(target, DirectLabel(target));
            if (value == UIIconKind.None) return null;
            var symbol = target.gameObject.AddComponent<UIIcon>();
            symbol.button = target; symbol.kind = value; symbol.automatic = true; symbol.Refresh();
            return symbol;
        }

        private void OnEnable() => Refresh();
        private void OnDisable() { hovered = pressed = selected = false; }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (button == null) button = GetComponent<Button>();
            if (button == null) return;
            if (label == null) label = DirectLabel(button);
            if (iconOnly && label != null) label.enabled = false;
            if (automatic) kind = Infer(button, label);
            var theme = UITheme.Current;
            var symbols = theme != null ? theme.Icon(kind) : null;
            if (symbols == null || symbols.normal == null)
            {
                if (icon != null) icon.enabled = false;
                return;
            }
            if (icon == null)
            {
                var child = new GameObject("Button icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                child.transform.SetParent(transform, false); child.transform.SetAsFirstSibling();
                icon = child.GetComponent<Image>(); icon.raycastTarget = false; icon.preserveAspect = true;
                UIThemeElement.Mark(child, UIThemeRole.Icon);
                previousSize = new Vector2(-1, -1);
            }
            icon.enabled = true;
            if (iconOnly) PrepareStandalone();
            bool available = button.IsInteractable();
            Sprite sprite = !available ? symbols.disabled
                : pressed && hovered ? symbols.pressed
                : hovered || selected ? symbols.highlighted : symbols.normal;
            if (sprite == null) sprite = symbols.normal;
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.color = !available && (symbols.disabled == null || theme.tintDisabledIconArtwork) ? theme.disabledIconTint : theme.iconTint;
            var size = ((RectTransform)transform).rect.size;
            // Spacing settings can change live even when the button's own rectangle stays the same.
            if (size != previousSize || !iconOnly)
            {
                previousSize = size;
                var rect = icon.rectTransform;
                if (iconOnly)
                {
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                else
                {
                    float side = Mathf.Clamp(size.y - theme.iconInset, Mathf.Max(1f, theme.iconMinSize), Mathf.Max(theme.iconMinSize, theme.iconMaxSize));
                    rect.anchorMin = rect.anchorMax = new Vector2(0, .5f); rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = new Vector2(theme.iconPadding + side * .5f, 0); rect.sizeDelta = Vector2.one * side;
                    labelPadding = theme.iconPadding + side + theme.iconGap;
                }
            }
        }

        private void PrepareStandalone()
        {
            var hitArea = button.GetComponent<Image>();
            if (hitArea != null)
            {
                hitArea.color = Color.clear;
                hitArea.raycastTarget = true;
            }
            if (!standalonePrepared)
            {
                UIThemeElement.Mark(button.gameObject, UIThemeRole.Ignore);
                icon.CrossFadeColor(Color.white, 0f, true, true);
                standalonePrepared = true;
            }
            icon.type = Image.Type.Simple;
            button.targetGraphic = icon;
            button.transition = Selectable.Transition.None;
        }

        private static Text DirectLabel(Button target)
        {
            foreach (Transform child in target.transform)
                if (child.TryGetComponent<Text>(out var text)) return text;
            return null;
        }

        private static UIIconKind Infer(Button target, Text text)
        {
            string caption = text != null ? text.text.Trim().ToLowerInvariant() : "";
            string name = target.name.Trim().ToLowerInvariant();
            // The building inspector reuses its management button for two different workplaces.
            if (caption == "open expedition world map") return UIIconKind.Expeditions;
            switch (name)
            {
                case "building": return UIIconKind.Building;
                case "park management": case "park": return UIIconKind.ParkManagement;
                case "encyclopedia": return UIIconKind.Encyclopedia;
                case "pause": return UIIconKind.Pause;
                case "new game": case "resume": return UIIconKind.Play;
                case "settings": return UIIconKind.Settings;
                case "quit": case "quit game": case "quit to main menu": return UIIconKind.Quit;
                case "statistics": case "finances": case "prices & budget": case "management":
                case "economy & prices": case "park funds mode": return UIIconKind.Statistics;
                case "expeditions": case "send expedition": case "buy location": case "buy team": return UIIconKind.Expeditions;
                case "technology research": case "research": case "start research": case "researching": return UIIconKind.Research;
                case "visitor welfare": return UIIconKind.Visitors;
                default: return UIIconKind.None;
            }
        }

        public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; Refresh(); }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = hovered = true; Refresh(); } }
        public void OnPointerUp(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = false; Refresh(); } }
        public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { selected = false; Refresh(); }
    }
}

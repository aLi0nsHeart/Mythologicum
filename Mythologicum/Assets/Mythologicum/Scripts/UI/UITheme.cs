using System;
using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/UI theme", fileName = "SteampunkTheme")]
    public sealed class UITheme : ScriptableObject
    {
        private static UITheme current;
        public static UITheme Current
        {
            get
            {
                var layout = UILayoutSettings.Current;
                if (layout != null && layout.activeTheme != null) return layout.activeTheme;
                return current != null ? current : (current = Resources.Load<UITheme>("SteampunkTheme"));
            }
        }
        [Header("Default fonts")]
        [Tooltip("Fallback fonts used when a text profile has no font of its own.")]
        public Font bodyFont, headingFont, titleFont;
        [Header("Typography")]
        [Tooltip("Scales all interface text. Profiles and individual overrides can refine this.")]
        [Range(.5f, 2f)] public float globalTextScale = 1f;
        public UITextAppearance bodyText = new UITextAppearance();
        public UITextAppearance headingText = new UITextAppearance();
        public UITextAppearance titleText = new UITextAppearance();
        public UITextAppearance buttonText = new UITextAppearance { fitMode = UITextFitMode.ShrinkToFit };
        public UITextAppearance inputText = new UITextAppearance();
        public UITextAppearance smallText = new UITextAppearance();
        public UITextAppearance hudText = new UITextAppearance();
        [Header("Text palette")]
        public Color textColor = new Color(.96f, .88f, .72f);
        public Color headingColor = new Color(1f, .85f, .51f);
        public Color mutedColor = new Color(.58f, .55f, .48f);
        public Color dangerColor = new Color(1f, .48f, .33f);
        public Color accentColor = new Color(.77f, .84f, .58f);
        [Tooltip("Keep warning, positive/accent and disabled text distinguishable even when a text profile supplies a colour.")]
        public bool preserveStatusColors = true;
        [Header("Text effects")]
        public bool textShadows = true;
        public Color shadowColor = new Color(.025f, .012f, .006f, .8f);
        public Vector2 shadowOffset = new Vector2(1, -1);
        public bool textOutlines;
        public Color outlineColor = new Color(.055f, .035f, .01f, 1);
        public Vector2 outlineOffset = new Vector2(1, -1);
        [Header("HUD stars and outline")]
        public Color starFilledColor = new Color(1, .78f, .2f, 1);
        public Color starEmptyColor = new Color(.18f, .24f, .25f, 1);
        public bool hudOutlines = true;
        public Color hudOutlineColor = new Color(.055f, .035f, .01f, 1);
        public Vector2 hudOutlineOffset = new Vector2(1, -1);
        [Header("Surfaces and control colours")]
        public Color panelLiningColor = new Color(.055f, .035f, .018f, .985f);
        public Color frameTint = Color.white;
        public Color surfaceColor = new Color(.055f, .035f, .02f, .65f);
        public Color overlayColor = new Color(.055f, .033f, .018f, .93f);
        public Color buttonTint = Color.white;
        public Color buttonDisabledTint = new Color(.47f, .45f, .42f, .8f);
        public Color inputTint = Color.white;
        public Color trackTint = Color.white;
        public Color fillTint = Color.white;
        public Color thumbTint = Color.white;
        public Color badgeColor = new Color(.85f, .27f, .17f);
        public Color hintTint = new Color(1, 1, 1, .93f);
        [Header("Control spacing")]
        [Tooltip("Horizontal and vertical padding inside text buttons.")]
        public Vector2 buttonTextPadding = new Vector2(10, 4);
        [Min(.1f)] public float buttonArtworkScale = 4f;
        [Min(.1f)] public float controlArtworkScale = 3f;
        [Header("Frames and controls")]
        public Sprite windowFrame, panelFrame;
        public Sprite verticalInfoFrame, horizontalInfoFrame, expeditionMapBackground;
        [Tooltip("Optional tooltip artwork. Empty uses the standard small window frame.")]
        public Sprite tooltipFrame;
        public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
        public Sprite inputField, sliderTrack, sliderFill, sliderHandle, scrollbarTrack, scrollbarHandle;
        public Sprite checkmark, envelope, envelopeUnread, closeIcon;
        [Header("Icons")]
        public Sprite[] icons;
        public UIIconSet[] iconSets;
        public Color iconTint = Color.white;
        public Color disabledIconTint = new Color(.65f, .65f, .65f, .75f);
        [Tooltip("Apply the disabled tint even when the icon has dedicated disabled artwork.")]
        public bool tintDisabledIconArtwork;
        [Min(0)] public float iconPadding = 14f;
        [Min(0)] public float iconGap = 9f;
        [Min(0)] public float iconInset = 12f;
        [Min(1)] public float iconMinSize = 18f;
        [Min(1)] public float iconMaxSize = 44f;
        [Header("Cursor and menu background")]
        public Sprite cursorSource;
        public Texture2D cursorTexture;
        public Vector2 cursorHotspot = new Vector2(2, 2);
        public Texture2D background;
        [Header("Persistent individual text overrides")]
        [Tooltip("Optional exceptions for a particular label. Later matching entries take priority. These asset settings survive leaving Play mode.")]
        public UITextOverride[] textOverrides = Array.Empty<UITextOverride>();
        public static Font BodyFont => Current != null && Current.bodyFont != null ? Current.bodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public UIIconSet Icon(UIIconKind kind)
        {
            if (iconSets == null || kind == UIIconKind.None) return null;
            foreach (var set in iconSets) if (set != null && set.kind == kind) return set;
            return null;
        }

        public UITextAppearance TextStyle(UITextCategory category)
        {
            switch (category)
            {
                case UITextCategory.Heading: return headingText;
                case UITextCategory.Title: return titleText;
                case UITextCategory.Button: return buttonText;
                case UITextCategory.Input: return inputText;
                case UITextCategory.Small: return smallText;
                case UITextCategory.Hud: return hudText;
                default: return bodyText;
            }
        }

        public UITextAppearance TextOverride(Transform target)
        {
            if (textOverrides == null) return null;
            for (int i = textOverrides.Length - 1; i >= 0; i--)
                if (textOverrides[i] != null && textOverrides[i].Matches(target)) return textOverrides[i].appearance;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }

    public enum UITextCategory { Automatic, Body, Heading, Title, Button, Input, Small, Hud }
    public enum UITextFitMode { KeepOriginal, ShrinkToFit, FixedSize }

    [Serializable]
    public sealed class UITextAppearance
    {
        [Tooltip("Leave empty to use the default font for this category.")]
        public Font font;
        [Tooltip("0 preserves the label's designed size. A positive value sets this category or label to that size.")]
        [Min(0)] public int fontSize;
        [Tooltip("Multiplies the designed or fixed size, in addition to Global text scale.")]
        [Range(.5f, 2f)] public float sizeScale = 1f;
        public bool overrideColor;
        public Color color = Color.white;
        public bool overrideFontStyle;
        public FontStyle fontStyle = FontStyle.Normal;
        public bool overrideAlignment;
        public TextAnchor alignment = TextAnchor.UpperLeft;
        public bool overrideLineSpacing;
        [Min(.1f)] public float lineSpacing = 1f;
        [Tooltip("KeepOriginal follows this label's designed behaviour; ShrinkToFit lets button captions shrink; FixedSize disables shrinking.")]
        public UITextFitMode fitMode = UITextFitMode.KeepOriginal;
        [Min(1)] public int minimumFontSize = 11;
        [Tooltip("Maximum number of font-size points that ShrinkToFit may reduce.")]
        [Min(0)] public int shrinkRange = 4;
    }

    [Serializable]
    public sealed class UITextOverride
    {
        [Tooltip("Exact GameObject name, or a hierarchy path ending at that label. Names are case-sensitive. Use the designer menu to capture a selected label.")]
        public string elementNameOrPath;
        [Tooltip("Optional exact ancestor name or scene name, such as Building information. Leave empty to match throughout the interface.")]
        public string screenName;
        public UITextAppearance appearance = new UITextAppearance();

        public bool Matches(Transform target)
        {
            if (target == null || string.IsNullOrWhiteSpace(elementNameOrPath)) return false;
            string match = elementNameOrPath.Trim().Trim('/');
            if (match.IndexOf('/') >= 0)
            {
                string path = target.name;
                for (Transform parent = target.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                if (!string.Equals(path, match, StringComparison.Ordinal) && !path.EndsWith("/" + match, StringComparison.Ordinal)) return false;
            }
            else if (!string.Equals(target.name, match, StringComparison.Ordinal)) return false;
            if (string.IsNullOrWhiteSpace(screenName)) return true;
            string scope = screenName.Trim();
            if (string.Equals(target.gameObject.scene.name, scope, StringComparison.Ordinal)) return true;
            for (Transform parent = target; parent != null; parent = parent.parent)
                if (string.Equals(parent.name, scope, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public enum UIIconKind
    {
        None, Building, ParkManagement, Encyclopedia, Pause, Play, Settings, Quit,
        Expeditions, Research, Statistics, Visitors, Rest, Food, Mail, MailUnread, Funds
    }

    [Serializable]
    public sealed class UIIconSet
    {
        public UIIconKind kind;
        public Sprite normal, highlighted, pressed, disabled;
    }

    public enum UIThemeRole { Auto, Text, Title, Heading, Button, Panel, Window, Input, Overlay, Track, Fill, Thumb, Icon, Badge, Ignore, MapBackground }
}

using System;
using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/UI theme", fileName = "SteampunkTheme")]
    public sealed class UITheme : ScriptableObject
    {
        private static UITheme current;
        public static UITheme Current => current != null ? current : (current = Resources.Load<UITheme>("SteampunkTheme"));
        public Font bodyFont, headingFont, titleFont;
        public Sprite windowFrame, panelFrame;
        public Sprite buttonNormal, buttonHover, buttonPressed, buttonDisabled;
        public Sprite inputField, sliderTrack, sliderFill, sliderHandle, scrollbarTrack, scrollbarHandle;
        public Sprite checkmark, envelope, envelopeUnread, closeIcon;
        public Sprite[] icons;
        public UIIconSet[] iconSets;
        public Sprite cursorSource;
        public Texture2D cursorTexture;
        public Vector2 cursorHotspot = new Vector2(2, 2);
        public Texture2D background;
        public Color textColor = new Color(.96f, .88f, .72f);
        public Color headingColor = new Color(1f, .85f, .51f);
        public Color mutedColor = new Color(.58f, .55f, .48f);
        public Color dangerColor = new Color(1f, .48f, .33f);
        public Color accentColor = new Color(.77f, .84f, .58f);
        public static Font BodyFont => Current != null && Current.bodyFont != null ? Current.bodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public UIIconSet Icon(UIIconKind kind)
        {
            if (iconSets == null || kind == UIIconKind.None) return null;
            foreach (var set in iconSets) if (set != null && set.kind == kind) return set;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
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

    public enum UIThemeRole { Auto, Text, Title, Heading, Button, Panel, Window, Input, Overlay, Track, Fill, Thumb, Icon, Badge, Ignore }
}

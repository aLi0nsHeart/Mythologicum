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
        public Sprite checkmark, envelope, closeIcon;
        public Sprite[] icons;
        public Texture2D background;
        public Color textColor = new Color(.96f, .88f, .72f);
        public Color headingColor = new Color(1f, .85f, .51f);
        public Color mutedColor = new Color(.58f, .55f, .48f);
        public Color dangerColor = new Color(1f, .48f, .33f);
        public Color accentColor = new Color(.77f, .84f, .58f);
        public static Font BodyFont => Current != null && Current.bodyFont != null ? Current.bodyFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }

    public enum UIThemeRole { Auto, Text, Title, Heading, Button, Panel, Window, Input, Overlay, Track, Fill, Thumb, Icon, Badge, Ignore }
}

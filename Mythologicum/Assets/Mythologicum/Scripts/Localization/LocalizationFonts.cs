using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    [Serializable] public sealed class LanguageFont
    { public GameLanguage language; public Font font; }

    [CreateAssetMenu(menuName = "Mythologicum/Localization fonts", fileName = "DefaultLocalizationFonts")]
    public sealed class LocalizationFonts : ScriptableObject
    {
        [Tooltip("Optional complete font for a language. This overrides the ornamental theme fonts for that language.")]
        public LanguageFont[] languages = Array.Empty<LanguageFont>();
        [Tooltip("Used when the theme font lacks a character. Add bundled CJK fonts here before shipping Asian translations.")]
        public Font[] fallbackFonts = Array.Empty<Font>();
        private static LocalizationFonts current;
        private static readonly Dictionary<(Font, string, GameLanguage), Font> cache = new Dictionary<(Font, string, GameLanguage), Font>();
        public static Font For(Font preferred, string text)
        {
            if (current == null) current = Resources.Load<LocalizationFonts>("Localization/DefaultLocalizationFonts");
            if (current == null) return preferred;
            var language = Loc.Language;
            foreach (var item in current.languages ?? Array.Empty<LanguageFont>())
                if (item != null && item.language == language && item.font != null) return item.font;
            if (preferred == null || string.IsNullOrEmpty(text)) return preferred;
            var key = (preferred, text, language);
            if (cache.TryGetValue(key, out var found)) return found;
            found = preferred;
            if (!Supports(preferred, text))
                foreach (var font in current.fallbackFonts ?? Array.Empty<Font>())
                    if (font != null && Supports(font, text)) { found = font; break; }
            if (cache.Count >= 2048) cache.Clear();
            cache[key] = found; return found;
        }
        public static bool Supports(Font font, string text)
        {
            if (font == null) return false;
            foreach (char character in text)
                if (!char.IsControl(character) && !char.IsWhiteSpace(character) && !font.HasCharacter(character)) return false;
            return true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { current = null; cache.Clear(); }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Player language and ID lookup. Stable object/save identities never use translated text.</summary>
    public static class Loc
    {
        private const string Preference = "Mythologicum.Settings.Language";
        private static LocalizationTable table;
        private static GameLanguage language;
        private static bool headless;
        [Serializable] private sealed class ObjectName { public string id, name; }
        [Serializable] private sealed class ObjectNames { public ObjectName[] names; }
        private static Dictionary<string, string> objectNames;
        private static readonly HashSet<string> missing = new HashSet<string>(StringComparer.Ordinal);
        public static event Action Changed;
        public static int Revision { get; private set; }
        public static GameLanguage Language { get { EnsureLoaded(); return language; } }
        public static CultureInfo Culture => LocalizationTable.Culture(Language);
        public static LocalizationTable Table { get { EnsureLoaded(); return table; } }
        private static void EnsureLoaded()
        {
            if (table != null) return;
            // The model runner has no Unity engine. It uses the same shipped CSV explicitly.
            string modelCsv = Environment.GetEnvironmentVariable("MYTHOLOGICUM_LOCALIZATION_CSV");
            headless = !string.IsNullOrEmpty(modelCsv);
            if (headless) { table = LocalizationTable.Parse(File.ReadAllText(modelCsv)); language = GameLanguage.ENGLISH; return; }
            var source = Resources.Load<TextAsset>("Localization/localization");
            try { table = LocalizationTable.Parse(source == null ? "" : source.text); }
            catch (FormatException error) { table = new LocalizationTable(); Debug.LogError("Localization: " + error.Message); }
            if (!System.Enum.TryParse(PlayerPrefs.GetString(Preference, "ENGLISH"), out language) || !System.Enum.IsDefined(typeof(GameLanguage), language))
                language = GameLanguage.ENGLISH;
        }
        public static bool Has(string id) => Table.Contains(id);
        public static string Text(string id)
        {
            EnsureLoaded();
            if (id == null) return "";
            if (!table.Contains(id) && missing.Add(id) && !headless) Debug.LogWarning("Missing localization ID: " + id);
            return table.Get(id, language);
        }
        public static string Format(string id, params object[] args)
        {
            string value = Text(id);
            try { return string.Format(Culture, value, args ?? Array.Empty<object>()); }
            catch (FormatException error)
            {
                if (!headless) Debug.LogError("Localization format error in " + id + ": " + error.Message);
                string english = English(id);
                try { return string.Format(LocalizationTable.Culture(GameLanguage.ENGLISH), english, args ?? Array.Empty<object>()); }
                catch (FormatException) { return english; }
            }
        }
        /// <summary>Compatibility boundary for earlier saves, authored data and player-entered text.</summary>
        public static string Resolve(string value) => value != null && Table.Contains(value) ? Text(value) : value ?? "";
        public static string English(string id) => Table.Get(id, GameLanguage.ENGLISH);
        public static string InternalName(string idOrName)
        {
            if (!Table.Contains(idOrName)) return idOrName;
            if (headless) return English(idOrName);
            if (objectNames == null)
            {
                objectNames = new Dictionary<string, string>(StringComparer.Ordinal);
                var source = Resources.Load<TextAsset>("Localization/object-names");
                if (source != null)
                    foreach (var item in JsonUtility.FromJson<ObjectNames>(source.text).names)
                        if (item != null && item.id != null) objectNames[item.id] = item.name;
            }
            return objectNames.TryGetValue(idOrName, out string name) ? name : idOrName;
        }
        public static string Enum<T>(T value) where T : struct, System.Enum => Text("enum." + typeof(T).Name + "." + value);
        public static void Set(Text target, string id, params object[] args)
        {
            if (target == null) return;
            var binding = target.GetComponent<LocalizedText>() ?? target.gameObject.AddComponent<LocalizedText>();
            binding.Set(id, args);
        }
        public static void SetLanguage(GameLanguage value)
        {
            EnsureLoaded();
            if (!System.Enum.IsDefined(typeof(GameLanguage), value)) value = GameLanguage.ENGLISH;
            if (language == value) return;
            language = value; Revision++;
            if (!headless) SaveLanguagePreference(value);
            Changed?.Invoke();
            if (!headless) RefreshInterface();
        }
        public static void Reload()
        {
            table = null; missing.Clear(); EnsureLoaded(); Revision++;
            Changed?.Invoke(); if (!headless) RefreshInterface();
        }
        private static void SaveLanguagePreference(GameLanguage value)
        { PlayerPrefs.SetString(Preference, value.ToString()); PlayerPrefs.Save(); }
        private static void RefreshInterface()
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UIThemeApplicator.ApplyAll(canvas);
            Canvas.ForceUpdateCanvases();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        { table = null; objectNames = null; language = GameLanguage.ENGLISH; Changed = null; Revision = 0; missing.Clear(); headless = false; }
    }
}

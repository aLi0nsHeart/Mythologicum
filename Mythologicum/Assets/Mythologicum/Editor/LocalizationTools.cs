using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    public static class LocalizationValidation
    {
        public const string CSVPath = "Assets/Mythologicum/Resources/Localization/localization.csv";
        public static List<GameplayDataIssue> Validate()
        {
            var issues = new List<GameplayDataIssue>();
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(CSVPath);
            if (source == null) { issues.Add(new GameplayDataIssue(MessageType.Error, "The localization CSV is missing.", null)); return issues; }
            LocalizationTable table;
            try { table = LocalizationTable.Parse(source.text); }
            catch (FormatException error) { issues.Add(new GameplayDataIssue(MessageType.Error, error.Message, source)); return issues; }
            var keys = new HashSet<string>(table.IDs, StringComparer.Ordinal);
            var prefixes = new HashSet<string>(keys.Select(key => key.Split('.')[0]), StringComparer.Ordinal);
            var pattern = new Regex("[\"'](?<key>[a-z][a-z_]*\\.[a-zA-Z0-9_.-]+)[\"']");
            foreach (string path in Directory.EnumerateFiles("Assets/Mythologicum/Scripts", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles("Assets/Mythologicum/Resources", "*.asset", SearchOption.AllDirectories)))
            {
                string code = File.ReadAllText(path);
                var candidates = new HashSet<string>();
                foreach (Match match in pattern.Matches(code)) candidates.Add(match.Groups["key"].Value);
                if (path.EndsWith(".asset", StringComparison.Ordinal))
                    foreach (Match match in Regex.Matches(code, @"(?m)^\s*(?:displayName|origin|name|description):\s*([a-z][a-z_]*\.[a-zA-Z0-9_.-]+)\s*$")) candidates.Add(match.Groups[1].Value);
                foreach (string candidate in candidates)
                    if (prefixes.Contains(candidate.Split('.')[0]) && !keys.Contains(candidate) && !candidate.EndsWith(".", StringComparison.Ordinal))
                        issues.Add(new GameplayDataIssue(MessageType.Error, "Missing localization ID '" + candidate + "' in " + path,
                            AssetDatabase.LoadMainAssetAtPath(path.Replace('\\', '/'))));
            }
            var fonts = Resources.Load<LocalizationFonts>("Localization/DefaultLocalizationFonts");
            if (fonts == null) issues.Add(new GameplayDataIssue(MessageType.Error, "Localization font settings are missing.", source));
            else foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                if (language == GameLanguage.ENGLISH) continue;
                var languageFonts = (fonts.languages ?? Array.Empty<LanguageFont>()).Where(item => item != null && item.language == language && item.font != null).Select(item => item.font)
                    .Concat(fonts.fallbackFonts ?? Array.Empty<Font>()).Where(font => font != null).ToArray();
                var unsupported = new HashSet<char>();
                foreach (string id in table.IDs)
                    foreach (char character in table.Raw(id, language))
                        if (!char.IsControl(character) && !char.IsWhiteSpace(character) && !languageFonts.Any(font => font.HasCharacter(character))) unsupported.Add(character);
                if (unsupported.Count > 0)
                    issues.Add(new GameplayDataIssue(MessageType.Warning, language + " needs a bundled font for these translated characters: "
                        + new string(unsupported.Take(24).ToArray()), fonts));
            }
            if (!issues.Any(issue => issue.severity == MessageType.Error))
                issues.Insert(0, new GameplayDataIssue(MessageType.Info, table.Count + " localization IDs validated. Empty translation cells use English.", source));
            return issues;
        }
    }

    public sealed class LocalizationTools : EditorWindow
    {
        private LocalizationTable table;
        private List<GameplayDataIssue> issues = new List<GameplayDataIssue>();
        private GameLanguage language;
        private string search = "";
        private Vector2 scroll;
        [MenuItem("Mythologicum/Localization/CSV and validation")]
        public static void Open()
        { var window = GetWindow<LocalizationTools>("Localization"); window.minSize = new Vector2(660, 450); window.Refresh(); window.Show(); }
        private void Refresh()
        {
            issues = LocalizationValidation.Validate();
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationValidation.CSVPath);
            try { table = source == null ? null : LocalizationTable.Parse(source.text); } catch (FormatException) { table = null; }
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("One CSV is the source for every player-facing string.", EditorStyles.boldLabel);
            if (GUILayout.Button("Open localization.csv")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(LocalizationValidation.CSVPath));
            if (GUILayout.Button("Select font settings")) Selection.activeObject = Resources.Load<LocalizationFonts>("Localization/DefaultLocalizationFonts");
            if (GUILayout.Button("Validate and refresh")) { Refresh(); if (Application.isPlaying) Loc.Reload(); }
            foreach (var issue in issues) EditorGUILayout.HelpBox(issue.message, issue.severity);
            language = (GameLanguage)EditorGUILayout.EnumPopup("Preview language", language);
            search = EditorGUILayout.TextField("Find ID or English text", search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (table != null) foreach (string id in table.IDs.Where(id => string.IsNullOrEmpty(search)
                || id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || table.Get(id, GameLanguage.ENGLISH).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).Take(150))
            {
                EditorGUILayout.SelectableLabel(id, EditorStyles.boldLabel, GUILayout.Height(19));
                EditorGUILayout.LabelField(table.Get(id, language), EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }
    }
    public sealed class LocalizationCSVImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
        { if (Application.isPlaying && imported.Contains(LocalizationValidation.CSVPath)) EditorApplication.delayCall += Loc.Reload; }
    }
}

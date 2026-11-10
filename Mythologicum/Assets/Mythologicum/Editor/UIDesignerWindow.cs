using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum.Editor
{
    /// <summary>Turns adjustments to generated controls into persistent project settings.</summary>
    public sealed class UIDesignerWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Mythologicum/UI/Designer Tools")]
        public static void Open() => GetWindow<UIDesignerWindow>("UI Designer");

        [MenuItem("Mythologicum/UI/Select Active Theme")]
        public static void SelectTheme() => Select(UITheme.Current);

        [MenuItem("Mythologicum/UI/Select Layout Settings")]
        public static void SelectLayout() => Select(UILayoutSettings.Current);

        [MenuItem("Mythologicum/UI/Select Designer Guide")]
        public static void SelectGuide() => Select(AssetDatabase.LoadMainAssetAtPath("Assets/Mythologicum/Documentation/UI-Designer-Guide.md"));

        private static void Select(UnityEngine.Object asset)
        { Selection.activeObject = asset; if (asset != null) EditorGUIUtility.PingObject(asset); }

        private void OnSelectionChange() => Repaint();
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Appearance and layout assets keep your edits after Play mode. For an individual control, start Play, select its object in the Hierarchy, then use this window. The fields below save persistent exceptions immediately.", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Theme: fonts and colours")) SelectTheme();
            if (GUILayout.Button("Layout: panels and HUD")) SelectLayout();
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Open the designer guide in Project")) SelectGuide();
            var selected = Selection.activeGameObject;
            if (selected == null || selected.GetComponentInParent<Canvas>(true) == null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Select a UI object in the Hierarchy to adjust one control.", EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndScrollView(); return;
            }
            EditorGUILayout.Space();
            string path = ElementPath(selected.transform);
            EditorGUILayout.LabelField("Selected control", selected.name, EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(36));
            if (GUILayout.Button("Copy matching path")) EditorGUIUtility.systemCopyBuffer = path;
            var text = selected.GetComponent<Text>();
            if (text != null) DrawTextEditor(text, path);
            var rect = selected.transform as RectTransform;
            if (rect != null)
            {
                bool framePart = selected.GetComponent<UIFramedPanel>() == null
                    && selected.GetComponentInParent<UIFramedPanel>(true) != null
                    && (selected.name == "Panel artwork" || selected.name == "Panel lining" || selected.name == "Panel content" || selected.name == "Panel heading");
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Position and size", EditorStyles.boldLabel);
                if (framePart)
                {
                    EditorGUILayout.HelpBox("Adjust this frame part through the safe-area settings in Layout. Select the parent panel to save its width or position.", MessageType.Info);
                    if (GUILayout.Button("Select parent panel")) Selection.activeGameObject = selected.GetComponentInParent<UIFramedPanel>(true).gameObject;
                    EditorGUILayout.EndScrollView(); return;
                }
                EditorGUILayout.HelpBox("Edit the saved values below for a generated control. Illustrated panels keep their artwork proportional. For changes made directly to a RectTransform while the Editor is paused, use Save selected position and size.", MessageType.None);
                DrawGeometryEditor(rect, path);
                if (GUILayout.Button("Save selected position and size")) SaveGeometry(rect, path);
                if (GUILayout.Button("Remove this control's saved layout exception")) RemoveGeometry(rect, path);
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawTextEditor(Text text, string path)
        {
            var theme = UITheme.Current;
            if (theme == null) return;
            UITextOverride exact = null;
            if (theme.textOverrides != null)
                foreach (var entry in theme.textOverrides)
                    if (entry != null && entry.elementNameOrPath == path && string.IsNullOrEmpty(entry.screenName)) exact = entry;
            var appearance = exact != null && exact.appearance != null
                ? JsonUtility.FromJson<UITextAppearance>(JsonUtility.ToJson(exact.appearance)) : new UITextAppearance { color = text.color };
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Saved text exception", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Empty Font and Size 0 inherit the theme. Global text scale still applies. Colour changes use Custom colour; normal warning and disabled colours remain active while Preserve status colours is enabled in the theme.", MessageType.None);
            EditorGUI.BeginChangeCheck();
            appearance.font = (Font)EditorGUILayout.ObjectField("Font (empty = inherit)", appearance.font, typeof(Font), false);
            appearance.fontSize = Mathf.Max(0, EditorGUILayout.IntField("Size (0 = inherit)", appearance.fontSize));
            appearance.sizeScale = EditorGUILayout.Slider("Size multiplier", appearance.sizeScale, .5f, 2f);
            appearance.overrideColor = EditorGUILayout.Toggle("Custom colour", appearance.overrideColor);
            if (appearance.overrideColor) appearance.color = EditorGUILayout.ColorField("Text colour", appearance.color);
            appearance.overrideFontStyle = EditorGUILayout.Toggle("Custom font style", appearance.overrideFontStyle);
            if (appearance.overrideFontStyle) appearance.fontStyle = (FontStyle)EditorGUILayout.EnumPopup("Font style", appearance.fontStyle);
            appearance.overrideAlignment = EditorGUILayout.Toggle("Custom alignment", appearance.overrideAlignment);
            if (appearance.overrideAlignment) appearance.alignment = (TextAnchor)EditorGUILayout.EnumPopup("Alignment", appearance.alignment);
            appearance.overrideLineSpacing = EditorGUILayout.Toggle("Custom line spacing", appearance.overrideLineSpacing);
            if (appearance.overrideLineSpacing) appearance.lineSpacing = Mathf.Max(.1f, EditorGUILayout.FloatField("Line spacing", appearance.lineSpacing));
            appearance.fitMode = (UITextFitMode)EditorGUILayout.EnumPopup("Text fitting", appearance.fitMode);
            if (EditorGUI.EndChangeCheck()) SaveText(theme, path, appearance);
            if (GUILayout.Button("Save current visible text appearance"))
            {
                SaveText(theme, path, new UITextAppearance
                {
                    font = text.font, fontSize = Mathf.Max(1, Mathf.RoundToInt(text.fontSize / Mathf.Max(.1f, theme.globalTextScale))),
                    overrideColor = true, color = text.color, overrideFontStyle = true, fontStyle = text.fontStyle,
                    overrideAlignment = true, alignment = text.alignment, overrideLineSpacing = true, lineSpacing = text.lineSpacing,
                    fitMode = text.resizeTextForBestFit ? UITextFitMode.ShrinkToFit : UITextFitMode.FixedSize,
                    minimumFontSize = Mathf.Max(1, text.resizeTextMinSize), shrinkRange = Mathf.Max(0, text.resizeTextMaxSize - text.resizeTextMinSize)
                });
            }
            if (GUILayout.Button("Remove this control's saved text exception"))
            {
                Undo.RecordObject(theme, "Remove UI text exception");
                var entries = new List<UITextOverride>(theme.textOverrides ?? Array.Empty<UITextOverride>());
                entries.RemoveAll(entry => entry != null && entry.elementNameOrPath == path && string.IsNullOrEmpty(entry.screenName));
                theme.textOverrides = entries.ToArray(); Save(theme);
            }
        }

        private static void SaveText(UITheme theme, string path, UITextAppearance appearance)
        {
            Undo.RecordObject(theme, "Edit UI text appearance");
            var entries = new List<UITextOverride>(theme.textOverrides ?? Array.Empty<UITextOverride>());
            var entry = entries.FindLast(item => item != null && item.elementNameOrPath == path && string.IsNullOrEmpty(item.screenName));
            if (entry == null) { entry = new UITextOverride { elementNameOrPath = path }; entries.Add(entry); }
            entry.appearance = appearance; theme.textOverrides = entries.ToArray(); Save(theme);
        }

        private static void SaveGeometry(RectTransform rect, string path)
        {
            var layout = UILayoutSettings.Current;
            Undo.RecordObject(layout, "Save UI control layout");
            var frame = rect.GetComponent<UIFramedPanel>();
            if (frame != null)
            {
                var entries = new List<UIPanelLayout>(layout.panels ?? Array.Empty<UIPanelLayout>());
                var panel = layout.Panel(frame.LayoutKey);
                if (panel == null) { panel = new UIPanelLayout { key = frame.LayoutKey }; entries.Add(panel); }
                panel.preferredWidth = rect.rect.width; panel.widthScale = 1f;
                panel.overridePosition = true; panel.position = rect.anchoredPosition;
                panel.overrideAnchor = true; panel.anchor = rect.anchorMin; panel.pivot = rect.pivot;
                layout.panels = entries.ToArray();
            }
            else
            {
                var entries = new List<UIElementLayoutOverride>(layout.elementOverrides ?? Array.Empty<UIElementLayoutOverride>());
                var entry = entries.FindLast(item => item != null && item.elementNameOrPath == path && string.IsNullOrEmpty(item.screenName));
                if (entry == null) { entry = new UIElementLayoutOverride { elementNameOrPath = path }; entries.Add(entry); }
                entry.enabled = true; entry.overridePosition = entry.overrideSize = true;
                entry.position = rect.anchoredPosition; entry.size = rect.rect.size;
                layout.elementOverrides = entries.ToArray();
            }
            Save(layout);
        }

        private static void DrawGeometryEditor(RectTransform rect, string path)
        {
            var layout = UILayoutSettings.Current;
            var frame = rect.GetComponent<UIFramedPanel>();
            if (frame != null)
            {
                var original = layout.Panel(frame.LayoutKey);
                var panel = original != null ? JsonUtility.FromJson<UIPanelLayout>(JsonUtility.ToJson(original))
                    : new UIPanelLayout { key = frame.LayoutKey, preferredWidth = rect.rect.width, position = rect.anchoredPosition };
                EditorGUI.BeginChangeCheck();
                panel.preferredWidth = Mathf.Max(100, EditorGUILayout.FloatField("Logical panel width", panel.preferredWidth > 0 ? panel.preferredWidth : rect.rect.width));
                panel.overridePosition = EditorGUILayout.Toggle("Override panel position", panel.overridePosition);
                if (panel.overridePosition) panel.position = EditorGUILayout.Vector2Field("Anchored position", panel.position);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(layout, "Edit illustrated panel layout");
                    var entries = new List<UIPanelLayout>(layout.panels ?? Array.Empty<UIPanelLayout>());
                    int index = entries.FindIndex(item => item != null && item.key == frame.LayoutKey);
                    if (index >= 0) entries[index] = panel; else entries.Add(panel);
                    layout.panels = entries.ToArray(); Save(layout);
                }
                return;
            }
            UIElementLayoutOverride exact = null;
            if (layout.elementOverrides != null)
                foreach (var item in layout.elementOverrides)
                    if (item != null && item.elementNameOrPath == path && string.IsNullOrEmpty(item.screenName)) exact = item;
            var entry = exact != null ? JsonUtility.FromJson<UIElementLayoutOverride>(JsonUtility.ToJson(exact))
                : new UIElementLayoutOverride { elementNameOrPath = path, position = rect.anchoredPosition, size = rect.rect.size };
            EditorGUI.BeginChangeCheck();
            entry.overridePosition = EditorGUILayout.Toggle("Override position", entry.overridePosition);
            if (entry.overridePosition) entry.position = EditorGUILayout.Vector2Field("Anchored position", entry.position);
            entry.overrideSize = EditorGUILayout.Toggle("Override size", entry.overrideSize);
            if (entry.overrideSize) entry.size = Vector2.Max(Vector2.one, EditorGUILayout.Vector2Field("Width and height", entry.size));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(layout, "Edit UI control layout");
                var entries = new List<UIElementLayoutOverride>(layout.elementOverrides ?? Array.Empty<UIElementLayoutOverride>());
                entries.RemoveAll(item => item != null && item.elementNameOrPath == path && string.IsNullOrEmpty(item.screenName));
                entries.Add(entry); layout.elementOverrides = entries.ToArray(); Save(layout);
            }
        }

        private static void RemoveGeometry(RectTransform rect, string path)
        {
            var layout = UILayoutSettings.Current;
            Undo.RecordObject(layout, "Remove UI layout exception");
            var frame = rect.GetComponent<UIFramedPanel>();
            if (frame != null)
            {
                var panel = layout.Panel(frame.LayoutKey);
                if (panel != null) { panel.overridePosition = panel.overrideAnchor = false; panel.preferredWidth = 0; panel.widthScale = 1; }
            }
            else
            {
                var entries = new List<UIElementLayoutOverride>(layout.elementOverrides ?? Array.Empty<UIElementLayoutOverride>());
                entries.RemoveAll(item => item != null && item.elementNameOrPath == path && string.IsNullOrEmpty(item.screenName));
                layout.elementOverrides = entries.ToArray();
            }
            Save(layout);
        }

        private static string ElementPath(Transform target)
        {
            var names = new List<string>();
            for (Transform part = target; part != null; part = part.parent)
            {
                var canvas = part.GetComponent<Canvas>();
                if (canvas != null && canvas.isRootCanvas) break;
                names.Add(part.name);
            }
            names.Reverse(); return string.Join("/", names);
        }

        private static void Save(UnityEngine.Object asset)
        {
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            UIThemeEditor.RefreshRunningInterface();
        }
    }
}

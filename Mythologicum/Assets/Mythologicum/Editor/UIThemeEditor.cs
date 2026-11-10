using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    /// <summary>Artist-facing theme groups. Editing the asset never rebuilds or replaces its artwork.</summary>
    [CustomEditor(typeof(UITheme))]
    public sealed class UIThemeEditor : UnityEditor.Editor
    {
        private int page;
        private readonly string[] pages = { "Typography", "Colours", "Artwork", "Icons", "Overrides" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Edit this asset to keep your settings after Play mode. Changes update the running interface immediately. Size 0 keeps a label's designed size; enter a positive size to standardize a category.", MessageType.Info);
            page = GUILayout.Toolbar(page, pages);
            EditorGUILayout.Space(8);
            switch (page)
            {
                case 0:
                    Group("Default fonts", "bodyFont", "headingFont", "titleFont");
                    Group("Text sizes and category exceptions", "globalTextScale", "bodyText", "headingText", "titleText", "buttonText", "inputText", "smallText", "hudText");
                    Group("Text effects", "textShadows", "shadowColor", "shadowOffset", "textOutlines", "outlineColor", "outlineOffset");
                    Preview();
                    break;
                case 1:
                    Group("Text palette", "textColor", "headingColor", "mutedColor", "dangerColor", "accentColor", "preserveStatusColors");
                    Group("Frames and backgrounds", "panelLiningColor", "frameTint", "surfaceColor", "overlayColor", "hintTint");
                    Group("Controls", "buttonTint", "buttonDisabledTint", "inputTint", "trackTint", "fillTint", "thumbTint", "badgeColor");
                    Group("HUD", "starFilledColor", "starEmptyColor", "hudOutlines", "hudOutlineColor", "hudOutlineOffset");
                    break;
                case 2:
                    Group("Frames", "verticalInfoFrame", "horizontalInfoFrame", "expeditionMapBackground", "windowFrame", "panelFrame", "tooltipFrame");
                    Group("Button artwork", "buttonNormal", "buttonHover", "buttonPressed", "buttonDisabled");
                    Group("Inputs, sliders and scrollbars", "inputField", "sliderTrack", "sliderFill", "sliderHandle", "scrollbarTrack", "scrollbarHandle", "checkmark");
                    Group("Spacing and border scale", "buttonTextPadding", "buttonArtworkScale", "controlArtworkScale");
                    Group("Main-menu background", "background");
                    break;
                case 3:
                    Group("Icon artwork", "iconSets", "envelope", "envelopeUnread", "closeIcon", "icons");
                    Group("Icon colour and spacing", "iconTint", "disabledIconTint", "tintDisabledIconArtwork", "iconPadding", "iconGap", "iconInset", "iconMinSize", "iconMaxSize");
                    Group("Cursor", "cursorSource", "cursorTexture", "cursorHotspot");
                    break;
                case 4:
                    EditorGUILayout.HelpBox("Individual exceptions are matched by a label's exact GameObject name or hierarchy path. Optional Screen name limits a match to a panel or scene. Later matches take priority. Use the designer menu to capture a selected label from the Hierarchy.", MessageType.Info);
                    Group("Persistent label exceptions", "textOverrides");
                    break;
            }
            if (serializedObject.ApplyModifiedProperties()) RefreshRunningInterface();
            EditorGUILayout.Space();
            if (GUILayout.Button("Save theme asset")) AssetDatabase.SaveAssetIfDirty(target);
        }

        private void Group(string heading, params string[] properties)
        {
            EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
            foreach (string name in properties)
            {
                var property = serializedObject.FindProperty(name);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            EditorGUILayout.Space(8);
        }

        private void Preview()
        {
            var theme = (UITheme)target;
            var profile = theme.bodyText;
            int size = profile != null && profile.fontSize > 0 ? profile.fontSize : 18;
            float scale = profile != null ? profile.sizeScale : 1;
            var style = new GUIStyle(EditorStyles.label)
            {
                font = profile != null && profile.font != null ? profile.font : theme.bodyFont,
                fontSize = Mathf.Clamp(Mathf.RoundToInt(size * scale * theme.globalTextScale), 8, 72),
                wordWrap = true
            };
            style.normal.textColor = profile != null && profile.overrideColor ? profile.color : theme.textColor;
            EditorGUILayout.LabelField("Body text preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Mythologicum - The Grand Exhibition", style, GUILayout.Height(Mathf.Max(40, style.fontSize * 2)));
        }

        internal static void RefreshRunningInterface()
        {
            if (!Application.isPlaying) return;
            foreach (var frame in Object.FindObjectsByType<UIFramedPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                frame.Refresh();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) UIThemeApplicator.ApplyAll(canvas);
            foreach (var controller in Object.FindObjectsByType<BuildModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                controller.RefreshInterfacePresentation();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) UIThemeApplicator.ApplyAll(canvas);
            foreach (var tooltip in Object.FindObjectsByType<UIHoverTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                tooltip.RefreshVisible();
            Canvas.ForceUpdateCanvases();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }

    [CustomPropertyDrawer(typeof(UITextAppearance))]
    public sealed class UITextAppearanceDrawer : PropertyDrawer
    {
        private static IEnumerable<string> Rows(SerializedProperty property)
        {
            yield return "font";
            yield return "fontSize";
            yield return "sizeScale";
            yield return "overrideColor";
            if (property.FindPropertyRelative("overrideColor").boolValue) yield return "color";
            yield return "overrideFontStyle";
            if (property.FindPropertyRelative("overrideFontStyle").boolValue) yield return "fontStyle";
            yield return "overrideAlignment";
            if (property.FindPropertyRelative("overrideAlignment").boolValue) yield return "alignment";
            yield return "overrideLineSpacing";
            if (property.FindPropertyRelative("overrideLineSpacing").boolValue) yield return "lineSpacing";
            yield return "fitMode";
            if (property.FindPropertyRelative("fitMode").enumValueIndex == (int)UITextFitMode.ShrinkToFit)
            {
                yield return "minimumFontSize";
                yield return "shrinkRange";
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded)
                foreach (string name in Rows(property))
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(name), true) + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (string name in Rows(property))
                {
                    var child = property.FindPropertyRelative(name);
                    row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUI.GetPropertyHeight(child, true);
                    GUIContent childLabel = name == "fontSize" ? new GUIContent("Size (0 = designed size)", child.tooltip)
                        : name == "overrideColor" ? new GUIContent("Custom colour", child.tooltip) : new GUIContent(child.displayName, child.tooltip);
                    EditorGUI.PropertyField(row, child, childLabel, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}

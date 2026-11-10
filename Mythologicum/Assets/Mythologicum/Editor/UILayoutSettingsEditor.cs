using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    [CustomEditor(typeof(UILayoutSettings))]
    public sealed class UILayoutSettingsEditor : UnityEditor.Editor
    {
        private int section;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("These settings belong to the asset and remain after Play mode. Changes preview in the running game. Fonts and colours are on the active UI theme.", MessageType.Info);
            Draw("activeTheme");
            section = GUILayout.Toolbar(section, new[] { "Panels", "HUD & menus", "Scroll & hover", "Individual controls" });
            EditorGUILayout.Space();
            switch (section)
            {
                case 0:
                    EditorGUILayout.HelpBox("Frame width preserves the illustration's native proportions. Safe areas use fractions: 0.1 reserves ten percent of that edge. Panel positions use Unity's anchored coordinates.", MessageType.None);
                    Draw("canvasMargin");
                    Draw("matchFrameBackingToArtwork", "frameBackingOverlapPixels");
                    Draw("verticalBodyInsets", "verticalLiningInsets", "verticalHeadingInsets");
                    Draw("horizontalBodyInsets", "horizontalLiningInsets", "horizontalHeadingInsets");
                    Draw("panels");
                    break;
                case 1:
                    Draw("toolbarIconSize", "buildingIconPosition", "managementIconPosition", "encyclopediaIconPosition", "mailIconPosition", "pauseIconPosition");
                    Draw("fundsPosition", "statusSize", "fundsSize", "starsPosition", "starsSize");
                    Draw("categoryPosition", "categoryCellSize", "categoryColumns", "choicePosition", "choiceCellSize", "choiceColumns");
                    Draw("compactThreshold", "compactPosition", "compactColumns", "flyoutPadding", "flyoutGap");
                    break;
                case 2:
                    Draw("scrollSensitivity", "scrollbarWidth", "scrollbarGutter", "overflowTolerance");
                    Draw("tooltipDelay", "tooltipPadding", "tooltipMinimumWidth", "tooltipMaximumWidth", "tooltipScreenMargin", "tooltipOffset");
                    break;
                case 3:
                    EditorGUILayout.HelpBox("Use a stable Hierarchy object name or a hierarchy suffix. A screen name narrows the match when labels repeat. Later matching entries win. Adjust illustrated panels with their Panel settings so their artwork stays proportional.", MessageType.Info);
                    Draw("elementOverrides");
                    break;
            }
            if (serializedObject.ApplyModifiedProperties()) UIThemeEditor.RefreshRunningInterface();
        }

        private void Draw(params string[] names)
        {
            foreach (string name in names)
            {
                var property = serializedObject.FindProperty(name);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
        }
    }

    [CustomPropertyDrawer(typeof(UIPanelLayout))]
    public sealed class UIPanelLayoutDrawer : PropertyDrawer
    {
        private static float Step => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;
            int rows = 6;
            if (property.FindPropertyRelative("overridePosition").boolValue) rows++;
            if (property.FindPropertyRelative("overrideAnchor").boolValue) rows += 2;
            return rows * Step;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            string key = property.FindPropertyRelative("key").stringValue;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, string.IsNullOrEmpty(key) ? label.text : key, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                Field(ref row, property, "key");
                Field(ref row, property, "preferredWidth");
                Field(ref row, property, "widthScale");
                Field(ref row, property, "overridePosition");
                if (property.FindPropertyRelative("overridePosition").boolValue) Field(ref row, property, "position");
                Field(ref row, property, "overrideAnchor");
                if (property.FindPropertyRelative("overrideAnchor").boolValue)
                {
                    Field(ref row, property, "anchor");
                    Field(ref row, property, "pivot");
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        private static void Field(ref Rect row, SerializedProperty owner, string name)
        {
            row.y += Step;
            EditorGUI.PropertyField(row, owner.FindPropertyRelative(name), true);
        }
    }
}

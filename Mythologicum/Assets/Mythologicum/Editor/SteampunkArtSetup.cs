using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    /// <summary>Rebuilds the project's shared UGUI theme from the retained UI artwork.</summary>
    public static class SteampunkArtSetup
    {
        private const string Root = "Assets/Mythologicum/UI";
        private const string ThemePath = Root + "/Resources/SteampunkTheme.asset";

        [MenuItem("Mythologicum/UI/Rebuild Steampunk Theme")]
        public static void Apply()
        {
            EnsureFolder(Root + "/Resources");
            var theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme>();
                theme.name = "Steampunk Theme";
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.bodyFont = RequiredAsset<Font>("Fonts/RobotoSlab.ttf");
            theme.headingFont = RequiredAsset<Font>("Fonts/Arvo.ttf");
            theme.titleFont = RequiredAsset<Font>("Fonts/BebasNeue.ttf");
            theme.background = RequiredAsset<Texture2D>("Art/BackGround.png");

            theme.windowFrame = Sprite("Steampunk_UI_Frames_3", 3);
            theme.panelFrame = Sprite("Steampunk_UI_Frames_1", 3);
            theme.buttonNormal = Sprite("Steampunk_UI_Buttons_2", 3);
            theme.buttonHover = Sprite("Steampunk_UI_Buttons_2", 4);
            theme.buttonPressed = Sprite("Steampunk_UI_Buttons_2", 5);
            // The pack supplies three states; the skin dims the normal state when disabled.
            theme.buttonDisabled = theme.buttonNormal;
            theme.inputField = theme.panelFrame;
            theme.sliderTrack = Sprite("Steampunk_UI_Loading_Bars", 6);
            theme.sliderFill = Sprite("Steampunk_UI_Loading_Bars", 1);
            theme.sliderHandle = Sprite("Steampunk_UI_Slots_Arrows", 9);
            theme.scrollbarTrack = theme.panelFrame;
            theme.scrollbarHandle = theme.sliderHandle;

            // These symbols are intentionally rendered by the UI's existing text fallbacks.
            // None of the imported sprite sheets contains an envelope, cross, or tick.
            theme.checkmark = null;
            theme.envelope = null;
            theme.closeIcon = null;
            theme.icons = new[]
            {
                Sprite("Steampunk_UI_gameplay_icons_1", 4),  // Globe
                Sprite("Steampunk_UI_gameplay_icons_1", 8),  // Zoom in
                Sprite("Steampunk_UI_gameplay_icons_1", 14), // Zoom out
                Sprite("Steampunk_UI_gameplay_icons_1", 20), // Records
                Sprite("Steampunk_UI_gameplay_icons_1", 24), // Rest
                Sprite("Steampunk_UI_gameplay_icons_1", 29), // Sleep
                Sprite("Steampunk_UI_gameplay_icons_1", 37), // Refreshments
                Sprite("Steampunk_UI_gameplay_icons_1", 41), // Visitor
                Sprite("Steampunk_UI_gameplay_icons_2", 0),  // Play
                Sprite("Steampunk_UI_gameplay_icons_2", 4),  // Pause
                Sprite("Steampunk_UI_gameplay_icons_2", 12), // Time
                Sprite("Steampunk_UI_gameplay_icons_3", 0),  // Expedition map
                Sprite("Steampunk_UI_gameplay_icons_3", 4),  // Compass
                Sprite("Steampunk_UI_gameplay_icons_3", 8),  // Research
                Sprite("Steampunk_UI_gameplay_icons_4", 0),  // Help scroll
                Sprite("Steampunk_UI_gameplay_icons_4", 4),  // Alert scroll
                Sprite("Steampunk_UI_gameplay_icons_4", 8),  // Help with quill
                Sprite("Steampunk_UI_gameplay_icons_4", 12), // Alert with quill
                Sprite("Steampunk_UI_settings_icons", 8),   // Settings
                Sprite("Steampunk_UI_settings_icons", 4),   // Exit door
                Sprite("Steampunk_UI_Slots_Arrows", 0),      // Next
            };

            theme.textColor = new Color(0.96f, 0.92f, 0.83f);
            theme.headingColor = new Color(1f, 0.82f, 0.48f);
            theme.mutedColor = new Color(0.73f, 0.69f, 0.61f);
            theme.dangerColor = new Color(1f, 0.47f, 0.35f);
            theme.accentColor = new Color(0.83f, 0.59f, 0.28f);

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("Built Mythologicum's Steampunk theme from project UI art and fonts.");
        }

        private static T RequiredAsset<T>(string relativePath) where T : UnityEngine.Object
        {
            string path = Root + "/" + relativePath;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing UI asset: " + path);
            return asset;
        }

        private static Sprite Sprite(string atlas, int index)
        {
            string path = Root + "/Art/" + atlas + ".png";
            string name = atlas + "_" + index;
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .FirstOrDefault(candidate => candidate.name == name);
            if (sprite == null) throw new InvalidOperationException("Missing UI sprite: " + name);
            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}

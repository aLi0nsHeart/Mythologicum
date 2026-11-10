using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    /// <summary>Supplies missing artwork without resetting the designer's saved appearance.</summary>
    public static class SteampunkArtSetup
    {
        private const string Root = "Assets/Mythologicum/UI";
        private const string ThemePath = Root + "/Resources/SteampunkTheme.asset";

        [MenuItem("Mythologicum/UI/Repair Missing Steampunk References")]
        public static void Apply()
        {
            EnsureFolder(Root + "/Resources");
            var existing = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.name = "Steampunk Theme";

            theme.bodyFont = RequiredAsset<Font>("Fonts/RobotoSlab.ttf");
            theme.headingFont = RequiredAsset<Font>("Fonts/Arvo.ttf");
            theme.titleFont = RequiredAsset<Font>("Fonts/BebasNeue.ttf");
            theme.background = RequiredAsset<Texture2D>("Art/BackGround.png");

            theme.windowFrame = Sprite("Steampunk_UI_Frames_3", 3);
            theme.panelFrame = Sprite("Steampunk_UI_Frames_1", 3);
            theme.verticalInfoFrame = Sprite("Steampunk_UI_Frames_1", 2);
            theme.horizontalInfoFrame = Sprite("Steampunk_UI_Dialogue_2", 0);
            theme.expeditionMapBackground = Sprite("Steampunk_UI_Dialogue_1", 1);
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

            // The mail artwork uses the requested quill-and-scroll sprites.
            // Cross and tick symbols keep the existing text/vector fallback.
            theme.checkmark = null;
            theme.envelope = Sprite("Steampunk_UI_gameplay_icons_4", 12);
            theme.envelopeUnread = Sprite("Steampunk_UI_gameplay_icons_4", 14);
            theme.closeIcon = null;
            theme.iconSets = new[]
            {
                Icons(UIIconKind.Building, "Steampunk_UI_gameplay_icons_4", 5, 6, 5, 7),
                Icons(UIIconKind.ParkManagement, "Steampunk_UI_gameplay_icons_1", 20, 22, 21, 23),
                Icons(UIIconKind.Encyclopedia, "Steampunk_UI_gameplay_icons_4", 8, 10, 9, 11),
                Icons(UIIconKind.Pause, "Steampunk_UI_gameplay_icons_2", 4, 6, 5, 7),
                Icons(UIIconKind.Play, "Steampunk_UI_gameplay_icons_2", 0, 2, 1, 3),
                Icons(UIIconKind.Settings, "Steampunk_UI_settings_icons", 8, 10, 9, 11),
                Icons(UIIconKind.Quit, "Steampunk_UI_settings_icons", 4, 6, 5, 7),
                Icons(UIIconKind.Expeditions, "Steampunk_UI_gameplay_icons_3", 0, 2, 1, 3),
                Icons(UIIconKind.Research, "Steampunk_UI_gameplay_icons_3", 8, 11, 10, 12),
                Icons(UIIconKind.Statistics, "Steampunk_UI_gameplay_icons_1", 20, 22, 21, 23),
                Icons(UIIconKind.Visitors, "Steampunk_UI_gameplay_icons_1", 41, 43, 42, 44),
                Icons(UIIconKind.Rest, "Steampunk_UI_gameplay_icons_1", 24, 26, 25, 27),
                Icons(UIIconKind.Food, "Steampunk_UI_gameplay_icons_1", 37, 39, 38, 40),
                Icons(UIIconKind.Mail, "Steampunk_UI_gameplay_icons_4", 12, 13, 13, 15),
                Icons(UIIconKind.MailUnread, "Steampunk_UI_gameplay_icons_4", 14, 14, 14, 15),
                Icons(UIIconKind.Funds, "Steampunk_UI_inventory_cursor_icons", 31, 31, 31, 31)
            };
            theme.cursorSource = Sprite("Steampunk_UI_inventory_cursor_icons", 6);
            theme.cursorTexture = existing != null && existing.cursorTexture != null
                ? existing.cursorTexture : BuildCursor(theme.cursorSource);
            theme.cursorHotspot = new Vector2(2, 2);
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

            if (existing != null)
            {
                Undo.RecordObject(existing, "Repair missing UI references");
                foreach (var field in typeof(UITheme).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    object saved = field.GetValue(existing);
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                    {
                        if ((UnityEngine.Object)saved == null) field.SetValue(existing, field.GetValue(theme));
                    }
                    else if (saved is Array array && array.Length == 0 || saved == null && field.FieldType.IsArray)
                        field.SetValue(existing, field.GetValue(theme));
                }
                UnityEngine.Object.DestroyImmediate(theme);
                theme = existing;
            }
            else AssetDatabase.CreateAsset(theme, ThemePath);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            Debug.Log("Repaired missing UI references. Existing fonts, colours, sizes and overrides were preserved.");
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

        private static UIIconSet Icons(UIIconKind kind, string atlas, int normal, int highlighted, int pressed, int disabled)
        {
            return new UIIconSet { kind = kind, normal = Sprite(atlas, normal), highlighted = Sprite(atlas, highlighted),
                pressed = Sprite(atlas, pressed), disabled = Sprite(atlas, disabled) };
        }

        private static Texture2D BuildCursor(Sprite sprite)
        {
            const int size = 48;
            string path = Root + "/Cursors/ExhibitionCursor.png";
            EnsureFolder(Root + "/Cursors");
            Texture2D source = null, cursor = null;
            try
            {
                // Read the original PNG, avoiding compression and readability changes to the atlas.
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)), false))
                    throw new InvalidOperationException("Could not read the cursor's source atlas.");
                cursor = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Rect region = sprite.rect;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float u = (region.x + (x + .5f) * region.width / size) / source.width;
                    float v = (region.y + (y + .5f) * region.height / size) / source.height;
                    pixels[y * size + x] = source.GetPixelBilinear(u, v);
                }
                cursor.SetPixels(pixels); cursor.Apply(false, false);
                File.WriteAllBytes(path, cursor.EncodeToPNG());
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (cursor != null) UnityEngine.Object.DestroyImmediate(cursor);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.isReadable = true; importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 64; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}

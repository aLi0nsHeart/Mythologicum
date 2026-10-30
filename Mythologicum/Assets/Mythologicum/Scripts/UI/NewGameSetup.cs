using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>New-park settings and a top-down preview generated from the actual terrain rules.</summary>
    public sealed class NewGameSetup : MonoBehaviour
    {
        private static NewGameSetup active;
        private static MapGenerationOptions lastUsed;
        public static MapGenerationOptions Pending;
        public static bool IsOpen => active != null && active.gameObject.activeSelf;
        public MapGenerationOptions CurrentOptions => options.Clone();

        private MapGenerationOptions options;
        private Action<MapGenerationOptions> start;
        private InputField seedInput;
        private Slider hills, hillWidth, forest;
        private Text hillsLabel, widthLabel, forestLabel, previewSummary, errorLabel;
        private RawImage preview;
        private Texture2D previewTexture;
        private GridHeightField previewField;
        private Button create;
        private readonly Button[] sizeButtons = new Button[3];
        private readonly Button[] styleButtons = new Button[3];
        private bool refreshing;
        private static readonly int[] Sizes = { 32, 50, 72 };
        private static readonly string[] SizeNames = { "Small", "Standard", "Large" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            active = null; Pending = null; lastUsed = null;
        }

        public static void Open(Canvas canvas, Action<MapGenerationOptions> start)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (start == null) throw new ArgumentNullException(nameof(start));
            if (IsOpen) { active.transform.SetAsLastSibling(); return; }
            Canvas.ForceUpdateCanvases();
            var root = ManagementWidgets.Modal(canvas, "New park setup");
            active = root.AddComponent<NewGameSetup>();
            active.options = lastUsed?.Clone() ?? new MapGenerationOptions();
            active.start = start;
            active.Build(canvas);
            root.SetActive(true); root.transform.SetAsLastSibling();
            active.Refresh();
        }

        private void Build(Canvas canvas)
        {
            var card = ManagementWidgets.Card(gameObject, canvas);
            ManagementWidgets.Label(card, "Create a new park", new Vector2(24, 12), new Vector2(850, 42), 28);
            ManagementWidgets.Button(card, "Close", new Vector2(1040, 10), new Vector2(100, 38), Close);
            ManagementWidgets.Label(card, "Map size", new Vector2(24, 76), new Vector2(470, 30), 21);
            for (int i = 0; i < Sizes.Length; i++)
            {
                int size = Sizes[i];
                sizeButtons[i] = ManagementWidgets.Button(card, SizeNames[i] + "\n" + (size * 2) + " x " + (size * 2) + " m",
                    new Vector2(24 + i * 157, 111), new Vector2(147, 52), () => { options.width = options.depth = size; Refresh(); }, 18);
            }
            ManagementWidgets.Label(card, "Map seed", new Vector2(24, 180), new Vector2(470, 30), 21);
            seedInput = SeedInput(card, new Vector2(24, 216), new Vector2(292, 38));
            seedInput.SetTextWithoutNotify(options.seed.ToString(CultureInfo.InvariantCulture));
            seedInput.onEndEdit.AddListener(value => { if (ReadSeed()) Refresh(); });
            seedInput.onValueChanged.AddListener(value =>
            {
                if (refreshing) return;
                bool valid = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
                errorLabel.text = valid ? "" : "Enter a whole-number seed, for example 2471.";
                create.interactable = valid;
            });
            ManagementWidgets.Button(card, "Random seed", new Vector2(326, 216), new Vector2(159, 38), () =>
            {
                options.seed = Guid.NewGuid().GetHashCode();
                seedInput.SetTextWithoutNotify(options.seed.ToString(CultureInfo.InvariantCulture)); Refresh();
            }, 18);

            ManagementWidgets.Label(card, "Terrain style", new Vector2(24, 274), new Vector2(470, 30), 21);
            styleButtons[0] = ManagementWidgets.Button(card, "Flat", new Vector2(24, 309), new Vector2(147, 38), () => SetStyle(0, 28));
            styleButtons[1] = ManagementWidgets.Button(card, "Rolling hills", new Vector2(181, 309), new Vector2(147, 38), () => SetStyle(7, 28));
            styleButtons[2] = ManagementWidgets.Button(card, "Rugged", new Vector2(338, 309), new Vector2(147, 38), () => SetStyle(14, 18));
            hillsLabel = ManagementWidgets.Label(card, "Hill height", new Vector2(24, 365), new Vector2(470, 26));
            hills = SliderControl(card, "Hill height", new Vector2(24, 397), 0, 20,
                value => { options.relief = value; Refresh(); });
            widthLabel = ManagementWidgets.Label(card, "Hill width", new Vector2(24, 436), new Vector2(470, 26));
            hillWidth = SliderControl(card, "Hill width", new Vector2(24, 468), 12, 64,
                value => { options.featureSize = value; Refresh(); });
            forestLabel = ManagementWidgets.Label(card, "Forest density", new Vector2(24, 507), new Vector2(470, 26));
            forest = SliderControl(card, "Forest density", new Vector2(24, 539), 0, 1,
                value => { options.forestDensity = value; Refresh(); });
            errorLabel = ManagementWidgets.Label(card, "", new Vector2(24, 577), new Vector2(710, 30), 17);
            errorLabel.color = new Color(1f, .45f, .25f);

            ManagementWidgets.Label(card, "Map preview", new Vector2(596, 50), new Vector2(510, 32), 22);
            var box = ManagementWidgets.Box(card, "Map preview frame", new Vector2(599, 90), new Vector2(486, 486), new Color(.08f,.14f,.12f));
            var imageRect = ManagementWidgets.Rect(box.transform, "Generated map preview", new Vector2(7, 7), new Vector2(472, 472));
            preview = imageRect.gameObject.AddComponent<RawImage>(); preview.raycastTarget = false;
            previewSummary = ManagementWidgets.Label(card, "", new Vector2(596, 581), new Vector2(530, 35), 16);
            ManagementWidgets.Button(card, "Cancel", new Vector2(24, 612), new Vector2(170, 38), Close);
            create = ManagementWidgets.Button(card, "Create park", new Vector2(884, 612), new Vector2(235, 38), CreatePark, 21);
            ManagementWidgets.Label(card, "Place the park entrance anywhere you choose.", new Vector2(214, 619), new Vector2(650, 28), 18);
        }

        private void SetStyle(float relief, float feature)
        {
            options.relief = relief; options.featureSize = feature; Refresh();
        }
        private bool ReadSeed()
        {
            if (int.TryParse(seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            { options.seed = value; return true; }
            errorLabel.text = "Enter a whole-number seed between -2147483648 and 2147483647.";
            create.interactable = false; return false;
        }
        private void CreatePark()
        {
            if (!ReadSeed() || !options.Validate(out string error))
            { if (errorLabel.text.Length == 0) errorLabel.text = "Check the map settings."; return; }
            var selected = options.Clone(); lastUsed = selected.Clone();
            var callback = start; Close(); callback(selected);
        }
        public void Close()
        {
            gameObject.SetActive(false);
            if (active == this) active = null;
            Destroy(gameObject);
        }
        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }
        private void Refresh()
        {
            if (refreshing) return;
            refreshing = true;
            hills.SetValueWithoutNotify(options.relief);
            hillWidth.SetValueWithoutNotify(options.featureSize);
            forest.SetValueWithoutNotify(options.forestDensity);
            hillsLabel.text = "Hill height: " + options.relief.ToString("0.0") + " m";
            widthLabel.text = "Hill width: " + options.featureSize.ToString("0") + " m";
            forestLabel.text = "Forest density: " + (options.forestDensity * 100).ToString("0") + "%";
            for (int i = 0; i < sizeButtons.Length; i++) Tint(sizeButtons[i], options.width == Sizes[i]);
            Tint(styleButtons[0], options.relief < .01f);
            Tint(styleButtons[1], Mathf.Abs(options.relief - 7) < .1f && Mathf.Abs(options.featureSize - 28) < .1f);
            Tint(styleButtons[2], Mathf.Abs(options.relief - 14) < .1f && Mathf.Abs(options.featureSize - 18) < .1f);
            bool valid = int.TryParse(seedInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            create.interactable = valid;
            errorLabel.text = valid ? "" : "Enter a whole-number seed, for example 2471.";
            RegeneratePreview(); refreshing = false;
        }
        private static void Tint(Button button, bool selected)
        { button.GetComponent<Image>().color = selected ? new Color(.43f,.37f,.18f) : new Color(.18f,.28f,.31f); }

        private void RegeneratePreview()
        {
            previewField = options.CreateHeightField();
            const int pixels = 144;
            if (previewTexture == null)
            {
                previewTexture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false) { name = "New park terrain preview", filterMode = FilterMode.Point };
                preview.texture = previewTexture;
            }
            var colors = new Color[pixels * pixels];
            for (int z = 0; z < pixels; z++) for (int x = 0; x < pixels; x++)
            {
                float u = (x + .5f) / pixels * options.width, v = (z + .5f) / pixels * options.depth;
                float h = previewField.Sample(u, v);
                float dx = previewField.Sample(u + .2f, v) - previewField.Sample(u - .2f, v);
                float dz = previewField.Sample(u, v + .2f) - previewField.Sample(u, v - .2f);
                float shade = Mathf.Clamp(.9f + (dz - dx) * .45f, .62f, 1.16f);
                var color = Color.Lerp(new Color(.31f,.47f,.24f), new Color(.75f,.7f,.47f), Mathf.Clamp01(h / 20f));
                if (NatureGeneration.GroundIsSoil(Mathf.Floor(u)+.5f, Mathf.Floor(v)+.5f, options.seed)) color = Color.Lerp(color, new Color(.46f,.37f,.24f), .55f);
                colors[z * pixels + x] = color * shade;
                colors[z * pixels + x].a = 1;
            }
            var art = Resources.Load<TerrainArtDefinition>("Terrain/TerrainArt");
            int band = art?.forest?.enabled==true ? art.forest.widthInCells : 0, forestSeed = art?.forest?.seed ?? 819;
            var nature = NatureGeneration.Plan(options.width, options.depth, band, options.forestDensity, options.seed ^ forestSeed);
            foreach (var cell in nature.Trees) Dot(cell, new Color(.09f,.24f,.1f), 1);
            foreach (var cell in nature.Rocks) Dot(cell, new Color(.47f,.48f,.46f), 0);
            void Dot(Vector2Int cell, Color color, int radius)
            {
                int cx = (int)((cell.x + .5f) / options.width * pixels), cz = (int)((cell.y + .5f) / options.depth * pixels);
                for (int z = Mathf.Max(0, cz - radius); z <= Mathf.Min(pixels - 1, cz + radius); z++)
                    for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(pixels - 1, cx + radius); x++) colors[z * pixels + x] = color;
            }
            previewTexture.SetPixels(colors); previewTexture.Apply(false, false);
            previewSummary.text = $"{options.width * 2} x {options.depth * 2} m | Seed {options.seed}\nLight: higher ground | Dark green: forest | Gray: rocks";
        }
        private static InputField SeedInput(Transform parent, Vector2 position, Vector2 size)
        {
            var go = ManagementWidgets.Box(parent, "Map seed", position, size, new Color(.14f,.2f,.22f));
            var text = ManagementWidgets.Label(go.transform, "Entered seed", new Vector2(8, 6), size - new Vector2(16, 12), 20);
            text.supportRichText = false;
            var field = go.AddComponent<InputField>();field.targetGraphic = go.GetComponent<Image>(); field.textComponent = text;
            field.characterValidation = InputField.CharacterValidation.Integer; field.characterLimit = 11;
            field.lineType = InputField.LineType.SingleLine; return field;
        }
        private static Slider SliderControl(Transform parent, string name, Vector2 position, float minimum, float maximum, Action<float> changed)
        {
            var rect = ManagementWidgets.Rect(parent, name, position, new Vector2(461, 22));
            var slider = rect.gameObject.AddComponent<Slider>(); slider.minValue = minimum; slider.maxValue = maximum;
            var background = ManagementWidgets.Box(rect, "Track", new Vector2(0, 7), new Vector2(461, 8), new Color(.11f,.18f,.19f));
            var fillArea = ManagementWidgets.Rect(rect, "Fill area", new Vector2(10, 7), new Vector2(441, 8));
            var fill = ManagementWidgets.Box(fillArea, "Fill", Vector2.zero, new Vector2(441,8), new Color(.53f,.57f,.31f));
            var fillRect = (RectTransform)fill.transform; fillRect.anchorMin = Vector2.zero;fillRect.anchorMax = Vector2.one;fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var handleArea = ManagementWidgets.Rect(rect,"Handle area",Vector2.zero,new Vector2(461,22));
            var handle = ManagementWidgets.Box(handleArea, "Handle",Vector2.zero,new Vector2(18,22),new Color(.88f,.84f,.64f));
            var handleRect = (RectTransform)handle.transform;handleRect.pivot = new Vector2(.5f,.5f);handleRect.anchoredPosition = Vector2.zero;handleRect.sizeDelta=new Vector2(18,0);
            slider.fillRect = fillRect;slider.handleRect = handleRect;slider.targetGraphic = handle.GetComponent<Image>();slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(value => changed(value)); return slider;
        }
        private void OnDestroy()
        { if (previewTexture != null) Destroy(previewTexture); if (active == this) active = null; }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Live audio/display/camera preferences. Opening this window never changes simulation time.</summary>
    public sealed class SettingsPresentation : MonoBehaviour
    {
        private static SettingsPresentation active;
        public static bool IsOpen => active != null && active.gameObject.activeSelf;
        private Slider volume, movement, rotation, zoom, interfaceScale;
        private Dropdown display, quality, language;
        private Text volumeLabel, movementLabel, rotationLabel, zoomLabel, scaleLabel, notice, movementKeys;
        private GameObject shortcutsWindow;
        private Text bindingMessage;
        private readonly Dictionary<GameplayAction, Button> bindingButtons = new Dictionary<GameplayAction, Button>();
        private bool refreshing;
        private float saveAt;
        private void OnEnable() => Loc.Changed += LanguageChanged;
        private void OnDisable() => Loc.Changed -= LanguageChanged;
        private void LanguageChanged() { Refresh(); RefreshBindingButtons(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => active = null;

        public static SettingsPresentation Open(Canvas canvas)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (IsOpen) { active.transform.SetAsLastSibling(); return active; }
            Canvas.ForceUpdateCanvases();
            var root = ManagementWidgets.Modal(canvas, "Settings window");
            UIThemeElement.Mark(root, UIThemeRole.Overlay);
            active = root.AddComponent<SettingsPresentation>(); active.Build(canvas);
            root.SetActive(true); root.transform.SetAsLastSibling(); active.Refresh(); UIThemeApplicator.ApplyAll(canvas); return active;
        }
        public static void CloseActive()
        {
            if (active == null) return;
            if (active.shortcutsWindow != null) active.CloseShortcuts(); else active.Close();
        }
        public void Close()
        {
            CloseShortcuts();
            GameSettings.Save(); gameObject.SetActive(false);
            if (active == this) active = null;
            Destroy(gameObject);
        }
        private void Build(Canvas canvas)
        {
            var card = ManagementWidgets.Card(gameObject, canvas);
            UIThemeElement.Mark(card.gameObject, UIThemeRole.Window);
            ManagementWidgets.Label(card, "ui.settings.title", new Vector2(30, 18), new Vector2(1000, 48), 30);
            ManagementWidgets.Label(card, "ui.settings.sound_display", new Vector2(40, 12), new Vector2(470, 36), 23);
            ManagementWidgets.Label(card, "ui.settings.camera_controls", new Vector2(640, 12), new Vector2(470, 36), 23);
            volumeLabel = ManagementWidgets.Label(card, "ui.settings.master_volume", new Vector2(40, 52), new Vector2(470, 30), 20);
            volume = SliderControl(card, "Master volume", new Vector2(40, 85), 0, 1, GameSettings.SetMasterVolume);
            ManagementWidgets.Label(card, "ui.settings.window_mode", new Vector2(40, 122), new Vector2(470, 30), 20);
            display = DropdownControl(card, "Window mode", new Vector2(40, 155), WindowModeNames(), GameSettings.SetWindowMode);
            ManagementWidgets.Label(card, "ui.settings.graphics_quality", new Vector2(40, 192), new Vector2(470, 30), 20);
            quality = DropdownControl(card, "Quality", new Vector2(40, 225), QualityNames(), GameSettings.SetQuality);
            scaleLabel = ManagementWidgets.Label(card, "ui.settings.interface_scale", new Vector2(40, 278), new Vector2(470, 28), 20);
            interfaceScale = SliderControl(card, "Interface scale", new Vector2(40, 311),
                PlayControlsDefinition.Current.minimumUIScale, PlayControlsDefinition.Current.maximumUIScale, GameSettings.SetUIScale);
            movementLabel = ManagementWidgets.Label(card, "ui.settings.camera_movement", new Vector2(640, 52), new Vector2(470, 30), 20);
            movement = SliderControl(card, "Camera movement", new Vector2(640, 85), .25f, 3, GameSettings.SetMoveSensitivity);
            rotationLabel = ManagementWidgets.Label(card, "ui.settings.camera_rotation", new Vector2(640, 122), new Vector2(470, 30), 20);
            rotation = SliderControl(card, "Camera rotation", new Vector2(640, 155), .25f, 3, GameSettings.SetRotationSensitivity);
            zoomLabel = ManagementWidgets.Label(card, "ui.settings.camera_zoom", new Vector2(640, 192), new Vector2(470, 30), 20);
            zoom = SliderControl(card, "Camera zoom", new Vector2(640, 225), .25f, 3, GameSettings.SetZoomSensitivity);
            ManagementWidgets.Label(card, "ui.settings.movement_controls", new Vector2(640, 278), new Vector2(470, 28), 20);
            movementKeys = ManagementWidgets.Label(card, "Movement keys", new Vector2(640, 312), new Vector2(470, 70), 16);
            ManagementWidgets.Label(card, "ui.settings.language", new Vector2(40, 356), new Vector2(160, 30), 20);
            language = DropdownControl(card, "Language", new Vector2(212, 350), LanguageNames(), value => GameSettings.SetLanguage((GameLanguage)value), 298);
            Loc.Set(ManagementWidgets.Label(card, "Vegetation credits", new Vector2(40, 394), new Vector2(470, 20), 15), "ui.settings.vegetation_credits");
            ManagementWidgets.Button(card, "ui.settings.keyboard_shortcuts", new Vector2(850, 390), new Vector2(260, 36), () => OpenShortcuts(canvas), 17);
            notice = ManagementWidgets.Label(card, "ui.settings.notice", new Vector2(40, 416), new Vector2(780, 20), 15);
            ManagementWidgets.Button(card, "ui.settings.reset", new Vector2(40, 441), new Vector2(280, 44), () => { GameSettings.ResetDefaults(); Refresh(); });
            ManagementWidgets.Button(card, "ui.settings.back", new Vector2(850, 441), new Vector2(260, 44), Close);
        }
        private void Changed(Action action)
        {
            if (refreshing) return;
            action(); saveAt = Time.unscaledTime + .3f; Refresh();
        }
        private void Update()
        {
            GameInput.UpdateCapture();
            // Escape is handled once by GameSaveMenu, preventing a close from also toggling Pause.
            if (GameSettings.HasUnsavedChanges && Time.unscaledTime >= saveAt) GameSettings.Save();
        }
        private void Refresh()
        {
            refreshing = true;
            volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            movement.SetValueWithoutNotify(GameSettings.MoveSensitivity);
            rotation.SetValueWithoutNotify(GameSettings.RotationSensitivity);
            zoom.SetValueWithoutNotify(GameSettings.ZoomSensitivity);
            interfaceScale.SetValueWithoutNotify(GameSettings.UIScale);
            SetOptions(display, WindowModeNames()); SetOptions(quality, QualityNames()); SetOptions(language, LanguageNames());
            language.SetValueWithoutNotify((int)Loc.Language);
            display.SetValueWithoutNotify(GameSettings.WindowMode); quality.SetValueWithoutNotify(GameSettings.Quality);
            Loc.Set(volumeLabel, "ui.settings.volume_value", GameSettings.MasterVolume);
            Loc.Set(movementLabel, "ui.settings.movement_value", GameSettings.MoveSensitivity);
            Loc.Set(rotationLabel, "ui.settings.rotation_value", GameSettings.RotationSensitivity);
            Loc.Set(zoomLabel, "ui.settings.zoom_value", GameSettings.ZoomSensitivity);
            Loc.Set(scaleLabel, "ui.settings.scale_value", GameSettings.UIScale);
            bool defaults = GameInput.GetKey(GameplayAction.MoveForward) == GameInput.DefaultKey(GameplayAction.MoveForward)
                && GameInput.GetKey(GameplayAction.MoveBack) == GameInput.DefaultKey(GameplayAction.MoveBack)
                && GameInput.GetKey(GameplayAction.MoveLeft) == GameInput.DefaultKey(GameplayAction.MoveLeft)
                && GameInput.GetKey(GameplayAction.MoveRight) == GameInput.DefaultKey(GameplayAction.MoveRight);
            string movementCaption = defaults ? Loc.Text("ui.settings.default_movement") : string.Join("/", GameInput.KeyCaption(GameplayAction.MoveForward),
                GameInput.KeyCaption(GameplayAction.MoveLeft), GameInput.KeyCaption(GameplayAction.MoveBack), GameInput.KeyCaption(GameplayAction.MoveRight)) + " / " + Loc.Text("ui.settings.arrows");
            Loc.Set(movementKeys, "ui.settings.movement_instructions", movementCaption, GameInput.KeyCaption(GameplayAction.RotateLeft), GameInput.KeyCaption(GameplayAction.RotateRight));
            if (notice != null) Loc.Set(notice, Application.isEditor ? "ui.settings.notice_editor" : "ui.settings.notice");
            refreshing = false;
        }

        private void OpenShortcuts(Canvas canvas)
        {
            if (shortcutsWindow != null) return;
            shortcutsWindow = ManagementWidgets.Modal(canvas, "Keyboard shortcuts window");
            var card = ManagementWidgets.Card(shortcutsWindow, canvas);
            ManagementWidgets.Label(card, "ui.settings.keyboard_shortcuts", new Vector2(20, 12), new Vector2(900, 38), 27);
            Loc.Set(ManagementWidgets.Button(card, "ui.settings.close_shortcuts", new Vector2(1030, 8), new Vector2(110, 36), CloseShortcuts, 16)
                .GetComponentInChildren<Text>(), "ui.common.close");
            bindingMessage = ManagementWidgets.Label(card, "ui.settings.select_shortcut",
                new Vector2(20, 48), new Vector2(1100, 42), 17);
            var actions = (GameplayAction[])Enum.GetValues(typeof(GameplayAction));
            int rows = (actions.Length + 1) / 2;
            var content = ManagementWidgets.Scroll(card, "Shortcut list", new Vector2(20, 100), new Vector2(1100, 330), rows * 50);
            bindingButtons.Clear();
            for (int i = 0; i < actions.Length; i++)
            {
                GameplayAction action = actions[i];
                float x = (i / rows) * 550, y = (i % rows) * 50;
                ManagementWidgets.Label(content, GameInput.NameId(action), new Vector2(x + 8, y + 6), new Vector2(320, 36), 18);
                var button = ManagementWidgets.Button(content, "Rebind " + action, new Vector2(x + 330, y), new Vector2(190, 36), () =>
                {
                    Loc.Set(bindingMessage, "ui.settings.press_shortcut", GameInput.Name(action));
                    GameInput.BeginCapture(action, (success, message) =>
                    {
                        if (this == null || bindingMessage == null) return;
                        bindingMessage.text = message; RefreshBindingButtons(); Refresh();
                    });
                    RefreshBindingButtons();
                }, 17);
                bindingButtons.Add(action, button);
            }
            ManagementWidgets.Button(card, "ui.settings.reset_shortcuts", new Vector2(20, 448), new Vector2(320, 38), () =>
            {
                GameInput.ResetDefaults(); RefreshBindingButtons(); Refresh();
                Loc.Set(bindingMessage, "ui.settings.shortcuts_restored");
            }, 17);
            shortcutsWindow.SetActive(true); shortcutsWindow.transform.SetAsLastSibling();
            RefreshBindingButtons(); UIThemeApplicator.ApplyAll(canvas);
        }
        private void RefreshBindingButtons()
        {
            foreach (var pair in bindingButtons)
                if (pair.Value != null) pair.Value.GetComponentInChildren<Text>().text = GameInput.KeyCaption(pair.Key);
        }
        private void CloseShortcuts()
        {
            GameInput.CancelCapture();
            if (shortcutsWindow == null) return;
            shortcutsWindow.SetActive(false); Destroy(shortcutsWindow); shortcutsWindow = null; bindingButtons.Clear();
        }

        private Slider SliderControl(Transform parent, string name, Vector2 position, float min, float max, Action<float> changed)
        {
            var rect = ManagementWidgets.Rect(parent, name, position, new Vector2(470, 30));
            var slider = rect.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            ManagementWidgets.Box(rect, "Track", new Vector2(0, 10), new Vector2(470, 10), new Color(.13f, .11f, .075f));
            var fillArea = ManagementWidgets.Rect(rect, "Fill area", new Vector2(12, 10), new Vector2(446, 10));
            var fill = ManagementWidgets.Box(fillArea, "Fill", Vector2.zero, new Vector2(446, 10), new Color(.68f, .48f, .19f));
            var fillRect = (RectTransform)fill.transform; fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var handleArea = ManagementWidgets.Rect(rect, "Handle area", Vector2.zero, new Vector2(470, 30));
            var handle = ManagementWidgets.Box(handleArea, "Handle", Vector2.zero, new Vector2(24, 30), new Color(.9f, .75f, .4f));
            var handleRect = (RectTransform)handle.transform; handleRect.pivot = new Vector2(.5f, .5f); handleRect.anchoredPosition = Vector2.zero; handleRect.sizeDelta = new Vector2(24, 0);
            slider.fillRect = fillRect; slider.handleRect = handleRect; slider.targetGraphic = handle.GetComponent<Image>(); slider.direction = Slider.Direction.LeftToRight;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(value => Changed(() => changed(value))); return slider;
        }

        private static string[] WindowModeNames() => new[] { Loc.Text("ui.settings.windowed"), Loc.Text("ui.settings.borderless"), Loc.Text("ui.settings.exclusive") };
        private static string[] QualityNames()
        {
            var names = QualitySettings.names;
            if (names.Length == 0) return new[] { Loc.Text("ui.settings.default_quality") };
            return Array.ConvertAll(names, name => Loc.Resolve("quality." + name));
        }
        private static string[] LanguageNames() => Array.ConvertAll((GameLanguage[])Enum.GetValues(typeof(GameLanguage)), value => Loc.Text("language." + value));
        private static void SetOptions(Dropdown target, string[] values)
        {
            if (target == null) return;
            if (target.options.Count != values.Length)
            { target.ClearOptions(); target.AddOptions(new List<string>(values)); }
            else for (int i = 0; i < values.Length; i++) target.options[i].text = values[i];
            target.RefreshShownValue();
        }
        private Dropdown DropdownControl(Transform parent, string name, Vector2 position, string[] options, Action<int> changed, float width = 470)
        {
            var box = ManagementWidgets.Box(parent, name, position, new Vector2(width, 40), new Color(.13f, .11f, .075f));
            var dropdown = box.AddComponent<Dropdown>(); dropdown.targetGraphic = box.GetComponent<Image>(); dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            var caption = ManagementWidgets.Label(box.transform, "Selected option", new Vector2(14, 5), new Vector2(width-60, 30), 19); caption.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText = caption;
            var arrow = ManagementWidgets.Label(box.transform, "Dropdown arrow", new Vector2(width-40, 5), new Vector2(28, 30), 22); Loc.Set(arrow,"ui.symbol.dropdown"); arrow.alignment = TextAnchor.MiddleCenter;
            var template = ManagementWidgets.Box(box.transform, "Template", new Vector2(0, 42), new Vector2(width, Math.Min(240, options.Length * 42 + 8)), new Color(.075f, .06f, .04f));
            var scroll = template.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = ManagementWidgets.Rect(template.transform, "Viewport", Vector2.zero, ((RectTransform)template.transform).sizeDelta);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero; viewport.gameObject.AddComponent<RectMask2D>();
            var content = ManagementWidgets.Rect(viewport, "Content", new Vector2(0, 4), new Vector2(width, 42));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.sizeDelta = new Vector2(0, 42);
            var item = ManagementWidgets.Box(content, "Item", Vector2.zero, new Vector2(width, 42), new Color(.18f, .14f, .08f));
            var toggle = item.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>(); toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            var check = ManagementWidgets.Box(item.transform, "Item checkmark", new Vector2(10, 12), new Vector2(18, 18), new Color(.83f, .65f, .26f)); toggle.graphic = check.GetComponent<Image>();
            var text = ManagementWidgets.Label(item.transform, "Item label", new Vector2(40, 5), new Vector2(width-52, 32), 19); text.alignment = TextAnchor.MiddleLeft;
            dropdown.template = (RectTransform)template.transform; dropdown.itemText = text;
            dropdown.options = new List<Dropdown.OptionData>(); foreach (string option in options) dropdown.options.Add(new Dropdown.OptionData(option));
            dropdown.RefreshShownValue();
            scroll.viewport = viewport; scroll.content = content; scroll.scrollSensitivity = 24;
            template.SetActive(false); dropdown.onValueChanged.AddListener(value => Changed(() => changed(value))); return dropdown;
        }
        private void OnDestroy() { CloseShortcuts(); GameSettings.Save(); if (active == this) active = null; }
    }
}

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
        private Slider volume, movement, rotation, zoom;
        private Dropdown display, quality;
        private Text volumeLabel, movementLabel, rotationLabel, zoomLabel, notice;
        private bool refreshing;
        private float saveAt;

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
        public static void CloseActive() { if (active != null) active.Close(); }
        public void Close()
        {
            GameSettings.Save(); gameObject.SetActive(false);
            if (active == this) active = null;
            Destroy(gameObject);
        }
        private void Build(Canvas canvas)
        {
            var card = ManagementWidgets.Card(gameObject, canvas);
            UIThemeElement.Mark(card.gameObject, UIThemeRole.Window);
            ManagementWidgets.Label(card, "Settings", new Vector2(30, 18), new Vector2(1000, 48), 30);
            ManagementWidgets.Label(card, "Sound & display", new Vector2(40, 88), new Vector2(470, 36), 23);
            ManagementWidgets.Label(card, "Camera controls", new Vector2(640, 88), new Vector2(470, 36), 23);
            volumeLabel = ManagementWidgets.Label(card, "Master volume", new Vector2(40, 145), new Vector2(470, 30), 20);
            volume = SliderControl(card, "Master volume", new Vector2(40, 184), 0, 1, GameSettings.SetMasterVolume);
            ManagementWidgets.Label(card, "Window mode", new Vector2(40, 247), new Vector2(470, 30), 20);
            display = DropdownControl(card, "Window mode", new Vector2(40, 286), new[] { "Windowed", "Borderless fullscreen", "Exclusive fullscreen" }, GameSettings.SetWindowMode);
            ManagementWidgets.Label(card, "Graphics quality", new Vector2(40, 350), new Vector2(470, 30), 20);
            string[] names = QualitySettings.names;
            quality = DropdownControl(card, "Quality", new Vector2(40, 389), names.Length > 0 ? names : new[] { "Default" }, GameSettings.SetQuality);
            movementLabel = ManagementWidgets.Label(card, "Camera movement", new Vector2(640, 145), new Vector2(470, 30), 20);
            movement = SliderControl(card, "Camera movement", new Vector2(640, 184), .25f, 3, GameSettings.SetMoveSensitivity);
            rotationLabel = ManagementWidgets.Label(card, "Camera rotation", new Vector2(640, 247), new Vector2(470, 30), 20);
            rotation = SliderControl(card, "Camera rotation", new Vector2(640, 286), .25f, 3, GameSettings.SetRotationSensitivity);
            zoomLabel = ManagementWidgets.Label(card, "Camera zoom", new Vector2(640, 350), new Vector2(470, 30), 20);
            zoom = SliderControl(card, "Camera zoom", new Vector2(640, 389), .25f, 3, GameSettings.SetZoomSensitivity);
            notice = ManagementWidgets.Label(card, "Preferences apply immediately and are saved automatically.", new Vector2(40, 484), new Vector2(1070, 54), 18);
            ManagementWidgets.Button(card, "Reset defaults", new Vector2(40, 570), new Vector2(280, 50), () => { GameSettings.ResetDefaults(); Refresh(); });
            ManagementWidgets.Button(card, "Back", new Vector2(850, 570), new Vector2(260, 50), Close);
        }
        private void Changed(Action action)
        {
            if (refreshing) return;
            action(); saveAt = Time.unscaledTime + .3f; Refresh();
        }
        private void Update()
        {
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
            display.SetValueWithoutNotify(GameSettings.WindowMode); quality.SetValueWithoutNotify(GameSettings.Quality);
            volumeLabel.text = $"Master volume: {GameSettings.MasterVolume:P0}";
            movementLabel.text = $"Camera movement: {GameSettings.MoveSensitivity:0.00}x";
            rotationLabel.text = $"Camera rotation: {GameSettings.RotationSensitivity:0.00}x";
            zoomLabel.text = $"Camera zoom: {GameSettings.ZoomSensitivity:0.00}x";
            if (notice != null) notice.text = Application.isEditor
                ? "Preferences apply immediately and are saved automatically. Window mode applies to standalone builds."
                : "Preferences apply immediately and are saved automatically.";
            refreshing = false;
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

        private Dropdown DropdownControl(Transform parent, string name, Vector2 position, string[] options, Action<int> changed)
        {
            var box = ManagementWidgets.Box(parent, name, position, new Vector2(470, 46), new Color(.13f, .11f, .075f));
            var dropdown = box.AddComponent<Dropdown>(); dropdown.targetGraphic = box.GetComponent<Image>(); dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            var caption = ManagementWidgets.Label(box.transform, "Selected option", new Vector2(14, 8), new Vector2(410, 30), 19); caption.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText = caption;
            var arrow = ManagementWidgets.Label(box.transform, "Dropdown arrow", new Vector2(430, 8), new Vector2(28, 30), 22); arrow.text = "v"; arrow.alignment = TextAnchor.MiddleCenter;
            var template = ManagementWidgets.Box(box.transform, "Template", new Vector2(0, 48), new Vector2(470, Math.Min(240, options.Length * 42 + 8)), new Color(.075f, .06f, .04f));
            var scroll = template.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = ManagementWidgets.Rect(template.transform, "Viewport", Vector2.zero, ((RectTransform)template.transform).sizeDelta);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero; viewport.gameObject.AddComponent<RectMask2D>();
            var content = ManagementWidgets.Rect(viewport, "Content", new Vector2(0, 4), new Vector2(470, 42));
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.sizeDelta = new Vector2(0, 42);
            var item = ManagementWidgets.Box(content, "Item", Vector2.zero, new Vector2(470, 42), new Color(.18f, .14f, .08f));
            var toggle = item.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>(); toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            var check = ManagementWidgets.Box(item.transform, "Item checkmark", new Vector2(10, 12), new Vector2(18, 18), new Color(.83f, .65f, .26f)); toggle.graphic = check.GetComponent<Image>();
            var text = ManagementWidgets.Label(item.transform, "Item label", new Vector2(40, 5), new Vector2(418, 32), 19); text.alignment = TextAnchor.MiddleLeft;
            dropdown.template = (RectTransform)template.transform; dropdown.itemText = text;
            dropdown.options = new List<Dropdown.OptionData>(); foreach (string option in options) dropdown.options.Add(new Dropdown.OptionData(option));
            dropdown.RefreshShownValue();
            scroll.viewport = viewport; scroll.content = content; scroll.scrollSensitivity = 24;
            template.SetActive(false); dropdown.onValueChanged.AddListener(value => Changed(() => changed(value))); return dropdown;
        }
        private void OnDestroy() { GameSettings.Save(); if (active == this) active = null; }
    }
}

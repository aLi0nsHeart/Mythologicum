using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mythologicum
{
    public static class SimulationSpeed
    {
        public static float SelectedSpeed { get; private set; } = 1f;
        public static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(.1f, Math.Min(6f, value));
        public static void Select(float speed)
        {
            SelectedSpeed = Sanitize(speed);
            if (!GameSaveMenu.IsOpen && !SettingsPresentation.IsOpen) Time.timeScale = SelectedSpeed;
        }
        public static void TogglePause()
        {
            if (GameSaveMenu.IsOpen || SettingsPresentation.IsOpen) return;
            Time.timeScale = Time.timeScale > 0 ? 0 : SelectedSpeed;
        }
        public static void StartPark()
        {
            SelectedSpeed = Sanitize(PlayControlsDefinition.Current.normalSpeed);
            Time.timeScale = SelectedSpeed;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => SelectedSpeed = 1f;
    }

    /// <summary>Compact play controls. Pausing the simulation does not open the game's pause menu.</summary>
    public sealed class SimulationSpeedControls : MonoBehaviour
    {
        private readonly Button[] buttons = new Button[4];
        private readonly Text[] captions = new Text[4];
        private int bindingRevision = -1;
        private float shownScale = float.NaN;
        private Vector3 shownSpeeds;
        private bool shownBlocked;
        public static SimulationSpeedControls Attach(Canvas canvas)
        {
            var rect = new GameObject("Simulation speed controls", typeof(RectTransform));
            rect.transform.SetParent(canvas.transform, false);
            var component = rect.AddComponent<SimulationSpeedControls>(); component.Build(canvas); return component;
        }
        private void Build(Canvas canvas)
        {
            SimulationSpeed.StartPark();
            var settings = PlayControlsDefinition.Current;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = settings.speedAnchor;
            rect.anchoredPosition = settings.speedPosition;
            float width = settings.speedButtonSize.x * 4 + settings.speedButtonGap * 3;
            rect.sizeDelta = new Vector2(width, settings.speedButtonSize.y);
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                buttons[i] = ManagementWidgets.Button(transform, i == 0 ? "Simulation hold" : "Simulation speed " + i,
                    new Vector2(i * (settings.speedButtonSize.x + settings.speedButtonGap), 0), settings.speedButtonSize,
                    () => { if (index == 0) SimulationSpeed.TogglePause(); else SimulationSpeed.Select(Speed(index)); }, 16);
                captions[i] = buttons[i].GetComponentInChildren<Text>();
            }
            UILayoutElement.Attach(rect);
            UIThemeApplicator.ApplyAll(canvas); Refresh();
        }
        private static float Speed(int index) => index == 1 ? PlayControlsDefinition.Current.normalSpeed
            : index == 2 ? PlayControlsDefinition.Current.doubleSpeed : PlayControlsDefinition.Current.tripleSpeed;
        private void Update()
        {
            bool textEntry = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.TryGetComponent<InputField>(out var input) && input.isFocused;
            if (!textEntry && !GameSaveMenu.IsOpen && !SettingsPresentation.IsOpen && !GameInput.BlocksGameplayInput)
            {
                if (GameInput.Pressed(GameplayAction.ToggleSimulationPause)) SimulationSpeed.TogglePause();
                if (GameInput.Pressed(GameplayAction.NormalSpeed)) SimulationSpeed.Select(Speed(1));
                if (GameInput.Pressed(GameplayAction.DoubleSpeed)) SimulationSpeed.Select(Speed(2));
                if (GameInput.Pressed(GameplayAction.TripleSpeed)) SimulationSpeed.Select(Speed(3));
            }
            Refresh();
        }
        private void Refresh()
        {
            bool blocked = GameSaveMenu.IsOpen || SettingsPresentation.IsOpen || GameInput.BlocksGameplayInput;
            bool bindingsChanged = bindingRevision != GameInput.Revision;
            var speeds = new Vector3(Speed(1), Speed(2), Speed(3));
            if (!bindingsChanged && shownScale == Time.timeScale && shownBlocked == blocked && shownSpeeds == speeds) return;
            shownScale = Time.timeScale; shownBlocked = blocked; shownSpeeds = speeds;
            bindingRevision = GameInput.Revision;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].interactable = !blocked;
                captions[i].text = i == 0 ? (Time.timeScale <= 0 ? ">" : "II")
                    : (Time.timeScale > 0 && Mathf.Approximately(Time.timeScale, Speed(i)) ? "> " : "") + Speed(i).ToString("0.#") + "x";
                if (bindingsChanged) UIHoverTooltip.Attach(buttons[i], i == 0
                    ? "Pause or resume simulation (" + GameInput.KeyCaption(GameplayAction.ToggleSimulationPause) + ")"
                    : "Simulation speed " + Speed(i).ToString("0.#") + "x (" + GameInput.KeyCaption(i == 1
                        ? GameplayAction.NormalSpeed : i == 2 ? GameplayAction.DoubleSpeed : GameplayAction.TripleSpeed) + ")");
            }
        }
    }
}

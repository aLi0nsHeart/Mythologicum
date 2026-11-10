using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Player preferences, shared by the menu and every gameplay camera.</summary>
    public static class GameSettings
    {
        private const string Prefix = "Mythologicum.Settings.";
        private static bool loaded, dirty;
        private static float masterVolume = 1f, moveSensitivity = 1f, rotationSensitivity = 1f, zoomSensitivity = 1f, uiScale = 1f;
        private static int windowMode, quality;
        public static float MasterVolume { get { EnsureLoaded(); return masterVolume; } }
        public static float MoveSensitivity { get { EnsureLoaded(); return moveSensitivity; } }
        public static float RotationSensitivity { get { EnsureLoaded(); return rotationSensitivity; } }
        public static float ZoomSensitivity { get { EnsureLoaded(); return zoomSensitivity; } }
        public static float UIScale { get { EnsureLoaded(); return uiScale; } }
        /// <summary>0 windowed, 1 borderless fullscreen, 2 exclusive fullscreen.</summary>
        public static int WindowMode { get { EnsureLoaded(); return windowMode; } }
        public static int Quality { get { EnsureLoaded(); return quality; } }
        public static bool HasUnsavedChanges => dirty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            loaded = false; dirty = false; EnsureLoaded();
            Application.quitting -= Save; Application.quitting += Save;
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            masterVolume = SanitizeVolume(PlayerPrefs.GetFloat(Prefix + "Volume", 1f));
            moveSensitivity = SanitizeSensitivity(PlayerPrefs.GetFloat(Prefix + "CameraMove", 1f));
            rotationSensitivity = SanitizeSensitivity(PlayerPrefs.GetFloat(Prefix + "CameraRotation", 1f));
            zoomSensitivity = SanitizeSensitivity(PlayerPrefs.GetFloat(Prefix + "CameraZoom", 1f));
            uiScale = SanitizeUIScale(PlayerPrefs.GetFloat(Prefix + "UIScale", 1f));
            int currentMode = Screen.fullScreenMode == FullScreenMode.Windowed ? 0
                : Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen ? 2 : 1;
            windowMode = Math.Max(0, Math.Min(2, PlayerPrefs.GetInt(Prefix + "WindowMode", currentMode)));
            quality = Math.Max(0, Math.Min(Math.Max(0, QualitySettings.names.Length - 1),
                PlayerPrefs.GetInt(Prefix + "Quality", QualitySettings.GetQualityLevel())));
            AudioListener.volume = masterVolume;
            ApplyDisplay();
            if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality, true);
        }

        public static float SanitizeVolume(float value) => Finite(value) ? Math.Max(0f, Math.Min(1f, value)) : 1f;
        public static float SanitizeSensitivity(float value) => Finite(value) ? Math.Max(.25f, Math.Min(3f, value)) : 1f;
        public static float SanitizeUIScale(float value) => SanitizeUIScale(value, PlayControlsDefinition.Current.minimumUIScale,
            PlayControlsDefinition.Current.maximumUIScale);
        public static float SanitizeUIScale(float value, float minimum, float maximum)
            => Finite(value) ? Math.Max(minimum, Math.Min(maximum, value)) : 1f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static void SetMasterVolume(float value)
        { EnsureLoaded(); masterVolume = SanitizeVolume(value); AudioListener.volume = masterVolume; Record(); }
        public static void SetMoveSensitivity(float value)
        { EnsureLoaded(); moveSensitivity = SanitizeSensitivity(value); Record(); }
        public static void SetRotationSensitivity(float value)
        { EnsureLoaded(); rotationSensitivity = SanitizeSensitivity(value); Record(); }
        public static void SetZoomSensitivity(float value)
        { EnsureLoaded(); zoomSensitivity = SanitizeSensitivity(value); Record(); }
        public static void SetUIScale(float value)
        { EnsureLoaded(); uiScale = SanitizeUIScale(value); Record(); PlayerUIScale.RefreshAll(); }
        public static void SetWindowMode(int value)
        { EnsureLoaded(); windowMode = Math.Max(0, Math.Min(2, value)); ApplyDisplay(); Record(); }
        public static void SetQuality(int value)
        {
            EnsureLoaded(); quality = Math.Max(0, Math.Min(Math.Max(0, QualitySettings.names.Length - 1), value));
            QualitySettings.SetQualityLevel(quality, true); Record();
        }

        private static void ApplyDisplay()
        {
            // The Editor owns its windows; the selected mode is applied to standalone builds.
            if (Application.isEditor) return;
            Screen.fullScreenMode = windowMode == 0 ? FullScreenMode.Windowed
                : windowMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
        }

        public static void ResetDefaults()
        {
            EnsureLoaded(); masterVolume = moveSensitivity = rotationSensitivity = zoomSensitivity = 1f;
            uiScale = 1f; GameInput.ResetDefaults(); PlayerUIScale.RefreshAll();
            quality = Math.Max(0, QualitySettings.names.Length - 1); windowMode = 1;
            AudioListener.volume = masterVolume; ApplyDisplay(); QualitySettings.SetQualityLevel(quality, true);
            Record(); Save();
        }
        private static void Record()
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", masterVolume);
            PlayerPrefs.SetFloat(Prefix + "CameraMove", moveSensitivity);
            PlayerPrefs.SetFloat(Prefix + "CameraRotation", rotationSensitivity);
            PlayerPrefs.SetFloat(Prefix + "CameraZoom", zoomSensitivity);
            PlayerPrefs.SetFloat(Prefix + "UIScale", uiScale);
            PlayerPrefs.SetInt(Prefix + "WindowMode", windowMode); PlayerPrefs.SetInt(Prefix + "Quality", quality);
            dirty = true;
        }
        public static void Save() { if (!loaded || !dirty) return; PlayerPrefs.Save(); dirty = false; }
    }
}

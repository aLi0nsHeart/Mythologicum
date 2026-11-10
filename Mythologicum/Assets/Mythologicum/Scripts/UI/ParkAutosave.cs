using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed class ParkAutosave : MonoBehaviour
    {
        private BuildModeController park;
        private readonly AutosaveSchedule schedule = new AutosaveSchedule();
        private Text status;
        private float hideAt;
        private string lastMessageId;
        public string LastMessage => string.IsNullOrEmpty(lastMessageId) ? "" : Loc.Text(lastMessageId);
        public static ParkAutosave Attach(BuildModeController controller)
        {
            var instance = controller.GetComponent<ParkAutosave>() ?? controller.gameObject.AddComponent<ParkAutosave>();
            instance.park = controller;
            return instance;
        }
        public void ResetSchedule() => schedule.Restart();
        private void Update()
        {
            if (status != null && Time.unscaledTime >= hideAt) status.gameObject.SetActive(false);
            SaveSettingsDefinition settings = SaveSettingsDefinition.Current;
            bool active = park != null && park.enabled && park.Terrain != null && park.Progression != null
                && Time.timeScale > 0 && !SceneTransition.IsLoading && park.IsReady && !GameSaveMenu.IsOpen && !SaveBrowserPresentation.IsOpen;
            if (!settings.autosaveEnabled || !schedule.Advance(Time.unscaledDeltaTime, active,
                settings.autosaveIntervalSeconds, settings.failureRetrySeconds)) return;
            TrySave();
        }
        public bool TrySave()
        {
            if (park == null || park.Terrain == null || park.Progression == null) return false;
            bool successful = false;
            try
            {
                ParkSaveData data = park.CapturePark();
                park.ValidateSave(data);
                ParkSaveStore.WriteAutosave(data, SaveSettingsDefinition.Current.autosaveSlots);
                lastMessageId = "ui.autosave.success";
                successful = true;
            }
            catch (Exception error)
            {
                lastMessageId = "ui.autosave.failed";
                Debug.LogWarning("Autosave failed: " + error.Message);
            }
            schedule.Completed(successful);
            ShowStatus();
            return successful;
        }
        private void ShowStatus()
        {
            if (status == null)
            {
                Canvas canvas = SceneTransition.GameplayCanvas;
                if (canvas == null) return;
                status = ManagementWidgets.Label(canvas.transform, "ui.park_autosave.autosave_status", Vector2.zero, new Vector2(370, 30), 16);
                status.alignment = TextAnchor.MiddleRight;
                var rect = status.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-18, 12);
                UIThemeElement.Mark(status.gameObject, UIThemeRole.Text).TextCategory = UITextCategory.Small;
            }
            Loc.Set(status, lastMessageId);
            hideAt = Time.unscaledTime + SaveSettingsDefinition.Current.statusDisplaySeconds;
            status.gameObject.SetActive(true);
        }
    }
}

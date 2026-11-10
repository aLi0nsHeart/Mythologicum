using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Named manual slots and rotating autosaves share one themed, scrollable browser.</summary>
    public sealed class SaveBrowserPresentation : MonoBehaviour
    {
        private static SaveBrowserPresentation active;
        public static bool IsOpen => active != null && active.gameObject.activeSelf;
        public static Transform ActiveRoot => IsOpen ? active.transform : null;
        private Action<SaveSlotInfo, bool> load;
        private Action<string, string> save;
        private Action closed;
        private RectTransform content;
        private Text feedback;
        private InputField nameInput;
        private bool saving;
        public static void Open(Canvas canvas, bool saving, Action<SaveSlotInfo, bool> load,
            Action<string, string> save, Action closed = null)
        {
            CloseActive();
            var root = ManagementWidgets.Modal(canvas, "Save browser");
            active = root.AddComponent<SaveBrowserPresentation>();
            active.saving = saving; active.load = load; active.save = save; active.closed = closed;
            active.Build(canvas); root.SetActive(true); root.transform.SetAsLastSibling();
            UIThemeApplicator.ApplyAll(canvas);
        }
        public static void CloseActive() { if (active != null) active.Close(); }
        public static void ShowFeedback(string id, params object[] arguments) { if (active != null) { Loc.Set(active.feedback, id, arguments); active.Refresh(); } }
        public static bool EditingName => active != null && active.nameInput != null && active.nameInput.isFocused;
        private void Build(Canvas canvas)
        {
            var card = ManagementWidgets.Card(gameObject, canvas);
            ManagementWidgets.Label(card, saving ? "ui.save_browser.save_game" : "ui.save_browser.load_game", new Vector2(20, 8), new Vector2(850, 40), 28);
            var closeButton = ManagementWidgets.Button(card, "ui.save_browser.close_save_browser", new Vector2(1020, 4), new Vector2(120, 38), Close, 17);
            Loc.Set(closeButton.GetComponentInChildren<Text>(), "ui.common.close");
            feedback = ManagementWidgets.Label(card, "ui.save_browser.save_browser_feedback", new Vector2(20, 4), new Vector2(1100, 46), 17);
            feedback.supportRichText = false;
            Loc.Set(feedback, saving ? "ui.save_browser.save_instructions" : "ui.save_browser.load_instructions");
            content = ManagementWidgets.Scroll(card, "Saved parks", new Vector2(20, 58), new Vector2(1120, saving ? 328 : 398), 0);
            if (saving)
            {
                ManagementWidgets.Label(card, "ui.save_browser.park_save_name", new Vector2(20, 404), new Vector2(190, 34), 18);
                var box = ManagementWidgets.Box(card, "Save name", new Vector2(212, 400), new Vector2(610, 42));
                nameInput = box.AddComponent<InputField>(); nameInput.targetGraphic = box.GetComponent<Image>();
                nameInput.contentType = InputField.ContentType.Standard; nameInput.characterLimit = 64;
                var label = ManagementWidgets.Label(box.transform, "ui.save_browser.entered_save_name", new Vector2(10, 5), new Vector2(590, 32), 19);
                label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
                nameInput.textComponent = label;
                nameInput.text = ParkSaveStore.ActiveParkName;
                ManagementWidgets.Button(card, "ui.save_browser.create_new_save", new Vector2(842, 400), new Vector2(298, 42),
                    () => save?.Invoke(ParkSaveStore.NewManualSlotId(), nameInput.text), 18);
            }
            var settings = SaveSettingsDefinition.Current;
            var autosaveInfo = ManagementWidgets.Label(card, "ui.save_browser.autosave_information", new Vector2(20, 464), new Vector2(1110, 30), 16);
            if (settings.autosaveEnabled) Loc.Set(autosaveInfo, "ui.save_browser.autosave_enabled", settings.autosaveIntervalSeconds / 60f, settings.autosaveSlots);
            else Loc.Set(autosaveInfo, "ui.save_browser.autosave_disabled");
            Refresh();
        }
        private void OnEnable() => Loc.Changed += Refresh;
        private void OnDisable() => Loc.Changed -= Refresh;
        private void Refresh()
        {
            if (content == null) return;
            ManagementWidgets.Clear(content);
            float y = 0;
            IReadOnlyList<SaveSlotInfo> slots;
            try { slots = ParkSaveStore.ListSlots(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Loc.Set(feedback, "ui.save_browser.folder_unreadable");
                slots = Array.Empty<SaveSlotInfo>();
            }
            foreach (SaveSlotInfo slot in slots)
            {
                if (saving && slot.IsAutosave) continue;
                var row = ManagementWidgets.Box(content, "Save slot " + slot.Id, new Vector2(0, y), new Vector2(1100, 88));
                string kind = slot.IsAutosave ? Loc.Text("ui.save_browser.autosave_kind") : slot.IsLegacy ? Loc.Text("ui.save_browser.legacy_kind") : Loc.Text("ui.save_browser.manual_kind");
                var title = ManagementWidgets.Label(row.transform, "ui.save_browser.save_title", new Vector2(12, 6), new Vector2(700, 29), 21);
                title.supportRichText = false; Loc.Set(title, "ui.save_browser.slot_title", slot.Name, kind);
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 13; title.resizeTextMaxSize = 21;
                var details = ManagementWidgets.Label(row.transform, "ui.save_browser.save_details", new Vector2(12, 39), new Vector2(700, 44), 16);
                details.supportRichText = false;
                if (slot.IsValid) Loc.Set(details, "ui.save_browser.slot_details", slot.SavedUtc.ToLocalTime(), slot.Width, slot.Depth, slot.Funds);
                else Loc.Set(details, slot.BackupIsValid ? "ui.save_browser.damaged_backup_available" : "ui.save_browser.damaged_other_saves_available");
                var button = ManagementWidgets.Button(row.transform, (saving ? "Overwrite " : "Load ") + slot.Id,
                    new Vector2(910, 23), new Vector2(174, 42), () =>
                    {
                        if (saving) save?.Invoke(slot.Id, nameInput.text);
                        else load?.Invoke(slot, false);
                    }, 18);
                Loc.Set(button.GetComponentInChildren<Text>(), saving ? "ui.save_browser.overwrite" : "ui.save_browser.load");
                button.interactable = saving || slot.IsValid;
                if (!saving && slot.BackupIsValid)
                {
                    var backupButton = ManagementWidgets.Button(row.transform, "Load backup " + slot.Id, new Vector2(720, 23), new Vector2(176, 42),
                        () => load?.Invoke(slot, true), 17);
                    Loc.Set(backupButton.GetComponentInChildren<Text>(), "ui.save_browser.load_backup");
                }
                y += 98;
            }
            if (y == 0)
            {
                ManagementWidgets.Label(content, saving ? "ui.save_browser.no_manual_saves" : "ui.save_browser.no_saves",
                    new Vector2(12, 12), new Vector2(1070, 55));
                y = 80;
            }
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
            UIOverflowScroll.Attach(content.GetComponentInParent<ScrollRect>(true)).Refresh();
        }
        private void Close()
        {
            gameObject.SetActive(false);
            if (active == this) active = null;
            closed?.Invoke();
            Destroy(gameObject);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => active = null;
    }
}

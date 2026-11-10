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
        public static void ShowFeedback(string value) { if (active != null) { active.feedback.text = value; active.Refresh(); } }
        public static bool EditingName => active != null && active.nameInput != null && active.nameInput.isFocused;
        private void Build(Canvas canvas)
        {
            var card = ManagementWidgets.Card(gameObject, canvas);
            ManagementWidgets.Label(card, saving ? "Save game" : "Load game", new Vector2(20, 8), new Vector2(850, 40), 28);
            ManagementWidgets.Button(card, "Close save browser", new Vector2(1020, 4), new Vector2(120, 38), Close, 17)
                .GetComponentInChildren<Text>().text = "Close";
            feedback = ManagementWidgets.Label(card, "Save browser feedback", new Vector2(20, 4), new Vector2(1100, 46), 17);
            feedback.supportRichText = false;
            feedback.text = saving ? "Create a named save, or overwrite one of your manual saves below. Autosaves use separate slots."
                : "Choose a park to load. Autosaves and your previous park save appear here too.";
            content = ManagementWidgets.Scroll(card, "Saved parks", new Vector2(20, 58), new Vector2(1120, saving ? 328 : 398), 0);
            if (saving)
            {
                ManagementWidgets.Label(card, "Park / save name", new Vector2(20, 404), new Vector2(190, 34), 18);
                var box = ManagementWidgets.Box(card, "Save name", new Vector2(212, 400), new Vector2(610, 42));
                nameInput = box.AddComponent<InputField>(); nameInput.targetGraphic = box.GetComponent<Image>();
                nameInput.contentType = InputField.ContentType.Standard; nameInput.characterLimit = 64;
                var label = ManagementWidgets.Label(box.transform, "Entered save name", new Vector2(10, 5), new Vector2(590, 32), 19);
                label.alignment = TextAnchor.MiddleLeft; label.supportRichText = false;
                nameInput.textComponent = label;
                nameInput.text = ParkSaveStore.ActiveParkName;
                ManagementWidgets.Button(card, "Create new save", new Vector2(842, 400), new Vector2(298, 42),
                    () => save?.Invoke(ParkSaveStore.NewManualSlotId(), nameInput.text), 18);
            }
            var settings = SaveSettingsDefinition.Current;
            ManagementWidgets.Label(card, "Autosave information", new Vector2(20, 464), new Vector2(1110, 30), 16).text
                = (settings.autosaveEnabled ? $"Autosaves: every {settings.autosaveIntervalSeconds / 60f:0.#} minutes of active play, {settings.autosaveSlots} rotating slots. "
                    : "Autosaves are disabled. ") + "Manual saves require overwrite confirmation.";
            Refresh();
        }
        private void Refresh()
        {
            ManagementWidgets.Clear(content);
            float y = 0;
            IReadOnlyList<SaveSlotInfo> slots;
            try { slots = ParkSaveStore.ListSlots(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                feedback.text = "The save folder could not be read. Check that the game can access its save location.";
                slots = Array.Empty<SaveSlotInfo>();
            }
            foreach (SaveSlotInfo slot in slots)
            {
                if (saving && slot.IsAutosave) continue;
                var row = ManagementWidgets.Box(content, "Save slot " + slot.Id, new Vector2(0, y), new Vector2(1100, 88));
                string kind = slot.IsAutosave ? "Autosave" : slot.IsLegacy ? "Previous save" : "Manual save";
                var title = ManagementWidgets.Label(row.transform, "Save title", new Vector2(12, 6), new Vector2(700, 29), 21);
                title.supportRichText = false; title.text = slot.Name + "  ·  " + kind;
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 13; title.resizeTextMaxSize = 21;
                var details = ManagementWidgets.Label(row.transform, "Save details", new Vector2(12, 39), new Vector2(700, 44), 16);
                details.supportRichText = false;
                details.text = slot.IsValid ? slot.SavedUtc.ToLocalTime().ToString("g") + $"  |  {slot.Width} × {slot.Depth} tiles  |  Funds {slot.Funds:0.00}"
                    : "Save file unavailable or damaged." + (slot.BackupIsValid ? " A readable backup is available." : " Other saves remain available.");
                var button = ManagementWidgets.Button(row.transform, (saving ? "Overwrite " : "Load ") + slot.Id,
                    new Vector2(910, 23), new Vector2(174, 42), () =>
                    {
                        if (saving) save?.Invoke(slot.Id, nameInput.text);
                        else load?.Invoke(slot, false);
                    }, 18);
                button.GetComponentInChildren<Text>().text = saving ? "Overwrite" : "Load";
                button.interactable = saving || slot.IsValid;
                if (!saving && slot.BackupIsValid)
                    ManagementWidgets.Button(row.transform, "Load backup " + slot.Id, new Vector2(720, 23), new Vector2(176, 42),
                        () => load?.Invoke(slot, true), 17).GetComponentInChildren<Text>().text = "Load backup";
                y += 98;
            }
            if (y == 0)
            {
                ManagementWidgets.Label(content, saving ? "No manual saves yet. Enter a name below to create one." : "No saved parks found.",
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

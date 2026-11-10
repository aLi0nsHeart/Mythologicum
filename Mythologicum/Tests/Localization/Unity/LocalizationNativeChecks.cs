using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mythologicum;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Run from the menu in the disposable native validation project only.</summary>
public static class LocalizationNativeChecks
{
    public static int Run(Canvas canvas)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("Localization: " + message);
            Debug.Log("LOCALIZATION PASS: " + message);
        }
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "SteampunkUIValidation",
            "Native language checks use the isolated validation application identity");
        Check(canvas != null && !SettingsPresentation.IsOpen && !SaveBrowserPresentation.IsOpen,
            "The menu is ready for a separate language-settings session");

        var source = Resources.Load<TextAsset>("Localization/localization");
        Check(source != null, "The shipped CSV imports as a Unity TextAsset");
        string sourcePath = Path.GetFullPath(AssetDatabase.GetAssetPath(source));
        byte[] originalHash = Hash(sourcePath);
        var originalLanguage = Loc.Language;
        var originalScene = SceneManager.GetActiveScene();
        float originalTimeScale = Time.timeScale;
        const string preference = "Mythologicum.Settings.Language";
        bool hadPreference = PlayerPrefs.HasKey(preference);
        string originalPreference = PlayerPrefs.GetString(preference, "");
        var entries = (Dictionary<string, string[]>)typeof(LocalizationTable)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Loc.Table);
        var originalCells = new Dictionary<(string, GameLanguage), string>();
        void SetCell(string id, GameLanguage language, string value)
        {
            var key = (id, language);
            if (!originalCells.ContainsKey(key)) originalCells.Add(key, entries[id][(int)language]);
            entries[id][(int)language] = value;
        }
        SettingsPresentation settings = null;
        try
        {
            Loc.SetLanguage(GameLanguage.ENGLISH);
            var menu = canvas.GetComponentInChildren<MainMenuPresentation>(true);
            Check(menu != null, "Language switching is tested against the real main menu");
            var newGame = menu.MenuButtons.Single(button => button.name == Loc.InternalName("ui.menu.new_game"));
            var newGameCaption = newGame.GetComponentInChildren<Text>(true);
            string internalButtonName = newGame.name;
            Check(newGameCaption.GetComponent<LocalizedText>()?.ID == "ui.menu.new_game",
                "The main-menu caption stores a localization ID");
            Check(newGameCaption.text == Loc.English("ui.menu.new_game"), "English menu text comes from the CSV");

            SetCell("ui.settings.title", GameLanguage.GERMAN, "Einstellungen");
            SetCell("ui.menu.new_game", GameLanguage.GERMAN, "Neues Spiel");
            // A player may choose exactly the text used as the field's construction placeholder.
            SetCell("ui.save_browser.entered_save_name", GameLanguage.GERMAN, "Test-Speichername");
            SetCell("ui.settings.title", GameLanguage.JAPANESE, "");
            SetCell("ui.menu.new_game", GameLanguage.JAPANESE, "");
            SetCell("park.notification.creature_escaped", GameLanguage.GERMAN, "{0} ist entkommen");
            SetCell("creature.name.chupacabra", GameLanguage.GERMAN, "Chupa-Test");
            var notices = new NotificationInbox();
            notices.PostLocalized("localization-fixture", ParkNoticeKind.CreatureEscaped,
                "park.notification.creature_escaped", "park.notification.creature_escaped_body",
                titleArguments: new[] { "creature.name.chupacabra" });
            var restoredNotice = JsonUtility.FromJson<ParkNotice>(JsonUtility.ToJson(notices.Entries.Single()));
            Check(restoredNotice.titleId == "park.notification.creature_escaped"
                && restoredNotice.titleArguments.Single() == "creature.name.chupacabra", "Saved letters preserve message and argument identities");

            settings = SettingsPresentation.Open(canvas);
            var dropdown = settings.GetComponentsInChildren<Dropdown>(true).Single(item => item.name == "Language");
            var heading = settings.GetComponentsInChildren<LocalizedText>(true).Single(item => item.ID == "ui.settings.title");
            var headingText = heading.GetComponent<Text>();
            string internalHeadingName = heading.name;
            Check(dropdown.options.Count == LocalizationTable.Columns.Length - 1 && dropdown.options.Count == 12,
                "Settings expose all twelve requested languages");
            Check(dropdown.value == (int)GameLanguage.ENGLISH, "The language selector reflects the active language");

            string submittedName = null;
            SaveBrowserPresentation.Open(canvas, true, null, (id, name) => submittedName = name);
            var browser = SaveBrowserPresentation.ActiveRoot.gameObject;
            var input = browser.GetComponentsInChildren<InputField>(true).Single(item => item.name == "Save name");
            string playerName = Loc.English("ui.save_browser.entered_save_name");
            input.text = playerName;
            Check(input.textComponent.text == playerName, "The real save-name field displays player-entered text");

            dropdown.value = (int)GameLanguage.GERMAN;
            Check(Loc.Language == GameLanguage.GERMAN && GameSettings.Language == GameLanguage.GERMAN,
                "Changing the real dropdown changes the shared player language");
            Check(PlayerPrefs.GetString(preference) == "GERMAN", "The chosen language is persisted in player preferences");
            Check(headingText.text == "Einstellungen", "The already-open Settings title changes language immediately");
            Check(newGameCaption.text == "Neues Spiel", "An existing main-menu caption changes without rebuilding its scene");
            Check(restoredNotice.LocalizedTitle == "Chupa-Test ist entkommen", "A saved and restored letter resolves its current language and species name");
            Check(new ParkNotice { title = "Earlier letter", body = "Original wording" }.LocalizedBody == "Original wording",
                "Legacy letters without IDs retain their saved wording");
            Check(newGame.name == internalButtonName && heading.name == internalHeadingName,
                "Localized helpers preserve English scene-object names");
            Check(input.text == playerName && input.textComponent.text == playerName,
                "A language change preserves a player name matching the English construction placeholder");

            browser.SetActive(false); browser.SetActive(true);
            Check(input.text == playerName && input.textComponent.text == playerName,
                "Reactivating the save browser preserves its player-entered name");
            input.text = "ui.menu.new_game";
            dropdown.value = (int)GameLanguage.JAPANESE;
            Check(Loc.Language == GameLanguage.JAPANESE && dropdown.value == (int)GameLanguage.JAPANESE,
                "An untranslated language remains selectable");
            Check(newGameCaption.text == Loc.English("ui.menu.new_game") && headingText.text == Loc.English("ui.settings.title"),
                "Blank Japanese cells fall back to the English source in open windows");
            Check(input.text == "ui.menu.new_game" && input.textComponent.text == "ui.menu.new_game",
                "A player name that resembles an ID is kept as literal text");
            browser.GetComponentsInChildren<Button>(true).Single(button => button.name == Loc.InternalName("ui.save_browser.create_new_save"))
                .onClick.Invoke();
            Check(submittedName == "ui.menu.new_game", "The save action receives the original player-entered name");
            Check(source.text.Contains("m\u00b2") && Loc.English("creature.info.space_herd_escape").Contains("m\u00b2"),
                "UTF-8 source characters survive both Unity import and CSV lookup");
            Check(SceneManager.GetActiveScene() == originalScene && Mathf.Approximately(Time.timeScale, originalTimeScale),
                "Language settings do not replace the scene or alter simulation time");

            SaveBrowserPresentation.CloseActive();
            settings.Close();
            SetCell("ui.settings.title", GameLanguage.ENGLISH, "Options title fixture");
            Loc.SetLanguage(GameLanguage.ENGLISH);
            settings = SettingsPresentation.Open(canvas);
            var revisedHeading = settings.GetComponentsInChildren<LocalizedText>(true).Single(item => item.ID == "ui.settings.title");
            Check(revisedHeading.GetComponent<Text>().text == "Options title fixture",
                "New windows use revised English CSV text");
            Check(revisedHeading.name == internalHeadingName,
                "Editing the English caption does not change a newly created object's stable hierarchy name");
        }
        finally
        {
            SaveBrowserPresentation.CloseActive();
            if (settings != null) settings.Close();
            foreach (var pair in originalCells) entries[pair.Key.Item1][(int)pair.Key.Item2] = pair.Value;
            Loc.SetLanguage(originalLanguage);
            if (hadPreference) PlayerPrefs.SetString(preference, originalPreference); else PlayerPrefs.DeleteKey(preference);
            PlayerPrefs.Save();
        }
        Check(Hash(sourcePath).SequenceEqual(originalHash), "Native language checks leave the source CSV unchanged");
        Check(Loc.Language == originalLanguage && !SettingsPresentation.IsOpen && !SaveBrowserPresentation.IsOpen,
            "Temporary translations, language selection and windows are restored after the check");
        return checks;
    }
    private static byte[] Hash(string path)
    {
        using (var sha = SHA256.Create()) return sha.ComputeHash(File.ReadAllBytes(path));
    }
}

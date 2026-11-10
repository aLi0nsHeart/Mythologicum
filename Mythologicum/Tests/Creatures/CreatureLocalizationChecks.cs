using System;
using System.Reflection;
using Mythologicum;

public static class CreatureLocalizationChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var tableField = typeof(Loc).GetField("table", BindingFlags.Static | BindingFlags.NonPublic);
        var languageField = typeof(Loc).GetField("language", BindingFlags.Static | BindingFlags.NonPublic);
        var headlessField = typeof(Loc).GetField("headless", BindingFlags.Static | BindingFlags.NonPublic);
        object originalTable = tableField.GetValue(null), originalLanguage = languageField.GetValue(null), originalHeadless = headlessField.GetValue(null);
        string Row(string id, string english, string german)
            => id + "," + english + "," + german + new string(',', LocalizationTable.Columns.Length - 3);
        try
        {
            string fixture = string.Join(",", LocalizationTable.Columns) + "\n"
                + Row("test.name", "Goat", "Ziege") + "\n"
                + Row("test.status", "Studying {0}", "Beobachtet {0}") + "\n"
                + Row("test.progress", "Knowledge: {0:0.0}%", "Wissen: {0:0.0}%") + "\n";
            tableField.SetValue(null, LocalizationTable.Parse(fixture));
            headlessField.SetValue(null, true); languageField.SetValue(null, GameLanguage.ENGLISH);
            var status = new StatusMessage("test.status"); status.Set("test.status", "test.name");
            Check(status.Value == "Studying Goat", "Status resolves its message ID and nested species display ID.");
            Loc.SetLanguage(GameLanguage.GERMAN);
            Check(status.Value == "Beobachtet Ziege", "An ongoing status changes language without reassigning its task.");
            Loc.SetLanguage(GameLanguage.JAPANESE);
            Check(status.Value == "Studying Goat", "Untranslated status and species text both fall back to English.");
            status.Set("test.progress", 12.5f);
            Check(status.Value == "Knowledge: 12.5%", "Formatted status keeps a typed numeric argument.");
            Loc.SetLanguage(GameLanguage.GERMAN);
            Check(status.Value == "Wissen: 12,5%", "Status numeric formatting follows the selected language.");
            status.Set("Legacy status from an earlier park");
            Check(status.Value == "Legacy status from an earlier park", "Literal statuses from older data remain readable.");
            status.Set("");
            Check(status.Value == "", "An empty status remains empty.");
            status.Set(null);
            Check(status.Value == "", "A missing status remains safe to display.");
        }
        finally
        {
            tableField.SetValue(null, originalTable); languageField.SetValue(null, originalLanguage); headlessField.SetValue(null, originalHeadless);
        }
        Console.WriteLine("PASS: " + checks + " live creature/staff status localization checks.");
    }
}

using System;
using System.IO;
using System.Linq;
using Mythologicum;

public static class LocalizationChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (FormatException) { rejected = true; } Check(rejected, message); }
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Row(string id, string english, string german = "") => string.Join(",", new[] { id, english, german }
        .Concat(Enumerable.Repeat("", LocalizationTable.Columns.Length - 3)).Select(Quote));
    public static void Main()
    {
        string header = string.Join(",", LocalizationTable.Columns) + "\r\n";
        string fixture = "\ufeff" + header + Row("test.one", "Hello, \"visitor\"!\nSecond line.", "Hallo, Besucher!") + "\r\n"
            + Row("test.number", "Funds: {0:0.00}", "Geld: {0:0.00}") + "\r\n";
        var table = LocalizationTable.Parse(fixture);
        Check(table.Count == 2, "BOM, quoted comma, escaped quote and multiline records parse as two entries.");
        Check(table.Get("test.one", GameLanguage.ENGLISH) == "Hello, \"visitor\"!\nSecond line.", "Quoted text and line breaks round-trip.");
        Check(table.Get("test.one", GameLanguage.GERMAN) == "Hallo, Besucher!", "A provided translation is selected.");
        foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            Check(!string.IsNullOrEmpty(table.Get("test.one", language)), "Every missing language falls back to English: " + language);
        Check(table.Format("test.number", GameLanguage.ENGLISH, 12.5f) == "Funds: 12.50", "English numeric formatting.");
        Check(table.Format("test.number", GameLanguage.GERMAN, 12.5f) == "Geld: 12,50", "German numeric formatting.");
        Check(table.Get("missing.id", GameLanguage.ENGLISH) == "missing.id", "A missing ID remains identifiable.");
        Check(table.Get(null, GameLanguage.ENGLISH) == "", "Null text is safe.");
        Check(LocalizationTable.ArgumentIndices("{{text}} {1:0.0} {0} {1}").SetEquals(new[] { 0, 1 }), "Escaped braces and repeated/reordered arguments work.");
        Check(LocalizationTable.Parse(header + Row("test.order", "{0} then {1}", "{1} vor {0}")).Count == 1, "Translators can reorder arguments.");
        Check(LocalizationTable.Parse(header.Replace(',', '\t') + "test.tab\tEnglish" + new string('\t', 11)).Count == 1, "Tab-delimited spreadsheet exports retain the requested structure.");
        Reject(() => LocalizationTable.Parse(header + Row("same", "One") + "\n" + Row("same", "Two")), "Duplicate IDs are rejected.");
        Reject(() => LocalizationTable.Parse(header + Row("test.empty", "")), "English source is required.");
        Reject(() => LocalizationTable.Parse(header + Row("test.tokens", "{0}", "{1}")), "Changed translation argument numbers are rejected.");
        Reject(() => LocalizationTable.Parse(header + Row("test.tokens", "{0}", "Ohne Parameter")), "Removed translation arguments are rejected.");
        Reject(() => LocalizationTable.Parse(header + "test.bad,unquoted,comma"), "Wrong column counts are rejected.");
        Reject(() => LocalizationTable.Parse(header + "\"unclosed"), "Unclosed quoted fields are rejected.");
        Reject(() => LocalizationTable.Parse("ID,ENGLISH\nkey,text"), "Incorrect headers are rejected.");
        Reject(() => LocalizationTable.Parse(header + Row("test.named", "{visitor}")), "Named tokens cannot accidentally enter numbered format strings.");
        Reject(() => LocalizationTable.Parse(header + Row("test.brace", "text }")), "Unescaped literal braces are rejected.");
        var unicode = LocalizationTable.Parse(header + Row("test.unicode", "Goat", "Ziege: ä ö ü ß 日本語 中文 한국어"));
        Check(unicode.Get("test.unicode", GameLanguage.GERMAN).Contains("日本語 中文 한국어"), "Unicode data is preserved.");
        var actual = LocalizationTable.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MYTHOLOGICUM_LOCALIZATION_CSV")));
        Check(actual.Count > 500, "The shipped catalogue contains the migrated game text.");
        Check(actual.Get("ui.settings.title", GameLanguage.ENGLISH) == "Settings", "Shipped English source remains readable.");
        Check(actual.Get("ui.settings.title", GameLanguage.CHINESE_SIMPLE) == "Settings", "Shipped blank translation cells use English.");
        Console.WriteLine("PASS: " + checks + " CSV, translation fallback, formatting and shipped-catalogue checks.");
    }
}

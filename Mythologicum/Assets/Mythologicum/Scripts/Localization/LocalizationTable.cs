using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Mythologicum
{
    public enum GameLanguage { ENGLISH, GERMAN, FRENCH, ITALIAN, SPANISH_SPAIN, PORTUGUESE_BRAZIL, DUTCH, POLISH, RUSSIAN, JAPANESE, KOREAN, CHINESE_SIMPLE }

    /// <summary>Engine-independent UTF-8 CSV catalogue. Empty translations use the English source.</summary>
    public sealed class LocalizationTable
    {
        public static readonly string[] Columns = { "ID", "ENGLISH", "GERMAN", "FRENCH", "ITALIAN", "SPANISH_SPAIN",
            "PORTUGUESE_BRAZIL", "DUTCH", "POLISH", "RUSSIAN", "JAPANESE", "KOREAN", "CHINESE_SIMPLE" };
        private readonly Dictionary<string, string[]> entries = new Dictionary<string, string[]>(StringComparer.Ordinal);
        public IEnumerable<string> IDs => entries.Keys;
        public int Count => entries.Count;
        public bool Contains(string id) => id != null && entries.ContainsKey(id);
        public string Get(string id, GameLanguage language)
        {
            if (id == null) return "";
            if (!entries.TryGetValue(id, out var values)) return id;
            int index = (int)language;
            if (index < 0 || index >= values.Length) index = 0;
            return string.IsNullOrWhiteSpace(values[index]) ? values[0] : values[index];
        }
        public string Raw(string id, GameLanguage language) => entries.TryGetValue(id, out var values) ? values[(int)language] : "";
        public string Format(string id, GameLanguage language, params object[] args)
            => string.Format(Culture(language), Get(id, language), args ?? Array.Empty<object>());
        public static CultureInfo Culture(GameLanguage language)
        {
            string[] cultures = { "en-US", "de-DE", "fr-FR", "it-IT", "es-ES", "pt-BR", "nl-NL", "pl-PL", "ru-RU", "ja-JP", "ko-KR", "zh-CN" };
            int index = (int)language;
            return CultureInfo.GetCultureInfo(cultures[index >= 0 && index < cultures.Length ? index : 0]);
        }
        public static LocalizationTable Parse(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) throw new FormatException("The localization CSV is empty.");
            csv = csv.TrimStart('\ufeff');
            int end = csv.IndexOfAny(new[] { '\r', '\n' });
            char separator = csv.Substring(0, end >= 0 ? end : csv.Length).Contains("\t") ? '\t' : ',';
            var records = ReadRecords(csv, separator);
            if (records.Count == 0 || !records[0].SequenceEqual(Columns))
                throw new FormatException("Localization columns must be: " + string.Join(",", Columns));
            var table = new LocalizationTable();
            for (int row = 1; row < records.Count; row++)
            {
                var fields = records[row];
                if (fields.All(string.IsNullOrWhiteSpace)) continue;
                if (fields.Length != Columns.Length) throw new FormatException($"Localization row {row + 1} has {fields.Length} columns; expected {Columns.Length}.");
                string id = fields[0];
                if (string.IsNullOrWhiteSpace(id) || id.Any(character => !(char.IsLetterOrDigit(character) || character == '.' || character == '_' || character == '-')))
                    throw new FormatException($"Localization row {row + 1} needs a stable ID using letters, digits, dots, underscores or hyphens.");
                if (table.entries.ContainsKey(id)) throw new FormatException("Duplicate localization ID: " + id);
                if (string.IsNullOrWhiteSpace(fields[1])) throw new FormatException("Missing English source for " + id);
                string[] values = fields.Skip(1).ToArray();
                var expected = ArgumentIndices(values[0]);
                for (int language = 1; language < values.Length; language++)
                    if (!string.IsNullOrWhiteSpace(values[language]) && !expected.SetEquals(ArgumentIndices(values[language])))
                        throw new FormatException(id + ": " + Columns[language + 1] + " must retain the English placeholder numbers.");
                table.entries.Add(id, values);
            }
            return table;
        }
        public static HashSet<int> ArgumentIndices(string text)
        {
            var result = new HashSet<int>();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') { i++; continue; }
                    int start = ++i;
                    while (i < text.Length && char.IsDigit(text[i])) i++;
                    if (start == i || !int.TryParse(text.Substring(start, i - start), NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index > 100)
                        throw new FormatException("Use numbered localization placeholders such as {0} or {1:0.0}.");
                    result.Add(index);
                    while (i < text.Length && text[i] != '}')
                    { if (text[i] == '{') throw new FormatException("Nested localization placeholders are unsupported."); i++; }
                    if (i == text.Length) throw new FormatException("Unclosed localization placeholder.");
                }
                else if (text[i] == '}')
                {
                    if (i + 1 < text.Length && text[i + 1] == '}') i++;
                    else throw new FormatException("Escape a literal closing brace as }}.");
                }
            }
            return result;
        }
        private static List<string[]> ReadRecords(string csv, char separator)
        {
            var records = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder();
            bool quoted = false, closed = false;
            for (int i = 0; i < csv.Length; i++)
            {
                char character = csv[i];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                        else { quoted = false; closed = true; }
                    }
                    else if (character == '\r') { if (i + 1 < csv.Length && csv[i + 1] == '\n') i++; field.Append('\n'); }
                    else field.Append(character);
                    continue;
                }
                if (character == separator) { row.Add(field.ToString()); field.Clear(); closed = false; }
                else if (character == '\r' || character == '\n')
                {
                    if (character == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(field.ToString()); records.Add(row.ToArray()); row.Clear(); field.Clear(); closed = false;
                }
                else if (character == '"' && field.Length == 0 && !closed) quoted = true;
                else if (closed || character == '"') throw new FormatException("Unexpected text outside a quoted localization field.");
                else field.Append(character);
            }
            if (quoted) throw new FormatException("Unclosed quoted localization field.");
            if (field.Length > 0 || row.Count > 0 || closed) { row.Add(field.ToString()); records.Add(row.ToArray()); }
            return records;
        }
    }
}

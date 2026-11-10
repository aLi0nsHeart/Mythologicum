# Localization

The game's player-facing text is in:

`Assets/Mythologicum/Resources/Localization/localization.csv`

Only **ENGLISH** is filled in. The other language columns are intentionally empty
for your translators. An empty translation displays the English source.

## Editing the spreadsheet

1. Open the CSV in Excel, LibreOffice or a text editor. When importing, choose
   **UTF-8** and **comma** as the separator. Quoted cells may contain commas and
   several lines. Tab-separated exports are also accepted.
2. Keep this exact header and column order:

   ```text
   ID ENGLISH GERMAN FRENCH ITALIAN SPANISH_SPAIN PORTUGUESE_BRAZIL DUTCH POLISH RUSSIAN JAPANESE KOREAN CHINESE_SIMPLE
   ```

3. Translate the appropriate language cells. Keep the **ID** column unchanged;
   code and creature/building data refer to those identifiers.
4. Save as a UTF-8 CSV in the same location, then return to Unity and let it import.
5. Open **Mythologicum > Localization > CSV and validation** and click
   **Validate and refresh**. You can search by ID or English wording and preview
   another language. Gameplay-data validation also checks localization IDs.
6. In Play mode, choose **Settings > Language**. The language choice is saved on
   this computer and applies to the main menu and park interface.

You may edit English wording too. Internal control names are kept separately, so
changing a caption does not rename a button or invalidate saved UI adjustments.
`object-names.json` is internal compatibility metadata; leave it unchanged when
translating. It is not another translation table.

## Numbers, names and line breaks

Some messages have placeholders, such as:

```text
Current upkeep: {0:0.0} / minute
```

Keep every placeholder number and its braces. Translators may move placeholders
to a different position in the sentence. `{0:0.0}` formats a number with one
decimal place; `{1}` inserts another value. The validator catches missing or
changed placeholder numbers. Use `{{` and `}}` for literal braces.

Preserve meaningful line breaks inside quoted cells. Numbers use the selected
language's formatting. Player-entered park/save names are literal text and are
never translated. Species filenames, research IDs, save-slot IDs and other game
identities also stay unchanged.

New notification letters store message IDs and arguments, so they can be shown
in the current language after loading. Older saves remain readable; letters
saved before localization retain their original wording when they have no ID.

## Fonts

The existing ornamental theme fonts remain the default. Select
`Resources/Localization/DefaultLocalizationFonts.asset` to configure:

- **Languages:** a complete font override for an individual language.
- **Fallback fonts:** bundled fonts used when a theme font lacks a character.

Liberation Sans is already a fallback. The existing font collection does not
cover all Japanese, Korean or Chinese characters. When those translations are
ready, assign suitable licensed fonts in this asset. Validation reports missing
translated characters. Font settings do not require changing every UI panel.

## Adding text later

Give each new message a stable ID and add its English source to the CSV. Reuse a
shared ID only when the meaning and grammatical context match.

For programmers, `Loc.Text(id)` retrieves text, `Loc.Format(id, arguments)` formats
a message, and `Loc.Set(label, id, arguments)` binds a UI label to language changes.
Keep translated wording out of gameplay comparisons and save identities. Existing
definition fields such as `displayName` contain IDs; use their localized display
getters when showing them to the player.

# Steampunk UI integration

The artwork from the SteampunkUI package is integrated into `Assets/Mythologicum/UI`:

- `Art`: all 33 supplied interface sprite sheets, backgrounds, and decorative images.
- `Fonts`: the original Arvo, Bebas Neue, Roboto Slab, and Liberation Sans TrueType fonts.
- `Resources/SteampunkTheme.asset`: the shared visual theme used by the game's UGUI interface.

The publisher's original PDF and the supplied font and environment-map license notices are retained beside this file. The original package was published by Gentleland. Demo scenes, example prefabs and behaviours, TMP-only font assets, marketing overviews, environment maps, and the publisher's Editor utilities are not needed by Mythologicum's interface.

The original `.meta` GUIDs, atlas sprite rectangles, sprite IDs, and subasset IDs are preserved. A few selected sprite borders have been set for nine-slice resizing: the main window, dark inset panel, slider track, and bronze slider fill. All other imported artwork keeps its original slicing.

The active palette combines cream text, brass headings, copper controls, and warm leather panels. The main window uses dark chestnut leather with a subtle built-in rim. Roboto Slab is used for body text, Arvo for headings, and Bebas Neue for large titles. The setup can be rebuilt with **Mythologicum > UI > Rebuild Steampunk Theme**. This updates only the theme asset and does not edit scenes.

The package has no envelope, close-cross, or checkmark artwork. Those controls continue to use the game's text symbols. The theme retains useful globe, records, rest, food, visitor, time, expedition, research, alert, settings, and navigation sprites for later interface work.

## Primary theme sprites

| Role | Atlas subasset |
| --- | --- |
| Window | `Steampunk_UI_Frames_3_3` |
| Inset panel / input | `Steampunk_UI_Frames_1_3` |
| Button normal / hover / pressed | `Steampunk_UI_Buttons_2_3`, `_4`, `_5` |
| Slider track | `Steampunk_UI_Loading_Bars_6` |
| Slider fill | `Steampunk_UI_Loading_Bars_1` |
| Slider / scrollbar handle | `Steampunk_UI_Slots_Arrows_9` |

No vendor scripts or TMP resources are dependencies of the retained art or theme.
The unused import and its generated settings folder were removed after checking
for project references to their GUIDs. `Documentation/UI.md` explains the runtime
theme, menu, settings and verification workflow.

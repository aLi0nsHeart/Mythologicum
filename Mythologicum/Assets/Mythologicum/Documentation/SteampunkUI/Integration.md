# Steampunk UI integration

The artwork from the SteampunkUI package is integrated into `Assets/Mythologicum/UI`:

- `Art`: all 33 supplied interface sprite sheets, backgrounds, and decorative images.
- `Fonts`: the original Arvo, Bebas Neue, Roboto Slab, and Liberation Sans TrueType fonts.
- `Resources/SteampunkTheme.asset`: the shared visual theme used by the game's UGUI interface.
- `Cursors/ExhibitionCursor.png`: a small generated cursor cropped from the retained atlas.

The publisher's original PDF and the supplied font and environment-map license notices are retained beside this file. The original package was published by Gentleland. Demo scenes, example prefabs and behaviours, TMP-only font assets, marketing overviews, environment maps, and the publisher's Editor utilities are not needed by Mythologicum's interface.

The original `.meta` GUIDs, atlas sprite rectangles, sprite IDs, and subasset IDs are preserved. A few selected sprite borders have been set for nine-slice resizing: the main window, dark inset panel, slider track, and bronze slider fill. All other imported artwork keeps its original slicing.

The theme exposes fonts, text sizes, colours and artwork through **Mythologicum > UI > Designer Tools**. Layout controls live in `UI/Resources/ExhibitionLayout.asset`. **Repair Missing Steampunk References** supplies missing defaults while preserving saved artist settings. These tools do not edit scenes or source atlases. See `Documentation/UI-Designer-Guide.md` for the current authoring workflow.

The quill-and-scroll artwork supplies the normal and unread Mail icons. Matching art is used for the primary building, park management, encyclopedia, pause, settings, expedition and research controls, including hover, press and disabled variants. Simple cross and tick controls keep their existing fallbacks.

## Primary theme sprites

| Role | Atlas subasset |
| --- | --- |
| Window | `Steampunk_UI_Frames_3_3` |
| Inset panel / input | `Steampunk_UI_Frames_1_3` |
| Button normal / hover / pressed | `Steampunk_UI_Buttons_2_3`, `_4`, `_5` |
| Slider track | `Steampunk_UI_Loading_Bars_6` |
| Slider fill | `Steampunk_UI_Loading_Bars_1` |
| Slider / scrollbar handle | `Steampunk_UI_Slots_Arrows_9` |
| Building | `Steampunk_UI_gameplay_icons_4_5` |
| Mail / unread Mail | `Steampunk_UI_gameplay_icons_4_12`, `_14` |
| Cursor source | `Steampunk_UI_inventory_cursor_icons_6` |
| Funds | `Steampunk_UI_inventory_cursor_icons_31` |

No vendor scripts or TMP resources are dependencies of the retained art or theme.
The unused import and its generated settings folder were removed after checking
for project references to their GUIDs. `Documentation/UI.md` explains the runtime
theme, menu, settings and verification workflow.

# Exhibition interface

The interface uses the retained Steampunk artwork and TrueType fonts in
`Assets/Mythologicum/UI`. No publisher, installer or Third Party folder is needed
outside Documentation. The original PDF and licenses are in
`Documentation/SteampunkUI`.

## Main menu

The menu is built at runtime from a list of actions: **New Game**, **Load Game**,
**Settings** and **Quit**. Load Game is greyed out while no save file exists. New
Game opens the existing map setup and preview; loading preserves the existing park
save flow. Add future menu entries in `MainMenuPresentation.CreateActions`; the
scrolling layout does not need rebuilding for another action.

The authored scenes are preserved. Their earlier menu graphics are hidden at
runtime, and the new menu retains the existing scene navigation component.

## Artist controls

Select `UI/Resources/SteampunkTheme.asset` in the Inspector to adjust the active
artwork, fonts and palette. Windows, inset panels, buttons and their states, input
fields, slider tracks and handles, and scrollbars have shared roles. The theme uses
Roboto Slab body text, Arvo headings, Bebas Neue titles, cream lettering, copper
controls, brass accents and dark leather backing. Disabled controls are dimmed.

Research, expedition, park management, encyclopedia, notification, new-game, pause
and confirmation panels share the same theme. Controls keep their existing gameplay
actions. Decorative graphics do not replace interactive targets or mask viewports.

Vertical information panels use `Steampunk_UI_Frames_1_2` at its native 438:627
ratio; horizontal panels use `Steampunk_UI_Dialogue_2_0` at 885:500. The expedition
parchment keeps its native 794:570 ratio. These illustrations use Simple rendering
and uniform scaling, with no sliced or stretched ornaments. `UIFramedPanel` owns
the artwork, dark lining, safe body area and heading area. Inspectors share the
same vertical layout; larger menus use a standard 1160-by-500 logical body fitted
inside the horizontal frame. Controls reflow inside that body rather than changing
the frame's proportions.

`UIOverflowScroll` measures visible content after fonts and layout are applied.
Scrolling and scrollbars activate only when that content exceeds the viewport.
Short reports stay stationary, hidden sections leave no blank gaps, and changing
from a long report to a short one clears the old scroll offset. Building research
notes share the report's single scroll area rather than nesting a second one.

World objects have no floating status text. Click a building, creature, visitor or
staff member to read its information and smoothly focus the camera on it. The
camera follows moving selections; Q/E and the mouse wheel still rotate and zoom.
WASD/arrow movement, clicking empty ground, Escape, or starting construction
releases follow. Removed objects and creatures that die release it automatically.
Loading a save restores the saved camera view without attaching it to an object.

## Park toolbar and encyclopedia

The in-game HUD follows the reference's open layout: **Building**, **Park
Management**, **Encyclopedia**, **Mail** and **Pause** form a curve in the bottom
left. Each control is its standalone Steampunk illustration, with no persistent
caption or button backplate. Hovering for 0.4 seconds reveals a temporary name.
Building unfolds a compact Buildings, Enclosures, Terrain, Paths, Fences, Creatures
and Demolish flyout. Choosing a tool folds the menu. Existing category shortcuts
still work and respect entrance and research requirements. Escape closes an open
menu or cancels the active tool before opening Pause. Pause uses the existing
save/load/settings/exit menu.

The top left shows only the dollar-prefixed funds total and five rating stars
underneath, with no background panel, money icon or numeric rating label. Outlines
keep the figures readable over the terrain. The display reads the existing economy
and reputation values, including after loading a paused park. Detailed park totals,
prices and the infinite-funds toggle live in Park Management. The old large overview
and top-right pause button are hidden or removed at runtime.
Idle construction no longer displays a category-selection prompt. Tool-specific
placement instructions remain visible when needed.

The encyclopedia has Park, Creatures and Management sections, with entry lists and
scrollable articles. Its creature catalogue comes from the game's existing creature
definitions. Species entries use the same `SpeciesResearch.Description` and progress
as researchers and creature inspectors: food information at 25%, habitat and sleep at
50%, space and group needs at 75%, and containment at 100%. New discoveries update
the open entry while preserving the reader's scroll position. This shares existing
saved research and does not create a second progression system.

`EncyclopediaPresentation.AddGuides` contains the introductory park and management
articles. Further creature definitions appear automatically in the species register.

`UIThemeApplicator` attaches to root canvases and styles existing and dynamically
created UI. `UIThemeElement` can explicitly select a role for a custom element or
mark a graphic Ignore to preserve maps, previews and special data graphics. Native
viewport masks stay transparent, and selected/disabled/warning states remain
readable. The application uses unscaled time so pause menus continue responding.

**Mythologicum > UI > Rebuild Steampunk Theme** rebuilds the default theme from the
retained artwork. This deliberately resets the theme's asset choices and colours;
ordinary Play mode does not reset artist edits. No source-pack scripts or TMP demo
prefabs are required.

## Icons and cursor

The theme's named `iconSets` assign normal, highlighted, pressed and disabled
artwork to controls. `UIIcon.AttachStandalone` makes the artwork itself the visible
control while retaining a transparent hit area and a hidden caption for tooltips.
Menu, statistics, expedition and research controls can still use labeled icons.
Decorative images never intercept pointer input.

- Building: `Steampunk_UI_gameplay_icons_4_5`.
- Mail: `Steampunk_UI_gameplay_icons_4_12`, changing to `_14` while letters are
  unread. The unread count appears in its hover name; reading letters restores the
  normal icon. The bright alert variant is reserved for unread mail.
- Cursor: `Steampunk_UI_inventory_cursor_icons_6`.

The cursor is cropped from its sprite rectangle into
`UI/Cursors/ExhibitionCursor.png`, a small readable RGBA texture imported as Cursor.
The shared theme stores its texture, source sprite and click hotspot. The original
atlas is preserved. `ExhibitionCursor` applies it across scenes and restores the
system cursor when focus is lost or Play mode ends. Rebuilding the theme regenerates
the cursor texture as well as the icon assignments.

## Settings

Settings are available from the main menu and Pause:

- Master volume applies to Unity's AudioListener.
- Window mode offers windowed, borderless and exclusive fullscreen in standalone
  builds. The Unity Editor retains control of its own windows.
- Graphics quality uses the project's actual quality levels.
- Movement, rotation and zoom sliders multiply the existing camera speeds, with
  1x preserving current controls.
- Movement instructions are listed here instead of over the park: WASD/arrow keys,
  Q/E rotation and mouse-wheel zoom.
- The vegetation asset credit is displayed here instead of on the play view.

Changes apply immediately and persist through PlayerPrefs. Back saves the current
preferences; Reset defaults restores standard values. Opening or closing settings
does not alter the pause state. Settings block camera and construction input.

## Verification

`Tests/UI/Unity/SteampunkUIIntegrationValidation.cs` runs against the real menu and
game scenes in an isolated project. It validates theme references, pointer
raycasts, settings, navigation, save loading and the major in-game screens, and
captures 1280/1920-resolution examples with a graphics device. Normal simulation
checks remain in `Tests/Creatures/RunChecks.ps1`.

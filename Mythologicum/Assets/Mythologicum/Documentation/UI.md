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

The existing construction bar, HUD and inspection locations are retained. Research,
expedition, park management, notification, new-game, pause and confirmation panels
share the same theme. Controls keep their existing gameplay actions. Decorative
graphics do not replace interactive targets or mask viewports.

`UIThemeApplicator` attaches to root canvases and styles existing and dynamically
created UI. `UIThemeElement` can explicitly select a role for a custom element or
mark a graphic Ignore to preserve maps, previews and special data graphics. Native
viewport masks stay transparent, and selected/disabled/warning states remain
readable. The application uses unscaled time so pause menus continue responding.

**Mythologicum > UI > Rebuild Steampunk Theme** rebuilds the default theme from the
retained artwork. This deliberately resets the theme's asset choices and colours;
ordinary Play mode does not reset artist edits. No source-pack scripts or TMP demo
prefabs are required.

## Settings

Settings are available from the main menu and Pause:

- Master volume applies to Unity's AudioListener.
- Window mode offers windowed, borderless and exclusive fullscreen in standalone
  builds. The Unity Editor retains control of its own windows.
- Graphics quality uses the project's actual quality levels.
- Movement, rotation and zoom sliders multiply the existing camera speeds, with
  1x preserving current controls.

Changes apply immediately and persist through PlayerPrefs. Back saves the current
preferences; Reset defaults restores standard values. Opening or closing settings
does not alter the pause state. Settings block camera and construction input.

## Verification

`Tests/UI/Unity/SteampunkUIIntegrationValidation.cs` runs against the real menu and
game scenes in an isolated project. It validates theme references, pointer
raycasts, settings, navigation, save loading and the major in-game screens, and
captures 1280/1920-resolution examples with a graphics device. Normal simulation
checks remain in `Tests/Creatures/RunChecks.ps1`.

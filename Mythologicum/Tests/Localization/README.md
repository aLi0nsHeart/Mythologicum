# Localization checks

The runtime runner includes `LocalizationChecks` and `CreatureLocalizationChecks`.
It supplies `MYTHOLOGICUM_LOCALIZATION_CSV` so these model checks read the actual
shipped catalogue without requiring Unity's native engine.

Coverage includes UTF-8, quoted commas/quotes/multiline cells, exact headers,
duplicate IDs, empty-language fallback, reordered placeholders, invalid format
arguments, numeric culture and ongoing ID-based creature/staff statuses.

`Unity/LocalizationNativeChecks.cs` is called by the existing isolated UI harness.
It tests real language preferences/dropdown binding, live menu and Settings
changes, English-caption edits with stable hierarchy names, saved notification
IDs/arguments, legacy notices, and player-entered save names. Temporary German
translations are injected into memory and restored; source CSV bytes are checked
for changes. Test translations are not included in the shipped catalogue.

Construction Undo's native helper additionally checks that a recorded action's
description changes language without rebuilding history. The UI harness checks
the twelve-option popup and transparent outside-click blocker.

On 10 November 2026, all **35 model suites** passed. The final isolated Unity run
passed **4,704 checks**, producing **63 captures**. The catalogue contained **1,138
IDs**, with English source only and all eleven translation columns blank. Scenes,
project settings, packages and the existing theme asset were preserved.

See [test setup](../README.md) and the [translator guide](../../Assets/Mythologicum/Documentation/Localization-Guide.md).

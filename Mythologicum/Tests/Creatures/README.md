# Creature logic checks

Run from the Unity project folder in PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File Tests/Creatures/RunChecks.ps1
```

For a different Unity installation, add `-UnityEditorData 'C:\path\to\Unity\Editor\Data'`.

The runner uses Unity's cached compiler references and current runtime source files. Open the project in Unity and let it compile once before running it. Every suite compiles the current runtime code, then checks pure logic using Unity's bundled Mono runtime. The tests stay outside Assets, and all generated executables and response files go in Temp/CreatureChecks. A compilation or test failure exits with a nonzero code.

- `CreatureModelChecks.cs`: hunger, feeding, fear, recovery, limits and time-step independence.
- `CreatureNavigationTests.cs`: free roaming, routes, fences, gates, boundaries, visibility and building obstacles.
- `GrazingFieldChecks.cs`: grass depletion, regrowth and excluding paths/buildings.
- `BuildingOperatingChecks.cs`: dispensers work without paths while ordinary buildings still require connections.
- `CreatureWellbeingChecks.cs`: environment, space, herd and hunger/fear contributions, smooth happiness and agitation.
- `EnvironmentTreeChecks.cs`: tree counts, reversible forest coverage and environment queries.
- `FlightNavigationChecks.cs`: optional flight routes, water crossings, closed aviary containment and flight tuning.
- `PredatorIntentSettingsChecks.cs`: stalking/luring tuning, child-only eligibility and bounded searches.
- `../Enclosures/SpecialEnclosureChecks.cs`: completed cage topology, exact pool excavation, batched water edits and persistent water after a breach.

These checks do not run Unity's scene, physics or UI. Confirm placement, animal movement, predator reactions and dispenser output separately in Play mode.

`Unity/CreatureIntegrationValidation.cs` preserves the separate headless Play-mode harness. It is kept outside Assets and was run in an isolated project under Temp, with 50 checks for actual creature behaviour, animations, needs and earlier regressions. It is not invoked by `RunChecks.ps1`.

The North American additions also have native Play-mode helpers outside Assets:

- `Unity/NorthAmericanSpeciesNativeChecks.cs`: imports Wendigo, Thunderbird and
  Kalupelik; checks profile values, biology, habitat, research text, localization
  fallback, expedition pools and composed placeholder bodies with shared materials.
- `Unity/FlightNativeChecks.cs`: flight motion, completed aviary containment,
  blocked escape attempts, sleep/landing and wing/lightning presentation.
- `Unity/StalkingLuringNativeChecks.cs`: stalking, luring and normal predator
  behaviour with actual Unity actors.
- `../Enclosures/Unity/SpecialEnclosureNativeChecks.cs`: completed-loop geometry,
  pool creation and containment across breaches.

Use the disposable-project setup described in `Tests/README.md`. Copy
`Unity/NorthAmericanExpansionValidation.cs` and all four helpers above into the
disposable project's `Assets/Editor`, alongside the current runtime files,
Resources assets, scenes, Packages and ProjectSettings. Run the matching Unity
editor with `-batchmode -force-d3d11`,
`-executeMethod NorthAmericanExpansionValidation.Start`, a `-projectPath` pointing
to the disposable copy and a separate `-logFile`. Omit `-quit` and `-nographics`:
the harness enters Play mode and exits itself. It writes
`NorthAmericanExpansionValidationResults.txt` in the copy's project root and uses
the isolated `MythologicumValidation/NorthAmericanExpansionValidation` save and
settings identity. Launch only one editor against that copy at a time.

The four helpers expose `RunChecks()` and are not standalone entry points.
The working project's scenes and designer assets must stay separate from the
disposable copy. Check the current run's results and log timestamps; these source
files do not themselves establish that native validation has passed.

Verified on **2026-11-24**: the full model runner passed **68 suites / 204,620
assertions**. `NorthAmericanExpansionValidation.Start` passed **350 native
checks**, including production disk-save/scene-load continuity, with matching
runtime/Resources hashes and no unexpected exceptions. Evidence is in
`LocalCache/SaveValidation/NorthAmericanExpansionValidationResults.txt` and its
matching `NorthAmericanExpansionValidation.log`. These results describe that
run; check timestamps and repeat after relevant changes.

For a manual check, follow
`Assets/Mythologicum/Documentation/North-American-Creatures-Guide.md`. In
particular, compare an incomplete special-fence loop with a completed one, then
save and load a breached aquarium: its pool must keep its water without being
excavated again when repaired.

## Creature need pressure and priorities

`CreatureWellbeingChecks` reproduces starvation plus exhaustion with 54 thirst,
half-suitable ground and otherwise adequate care. It checks independent critical
need pressure, disabled species requirements/weights, editable response rates,
compound distress, diagnosis, bounded values, recovery and frame independence.
`CreatureBalanceChecks` also checks the new designer controls.

For actual imported data, physics, decisions and persistence, copy
`Unity/CreatureNeedsIntegrationValidation.cs` and
`Unity/CreatureNeedsPriorityNativeChecks.cs` into the disposable project's
`Assets/Editor`, alongside its current runtime scripts and Resources. Run
`CreatureNeedsIntegrationValidation.Start` using the disposable setup above
(`-batchmode -force-d3d11`, without `-quit` or `-nographics`). It uses its own
`MythologicumValidation/CreatureNeedsIntegrationValidation` settings/save identity
and removes only its generated manual slot.

The native checks cover existing implicit/explicit behaviour profiles, real
Waldschrat ground sampling and needs, authoritative inspection immediately after
paused placement, agitation while happiness is still high,
urgent herbivore/carnivore feeding, failed rest retries, legitimate escape
attempts, actual sleep recovery, sedation, the food-free Yurei exception,
localized inspection, gradual emotional recovery, and actual disk/scene loading.
The result is `CreatureNeedsIntegrationValidationResults.txt` with a matching
log in the disposable project root. Read the current run's timestamp and result;
the existence of this fixture does not establish a passing native run.

The full model runner passed on **2026-11-24**: **68 suites / 204,645 reported
assertions**, including 69 wellbeing and 23 balance-data checks. The matching
log is `LocalCache/CreatureNeedsValidation/AllModelChecks.log`.

The final focused Play-mode run on **2026-11-24** passed **48 native checks**,
including the real saved-crisis scene round trip. The native recovery fixture
freezes decisions after habitat revision refresh so it remains in its cared-for
test patch; separate priority fixtures exercise normal physical movement and
feeding. Evidence is
`LocalCache/SaveValidation/CreatureNeedsIntegrationValidationResults.txt` and its
matching `CreatureNeedsIntegrationValidation.log`. The tested helper's source
hash matched the working copy, and the successful run logged no unexpected
exceptions.

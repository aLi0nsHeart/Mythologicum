# Mythologicum checks

Test sources stay outside `Assets`, so they do not become part of the game build.
Use the project's Unity version from `ProjectSettings/ProjectVersion.txt`.

## Runtime model checks

Open the project in Unity once and let script compilation finish. From the Unity
project root in PowerShell, run:

```powershell
powershell -ExecutionPolicy Bypass -File Tests/Creatures/RunChecks.ps1
```

For another Unity installation, add
`-UnityEditorData 'C:\path\to\Unity\Editor\Data'`. The runner uses Unity's cached
compiler response file, compiles the current runtime sources for each suite, and
runs the resulting checks through Unity's bundled Mono. Executables and response
files go to `Temp/CreatureChecks`; compilation or assertion failures exit with a
nonzero code. `-CheckNames` accepts selected names relative to `Tests/Creatures`,
without `.cs`, for example `..\Progression\ParkAlertChecks`.

These checks cover simulation models and rules. Real scenes, physics, native Unity
objects, input and the rendered interface require the separate native checks.

## Native Unity validation

Use a **disposable project at `LocalCache/SaveValidation`**, with matching copies
of this project's `Assets`, `Packages` and `ProjectSettings`. Let Unity import and
compile that copy. Do not copy its modified assets or settings back into the
working game project. Run only one editor process against that copy at a time.

Copy these test sources into **the disposable project's `Assets/Editor`**:

- `Tests/UI/Unity/SteampunkUIIntegrationValidation.cs`
- `Tests/UI/Unity/ParkOperationsNativeChecks.cs`
- `Tests/Buildings/Unity/ConstructionUndoNativeChecks.cs`
- `Tests/Buildings/Unity/ConstructionUndoInputNativeChecks.cs`
- `Tests/Saving/Unity/SaveSlotNativeChecks.cs`
- `Tests/Localization/Unity/LocalizationNativeChecks.cs`
- `Tests/UI/Unity/UIMaintenanceNativeChecks.cs`

The UI harness calls the other helpers; they are not standalone entry
points. The normal editor/runtime code under `Assets/Mythologicum` must also be
up to date in the copy, including the gameplay data validator.

Launch the matching Unity editor in batch mode with a real graphics device:

```text
-batchmode -force-d3d11
-projectPath <absolute project path>/LocalCache/SaveValidation
-executeMethod SteampunkUIIntegrationValidation.Start
-logFile <absolute project path>/LocalCache/SaveValidation/ParkOperationsUIValidation.log
```

Run with the disposable project as the working directory. Use a hidden window
when launching with PowerShell `Start-Process`. Omit `-nographics`: captures require
a graphics device. Omit `-quit`: the harness enters Play mode, advances through
scenes, writes its result and exits itself. Unity licensing must be available.

The harness sets its own application identity to
`MythologicumValidation/SteampunkUIValidation`. Save and preference checks use that
identity, with bounded cleanup of its own managed test saves. Designer-asset write
checks also verify the disposable project path. The authored game project and
the player's normal save directory must remain separate from this fixture.

Read these files inside `LocalCache/SaveValidation`:

- `SteampunkUIValidationResults.txt`: final `PASS` or `FAIL` and failure detail.
- `ParkOperationsUIValidation.log`: full editor log, including individual checks.
- `LocalCache/SteampunkUIValidation/*.png`: UI captures at several resolutions.
- `LocalCache/SaveSlotValidation`: isolated repository test workspace.

A stale result from an earlier run is not evidence for the current source. Check
the process exit code and the result/log modification times together.

## Native construction shortcut checks

For a focused construction-shortcut regression, also copy
`Tests/Buildings/Unity/ConstructionUndoInputValidation.cs` and
`Tests/Buildings/Unity/ConstructionUndoInputNativeChecks.cs` into the disposable
project's `Assets/Editor`. Run `-executeMethod ConstructionUndoInputValidation.Start`
with the same graphics and self-exit flags. It uses the isolated
`MythologicumValidation/ConstructionUndoInputValidation` identity and writes
`ConstructionUndoInputResults.txt`. It destroys a selected UI button across real
frames, checks repeated idle updates, and verifies Ctrl+Z with both a destroyed
selection and a genuinely focused text field. The ordinary UI harness runs the
same regression before its save/load checks.

## Native loading-screen checks

Copy `Tests/UI/Unity/SceneTransitionNativeChecks.cs` into the disposable project's
`Assets/Editor`. Run a separate batch editor after any other validation exits:

```text
-executeMethod SceneTransitionNativeChecks.Start
-logFile <absolute project path>/LocalCache/SaveValidation/SceneTransitionValidation.log
```

Use the same real-graphics and self-exit flags described above. This helper uses
the separate `MythologicumValidation/SceneTransitionValidation` save identity and
cleans only its own unique test slot. It covers new games, save restoration,
returning to menu, input blocking, progress, pause restoration, failure recovery,
direct GameScene preparation and removal of temporary loading objects.
Results are in `SceneTransitionValidationResults.txt`; rendered loading examples
are in `LocalCache/SceneTransitionValidation` within the disposable project.
The ordinary UI harness waits for `SceneTransition.IsLoading` to clear before
interacting with the incoming scene.

## Native performance fixture

Copy `Tests/Performance/Unity/LargeParkPerformanceValidation.cs` into the disposable
project's `Assets/Editor`. Keep `Assets/Mythologicum/Editor/LargeParkBenchmark.cs`
and the runtime sources current there. Launch a separate batch run after the UI
run has exited, using the same project and graphics settings, with:

```text
-executeMethod LargeParkPerformanceValidation.Run
-logFile <absolute project path>/LocalCache/SaveValidation/LargeParkPerformance.log
```

This helper checks three simulation populations, sample counts, timing values and
cleanup of its temporary scene. It exits itself. Results are
`LargeParkPerformanceResults.txt`, the editor log, and JSON/CSV reports under
`LocalCache/Performance` in the disposable project. `LatestBenchmark.txt` points to
the newest JSON report. The fixture measures CPU simulation using placeholder
actors; it does not measure game frame rate, imported artwork, UI or GPU rendering.

For interactive use, open **Mythologicum > Gameplay > Run large park benchmark**
outside Play mode. See the [park operations guide](../Assets/Mythologicum/Documentation/Park-Operations-Guide.md)
for the player controls, balancing settings and a manual test checklist.

The [10 November 2026 baseline](Performance/Baselines/README.md) records the first
validated park-operations run, including raw timing data and the visitor-update
outlier that needs further profiling.

The [13 November 2026 comparison](Performance/Baselines/2026-11-13/README.md)
contains three before/after simulation runs, calibrated UI measurements and their
limits. Unsupported native allocation counters must not be read as zero garbage.

For the separate UI maintenance benchmark, copy
`Tests/Performance/Unity/UIPerformanceValidation.cs` to the disposable project's
`Assets/Editor` and use `-executeMethod UIPerformanceValidation.Start` with the
same graphics and self-exit flags. Results are `UIPerformanceResults.txt` and
`LocalCache/Performance/UIBenchmark-*.json`; `LatestUIBenchmark.txt` points to the
newest report. It uses the isolated `MythologicumValidation/UIPerformanceValidation`
application identity.

For visitor choice equivalence against the previous exhaustive implementation,
copy `Tests/Performance/Unity/VisitorPerformanceNativeChecks.cs` and use
`-executeMethod VisitorPerformanceNativeChecks.Run`. This Edit-mode fixture writes
`VisitorPerformanceValidationResults.txt`, checks exact animal/point selection,
and verifies scene/actor cleanup.

The default model runner includes `RouteSearchPerformanceChecks` and
`VisitorViewingCacheChecks`. Their timing output is informational; assertions
check equivalent behavior and reduced allocations rather than wall-clock limits.

## Creature behaviour rework

The default model runner also includes `ScarecrowEnvironmentChecks` and
`CreatureScareChecks` for enrichment, overlap/revisions and jump motion.

Copy these helpers from `Tests/Creatures/Unity` to the disposable project's
`Assets/Editor`:

- `CreatureEscapeNativeChecks.cs`
- `CreatureScareNativeChecks.cs`
- `ScarecrowConstructionNativeChecks.cs`
- `CreatureReworkSaveValidation.cs`

Run separate editors sequentially with the usual real-graphics flags and one of:

- `-executeMethod CreatureEscapeNativeChecks.Run`
- `-executeMethod CreatureScareNativeChecks.Run`
- `-executeMethod CreatureReworkSaveValidation.Start`

All three self-exit and write `CreatureEscapeValidationResults.txt`,
`CreatureScareValidationResults.txt`, or `CreatureReworkSaveResults.txt`. The save
harness also runs the scarecrow construction/Undo helper in a real prepared park.
It uses the isolated `MythologicumValidation/CreatureReworkSaveValidation` identity
and cleans only its own unique manual slot. Other fixture scenes/assets are
temporary and must not be copied back to the authored project.

## Central European creatures

The model runner includes `CreatureMeadowChecks`, `CreatureBurrowingChecks` and
`CentralEuropeProgressionChecks` for habitat scoring, dig-ground rules and complete
expedition/specimen-stock round-trips.

Copy these helpers from `Tests/Creatures/Unity` into the disposable editor project:

- `CentralEuropeSpeciesNativeChecks.cs`, entry `CentralEuropeSpeciesNativeChecks.Run`
- `CreatureHabitatNativeChecks.cs`, entry `CreatureHabitatNativeChecks.Run`
- `CreatureBurrowingNativeChecks.cs`, entry `CreatureBurrowingNativeChecks.Run`

Use the same sequential, real-graphics batch-editor setup. Each exits itself and
writes its own `CentralEuropeSpeciesResults.txt`,
`CreatureHabitatValidationResults.txt`, or `CreatureBurrowingValidationResults.txt`.
They check actual species assets, procedural bodies, meadow measurements, shaded
rest, safe underground sleeping/emergence, resisted/interrupted tunnels and hidden
prey. `CreatureReworkSaveValidation.Start` also restores all three new species and
surfaces a saved underground sleeper without losing its energy.

## Visitor differentiation

The model runner includes `CreatureAppealChecks` and `VisitorTypeChecks`. To run
only these suites from the project root:

```powershell
& .\Tests\Creatures\RunChecks.ps1 -CheckNames @(
    '..\Visitors\CreatureAppealChecks'
    '..\Visitors\VisitorTypeChecks'
)
```

These cover audience-appeal ranges, weighted arrival selection, interest and
novelty scoring, type multipliers and neutral General visitor compatibility.
They do not replace real model, UI or save checks.

Copy these sources from `Tests/Visitors/Unity` into the disposable project's
`Assets/Editor`, alongside current game sources and data:

- `CreatureAppealNativeChecks.cs`
- `VisitorTypesNativeChecks.cs`
- `VisitorPeopleUINativeChecks.cs`
- `VisitorDifferentiationValidation.cs`
- `VisitorTypeSaveValidation.cs`

Use the same sequential, real-graphics batch-editor setup described above.
`-executeMethod VisitorDifferentiationValidation.Run` combines the creature appeal,
visitor type, and people/UI checks. It verifies actual species assets, arrival
mix, exhibit choices, completed discovery history, needs, fence danger, strict
adult/child model pools, selected-visitor details and management type counts.
It self-exits, writes `VisitorDifferentiationResults.txt`, and uses the isolated
`MythologicumValidation/VisitorDifferentiationValidation` identity.

For separate diagnosis, run `-executeMethod VisitorTypesNativeChecks.Run` or
`-executeMethod VisitorPeopleUINativeChecks.Run`. These write
`VisitorTypesValidationResults.txt` or `VisitorPeopleUIResults.txt` respectively.
`CreatureAppealNativeChecks.RunChecks` is a helper called by the combined fixture,
not a standalone batch entry point.

Run `-executeMethod VisitorTypeSaveValidation.Start` separately to check a real
park save/load round-trip for all four types, needs, completed species/origin
discoveries and older-save General visitors. This Play-mode harness self-exits,
writes `VisitorTypeSaveResults.txt`, uses the isolated
`MythologicumValidation/VisitorTypeSaveValidation` identity, and cleans only its
own unique test slot. Omit `-quit` so its scene transitions can complete.

Treat the current run's result files, logs and process exit code as the evidence;
this documentation does not record a validation outcome. Designer settings and
manual checks are explained in the
[visitor types guide](../Assets/Mythologicum/Documentation/Visitor-Types-Guide.md).

## Visitor arrival preparation and timings

The model runner includes `VisitorArrivalPathChecks` for budgeted entrance BFS,
exact path order, cache reuse, revision invalidation and warmed Mono allocations.

Copy `Tests/Visitors/Unity/VisitorArrivalNativeChecks.cs` into the disposable
project's `Assets/Editor` and run `-executeMethod VisitorArrivalNativeChecks.Run`.
It writes `VisitorArrivalNativeResults.txt`. Its edit-mode checks cover unique
model preparation steps, cleanup, admission isolation, a complete first decision
at the visitor limit, changed paths/gates/physical obstacles, and immediate danger
response during preparation. Real loading frames are covered separately by the
ordinary UI and scene-transition Play-mode harnesses.

For CPU timings, copy
`Tests/Performance/Unity/VisitorArrivalPerformanceValidation.cs` and run
`-executeMethod VisitorArrivalPerformanceValidation.Run`. It uses a 128x128 flat
terrain, 4,935 connected path tiles, 48 exhibits and retained adult/child models.
It writes `VisitorArrivalPerformanceResults.txt` and
`LocalCache/Performance/VisitorArrivals/Latest.json` / `Latest.txt`.

Measurements separate loading-time model preparation, creation/admission, direct
first decisions and actual budgeted arrival updates. The optional preparation
rows also allow the same helper to run against the older runtime. Direct private
`Think` calls deliberately allow a synchronous fallback; the budgeted `Tick` row
tests the normal arrival path. Timing samples are informational, with no timing
thresholds. Rendering, skinning, GPU uploads and overall game FPS are outside this
edit-mode fixture. Keep the real-graphics, sequential disposable-editor setup
described above.

## Staff search performance checks

The default model runner includes `StaffReachableBufferChecks`. It compares
reusable buffers with an independent original BFS, including exact order, changed
topology, public result ownership and supported Mono allocation measurements.

Copy `Tests/Staff/Unity/StaffPerformanceNativeChecks.cs` into the disposable
project's `Assets/Editor` and run `-executeMethod StaffPerformanceNativeChecks.Run`.
It writes `StaffPerformanceNativeResults.txt` and verifies old/new approach goals
across walls, gates, fractional positions, ranges and path preferences. It also
checks strict containment, breaches, physical obstacles, sedation and actual fear
responses. Its query timings are informational and have no timing thresholds.

For actual staff tasks, copy `Tests/Staff/Unity/StaffIntegrationValidation.cs` and
run `-executeMethod StaffIntegrationValidation.Start`. It writes
`StaffValidationResults.txt` and checks hiring/accounts, research, care, carrying,
security and imported locomotion. It creates its fixture scene only in the
disposable project and uses the `MythologicumValidation/StaffIntegrationValidation`
identity. Its accounting visitors are neutral and use explicitly affordable
fixture prices; production pricing and visitor types have separate suites.

## Researchable attractions

The default model runner includes `AttractionDataChecks`, `FenceWindowChecks`,
`AttractionRideCycleChecks` and `VisitorAttractionSaveChecks`. To run these suites alone:

```powershell
& .\Tests\Creatures\RunChecks.ps1 -CheckNames @(
    '..\Buildings\AttractionDataChecks'
    '..\Buildings\FenceWindowChecks'
    '..\Buildings\AttractionRideCycleChecks'
    '..\Visitors\VisitorAttractionSaveChecks'
)
```

These cover stable building IDs, copied/validated settings, research dependencies,
rotated petting entrances, fence-window accounts and containment, and optional
active-visit save validation. Legacy economy snapshots are also covered by
`FinanceChecks`; the original starting-park balance audit excludes optional
researched attractions.

Petting checks include fence-mounted geometry in all four directions, preserving
an existing access path, duplicate/gate/window conflicts, empty tile occupancy,
and compatibility with the earlier building-shaped entrance. Active and orphaned
visit saves accept the appropriate adjacent or legacy two-step portal geometry.

Copy these sources into the disposable project's `Assets/Editor`, alongside the
current game sources and assets:

- `Tests/Buildings/Unity/AttractionDataNativeChecks.cs`
- `Tests/Buildings/Unity/AttractionVisualNativeChecks.cs`
- `Tests/Buildings/Unity/ViewingWindowConstructionNativeChecks.cs`
- `Tests/Buildings/Unity/AttractionIntegrationValidation.cs`
- `Tests/Visitors/Unity/VisitorAttractionNativeChecks.cs`
- `Tests/Visitors/Unity/AttractionPassengerNativeChecks.cs`

Run `-executeMethod VisitorAttractionNativeChecks.Run` for actual paid visits,
quoted prices, capacity reservations, cancelled visits, window visits, controlled
theatre performances and petting entry/approach/exit. This edit-mode fixture writes
`VisitorAttractionNativeResults.txt` and uses the isolated
`MythologicumValidation/VisitorAttractionValidation` identity.

Run `-executeMethod AttractionIntegrationValidation.Start` separately for real
research, construction, animated placeholder models, sloped viewing windows,
inspection, construction undo and a production scene save/load round-trip with an
active petting visit. It calls the three building native helpers above, which are
not standalone batch entry points. This Play-mode harness writes
`AttractionIntegrationResults.txt`, captures `Attractions-Catalogue.png` under
`LocalCache/SteampunkUIValidation`, and uses the isolated
`MythologicumValidation/AttractionIntegrationValidation` identity. It removes only
its own uniquely named save slot and associated backup/temporary files.

The petting fixture uses the production snapping resolver on every side of a
closed pen, places the gateway over an existing path, checks preview/model fence
alignment and fence-click inspection, undoes placement without removing the path
or fence, and retains mounted geometry across a real scene load.

The viewing building occupies two tiles along the fence and one row inside the
enclosure. Checks cover both attachment segments, foundation rules, protected
room entry and exit, completed-visit fees, legacy fence-window compatibility and
restoring an indoor visitor through production save/load. The visitor fixture
also removes the entrance path during an active visit: the guest must exit onto
clear ground without paying for the interrupted visit.

Run `-executeMethod AttractionPassengerNativeChecks.Start` separately for actual
procedural models and passengers. It checks inside visits and tower height,
distinct balloon basket positions, two consecutive groups, landing before
unloading, pause, quoted fees, carousel passengers and occupied-attraction
demolition protection. It writes `AttractionPassengerNativeResults.txt` and uses
the isolated `MythologicumValidation/AttractionPassengerValidation` identity.
The ordinary integration fixture also saves a balloon in flight and a visitor
on the tower, then restores their actual positions through production scene
loading. Ride clocks, timing/capacity snapshots and passenger seats are saved
separately; older saves have no shared ride state.

Use the sequential, real-graphics disposable-editor setup described above and
omit `-quit` so scene transitions can complete. The ordinary
`SteampunkUIIntegrationValidation.Start` harness covers the wider menu, layout and
save/load regressions. Check fresh result files, logs and exit codes for each run.

Designer settings and manual checks are described in the
[attractions guide](../Assets/Mythologicum/Documentation/Attractions-Guide.md).

## Water, biomes and shelters

The habitat model suites can be selected directly:

```powershell
& .\Tests\Creatures\RunChecks.ps1 -CheckNames @(
    'CreatureHabitatChecks'
    'CreatureThirstChecks'
    'CreatureShelterChecks'
    '..\Terrain\HabitatTerrainChecks'
    '..\Terrain\HabitatWaterGeometryChecks'
)
```

These cover thirst and finite drinking, compatible vitals restoration, shelter
configuration and persistent home records, biome painting, water depth, protected
basin excavation, shoreline geometry and habitat save validation. Ordinary terrain
and creature navigation suites continue to cover the existing foundation and
land movement rules.

Copy these sources into the disposable project's `Assets/Editor` alongside the
current runtime, resources and scenes:

- `Tests/Habitats/Unity/HabitatIntegrationValidation.cs`
- `Tests/Creatures/Unity/CreatureWaterNativeChecks.cs`
- `Tests/Creatures/Unity/CreatureShelterNativeChecks.cs`
- `Tests/Terrain/Unity/HabitatTerrainNativeChecks.cs`

Run `-executeMethod HabitatIntegrationValidation.Start` using the sequential,
real-graphics disposable-editor setup above. Omit `-quit` so Play-mode scene
loading can complete. The entry point calls the three `RunChecks()` helpers;
they are not standalone batch entry points.

The fixture verifies actual trough drinking and keeper refills through staff
access, proportional water consumption, refill expense accounting, feature
placement and Undo, pond excavation and Undo, protected brush cells, five visible
ground types, water as a separate layer from land navigation, persistent burrow
reuse and cave/tree/shade/grave shelter behaviour. Additional water cases check
tired creatures emerging from burrows, trees and graves before drinking,
finishing their drink and returning to the same home. The fixture then saves
and loads a real park containing a keeper, water stock, pond levels, biome overrides, individual
thirst and sleeping creatures associated with their saved homes. Terrain helpers
also check mesh normals, batched render updates and the expanded brush controls.

It writes `HabitatIntegrationResults.txt` and uses the isolated
`MythologicumValidation/HabitatIntegrationValidation` identity. Its cleanup removes
only its own uniquely named manual save slot and associated backup/temporary
files. Use fresh result files, logs and editor exit codes as validation evidence.

Designer settings and manual checks are described in the
[habitat guide](../Assets/Mythologicum/Documentation/Habitat-Guide.md).

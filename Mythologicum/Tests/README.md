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

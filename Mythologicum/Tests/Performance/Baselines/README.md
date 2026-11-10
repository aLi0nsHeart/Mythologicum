# Park operations baseline: 10 November 2026

Unity 6000.0.74f1, Intel Core i5-10600K. The JSON records the full hardware,
fixture settings and subsystem timings; the CSV is suitable for comparisons.
These are measurements from one editor run, not performance targets or game FPS.

Each profile used a 32 by 32 map, seed 2471, ten warm-up ticks and sixty measured
ticks. Each tick advances 0.1 simulation seconds. Actors use placeholder visuals;
rendering, UI, combat effects and mass construction are excluded.

| Actors (creatures / visitors / staff) | Median tick | 95th percentile | Maximum tick |
| --- | --- | --- | --- |
| 51 (20 / 25 / 6) | 3.199 ms | 9.970 ms | 39.906 ms |
| 142 (50 / 80 / 12) | 6.156 ms | 23.945 ms | 393.196 ms |
| 274 (100 / 150 / 24) | 16.369 ms | 56.047 ms | 1,314.629 ms |

All requested populations remained intact. The native helper verified sample
counts, restoration of the active scene, unchanged original scene dirty flags,
and removal of temporary actors.

The large run includes a 1,303.608 ms visitor-update outlier. Visitor/navigation
profiling is needed before drawing conclusions about larger playable parks. A
single stable-population CPU fixture does not cover every workload or explain
that outlier's cause.

The park operations implementation also passed all 33 runtime model suites and
4,638 native Unity UI/integration checks with 62 GPU captures. Native coverage
includes alerts, overlays, autosaving, named manual slots, backups, legacy saves,
pause/speeds, shortcut input, UI scaling, construction Undo and saved-park restore.
The authored scenes and existing theme asset were preserved.

See [the test instructions](../../README.md) to repeat the checks, and
[the park operations guide](../../../Assets/Mythologicum/Documentation/Park-Operations-Guide.md)
for the player controls and editable settings.

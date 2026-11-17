# Visitor arrival CPU measurements — 16 November 2026

Unity 6000.0.74f1, disposable project, real graphics device, edit-mode CPU fixture.
The workload has flat 128x128 terrain, 4,935 connected path tiles, 48 exhibits and
the retained adult/child model prefabs. Rendering, skinning, GPU uploads, the
ordinary park UI and overall game FPS are outside these measurements.

`Before.json` records the original runtime. `After.json` is the first run with
loading-time model preparation, shared entrance BFS and scheduled exhibition
snapshots. `AfterBudgeted.json` is another run of the same runtime with an added
measurement of actual visitor updates while rebuilding entrance paths.

| CPU phase, milliseconds | Before | After | After, extra budget measurement |
| --- | ---: | ---: | ---: |
| First paid arrival | 219.091 | 7.749 | 7.770 |
| Median paid arrival, 16 samples | 0.714 | 0.265 | 0.297 |
| Median direct first decision, cold viewing cache | 7.230 | 3.086 | 4.152 |
| Median direct first decision, shared viewing cache | 4.332 | 0.275 | 0.744 |
| Total loading-only model preparation | not run | 1016.872 | 14.184 |

Model preparation is measured separately and happens before the paid-arrival
samples. In the game it yields between unique models under the loading screen;
this edit-mode fixture drains those yields without rendered frames. Native asset,
Editor and OS caches strongly affect first-use preparation, as the two totals
show. These are observed runs, not a statistical FPS or worst-case guarantee.

The extra `Budgeted-arrival-Tick` sample invalidates the cache and prepares all
4,935 entrance-path cells over **20 updates** at the default 256-cell budget. The
median update costs 0.276 ms; its first/worst update costs 20.961 ms, including
initial `Tick` execution, revision refresh and exhibition work. The final update
also starts the visitor's exhibit decision. A cell budget limits search work,
not total wall-clock time or unrelated simulation/engine work.

Direct private `Think` samples retain their synchronous fallback for explicit
callers, so their first samples can still perform a whole BFS. Ordinary new
arrivals wait for the incrementally prepared complete path list, with danger and
leaving taking priority. Regression checks verify identical BFS/tie order and
exhibit preferences; timings have no pass/fail thresholds.

Run `VisitorArrivalPerformanceValidation.Run` as described in `Tests/README.md`.
The helper supports the older runtime by checking for preparation APIs before
using them. Its initial baseline version lacks the optional preparation/budget
rows; the existing measurements use the same workload and sample counts.

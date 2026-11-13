# Performance comparison: 13 November 2026

Unity 6000.0.74f1, Intel Core i5-10600K. Before and after reports were captured on
the same machine using the same fixture and populations. Runtime code was copied
into the disposable project before editing; allocation instrumentation was added
to the benchmark only. Authored game scenes and assets were preserved.
The unmodified runtime source was commit
`0c9c8c9fb6cdcd1c7f051d0c77e53f36cec11e3a`.

## Simulation

Each phase ran in three fresh editor processes. Each population used a 32 by 32
map, seed 2471, ten warm-up ticks and sixty measured ticks of 0.1 simulation
seconds. The table reports the median of the three per-run medians and the median
of the three per-run 95th percentiles. Visitor maxima are the worst observed
subsystem tick across all three runs.

| Actors (creatures / visitors / staff) | Median before / after | P95 before / after | Worst visitor update before / after |
| --- | --- | --- | --- |
| 51 (20 / 25 / 6) | 4.069 / 2.679 ms | 12.478 / 10.792 ms | 42.195 / 11.668 ms |
| 142 (50 / 80 / 12) | 6.148 / 4.286 ms | 23.534 / 21.909 ms | 373.083 / 32.416 ms |
| 274 (100 / 150 / 24) | 16.457 / 12.202 ms | 52.295 / 48.331 ms | 1,310.881 / 116.537 ms |

All requested actors remained intact. Scene restoration and removal of temporary
actors were checked. These CPU measurements exclude rendering, terrain art, UI,
combat effects and construction invalidation. Long updates remain; these results
are not a frame-rate claim or a promise that every large park is smooth.

## UI maintenance

One calibrated run per phase used the actual generated GameScene UI, 20 warm-up
updates and 120 measured synthetic 60 Hz updates, with a discovery scan every
12 updates. Both versions had 388 graphics, of which 23 were visible with menus
closed and 53 with the building catalogue open.

| UI state | Median before / after | P95 before / after |
| --- | --- | --- |
| Closed menus | 3.800 / 0.166 ms | 5.666 / 1.703 ms |
| Building catalogue | 3.872 / 0.328 ms | 4.618 / 2.052 ms |

This fixture includes active layout, scrolling, icon and frame maintenance. It
excludes simulation, rendering, canvas mesh rebuilding, input and dynamic report
generation. It does not measure the whole game's UI frame cost.

## Allocation counters and raw data

The native Unity per-thread allocation counter failed a known-allocation probe.
The before simulation reports contain unsupported zero allocation readings;
ignore those columns. The final benchmark exposes
`allocationCounterAvailable=false` and leaves unsupported CSV values blank.
Calibrated UI reports record `bytesPerUpdate=-1`. No native zero-allocation claim
is made. Separate bundled-Mono differential checks verify reduced allocations in
BFS and A* against the original implementations.

`Before/` and `After/` retain all three simulation JSON/CSV pairs and the calibrated
UI reports. Runtime validation also compares exact routes, weighted costs, BFS
order, occupancy revisions, terrain edits/restoration and visitor exhibit choices.

## Validation

- All 39 suites in the default model runner passed, including 29,399 differential
  search, occupancy, terrain and allocation checks and 4,803 viewing-cache checks.
- 967 native visitor exhibit-choice and cleanup checks passed.
- 4,723 native UI/integration checks passed with 63 GPU captures, including live
  designer changes, hidden-menu styling, localization, save/load and icon state.
- All six simulation benchmark runs retained exact requested populations and
  restored their original scene state.

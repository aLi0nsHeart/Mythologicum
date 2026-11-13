# Performance

The first optimization pass shares visitor exhibit visibility checks during each
simulation tick, uses indexed grid occupancy and revision-checked terrain centre
heights, and removes allocating search-frontier nodes and BFS neighbour iterators.
Routes retain their original costs, priorities and tie order. Creature decisions,
movement timing and the reported back-and-forth behaviour have not been redesigned.

UI maintenance styles visible graphics during ordinary frames and refreshes layout
and scroll components once in hierarchy order. Explicit refresh still includes
hidden menus for localization and designer edits. Most ordinary UI rectangles no
longer require individual layout update components. Creature placeholder animation
also reuses its material-property storage and dispenser bookkeeping buffers.

## Measuring your park

Use Unity's CPU Profiler while playing a representative park. The following
samples separate the main runtime costs:

- `Mythologicum.Creatures`
- `Mythologicum.Visitors`
- `Mythologicum.Staff`
- `Mythologicum.InterfaceReports`
- `Mythologicum.InterfaceMaintenance`

Test the same save, camera position, simulation speed and graphics settings before
and after a change. Check both ordinary frames and occasional long frames. A
standalone development build is useful for assessing the actual player workload;
the Editor and its tools add their own costs.

**Mythologicum > Gameplay > Run large park benchmark** measures creature, visitor
and staff simulation on repeatable populations and writes JSON/CSV to
`LocalCache/Performance`. The benchmark uses placeholder actors on flat ground.
It excludes rendering, imported models, terrain art, reports, combat effects and
construction invalidation; it does not predict game FPS. Run it outside Play mode.

The native UI benchmark in `Tests/Performance/Unity` separately measures generated
UI maintenance with closed and open construction menus. See `Tests/README.md` for
the disposable-project setup. It excludes rendering, canvas rebuilding, input and
dynamic report generation.

## Interpreting memory measurements

The benchmark calibrates the per-thread allocation counter using a known
allocation. This Unity Editor's native runtime does not expose a working counter.
When `allocationCounterAvailable` is false, ignore allocation totals; the CSV
leaves them blank and the UI report uses `-1`. Zero from an unsupported counter
does not mean no garbage was generated. The bundled Mono model checks separately
compare allocation counts against the original search implementations.

The grid caches use storage proportional to map area: approximately 4 bytes per
cell for occupancy, plus 8 bytes per cell when terrain centre heights are cached.
Terrain edits, foundations, regeneration and loading invalidate those heights.
Visitor geometry is refreshed every tick and its lists are reused.

Measured comparisons and their workload limits are recorded in
`Tests/Performance/Baselines/2026-11-13/README.md`. Remaining CPU spikes, rendering
and larger real parks should guide subsequent optimization. No additional Unity
Store package is required for this pass.

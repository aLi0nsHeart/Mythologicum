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

## Smoother visitor arrivals

Imported visitor models and animation resources are prepared one model per frame
behind the loading screen. Temporary models are removed before gameplay and never
become visitors or affect admissions. Ordinary arrivals still receive fresh actor
state and the correct adult/child model; models are not pooled between visitors.
This preparation reduces first-use work without promising that every graphics
driver or shader variant has been warmed.

The entrance's connected public paths are prepared incrementally and shared by
new arrivals. This preserves the original BFS order and exhibit-choice ties.
Grid, terrain, physical-obstacle and fence revisions restart preparation. A new
visitor can wait briefly at the entrance until the complete result is ready;
escape responses and leaving the park never wait for this preparation. Existing
visitors elsewhere continue to use their individual navigation searches.

Arrivals also reuse the scheduled exhibition/reputation snapshot instead of
forcing another recount. Its default interval remains 5 simulation seconds;
construction changes invalidate it immediately. Current admission prices and
visitor reputation feedback still apply normally.

Select `Resources/Visitors/DefaultVisitors.asset`, or open **Mythologicum >
Gameplay > Balancing Tools > Visitors**. Under **Simulation Updates**:

- **Prewarm Visitor Models** (`prewarmVisitorModels`, default on) moves model
  preparation into loading. Turning it off shortens preparation but can restore
  first-use work during gameplay.
- **Arrival Path Cells Per Frame** (`arrivalPathCellsPerFrame`, default 256)
  limits entrance-search work per simulation update. Lower it if preparation
  frames are expensive; raise it if arrivals wait too long after major path
  changes. It changes preparation time, not which paths visitors can reach.
- **Reputation Refresh Seconds** (default 5) controls the scheduled exhibition
  recount. Increase it for fewer recounts, at the cost of slower updates when
  creatures move into or out of view.

## Staff queries and status formatting

Staff ranged-approach queries now reject candidates that cannot improve the
existing score before testing sight or clear shots. The original BFS order and
strictly better-score rule remain unchanged. Synchronous staff searches share
one set of map-sized scratch buffers instead of allocating new lists, queues and
sets for every query; the public navigation API still returns an owned list.

Civilian staff reuse strict-ground component membership for danger checks. This
cache is invalidated by the existing grid/fence revision checks and continues to
treat intact fences and closed staff gates as containment. Actual work routes
still permit authorized staff gates. No per-worker map-sized caches were added.

Unused visitor/staff status formatting left over from the removed floating labels
has been removed. Selected-object panels continue to read their live localized
status messages, so changing language and opening those panels still work.

## Measuring your park

Use Unity's CPU Profiler while playing a representative park. The following
samples separate the main runtime costs:

- `Mythologicum.Creatures`
- `Mythologicum.Visitors`
- `Mythologicum.Staff`
- `Mythologicum.InterfaceReports`
- `Mythologicum.InterfaceMaintenance`

For an arrival hitch, compare these more specific samples:

- `Mythologicum.VisitorArrival`: admission and creation of one new visitor.
- `Mythologicum.VisitorModelPreparation`: loading-time model/avatar preparation.
- `Mythologicum.ArrivalPathPreparation`: the current frame's entrance-search work.
- `Mythologicum.VisitorDecision`: an individual visitor's service/exhibit decision.
- `Mythologicum.ExhibitRecount`: the scheduled or explicitly requested exhibition
  visibility/reputation recount.

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
The entrance search reserves one reusable path list, queue and visited set sized
to the map. It does not keep a separate map-sized arrival cache per visitor.

Measured comparisons and their workload limits are recorded in
`Tests/Performance/Baselines/2026-11-13/README.md`. Remaining CPU spikes, rendering
and larger real parks should guide subsequent optimization. No additional Unity
Store package is required for this pass.

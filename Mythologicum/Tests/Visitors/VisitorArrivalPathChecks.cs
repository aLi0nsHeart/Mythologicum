using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class VisitorArrivalPathChecks
{
    private static int checks;
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); checks++; }

    private static List<Vector2Int> Complete(VisitorArrivalPaths cache, VisitorNavigation navigation,
        Vector2Int start, int budget)
    {
        var expected = navigation.Reachable(start);
        int updates = 0;
        while (!cache.Prepare(start, budget))
        {
            updates++;
            Check(!cache.TryGet(start, out var partial) && partial == null,
                "An unfinished arrival search cannot publish a partial path component");
            Check(updates <= expected.Count + 1, "Budgeted arrival preparation makes bounded progress");
        }
        Check(cache.TryGet(start, out var paths), "A completed arrival component becomes available");
        Check(paths.SequenceEqual(expected), "Budgeted arrival paths match ordinary BFS order exactly");
        Check(updates <= (Math.Max(1, expected.Count) - 1) / Math.Max(1, budget),
            "Budgeted updates finish within the component's size, including already prepared work");
        Check(ReferenceEquals(paths, cache.GetOrBuild(start)), "Direct lookups reuse the complete entrance list");
        return paths;
    }

    private static SquareBuildGrid Corridor(int width, GridHeightField terrain = null)
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, width, 1, terrain);
        for (int x = 0; x < width; x++) grid.TryOccupy(V(x, 0), BuildCellContent.Path);
        return grid;
    }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 9, 7);
        for (int y = 0; y < grid.Depth; y++)
        for (int x = 0; x < grid.Width; x++) grid.TryOccupy(V(x, y), BuildCellContent.Path);
        var fences = new FenceLayout(grid);
        int walkabilityChecks = 0;
        var navigation = new VisitorNavigation(grid, fences, cell => { walkabilityChecks++; return true; });
        var cache = new VisitorArrivalPaths(grid, fences, navigation);
        Check(!cache.TryGet(V(3, 3), out var absent) && absent == null, "An unprepared cache publishes nothing");
        Check(!cache.Prepare(V(3, 3), 1) && walkabilityChecks <= 9,
            "The first one-cell update checks only the origin and its four possible edges");
        var first = Complete(cache, navigation, V(3, 3), 1);
        Check(first.Take(5).SequenceEqual(new[] { V(3, 3), V(4, 3), V(3, 4), V(2, 3), V(3, 2) }),
            "Arrival traversal retains right/up/left/down tie order");
        int beforeCached = walkabilityChecks;
        for (int i = 0; i < 100; i++)
            Check(cache.Prepare(V(3, 3), 1) && cache.TryGet(V(3, 3), out var reused) && ReferenceEquals(first, reused),
                "Stable revisions share the same completed path list across arrivals");
        Check(walkabilityChecks == beforeCached, "Repeated arrivals do not redo path or clearance checks");
        var snapshot = first.ToArray();
        Check(cache.GetOrBuild(V(0, 0)).SequenceEqual(navigation.Reachable(V(0, 0)))
            && cache.TryGet(V(3, 3), out var stillFirst) && ReferenceEquals(first, stillFirst) && snapshot.SequenceEqual(first),
            "Off-entrance requests use ordinary BFS without displacing or modifying the bounded entrance cache");

        var random = new System.Random(81832);
        foreach (int budget in new[] { 0, -7, 1, 2, 5, 17, 512 })
        {
            var variedGrid = new SquareBuildGrid(new Vector3(-10, 0, 4), 2, 18, 13);
            var variedFences = new FenceLayout(variedGrid);
            for (int y = 0; y < variedGrid.Depth; y++)
            for (int x = 0; x < variedGrid.Width; x++)
                if (random.NextDouble() < .8 || x == 0 || y == 0) variedGrid.TryOccupy(V(x, y), BuildCellContent.Path);
            for (int y = 1; y < 12; y += 3) variedFences.TryAdd(new FenceEdge(V(6, y), false));
            var variedNavigation = new VisitorNavigation(variedGrid, variedFences);
            Complete(new VisitorArrivalPaths(variedGrid, variedFences, variedNavigation), variedNavigation, V(0, 0), budget);
        }

        var line = Corridor(12); var lineFences = new FenceLayout(line);
        var blocked = new HashSet<Vector2Int>();
        var lineNavigation = new VisitorNavigation(line, lineFences, cell => !blocked.Contains(cell));
        var lineCache = new VisitorArrivalPaths(line, lineFences, lineNavigation);
        Complete(lineCache, lineNavigation, V(0, 0), 3);
        var edge = new FenceEdge(V(6, 0), false);
        Check(lineFences.TryAdd(edge) && !lineCache.TryGet(V(0, 0), out _), "A new fence immediately invalidates cached connectivity");
        Check(Complete(lineCache, lineNavigation, V(0, 0), 2).Count == 6, "Closed fences split the arrival component");
        Check(lineFences.TrySetGate(edge) && !lineCache.TryGet(V(0, 0), out _), "Adding a staff gate invalidates the fence revision");
        Check(Complete(lineCache, lineNavigation, V(0, 0), 2).Count == 12, "Visitors regain authorized path access through a gate");
        Check(lineFences.TryRemove(edge) && !lineCache.TryGet(V(0, 0), out _), "Fence removal also invalidates cached geometry");
        Complete(lineCache, lineNavigation, V(0, 0), 4);
        Check(line.TryRemove(V(4, 0), BuildCellContent.Path) && !lineCache.TryGet(V(0, 0), out _), "Path demolition invalidates the grid revision");
        Check(Complete(lineCache, lineNavigation, V(0, 0), 1).Count == 4, "Disconnected paths are absent after demolition");
        Check(line.TryOccupy(V(4, 0), BuildCellContent.Path), "The missing path can be reconstructed");
        Check(Complete(lineCache, lineNavigation, V(0, 0), 1).Count == 12, "New paths reconnect the complete arrival component");
        blocked.Add(V(4, 0)); line.NotifyPhysicalChange();
        Check(!lineCache.TryGet(V(0, 0), out _), "Physical-clearance notifications invalidate arrival paths");
        Check(Complete(lineCache, lineNavigation, V(0, 0), 2).Count == 4, "A physical obstacle blocks the same cells as normal visitor navigation");
        blocked.Clear(); line.NotifyPhysicalChange();
        Complete(lineCache, lineNavigation, V(0, 0), 3);
        line.NotifyPhysicalChange(); Check(!lineCache.Prepare(V(0, 0), 2), "Preparation can begin before a new construction revision");
        lineFences.TryAdd(edge);
        Check(Complete(lineCache, lineNavigation, V(0, 0), 1).Count == 6,
            "A revision during an unfinished search restarts instead of retaining stale discovered cells");
        Check(!lineCache.Prepare(V(11, 0), 1) && !lineCache.TryGet(V(0, 0), out _), "A moved entrance replaces the single prepared component");
        var switched = Complete(lineCache, lineNavigation, V(11, 0), 2);
        Check(switched[0] == V(11, 0) && switched.Count == 6, "The switched entrance has its own exact BFS origin and component");
        Check(Complete(lineCache, lineNavigation, V(-1, 0), 1).Count == 0, "An out-of-map entrance completes safely with no paths");
        line.TryRemove(V(0, 0), BuildCellContent.Path);
        Check(Complete(lineCache, lineNavigation, V(0, 0), 1).Count == 0, "An entrance without a path completes safely with an empty component");

        var terrain = new GridHeightField(12, 1, 2); var slopeGrid = Corridor(12, terrain);
        var slopeFences = new FenceLayout(slopeGrid); var slopeNavigation = new VisitorNavigation(slopeGrid, slopeFences);
        var slopeCache = new VisitorArrivalPaths(slopeGrid, slopeFences, slopeNavigation);
        Check(slopeCache.GetOrBuild(V(0, 0)).Count == 12, "A direct initial request synchronously prepares its entrance");
        var heights = new float[26];
        for (int y = 0; y <= 1; y++) for (int x = 6; x <= 12; x++) heights[y * 13 + x] = 20;
        terrain.ImportHeights(heights);
        Check(!slopeCache.TryGet(V(0, 0), out _), "Terrain edits invalidate the arrival cache through the grid revision");
        Check(Complete(slopeCache, slopeNavigation, V(0, 0), 1).Count == 5, "Steep path slopes remain impassable");
        Check(Complete(slopeCache, slopeNavigation, V(5, 0), 1).Count == 0, "An entrance on an impassable slope yields no paths");
        terrain.ImportHeights(new float[26]);
        Check(slopeCache.GetOrBuild(V(5, 0)).Count == 12, "Direct calls synchronously refresh terrain-invalidated cached paths");

        var largeGrid = new SquareBuildGrid(Vector3.zero, 2, 72, 72);
        for (int y = 0; y < largeGrid.Depth; y++) for (int x = 0; x < largeGrid.Width; x++)
            largeGrid.TryOccupy(V(x, y), BuildCellContent.Path);
        var largeFences = new FenceLayout(largeGrid); var largeNavigation = new VisitorNavigation(largeGrid, largeFences);
        var largeCache = new VisitorArrivalPaths(largeGrid, largeFences, largeNavigation);
        var all = Complete(largeCache, largeNavigation, V(0, 0), 17);
        Check(all.Count == 72 * 72, "A large park publishes its entire connected network after bounded updates");
        largeGrid.NotifyPhysicalChange();
        Complete(largeCache, largeNavigation, V(0, 0), 17);
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int run = 0; run < 3; run++)
        {
            largeGrid.NotifyPhysicalChange();
            while (!largeCache.Prepare(V(0, 0), 17)) { }
            largeCache.TryGet(V(0, 0), out _);
        }
        long allocations = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        Check(allocations == 0, "Reused BFS buffers allocate nothing while preparing a large arrival component");
        Console.WriteLine("PASS: " + checks + " visitor arrival BFS order, budgets, cache reuse, revision and allocation checks.");
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mythologicum;
using UnityEngine;

/// <summary>Differential checks against the pre-optimization route/occupancy implementations.</summary>
public static class RouteSearchPerformanceChecks
{
    private static int checks;
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }

    public static void Main()
    {
        CheckOccupancy();
        CheckTerrainCenters();
        CheckReachability();
        var random = new System.Random(93175);
        for (int layout = 0; layout < 80; layout++)
        {
            const int width = 18, depth = 14;
            var blocked = new bool[width * depth];
            for (int i = 0; i < blocked.Length; i++) blocked[i] = random.NextDouble() < .24;
            bool Allowed(Vector2Int node) => node.x >= 0 && node.y >= 0 && node.x < width && node.y < depth
                && !blocked[node.y * width + node.x];
            IEnumerable<Vector2Int> Neighbors(Vector2Int node)
            {
                foreach (var direction in Directions) yield return node + direction;
            }
            float Cost(Vector2Int a, Vector2Int b)
            {
                int value = (a.x * 11 + a.y * 23 + b.x * 3 + b.y * 7) & 31;
                if (layout % 4 == 0) return 1;
                if (layout % 4 == 3 && value == 0) return float.NaN;
                if (layout % 4 == 3 && value == 1) return float.PositiveInfinity;
                if (layout % 4 == 3 && value == 2) return .5f;
                return 1 + value % 4 * .25f;
            }
            for (int query = 0; query < 16; query++)
            {
                var start = new Vector2Int(random.Next(width), random.Next(depth));
                var goal = query == 0 ? start : new Vector2Int(random.Next(width), random.Next(depth));
                Func<Vector2Int, Vector2Int, int> estimate = layout % 4 == 2 ? (a, b) => 0 : Distance;
                var expected = ReferenceFind(start, goal, Neighbors, Allowed, estimate, Cost);
                var actual = ConstructionRouteFinder.Find(start, goal, Neighbors, Allowed, estimate, Cost);
                Check(expected.SequenceEqual(actual), "The complete route and equal-cost tie choices changed for layout " + layout + ", query " + query);
            }
        }

        // A wide, unreachable search stresses insertion/pop work instead of returning
        // immediately at a nearby goal. Record CPU and allocation data, without a noisy
        // wall-clock pass/fail threshold.
        const int side = 42, iterations = 12;
        var source = new Vector2Int(0, 0); var target = new Vector2Int(side - 1, side - 1);
        IEnumerable<Vector2Int> WideNeighbors(Vector2Int node)
        {
            foreach (var direction in Directions)
            {
                var next = node + direction;
                if (next.x >= 0 && next.y >= 0 && next.x < side && next.y < side
                    && next != target) yield return next;
            }
        }
        Func<List<Vector2Int>> reference = () => ReferenceFind(source, target, WideNeighbors, _ => true, Distance);
        Func<List<Vector2Int>> optimized = () => ConstructionRouteFinder.Find(source, target, WideNeighbors, _ => true, Distance);
        reference(); optimized();
        var oldMeasurement = Measure(reference, iterations);
        var newMeasurement = Measure(optimized, iterations);
        Check(newMeasurement.bytes < oldMeasurement.bytes, "Heap search must allocate less than the prior sorted-tree search on a wide frontier");
        Console.WriteLine("Wide route search (" + iterations + " unreachable 42x42 queries): old=" + oldMeasurement.ms.ToString("0.00")
            + " ms / " + oldMeasurement.bytes + " B, new=" + newMeasurement.ms.ToString("0.00") + " ms / " + newMeasurement.bytes + " B.");
        Console.WriteLine("PASS: " + checks + " route/occupancy compatibility and allocation checks.");
    }

    private static void CheckReachability()
    {
        var random = new System.Random(7513);
        for (int layout = 0; layout < 24; layout++)
        {
            var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 10);
            var fences = new FenceLayout(grid);
            for (int y = 0; y < grid.Depth; y++) for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                int kind = random.Next(6);
                if (kind == 0) grid.TryOccupy(cell, BuildCellContent.Building);
                else if (kind < 4) grid.TryOccupy(cell, BuildCellContent.Path);
                if (x > 0 && random.Next(5) == 0)
                {
                    var edge = new FenceEdge(cell, false);
                    fences.TryAdd(edge);
                    if (random.Next(3) == 0) fences.TrySetGate(edge);
                }
            }
            var start = new Vector2Int(random.Next(grid.Width), random.Next(grid.Depth));
            var creatures = new CreatureNavigation(grid, fences);
            var visitors = new VisitorNavigation(grid, fences);
            var staff = new StaffNavigation(grid, fences);
            foreach (bool mode in new[] { false, true })
            {
                Check(OldReachable(start, cell => visitors.IsWalkable(cell, mode),
                    (a, b) => visitors.CanCross(a, b, mode)).SequenceEqual(visitors.Reachable(start, mode)),
                    "Visitor reachable cells and BFS order changed");
                Check(OldReachable(start, cell => staff.IsWalkable(cell, mode),
                    (a, b) => staff.CanCross(a, b, mode)).SequenceEqual(staff.Reachable(start, mode)),
                    "Staff reachable cells and BFS order changed");
            }
            foreach (int budget in new[] { 0, 1, 3, 100 })
                Check(OldCreatureReachable(creatures, start, budget).SequenceEqual(creatures.Reachable(start, budget)),
                    "Creature reachable cells, depth budget or BFS order changed");
        }

        var wideGrid = new SquareBuildGrid(Vector3.zero, 2, 42, 42);
        var wideVisitor = new VisitorNavigation(wideGrid, new FenceLayout(wideGrid));
        var source = new Vector2Int(0, 0);
        Func<List<Vector2Int>> oldQuery = () => OldReachable(source, cell => wideVisitor.IsWalkable(cell, true),
            (a, b) => wideVisitor.CanCross(a, b, true));
        Func<List<Vector2Int>> newQuery = () => wideVisitor.Reachable(source, true);
        oldQuery(); newQuery();
        var before = Measure(oldQuery, 12); var after = Measure(newQuery, 12);
        Check(after.bytes < before.bytes, "Direct BFS expansion must remove per-cell neighbor iterator allocations");
        Console.WriteLine("Reachable BFS (12 open 42x42 queries): old=" + before.ms.ToString("0.00") + " ms / " + before.bytes
            + " B, new=" + after.ms.ToString("0.00") + " ms / " + after.bytes + " B.");
    }

    private static IEnumerable<Vector2Int> OldNeighbors(Vector2Int cell, Func<Vector2Int, Vector2Int, bool> cross)
    {
        foreach (var direction in Directions)
        {
            var next = cell + direction;
            if (cross(cell, next)) yield return next;
        }
    }
    private static List<Vector2Int> OldReachable(Vector2Int start, Predicate<Vector2Int> allowed,
        Func<Vector2Int, Vector2Int, bool> cross)
    {
        var result = new List<Vector2Int>();
        if (!allowed(start)) return result;
        var visited = new HashSet<Vector2Int> { start };
        var pending = new Queue<Vector2Int>(); pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var cell = pending.Dequeue(); result.Add(cell);
            foreach (var next in OldNeighbors(cell, cross))
                if (visited.Add(next)) pending.Enqueue(next);
        }
        return result;
    }
    private static List<Vector2Int> OldCreatureReachable(CreatureNavigation navigation, Vector2Int start, int budget)
    {
        var result = new List<Vector2Int>();
        if (!navigation.IsWalkable(start)) return result;
        var pending = new Queue<Vector2Int>(); var depth = new Dictionary<Vector2Int, int> { [start] = 0 };
        pending.Enqueue(start);
        while (pending.Count > 0)
        {
            var cell = pending.Dequeue(); result.Add(cell);
            if (depth[cell] >= budget) continue;
            foreach (var next in navigation.Neighbors(cell))
            {
                if (depth.ContainsKey(next)) continue;
                depth.Add(next, depth[cell] + 1); pending.Enqueue(next);
            }
        }
        return result;
    }

    private static void CheckTerrainCenters()
    {
        // Include non-integral origin/cell sizes so cached sampling also preserves
        // the original world-to-terrain floating point conversion exactly.
        var terrain = new GridHeightField(14, 11, 1.7f, maximumEarthwork: 20);
        var grid = new SquareBuildGrid(new Vector3(-19.35f, 3.4f, 12.81f), 1.7f, 14, 11, terrain);
        var nav = new CreatureNavigation(grid, new FenceLayout(grid));
        void Compare()
        {
            for (int y = -1; y <= grid.Depth; y++) for (int x = -1; x <= grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                Check(grid.CellCenter(cell).Equals(UncachedCenter(grid, cell)), "Cached centre changed a terrain position or outside-map clamping");
                Check(grid.CellCenter(cell).Equals(UncachedCenter(grid, cell)), "Repeated centre query must reuse the same exact position");
                if (grid.Contains(cell) && x + 1 < grid.Width)
                    Check(grid.MovementCost(cell, cell + Vector2Int.right) == UncachedCost(grid, cell, cell + Vector2Int.right),
                        "Caching changed weighted movement cost");
            }
            var start = new Vector2Int(1, 1); var goal = new Vector2Int(12, 9);
            Check(nav.FindPath(start, goal).SequenceEqual(ReferenceFind(start, goal, nav.Neighbors, nav.IsWalkable, Distance,
                (a, b) => UncachedCost(grid, a, b))), "Terrain caching changed the exact weighted route");
        }
        Compare();
        terrain.Generate(73631, .8f, 15); Compare();
        terrain.Generate(28119, 1.1f, 12); Compare();
        Check(terrain.TryEdit(grid, new Vector2(3, 3), 3, .05f, TerrainBrushMode.Raise, 0, out _),
            "Terrain cache test requires a successful brush edit");
        Compare();
        var shape = new BuildingFootprint(new BuildingDefinition { width = 2, depth = 2, entranceColumn = 1 }, new Vector2Int(5, 4), 0);
        Check(terrain.TryPlanFoundation(shape, grid, out var plan), "Terrain cache test requires a valid flattening plan");
        terrain.Apply(plan); Compare();
        var heights = terrain.Capture().heights;
        for (int i = 0; i < heights.Length; i++) heights[i] += .35f;
        terrain.ImportHeights(heights); Compare();
        var save = terrain.Capture(); save.heights[5] += .2f;
        terrain.Restore(save); Compare();
        float oldSum = 0, newSum = 0;
        var before = Measure(() =>
        {
            for (int y = 0; y < grid.Depth; y++) for (int x = 0; x < grid.Width; x++)
                oldSum += UncachedCenter(grid, new Vector2Int(x, y)).y;
            return null;
        }, 1000);
        var after = Measure(() =>
        {
            for (int y = 0; y < grid.Depth; y++) for (int x = 0; x < grid.Width; x++)
                newSum += grid.CellCenter(new Vector2Int(x, y)).y;
            return null;
        }, 1000);
        Check(oldSum == newSum, "Cached terrain reads must preserve the same sum without rounding changes");
        Console.WriteLine("Terrain centre reads (154,000 repeated positions): old=" + before.ms.ToString("0.00")
            + " ms, new=" + after.ms.ToString("0.00") + " ms.");
        var flat = new SquareBuildGrid(new Vector3(-1, 7, -2), 2, 3, 3);
        Check(flat.CellCenter(new Vector2Int(2, 1)).Equals(UncachedCenter(flat, new Vector2Int(2, 1))), "No-terrain positions remain unchanged");
    }
    private static Vector3 UncachedCenter(SquareBuildGrid grid, Vector2Int cell)
    {
        var point = grid.Origin + new Vector3((cell.x + .5f) * grid.CellSize, 0, (cell.y + .5f) * grid.CellSize);
        point.y = grid.GroundHeight(point); return point;
    }
    private static float UncachedCost(SquareBuildGrid grid, Vector2Int from, Vector2Int to)
    {
        float rise = UncachedCenter(grid, to).y - UncachedCenter(grid, from).y;
        return (float)Math.Sqrt(1f + rise * rise / (grid.CellSize * grid.CellSize))
            + Math.Max(0, rise) / grid.CellSize * (grid.Terrain?.UphillCostMultiplier ?? 0f);
    }

    private static (double ms, long bytes) Measure(Func<List<Vector2Int>> search, int count)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < count; i++) search();
        clock.Stop();
        return (clock.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void CheckOccupancy()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 21, 16);
        var previous = new Dictionary<Vector2Int, BuildCellContent>();
        var random = new System.Random(49163);
        int revision = 0;
        for (int i = 0; i < 6000; i++)
        {
            var cell = new Vector2Int(random.Next(-2, 23), random.Next(-2, 18));
            var content = (BuildCellContent)random.Next(3);
            bool inside = cell.x >= 0 && cell.y >= 0 && cell.x < 21 && cell.y < 16;
            bool expected;
            if (i % 2 == 0)
            {
                expected = content != BuildCellContent.Empty && inside && !previous.ContainsKey(cell);
                if (expected) previous.Add(cell, content);
                Check(grid.TryOccupy(cell, content) == expected, "Occupancy acceptance changed");
            }
            else
            {
                expected = content != BuildCellContent.Empty && previous.TryGetValue(cell, out var old) && old == content;
                if (expected) previous.Remove(cell);
                Check(grid.TryRemove(cell, content) == expected, "Removal acceptance changed");
            }
            if (expected) revision++;
            Check(grid.ContentAt(cell) == (previous.TryGetValue(cell, out var value) ? value : BuildCellContent.Empty), "Cell content changed");
            Check(grid.IsAvailable(cell) == (inside && !previous.ContainsKey(cell)), "Availability changed");
            Check(grid.Revision == revision, "Failed occupancy operations must not change revision");
        }
        Check(grid.ContentAt(new Vector2Int(int.MaxValue, int.MaxValue)) == BuildCellContent.Empty
            && grid.ContentAt(new Vector2Int(int.MinValue, 0)) == BuildCellContent.Empty, "Outside-map lookup must remain empty");
    }

    private static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    private readonly struct Entry<T>
    {
        public readonly T Node;
        public readonly float Cost, Priority;
        public readonly int Order;
        public Entry(T node, float cost, float priority, int order)
        { Node = node; Cost = cost; Priority = priority; Order = order; }
    }

    // Frozen implementation used before this performance change; keep it independent
    // of the optimized frontier so differential checks can detect ordering regressions.
    private static List<T> ReferenceFind<T>(T start, T goal, Func<T, IEnumerable<T>> neighbors,
        Predicate<T> canBuild, Func<T, T, int> estimate, Func<T, T, float> traversalCost = null)
    {
        var allowed = new Dictionary<T, bool>();
        bool CanVisit(T node)
        {
            if (!allowed.TryGetValue(node, out bool valid)) allowed[node] = valid = canBuild(node);
            return valid;
        }
        if (!CanVisit(start) || !CanVisit(goal)) return new List<T>();
        var frontier = new SortedSet<Entry<T>>(Comparer<Entry<T>>.Create((a, b) =>
        {
            int comparison = a.Priority.CompareTo(b.Priority);
            return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
        }));
        var cost = new Dictionary<T, float> { [start] = 0 };
        var previous = new Dictionary<T, T>();
        int order = 0;
        frontier.Add(new Entry<T>(start, 0, estimate(start, goal), order++));
        while (frontier.Count > 0)
        {
            Entry<T> current = frontier.Min;
            frontier.Remove(current);
            if (cost[current.Node] != current.Cost) continue;
            if (EqualityComparer<T>.Default.Equals(current.Node, goal))
            {
                var result = new List<T> { goal };
                T node = goal;
                while (!EqualityComparer<T>.Default.Equals(node, start))
                { node = previous[node]; result.Add(node); }
                result.Reverse(); return result;
            }
            foreach (T next in neighbors(current.Node))
            {
                if (!CanVisit(next)) continue;
                float step = traversalCost?.Invoke(current.Node, next) ?? 1f;
                if (float.IsNaN(step) || float.IsInfinity(step) || step < 1f) continue;
                float nextCost = current.Cost + step;
                if (cost.TryGetValue(next, out float oldCost) && oldCost <= nextCost) continue;
                cost[next] = nextCost; previous[next] = current.Node;
                frontier.Add(new Entry<T>(next, nextCost, nextCost + estimate(next, goal), order++));
            }
        }
        return new List<T>();
    }
}

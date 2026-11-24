using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEngine;

public static class StaffReachableBufferChecks
{
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static List<Vector2Int> Reference(StaffNavigation navigation, Vector2Int start, bool pathsOnly)
    {
        var result = new List<Vector2Int>();
        if (!navigation.IsWalkable(start, pathsOnly)) return result;
        var visited = new HashSet<Vector2Int> { start };
        var frontier = new Queue<Vector2Int>(); frontier.Enqueue(start);
        while (frontier.Count > 0)
        {
            var cell = frontier.Dequeue(); result.Add(cell);
            foreach (var direction in Directions)
            {
                var next = cell + direction;
                if (navigation.CanCross(cell, next, pathsOnly) && visited.Add(next)) frontier.Enqueue(next);
            }
        }
        return result;
    }
    private static void Equal(List<Vector2Int> actual, List<Vector2Int> expected, string reason)
    {
        Check(actual.Count == expected.Count, reason + " count");
        for (int i = 0; i < expected.Count; i++) Check(actual[i] == expected[i], reason + " exact order");
    }
    public static void Main()
    {
        var random = new System.Random(81053);
        var result = new List<Vector2Int>(120);
        var pending = new Queue<Vector2Int>(120);
        var visited = new HashSet<Vector2Int>(120);
        for (int scenario = 0; scenario < 40; scenario++)
        {
            var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 10);
            var fences = new FenceLayout(grid);
            var blocked = new HashSet<Vector2Int>();
            for (int y = 0; y < grid.Depth; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (random.NextDouble() < .12) grid.TryOccupy(cell, BuildCellContent.Building);
                else if (random.NextDouble() < .65) grid.TryOccupy(cell, BuildCellContent.Path);
                if (random.NextDouble() < .06) blocked.Add(cell);
                if (x > 0 && random.NextDouble() < .17)
                {
                    var edge = new FenceEdge(cell, false); fences.TryAdd(edge, (FenceKind)random.Next(4));
                    if (random.NextDouble() < .3) fences.TrySetGate(edge);
                }
                if (y > 0 && random.NextDouble() < .17)
                {
                    var edge = new FenceEdge(cell, true); fences.TryAdd(edge, (FenceKind)random.Next(4));
                    if (random.NextDouble() < .3) fences.TrySetGate(edge);
                }
            }
            var navigation = new StaffNavigation(grid, fences, cell => !blocked.Contains(cell));
            for (int query = 0; query < 10; query++)
            foreach (bool pathsOnly in new[] { false, true })
            {
                var start = query == 0 ? new Vector2Int(-1, 0) : query == 1 ? new Vector2Int(12, 10)
                    : new Vector2Int(random.Next(grid.Width), random.Next(grid.Depth));
                // Dirty caller buffers must be overwritten rather than appended or retained.
                result.Add(new Vector2Int(-99, -99)); pending.Enqueue(new Vector2Int(-99, -99)); visited.Add(new Vector2Int(-99, -99));
                navigation.FillReachable(start, pathsOnly, result, pending, visited);
                Equal(result, Reference(navigation, start, pathsOnly), "Reused work buffers preserve reference BFS");
                Check(pending.Count == 0, "A completed or invalid-start search leaves no pending work");
            }
            var first = navigation.Reachable(new Vector2Int(0, 0));
            var snapshot = first.ToArray();
            var second = navigation.Reachable(new Vector2Int(11, 9));
            Check(!ReferenceEquals(first, second), "Public Reachable returns independently owned lists");
            Check(first.Count == snapshot.Length, "Another public search does not resize an earlier result");
            for (int i = 0; i < snapshot.Length; i++) Check(first[i] == snapshot[i], "Another public search does not overwrite an earlier result");
            blocked.Clear(); grid.NotifyPhysicalChange();
            navigation.FillReachable(new Vector2Int(0, 0), false, result, pending, visited);
            Equal(result, Reference(navigation, new Vector2Int(0, 0), false), "Current physical clearance is observed without stale reachability");
        }
        var allocationGrid = new SquareBuildGrid(Vector3.zero, 2, 12, 10);
        var allocationFences = new FenceLayout(allocationGrid);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 12; x++) allocationGrid.TryOccupy(new Vector2Int(x, y), BuildCellContent.Path);
        var allocationNavigation = new StaffNavigation(allocationGrid, allocationFences);
        for (int i = 0; i < 24; i++) allocationNavigation.FillReachable(new Vector2Int(i % 12, i % 10), false, result, pending, visited);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) allocationNavigation.FillReachable(new Vector2Int(i % 12, i % 10), false, result, pending, visited);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "Warmed caller-owned BFS buffers allocate no managed memory on supported Mono");
        Console.WriteLine("PASS: " + checks + " staff reachability buffer, exact order, ownership and allocation checks.");
    }
}

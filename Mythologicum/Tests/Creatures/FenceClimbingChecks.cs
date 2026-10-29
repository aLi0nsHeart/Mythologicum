using System;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class FenceClimbingChecks
{
    private static int passed;
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 4, 4);
        var layout = new FenceLayout(grid);
        var walking = new CreatureNavigation(grid, layout);
        var climbing = new CreatureNavigation(grid, layout, null, true);
        var edge = new FenceEdge(V(2, 1), false);
        Check(!layout.CanClimb(edge), "Missing fencing is not a climbing target.");
        Check(layout.TryAdd(edge), "Old callers still create wooden fences by default.");
        Check(layout.KindAt(edge) == FenceKind.Wooden && layout.CanClimb(edge), "Wooden fences support climbing.");
        Check(layout.Edges.Count() == 1, "Placed barriers are available to obstacle detection.");
        Check(!walking.CanCross(V(1, 1), V(2, 1)), "Ordinary creatures cannot cross wood.");
        Check(climbing.CanCross(V(1, 1), V(2, 1)), "Climbing navigation plans across wood.");
        Check(climbing.CanCross(V(2, 1), V(1, 1)), "Climbing works in both directions.");
        Check(climbing.FindPath(V(1, 1), V(2, 1)).Count == 2, "Climbing route includes the direct fence crossing.");
        Check(walking.FindPath(V(1, 1), V(2, 1)).Count == 4, "Walking retains the detour.");
        int revision = layout.Revision;
        Check(layout.TrySetKind(edge, FenceKind.Spiked), "A wooden fence can be upgraded in place.");
        Check(layout.Revision == revision + 1, "Changing fence type invalidates cached navigation.");
        Check(!layout.TrySetKind(edge, FenceKind.Spiked), "An unchanged type does not mutate layout.");
        Check(!layout.CanClimb(edge) && !climbing.CanCross(V(1, 1), V(2, 1)), "Spikes stop climbers.");
        Check(!walking.CanCross(V(1, 1), V(2, 1)), "Spikes also stop ordinary walkers.");
        Check(climbing.HasLineOfSight(V(1, 1), V(2, 1)), "Spikes do not hide nearby prey.");
        Check(layout.TrySetGate(edge) && layout.KindAt(edge)==FenceKind.Spiked, "Spiked gates retain spike protection.");
        Check(layout.TryRemove(edge) && layout.TryAdd(edge,FenceKind.Wooden) && layout.TrySetGate(edge), "Wooden fence conversion to gate remains supported.");
        Check(!layout.CanClimb(edge) && !climbing.CanCross(V(1, 1), V(2, 1)), "Closed gates cannot be climbed.");
        Check(!layout.TrySetKind(edge, FenceKind.Spiked) && layout.IsGate(edge), "Type replacement cannot erase a gate.");
        Check(layout.TryRemove(edge) && !layout.IsGate(edge), "Demolition clears the gate flag.");
        Check(layout.TryAdd(edge, FenceKind.Spiked) && layout.KindAt(edge) == FenceKind.Spiked, "Explicit spike construction is supported.");
        Check(layout.TryRemove(edge) && layout.TryAdd(edge) && layout.CanClimb(edge), "Demolition clears type before default wooden rebuilding.");
        grid.TryOccupy(V(2, 1));
        Check(!climbing.CanCross(V(1, 1), V(2, 1)), "Climbing never bypasses building occupancy.");

        var wallGrid = new SquareBuildGrid(Vector3.zero, 2f, 4, 4);
        var wall = new FenceLayout(wallGrid);
        for (int y = 0; y < 4; y++) wall.TryAdd(new FenceEdge(V(2, y), false), FenceKind.Spiked);
        var wallNav = new CreatureNavigation(wallGrid, wall, null, true);
        Check(wallNav.FindPath(V(0, 1), V(3, 1)).Count == 0, "A continuous spiked wall blocks climbing routes.");
        wall.TrySetKind(new FenceEdge(V(2, 1), false), FenceKind.Wooden);
        Check(wallNav.FindPath(V(0, 1), V(3, 1)).Count == 4, "One wooden section provides a climbable route.");
        Console.WriteLine("Fence climbing: " + passed + " checks passed.");
    }
}

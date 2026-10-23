using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEngine;

public static class CreatureNavigationTests
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
        var grid = new SquareBuildGrid(Vector3.zero, 2, 7, 7);
        var fences = new FenceLayout(grid);
        var nav = new CreatureNavigation(grid, fences);
        Check(nav.FindPath(V(0,0), V(6,6)).Count == 13, "Free ground must not require paths or enclosure membership.");
        Check(nav.Reachable(V(3,3), 100).Count == 49, "Animals must roam across all free map ground.");
        Check(nav.Reachable(V(3,3), 0).Count == 1, "A zero-step budget contains only its starting cell.");
        Check(!nav.IsWalkable(V(-1,0)) && nav.FindPath(V(0,0), V(7,0)).Count == 0,
            "Map boundaries must block movement.");
        grid.TryOccupy(V(0,0), BuildCellContent.Path);
        Check(nav.IsWalkable(V(0,0)), "Animals may cross built path cells.");

        var between = new FenceEdge(V(3,2), false);
        fences.TryAdd(between);
        List<Vector2Int> detour = nav.FindPath(V(2,2), V(3,2));
        Check(detour.Count == 4, "A single fence must force a cardinal detour.");
        for (int i = 1; i < detour.Count; i++)
            Check(nav.CanCross(detour[i-1], detour[i]), "Routes must contain only traversable edges.");
        Check(!nav.CanCross(V(2,2), V(3,2)), "Crossing a fence is forbidden.");
        fences.TrySetGate(between);
        Check(!nav.CanCross(V(2,2), V(3,2)), "Closed staff gates must contain animals.");
        Check(nav.HasLineOfSight(V(2,2), V(3,2)), "Animals can see each other through wooden fences and gates.");
        fences.TryRemove(between);
        Check(nav.FindPath(V(2,2), V(3,2)).Count == 2, "Demolishing a barrier must immediately open movement.");

        for (int y = 0; y < grid.Depth; y++) fences.TryAdd(new FenceEdge(V(4,y), false));
        Check(nav.FindPath(V(1,3), V(6,3)).Count == 0, "Prey beyond a sealed barrier is unreachable.");
        Check(nav.HasLineOfSight(V(1,3), V(6,3)), "Sealed fencing must not obscure prey detection.");
        Check(nav.Reachable(V(1,3), 100).Count == 28, "Reachable cells must stay on their side of a fence wall.");
        fences.TryRemove(new FenceEdge(V(4,3), false));
        Check(nav.FindPath(V(1,3), V(6,3)).Count > 0, "Opening a fence gap must make the opposite side reachable.");

        grid.TryOccupy(V(2,3));
        Check(!nav.HasLineOfSight(V(1,3), V(3,3)), "Buildings must obscure line of sight.");
        Check(!nav.HasLineOfSight(V(3,3), V(1,3)), "Building occlusion must be symmetric.");
        Check(!nav.IsWalkable(V(2,3)), "Building footprints block animal movement.");
        grid.TryOccupy(V(1,0));
        Check(!nav.HasLineOfSight(V(0,0), V(2,2)), "Supercover rays must not see through building corners.");
        Check(!nav.HasLineOfSight(V(2,2), V(0,0)), "Corner occlusion must also be symmetric.");
        Check(nav.HasLineOfSight(V(0,1), V(0,6)), "Clear vertical vision must remain available.");
        Check(nav.HasLineOfSight(V(0,6), V(0,1)), "Vertical line of sight must work in both directions.");
        Check(nav.HasLineOfSight(V(4,6), V(6,6)), "Clear horizontal vision must remain available.");
        Check(nav.HasLineOfSight(V(2,2), V(2,2)), "Creatures can see within their own clear cell.");

        var blockedCell = V(5,5);
        var staticObstacles = new CreatureNavigation(grid, fences, cell => cell != blockedCell);
        Check(!staticObstacles.IsWalkable(blockedCell), "Physical static obstacles must block movement.");
        Check(!staticObstacles.HasLineOfSight(V(4,5), V(6,5)), "Physical static obstacles must obscure vision.");
        Check(!staticObstacles.CanCross(V(4,4), V(5,5)), "Diagonal corner cutting is forbidden.");
        Console.WriteLine("Creature navigation: " + passed + " checks passed.");
    }
}

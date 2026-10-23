using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEngine;

public static class VisitorNavigationChecks
{
    private static int checks;
    private static Vector2Int V(int x, int y) => new Vector2Int(x,y);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 7, 5);
        var fences = new FenceLayout(grid);
        var nav = new VisitorNavigation(grid, fences);
        Check(!nav.IsWalkable(V(1,1)), "Normal guests cannot walk on natural ground");
        Check(nav.IsWalkable(V(1,1), true), "Frightened guests can leave paths onto clear ground");
        Check(nav.FindPath(V(1,1), V(1,1)).Count == 0, "Invalid off-path start rejected");
        Check(nav.Reachable(V(1,1)).Count == 0, "Off-path cell has no normal reachable network");
        for (int x = 0; x < 7; x++) grid.TryOccupy(V(x,2), BuildCellContent.Path);
        var route = nav.FindPath(V(0,2), V(6,2));
        Check(route.Count == 7 && route[0] == V(0,2) && route[6] == V(6,2), "Path route includes both endpoints");
        Check(nav.Reachable(V(0,2)).Count == 7, "Normal reachable area only contains paths");
        Check(nav.FindPath(V(3,2), V(3,2)).Count == 1, "Valid identical endpoints return one cell");
        Check(nav.FindPath(V(3,0), V(3,2)).Count == 0, "Off-path return requires deliberate ground routing");
        Check(nav.FindPath(V(3,0), V(3,2), true).Count == 3, "Ground routing can recover back to a reachable path");
        Check(nav.Reachable(V(0,2), true).Count == 35, "Ground fleeing can reach all clear cells");
        Check(!nav.IsWalkable(V(-1,0), true) && !nav.IsWalkable(V(7,0)), "Map boundaries enforced in both modes");
        Check(!nav.CanCross(V(1,1), V(2,2), true), "No diagonal corner cutting");
        Check(!nav.CanCross(V(1,2), V(3,2)), "No teleporting across two tiles");
        Check(!nav.CanCross(V(1,2), V(1,2)), "Crossing requires distinct adjacent cells");

        var edge = new FenceEdge(V(3,2), false);
        fences.TryAdd(edge);
        Check(nav.FindPath(V(0,2), V(6,2)).Count == 0, "Wooden fence breaks normal path connectivity");
        Check(!nav.CanCross(V(2,2), V(3,2), true), "Ground fleeing cannot cross wooden fence");
        fences.TrySetGate(edge);
        Check(nav.FindPath(V(0,2), V(6,2)).Count == 7, "Authorized gate permits normal path traffic");
        Check(!nav.CanCross(V(2,2), V(3,2), true), "Panic does not open a closed physical gate");
        fences.TryRemove(edge); fences.TryAdd(edge, FenceKind.Spiked);
        Check(!nav.CanCross(V(2,2), V(3,2)) && !nav.CanCross(V(2,2), V(3,2), true), "Spiked fence blocks both visitor modes");
        fences.TryRemove(edge);
        Check(nav.CanCross(V(2,2), V(3,2)), "Demolition immediately restores path crossing");

        for (int y = 0; y < 5; y++) fences.TryAdd(new FenceEdge(V(4,y), false));
        Check(nav.FindPath(V(1,2), V(6,2), true).Count == 0, "Panic cannot escape around a fence wall outside map bounds");
        Check(nav.Reachable(V(1,2), true).Count == 20, "Ground reachable area respects enclosing barriers");
        fences.TryRemove(new FenceEdge(V(4,1), false));
        route = nav.FindPath(V(1,2), V(6,2), true);
        Check(route.Count > 0, "Open fence gap allows ground detour");
        for (int i = 1; i < route.Count; i++)
            Check(nav.CanCross(route[i-1], route[i], true), "Every route step traverses an allowed cardinal edge");
        Check(nav.FindPath(V(1,2), V(6,2)).Count == 0, "Off-path gap does not connect normal path network");

        grid.TryOccupy(V(1,1), BuildCellContent.Building);
        Check(!nav.IsWalkable(V(1,1), true), "Buildings block fleeing on ground");
        var blocked = new VisitorNavigation(grid, fences, cell => cell != V(2,2));
        Check(!blocked.IsWalkable(V(2,2)) && !blocked.IsWalkable(V(2,2), true), "Physical obstacles block both navigation modes");
        Check(blocked.FindPath(V(1,2), V(2,2), true).Count == 0, "Physical blocked goal cannot be reached");
        Check(!blocked.CanCross(V(2,2), V(3,2), true), "Movement cannot start in physically blocked cell");
        Console.WriteLine("PASS: " + checks + " visitor navigation checks.");
    }
}

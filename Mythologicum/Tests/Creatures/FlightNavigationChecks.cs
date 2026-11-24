using System;
using Mythologicum;
using UnityEngine;

public static class FlightNavigationChecks
{
    private static int checks;
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 10);
        var fences = new FenceLayout(grid);
        for (int z = 0; z < grid.Depth; z++) grid.Habitat.TrySetWater(V(6, z), 1);
        var ground = new CreatureNavigation(grid, fences);
        var air = new CreatureNavigation(grid, fences, canFly: true);
        Check(!ground.IsWalkable(V(6, 4)) && air.IsWalkable(V(6, 4)), "Flying is optional and crosses water without granting swimming to land creatures.");
        Check(ground.FindPath(V(1, 4), V(9, 4)).Count == 0 && air.FindPath(V(1, 4), V(9, 4)).Count == 9,
            "The flight graph reuses the cardinal pathfinder across a pond.");
        Check(!air.CanCross(V(1, 1), V(2, 2)) && !air.IsWalkable(V(-1, 1)), "Flight still respects cardinal cells and map limits.");
        var edge = new FenceEdge(V(3, 4), false);
        foreach (FenceKind kind in new[] { FenceKind.Wooden, FenceKind.Spiked, FenceKind.Heavy, FenceKind.Sanctified, FenceKind.Deep, FenceKind.Aquarium })
        {
            fences.TryAdd(edge, kind);
            Check(!ground.CanCross(V(2, 4), V(3, 4)) && air.CanCross(V(2, 4), V(3, 4)), "A capable bird can plan over ordinary unroofed barriers: " + kind);
            fences.TryRemove(edge);
        }
        grid.TryOccupy(V(4, 4), BuildCellContent.Building);
        Check(!air.IsWalkable(V(4, 4)) && !air.CanCross(V(3, 4), V(4, 4)), "Buildings remain occupied space for conservative flight routing.");
        var obstructed = new CreatureNavigation(grid, fences, cell => cell != V(2, 3), canFly: true);
        Check(!obstructed.IsWalkable(V(2, 3)), "The flight-specific physical-clearance predicate remains active.");

        var special = new SpecialEnclosureMap(grid, fences);
        var roofed = new CreatureNavigation(grid, fences, canFly: true, flightCrossingAllowed: special.CanFlyAcross);
        FenceEdge breach = default;
        for (int x = 1; x < 5; x++)
        {
            fences.TryAdd(new FenceEdge(V(x, 1), true), FenceKind.Aviary);
            var north = new FenceEdge(V(x, 5), true);
            if (x != 4) fences.TryAdd(north, FenceKind.Aviary); else breach = north;
        }
        for (int z = 1; z < 5; z++)
        { fences.TryAdd(new FenceEdge(V(1, z), false), FenceKind.Aviary); fences.TryAdd(new FenceEdge(V(5, z), false), FenceKind.Aviary); }
        Check(roofed.CanCross(V(1, 3), V(0, 3)), "Incomplete aviary walls never create an invisible completed roof.");
        fences.TryAdd(breach, FenceKind.Aviary);
        Check(!roofed.CanCross(V(1, 3), V(0, 3)) && !roofed.CanCross(V(0, 3), V(1, 3)), "A closed roof blocks flight both out and in without assigning creature ownership.");
        Check(roofed.CanCross(V(2, 2), V(3, 2)), "A contained bird can still move freely inside its cage.");
        var gate = new FenceEdge(V(1, 2), false); fences.TrySetGate(gate);
        Check(!roofed.CanCross(V(1, 2), V(0, 2)), "A closed staff gate retains aviary containment.");
        fences.TryRemove(breach);
        Check(roofed.CanCross(V(1, 3), V(0, 3)), "Breaking the perimeter releases flight routes on the next query.");
        Check(float.IsPositiveInfinity(special.FlightCeiling(V(2, 2))), "An incomplete cage has no virtual flight ceiling.");
        var settings = new CreatureFlightSettings();
        Check(!settings.canFly && settings.maximumHeight >= settings.cruiseHeight, "Existing species remain grounded unless flight is enabled explicitly.");
        settings.cruiseHeight = float.NaN; settings.maximumHeight = -1; settings.flightSpeed = -2;
        settings.pursuitSpeed = float.PositiveInfinity; settings.wingBeatDegrees = 1000; settings.lightningSeconds = 0;
        settings.Validate();
        Check(settings.cruiseHeight == 0 && settings.maximumHeight >= settings.cruiseHeight && settings.flightSpeed >= .1f
            && settings.pursuitSpeed >= settings.flightSpeed && settings.wingBeatDegrees <= 80 && settings.lightningSeconds >= .02f,
            "Invalid flight tuning normalizes to finite, usable settings.");
        Console.WriteLine("PASS: " + checks + " flight route, water crossing, optional capability, roof containment and tuning checks.");
    }
}

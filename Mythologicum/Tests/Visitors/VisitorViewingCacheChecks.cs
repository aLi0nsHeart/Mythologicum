using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEngine;

public static class VisitorViewingCacheChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    private static void Equivalent(SquareBuildGrid grid, CreatureNavigation sight, VisitorViewingCache cache,
        Vector2Int animalCell, Vector3 position, float range)
    {
        cache.BeginTick(range);
        var expected = new Dictionary<Vector2Int, float>();
        for (int z = 0; z < grid.Depth; z++)
        for (int x = 0; x < grid.Width; x++)
        {
            var cell = V(x, z);
            if (grid.ContentAt(cell) != BuildCellContent.Path) continue;
            var center = grid.CellCenter(cell);
            float distance = new Vector2(center.x - position.x, center.z - position.z).magnitude;
            if (distance > range || !sight.HasLineOfSight(cell, animalCell)) continue;
            expected.Add(cell, distance);
        }
        var actual = cache.ForAnimal(animalCell, position);
        Check(expected.Count == actual.Count, "The spatial query finds exactly the original visible path set");
        foreach (var point in actual)
            Check(expected.TryGetValue(point.Cell, out float distance) && distance == point.Distance,
                "Cached viewing distance and visibility match the exhaustive reference");
    }

    public static void Main()
    {
        var grid = new SquareBuildGrid(new Vector3(-12, 0, 8), 2, 19, 15);
        var fences = new FenceLayout(grid);
        var sight = new CreatureNavigation(grid, fences);
        for (int z = 0; z < grid.Depth; z++)
        for (int x = 0; x < grid.Width; x++)
            if (x % 3 == 0 || z % 3 == 0) grid.TryOccupy(V(x, z), BuildCellContent.Path);
        for (int z = 3; z <= 9; z++) grid.TryOccupy(V(7, z), BuildCellContent.Building);
        int sightTests = 0;
        var cache = new VisitorViewingCache(grid, (a, b) => { sightTests++; return sight.HasLineOfSight(a, b); });
        foreach (var cell in new[] { V(1, 1), V(9, 9), V(18, 14), V(8, 5) })
        foreach (float offset in new[] { -.99f, 0f, .99f })
        foreach (float range in new[] { 0f, .1f, 2f, 4.001f, 10f, 100f, -1f, float.NaN, float.PositiveInfinity })
            Equivalent(grid, sight, cache, cell, grid.CellCenter(cell) + new Vector3(offset, 3, -offset), range);

        cache.BeginTick(10);
        var first = cache.ForAnimal(V(3, 4), grid.CellCenter(V(3, 4)));
        int afterFirst = sightTests;
        var second = cache.ForAnimal(V(3, 4), grid.CellCenter(V(3, 4)));
        Check(ReferenceEquals(first, second) && sightTests == afterFirst,
            "Multiple visitors share the same animal-position sight tests within a tick");
        cache.ForAnimal(V(3, 4), grid.CellCenter(V(3, 4)) + new Vector3(.1f, 0, 0));
        Check(sightTests > afterFirst, "Different continuous animal positions cannot share stale distances");
        int beforeNextTick = sightTests;
        grid.TryOccupy(V(4, 4), BuildCellContent.Building);
        cache.BeginTick(10);
        cache.ForAnimal(V(3, 4), grid.CellCenter(V(3, 4)));
        Check(sightTests > beforeNextTick, "A new tick refreshes geometry after physical construction changes");
        Equivalent(grid, sight, cache, V(3, 4), grid.CellCenter(V(3, 4)), 10);
        Equivalent(grid, sight, cache, V(4, 4), grid.CellCenter(V(4, 4)), 10);
        Console.WriteLine("PASS: " + checks + " visitor viewing-cache equivalence and reuse checks.");
    }
}

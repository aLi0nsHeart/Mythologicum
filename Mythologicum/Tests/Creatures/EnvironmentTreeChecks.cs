using System;
using Mythologicum;
using UnityEngine;

public static class EnvironmentTreeChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 10, 10);
        var environment = new EnvironmentGrid(grid);
        var cell = new Vector2Int(2, 2);
        var other = new Vector2Int(3, 2);
        var outside = new Vector2Int(-1, 2);
        Check(environment.TreesAt(cell) == 0, "Natural ground starts without constructed trees");
        Check(!environment.AddTree(outside) && environment.Revision == 0, "Outside trees do not change environment");
        environment.SetForestCoverage(cell, 0.2f);
        int baseline = environment.Revision;
        Check(environment.AddTree(cell) && environment.Revision == baseline + 1, "Planting invalidates environment snapshots");
        Check(environment.TreesAt(cell) == 1, "Tree count recorded per cell");
        Check(!environment.AddTree(cell) && environment.Revision == baseline + 1, "Duplicate trees rejected without invalidation");
        Check(Near(environment.Sample(new[] { cell }).ForestPercent, 70f), "Tree canopy adds coverage above existing forest");
        Check(environment.TreeCount(new[] { cell, cell, other, outside }) == 1, "Region count ignores duplicate and outside cells");
        Check(environment.AddTree(other), "Second tree can be planted independently");
        Check(environment.TreeCount(new[] { cell, other }) == 2, "Multi-cell count includes both trees");
        Check(Near(environment.SampleAround(cell, 1).ForestArea, 4.8f), "Local circular sample includes adjacent tree and authored ground");
        Check(Near(environment.SampleAround(cell, 0).ForestArea, 2.8f), "Radius zero samples only current cell");
        environment.SetForestCoverage(cell, 0.8f);
        Check(Near(environment.Sample(new[] { cell }).ForestPercent, 100f), "Combined coverage saturates at full coverage");
        Check(environment.RemoveTree(cell), "Tree can be removed");
        Check(Near(environment.Sample(new[] { cell }).ForestPercent, 80f), "Removal restores authored forest including edits made after planting");
        int afterRemoval = environment.Revision;
        Check(!environment.RemoveTree(cell) && environment.Revision == afterRemoval, "Repeated removal has no side effects");
        Check(environment.TreeCount(new[] { cell, other }) == 1, "Demolition changes regional tree counts");
        Check(environment.RemoveTree(other) && Near(environment.Sample(new[] { other }).ForestArea, 0f), "Canopy fully removed where no underlying forest exists");
        Check(grid.IsAvailable(cell) && grid.IsAvailable(other), "Enrichment does not block logical creature ground");
        Console.WriteLine("PASS: " + checks + " tree enrichment, coverage restoration and local environment checks.");
    }
}

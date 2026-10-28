using System;
using Mythologicum;
using UnityEngine;

public static class GraveEnvironmentChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 6, 6);
        var environment = new EnvironmentGrid(grid);
        var cell = new Vector2Int(2, 2);
        Check(environment.GravestonesAt(cell) == 0, "Ground has no default grave");
        Check(!environment.AddGravestone(new Vector2Int(-1, 1)) && environment.Revision == 0, "Outside grave cannot change environment");
        Check(environment.AddGravestone(cell) && environment.Revision == 1, "Adding grave invalidates environment snapshots");
        Check(environment.GravestonesAt(cell) == 1 && environment.TreesAt(cell) == 0, "Graves are separate enrichment");
        Check(!environment.AddGravestone(cell) && environment.Revision == 1, "Duplicate grave is rejected without invalidation");
        Check(!environment.AddTree(cell), "Tree cannot overlap grave");
        Check(grid.IsAvailable(cell), "Graves do not reserve creature walking ground");
        Check(environment.Sample(new[] { cell }).ForestArea == 0, "Graves do not falsely count as forest");
        Check(environment.RemoveGravestone(cell) && environment.Revision == 2, "Grave demolition updates environment");
        Check(!environment.RemoveGravestone(cell) && environment.Revision == 2, "Repeated grave removal is harmless");
        Check(environment.AddTree(cell), "Tree placement can use demolished grave cell");
        Check(!environment.AddGravestone(cell), "Grave cannot overlap a tree");
        Check(environment.RemoveTree(cell) && environment.AddGravestone(cell), "Demolished tree can be replaced with grave");
        Console.WriteLine("PASS: " + checks + " grave enrichment and space checks.");
    }
}

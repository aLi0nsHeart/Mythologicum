using System;
using Mythologicum;
using UnityEngine;

public static class ScarecrowEnvironmentChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 8, 7);
        var environment = new EnvironmentGrid(grid);
        var cell = new Vector2Int(3, 2);
        Check(environment.ScarecrowCells.Count == 0 && environment.ScarecrowsAt(cell) == 0, "No scarecrow exists before construction");
        Check(!environment.AddScarecrow(new Vector2Int(-1, 0)) && environment.Revision == 0, "Off-map construction is rejected without invalidation");
        Check(environment.AddScarecrow(cell) && environment.Revision == 1 && environment.ScarecrowCells.Count == 1, "Construction publishes one fear-feeding target");
        Check(environment.ScarecrowsAt(cell) == 1 && grid.IsAvailable(cell), "Scarecrow enrichment remains walkable");
        Check(environment.Sample(new[] { cell }).ForestArea == 0, "Scarecrows do not count as forest");
        Check(!environment.AddScarecrow(cell) && environment.Revision == 1, "Duplicate targets do not invalidate environment");
        Check(!environment.AddTree(cell) && !environment.AddGravestone(cell), "Trees and gravestones cannot overwrite feeding enrichment");
        Check(environment.RemoveScarecrow(cell) && environment.ScarecrowCells.Count == 0 && environment.Revision == 2, "Demolition removes target and invalidates habitat state");
        Check(!environment.RemoveScarecrow(cell) && environment.Revision == 2, "Repeated demolition is harmless");
        Check(environment.AddTree(cell) && !environment.AddScarecrow(cell), "Scarecrow cannot overwrite a tree");
        Check(environment.RemoveTree(cell) && environment.AddGravestone(cell) && !environment.AddScarecrow(cell), "Scarecrow cannot overwrite a gravestone");
        Check(environment.RemoveGravestone(cell) && environment.AddScarecrow(cell), "Cleared enrichment ground can host a new feeding target");
        Console.WriteLine("PASS: " + checks + " scarecrow environment, overlap and revision checks.");
    }
}

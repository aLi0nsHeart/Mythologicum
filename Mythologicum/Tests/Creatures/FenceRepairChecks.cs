using System;
using Mythologicum;
using UnityEngine;

public static class FenceRepairChecks
{
    public static void Main()
    {
        int count = 0;
        void Check(bool value) { count++; if (!value) throw new Exception("Fence repair check failed: " + count); }
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 4, 4);
        var fences = new FenceLayout(grid);
        var edge = new FenceEdge(new Vector2Int(1, 1), false);
        Check(fences.TryRepair(edge, 100f) == 0f && !fences.HasFence(edge));
        fences.TryAdd(edge);
        Check(fences.TryRepair(edge, 5f) == 0f && fences.HealthAt(edge) == 120f);
        fences.TryDamage(edge, 35f);
        int revision = fences.Revision;
        Check(fences.TryRepair(edge, 10f) == 10f && fences.HealthAt(edge) == 95f);
        Check(fences.Revision == revision);
        Check(fences.TryRepair(edge, 1000f) == 25f && fences.HealthAt(edge) == 120f);
        Check(fences.TryRepair(edge, -1f) == 0f);
        Check(fences.TryRepair(edge, float.NaN) == 0f);
        Check(fences.TryRepair(edge, float.PositiveInfinity) == 0f);
        fences.TryDamage(edge, 1000f);
        Check(!fences.HasFence(edge) && fences.TryRepair(edge, 120f) == 0f);
        fences.TryAdd(edge, FenceKind.Sanctified); fences.TrySetGate(edge); fences.TryDamage(edge, 50f);
        Check(fences.TryRepair(edge, 20f) == 20f && fences.HealthAt(edge) == 270f);
        Check(fences.IsGate(edge) && fences.KindAt(edge) == FenceKind.Sanctified);
        fences.TryRemove(edge); fences.TryAdd(edge, FenceKind.Heavy);
        Check(fences.TryRepair(edge, 1000f) == 0f && fences.HealthAt(edge) == 800f);
        Console.WriteLine("PASS: " + count + " existing-only fence repair checks.");
    }
}

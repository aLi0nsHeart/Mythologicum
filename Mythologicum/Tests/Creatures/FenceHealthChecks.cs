using System;
using Mythologicum;
using UnityEngine;

public static class FenceHealthChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        var layout = new FenceLayout(new SquareBuildGrid(Vector3.zero, 2f, 4, 4));
        var edge = new FenceEdge(new Vector2Int(2, 1), false);
        Check(layout.HealthAt(edge) == 0f && layout.MaximumHealthAt(edge) == 0f, "Missing edges have no health.");
        Check(layout.TryDamage(edge, 10f) == 0f, "Missing edges cannot take damage.");
        Check(layout.TryAdd(edge) && layout.HealthAt(edge) == 120f, "New wood has full health.");
        int revision = layout.Revision;
        Check(layout.TryDamage(edge, 30f) == 30f && layout.HealthAt(edge) == 90f, "A nonlethal strike applies its damage.");
        Check(layout.HasFence(edge) && layout.Revision == revision, "Partial damage retains barrier and navigation cache.");
        foreach (float invalid in new[] { 0f, -10f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check(layout.TryDamage(edge, invalid) == 0f && layout.HealthAt(edge) == 90f, "Invalid damage never alters health.");
        Check(layout.TrySetGate(edge) && layout.HealthAt(edge) == 90f, "Gate conversion retains damage instead of repairing a fence.");
        Check(layout.TryDamage(edge, 500f) == 90f, "Lethal overkill reports only remaining health.");
        Check(!layout.HasFence(edge) && !layout.IsGate(edge) && layout.Revision == revision + 2, "Destruction removes gate and invalidates routes.");
        Check(layout.HealthAt(edge) == 0 && layout.MaximumHealthAt(edge) == 0, "Destroyed edges clear all health data.");
        Check(layout.TryAdd(edge, FenceKind.Spiked) && layout.HealthAt(edge) == 220f, "Rebuilding starts at new type health.");
        layout.TryDamage(edge, 20);
        Check(layout.TrySetKind(edge, FenceKind.Sanctified) && layout.HealthAt(edge) == 300f, "Changing barrier type resets its own health.");
        Check(layout.TrySetGate(edge) && !layout.CanPhase(edge), "Sanctified gates retain phase protection.");
        Check(layout.TryDamage(edge, 90f) == 90f && layout.HealthAt(edge) == 210f, "Sanctified gates still take physical damage.");
        layout.TryRemove(edge);
        Check(layout.TryAdd(edge, FenceKind.Heavy) && layout.HealthAt(edge) == 800f, "Heavy barrier has its own health reserve.");
        revision = layout.Revision;
        Check(layout.TryDamage(edge, float.MaxValue) == 0f && layout.HealthAt(edge) == 800f,
            "Reinforced barrier resists even extreme creature strikes.");
        Check(layout.Revision == revision && layout.HasFence(edge), "Resisted attacks do not mutate containment.");
        Check(layout.TrySetGate(edge) && layout.KindAt(edge) == FenceKind.Heavy && layout.HealthAt(edge) == 800f,
            "Reinforced gate retains protection and health.");
        Check(layout.TryDamage(edge, 100f) == 0f && layout.CanPhase(edge), "Reinforcement is physical protection, not sanctification.");
        Check(layout.TryRemove(edge) && layout.TryAdd(edge), "Demolition still removes reinforced barriers for the player.");
        Check(layout.HealthAt(edge) == 120f && !layout.IsGate(edge), "Rebuilding cannot inherit reinforcement, gate or damage data.");
        Console.WriteLine("PASS: " + checks + " fence health and protected-gate checks.");
    }
}

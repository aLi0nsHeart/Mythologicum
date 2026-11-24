using System;
using Mythologicum;
using UnityEngine;

public static class CreatureBurrowingChecks
{
    private static int checks;
    private static Vector2Int V(int x) => new Vector2Int(x, 0);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 3, 1);
        var fences = new FenceLayout(grid);
        var edge = new FenceEdge(V(1), false);
        Check(CreatureBurrowing.CanDigGround(grid, V(0)), "Natural clear ground permits digging");
        Check(!CreatureBurrowing.CanDigGround(grid, V(-1)) && !CreatureBurrowing.CanDigGround(grid, V(3)), "Burrowing cannot leave map bounds");
        grid.TryOccupy(V(0), BuildCellContent.Path);
        Check(!CreatureBurrowing.CanDigGround(grid, V(0)), "Paths cannot become burrow entrances");
        grid.TryRemove(V(0), BuildCellContent.Path);
        grid.TryOccupy(V(0), BuildCellContent.Building);
        Check(!CreatureBurrowing.CanDigGround(grid, V(0)), "Buildings cannot become burrow entrances");
        grid.TryRemove(V(0), BuildCellContent.Building);
        foreach (FenceKind kind in Enum.GetValues(typeof(FenceKind)))
        {
            fences.TryAdd(edge, kind);
            var ordinary = new CreatureNavigation(grid, fences);
            var burrowing = new CreatureNavigation(grid, fences, canBurrowFences: true);
            Check(!ordinary.CanCross(V(0), V(1)), "Ordinary ground movement stays fenced: " + kind);
            bool hasFoundation = kind == FenceKind.Heavy || kind == FenceKind.Deep || kind == FenceKind.Aquarium;
            Check(fences.CanBurrow(edge) == !hasFoundation, "Default buried protection matches type: " + kind);
            Check(burrowing.CanCross(V(0), V(1)) == !hasFoundation, "Burrow route obeys foundation protection: " + kind);
            Check(burrowing.CanCross(V(1), V(0)) == !hasFoundation, "Foundation crossing is symmetric: " + kind);
            fences.TrySetGate(edge);
            Check(burrowing.CanCross(V(0), V(1)) == !hasFoundation, "Staff gates retain buried protection: " + kind);
            fences.TryRemove(edge);
        }
        fences.TryAdd(edge);
        var dig = new CreatureNavigation(grid, fences, canBurrowFences: true);
        Check(dig.FindPath(V(0), V(2)).Count == 3, "Burrow route remains cardinal and contains endpoints");
        grid.TryOccupy(V(1), BuildCellContent.Path);
        Check(!dig.CanCross(V(0), V(1)), "A paved exit blocks a tunnel even through an ordinary fence");
        grid.TryRemove(V(1), BuildCellContent.Path);
        grid.TryOccupy(V(1), BuildCellContent.Building);
        Check(!dig.CanCross(V(0), V(1)), "A building exit blocks a tunnel");
        grid.TryRemove(V(1), BuildCellContent.Building);
        var custom = new FenceRules(); custom.wooden.blocksBurrowing = true; custom.heavy.blocksBurrowing = false;
        var customFence = new FenceLayout(grid, custom); customFence.TryAdd(edge);
        Check(!new CreatureNavigation(grid, customFence, canBurrowFences: true).CanCross(V(0), V(1)), "Designer can give wood buried protection");
        customFence.TrySetKind(edge, FenceKind.Heavy);
        Check(new CreatureNavigation(grid, customFence, canBurrowFences: true).CanCross(V(0), V(1)), "Designer can remove heavy buried protection");
        customFence.TrySetKind(edge, FenceKind.Deep);
        var customDig = new CreatureNavigation(grid, customFence, canBurrowFences: true);
        Check(!customDig.CanCross(V(0), V(1)), "Deep has its own foundation rule independent of heavy");
        custom.deep.blocksBurrowing = false;
        Check(customDig.CanCross(V(0), V(1)), "Designer can independently change Deep foundation protection");
        custom.deep = null; custom.Validate();
        Check(!customDig.CanCross(V(0), V(1)), "Missing Deep data restores its safe default foundation rules");

        Check((int)FenceKind.Wooden == 0 && (int)FenceKind.Spiked == 1 && (int)FenceKind.Heavy == 2
            && (int)FenceKind.Sanctified == 3 && (int)FenceKind.Deep == 4,
            "Appending Deep preserves all historical saved fence kind values");
        var lifecycle = new FenceLayout(grid); lifecycle.TryAdd(edge);
        var tunneller = new CreatureNavigation(grid, lifecycle, canBurrowFences: true);
        Check(tunneller.CanCross(V(0), V(1)), "Wood is tunnelable before installing a foundation");
        int revision = lifecycle.Revision;
        Check(lifecycle.TrySetKind(edge, FenceKind.Deep) && lifecycle.Revision > revision
            && !tunneller.CanCross(V(0), V(1)), "An in-place Deep upgrade immediately invalidates a planned tunnel");
        Check(lifecycle.CanClimb(edge) && lifecycle.CanPhase(edge) && lifecycle.CanBreak(edge),
            "Deep foundations do not grant spike, spirit or reinforcement protection");
        float health = lifecycle.HealthAt(edge);
        Check(lifecycle.TrySetWindow(edge, 5) && !tunneller.CanCross(V(0), V(1)), "Viewing windows retain Deep foundations");
        Check(lifecycle.TryDamage(edge, 30) == 30 && !tunneller.CanCross(V(0), V(1)), "Partial damage does not open a tunnel through Deep");
        Check(lifecycle.TryRepair(edge, 100) == 30 && lifecycle.HealthAt(edge) == health
            && !tunneller.CanCross(V(0), V(1)), "Repairs preserve Deep health limits and foundation protection");
        var saved = new SavedFence { cell = edge.Start, alongX = edge.AlongX, kind = lifecycle.KindAt(edge),
            health = lifecycle.HealthAt(edge), window = lifecycle.HasWindow(edge), windowPrice = 5 };
        var restored = new FenceLayout(grid);
        Check(restored.TryAdd(new FenceEdge(saved.cell, saved.alongX), saved.kind) && restored.RestoreHealth(edge, saved.health)
            && restored.TrySetWindow(edge, saved.windowPrice) && restored.KindAt(edge) == FenceKind.Deep && !restored.CanBurrow(edge),
            "Saved Deep type, health and window restore through the shared save representation");
        Check(lifecycle.TryDamage(edge, 10000) == health && !lifecycle.HasFence(edge) && !lifecycle.HasWindow(edge)
            && tunneller.CanCross(V(0), V(1)), "A fully breached Deep segment releases its foundation and window");
        Check(lifecycle.TryAdd(edge, FenceKind.Deep) && lifecycle.TrySetGate(edge) && !tunneller.CanCross(V(0), V(1)),
            "Rebuilt Deep staff gates protect their underground opening");
        saved.gate = true; saved.window = false; saved.health = 175;
        restored.TryRemove(edge);
        Check(restored.TryAdd(edge, saved.kind) && restored.TrySetGate(edge) && restored.RestoreHealth(edge, saved.health)
            && restored.HealthAt(edge) == 175 && !restored.CanBurrow(edge), "Damaged Deep gates restore with their foundation intact");
        Check(lifecycle.TryRemove(edge) && !lifecycle.IsGate(edge) && tunneller.CanCross(V(0), V(1)),
            "Demolishing a Deep gate removes both physical and foundation protection");
        var heights = new GridHeightField(3, 1, 2, maximumWalkSlope: 20);
        heights.ImportHeights(new float[] { 0, 0, 0, 10, 0, 0, 0, 10 });
        var terrain = new SquareBuildGrid(Vector3.zero, 2, 3, 1, heights);
        Check(!CreatureBurrowing.CanDigGround(terrain, V(2)), "Steep ground cannot host a burrow");
        var physical = new CreatureNavigation(grid, fences, cell => cell != V(1), canBurrowFences: true);
        Check(!physical.CanCross(V(0), V(1)), "Burrowing does not bypass physical exit clearance");
        Console.WriteLine("PASS: " + checks + " burrow ground, fence, gate, foundation and route checks.");
    }
}

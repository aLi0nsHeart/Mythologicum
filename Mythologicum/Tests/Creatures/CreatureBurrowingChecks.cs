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
            Check(fences.CanBurrow(edge) == (kind != FenceKind.Heavy), "Default buried protection matches type: " + kind);
            Check(burrowing.CanCross(V(0), V(1)) == (kind != FenceKind.Heavy), "Burrow route obeys foundation protection: " + kind);
            fences.TrySetGate(edge);
            Check(burrowing.CanCross(V(0), V(1)) == (kind != FenceKind.Heavy), "Staff gates retain buried protection: " + kind);
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
        var heights = new GridHeightField(3, 1, 2, maximumWalkSlope: 20);
        heights.ImportHeights(new float[] { 0, 0, 0, 10, 0, 0, 0, 10 });
        var terrain = new SquareBuildGrid(Vector3.zero, 2, 3, 1, heights);
        Check(!CreatureBurrowing.CanDigGround(terrain, V(2)), "Steep ground cannot host a burrow");
        var physical = new CreatureNavigation(grid, fences, cell => cell != V(1), canBurrowFences: true);
        Check(!physical.CanCross(V(0), V(1)), "Burrowing does not bypass physical exit clearance");
        Console.WriteLine("PASS: " + checks + " burrow ground, fence, gate, foundation and route checks.");
    }
}

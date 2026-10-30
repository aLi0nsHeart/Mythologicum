using System;
using Mythologicum;
using UnityEngine;

public static class FenceCapabilityChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 4, 4);
        var layout = new FenceLayout(grid);
        var walking = new CreatureNavigation(grid, layout);
        var climbing = new CreatureNavigation(grid, layout, null, true);
        var phasing = new CreatureNavigation(grid, layout, null, false, true);
        var breaking = new CreatureNavigation(grid, layout, null, false, false, true);
        var edge = new FenceEdge(V(2,1), false);
        foreach (FenceKind kind in new[] { FenceKind.Wooden, FenceKind.Spiked, FenceKind.Heavy, FenceKind.Sanctified })
        {
            layout.TryRemove(edge); layout.TryAdd(edge, kind);
            Check(!walking.CanCross(V(1,1), V(2,1)), "Ordinary movement cannot cross " + kind);
            Check(climbing.CanCross(V(1,1), V(2,1)) == (kind == FenceKind.Wooden), "Climbing respects " + kind);
            Check(phasing.CanCross(V(1,1), V(2,1)) == (kind != FenceKind.Sanctified), "Phasing respects " + kind);
            Check(breaking.CanCross(V(1,1), V(2,1)) == (kind != FenceKind.Heavy), "Attack planning respects " + kind);
            Check(phasing.CanCross(V(2,1), V(1,1)) == (kind != FenceKind.Sanctified), "Phase crossing is symmetric.");
            Check(breaking.CanCross(V(2,1), V(1,1)) == (kind != FenceKind.Heavy), "Attack crossing is symmetric.");
            Check(layout.TrySetGate(edge), "Protected gate conversion supports " + kind);
            Check(!climbing.CanCross(V(1,1), V(2,1)), "Gates cannot be climbed.");
            Check(phasing.CanCross(V(1,1), V(2,1)) == (kind != FenceKind.Sanctified), "Gate phase protection matches its fence.");
            Check(breaking.CanCross(V(1,1), V(2,1)) == (kind != FenceKind.Heavy), "Gate damage protection matches its fence.");
        }
        Check(!phasing.CanCross(V(1,1), V(3,1)) && !breaking.CanCross(V(1,1), V(3,1)), "Abilities never create non-cardinal jumps.");
        grid.TryOccupy(V(2,1));
        Check(!phasing.CanCross(V(1,1), V(2,1)) && !breaking.CanCross(V(1,1), V(2,1)), "Abilities never bypass buildings.");
        Check(!phasing.IsWalkable(V(-1,1)), "Abilities never bypass map bounds.");
        var obstructed = new CreatureNavigation(grid, layout, cell => cell != V(1,1), true, true, true);
        Check(!obstructed.CanCross(V(0,1), V(1,1)), "Abilities never bypass physical obstacles.");
        Console.WriteLine("PASS: " + checks + " fence capability and containment checks.");
    }
}

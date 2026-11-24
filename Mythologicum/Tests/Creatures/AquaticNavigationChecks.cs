using System;
using Mythologicum;
using UnityEngine;

public static class AquaticNavigationChecks
{
    private static int checks;
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 9, 7);
        var fences = new FenceLayout(grid);
        for (int z = 0; z < grid.Depth; z++) grid.Habitat.TrySetWater(V(4, z), .8f);
        var land = new CreatureNavigation(grid, fences);
        var swimmer = new CreatureNavigation(grid, fences, canSwim: true, minimumSwimDepth: .4f);
        Check(land.FindPath(V(1, 3), V(7, 3)).Count == 0, "A continuous pond blocks ordinary land creatures.");
        Check(swimmer.FindPath(V(1, 3), V(7, 3)).Count == 7, "An amphibious creature can cross between reachable banks.");
        Check(!land.IsWalkable(V(4, 3)) && swimmer.IsWalkable(V(4, 3)), "Swimming permission is species-specific.");
        Check(swimmer.CanCross(V(3, 3), V(4, 3)) && swimmer.CanCross(V(4, 3), V(3, 3)), "Bank entry and exit are symmetric.");
        var settings = new CreatureAquaticSettings { canSwim = true, minimumSwimDepth = .4f,canWadeShallows=false };
        Check(settings.CanOccupy(grid, V(4, 3)) && settings.CanOccupy(grid, V(3, 3)), "Save-friendly occupancy accepts both land and sufficiently deep water.");
        grid.Habitat.TrySetWater(V(4, 3), .2f);
        Check(!swimmer.IsWalkable(V(4, 3)) && !settings.CanOccupy(grid, V(4, 3)), "Insufficient depth cannot be used as an aquatic home.");
        settings.canWadeShallows=true;
        var wader=new CreatureNavigation(grid,fences,canSwim:true,minimumSwimDepth:.4f,canWadeShallows:true);
        Check(settings.CanOccupy(grid,V(4,3)) && !settings.IsSwimmable(grid,V(4,3)) && wader.IsWalkable(V(4,3)),
            "Amphibious wading crosses shallow edges without counting them as swimming habitat.");
        var shallow = new CreatureNavigation(grid, fences, canSwim: true, minimumSwimDepth: .1f);
        Check(shallow.IsWalkable(V(4, 3)), "Different depth requirements can give different traversable graphs.");
        grid.Habitat.TrySetWater(V(4, 3), .8f);
        for (int z = 0; z < grid.Depth; z++) fences.TryAdd(new FenceEdge(V(4, z), false));
        Check(swimmer.FindPath(V(1, 3), V(7, 3)).Count == 0, "A fence remains a barrier at a water bank.");
        var gate = new FenceEdge(V(4, 3), false); fences.TrySetGate(gate);
        Check(!swimmer.CanCross(V(3, 3), V(4, 3)), "Closed staff gates contain swimmers too.");
        fences.TryRemove(gate);
        Check(swimmer.FindPath(V(1, 3), V(7, 3)).Count > 0, "A physical breach opens swimming access immediately.");
        var blocked = new CreatureNavigation(grid, fences, cell => cell != V(4, 3), canSwim: true);
        Check(blocked.FindPath(V(1, 3), V(7, 3)).Count == 0, "Physical obstructions remain authoritative in water.");
        Check(swimmer.HasLineOfSight(V(3, 3), V(5, 3)), "A water strip does not become an opaque vision wall.");
        settings.minimumSwimDepth = float.NaN; settings.swimSpeed = float.PositiveInfinity;
        settings.waterSearchRadius = -10; settings.preferredWaterArea = float.NaN;
        settings.captureSeconds = -1; settings.drowningSeconds = 0;
        settings.Validate();
        Check(settings.minimumSwimDepth >= .05f && settings.swimSpeed >= .1f && settings.waterSearchRadius >= .1f,
            "Invalid aquatic navigation tuning is normalized before use.");
        Check(settings.preferredWaterArea == 0 && settings.captureSeconds >= .1f && settings.drowningSeconds >= .1f,
            "Invalid habitat and capture tuning stays finite and usable.");
        Check(!new CreatureAquaticSettings().CanOccupy(grid, V(4, 3)), "Older profiles remain land-only by default.");
        Console.WriteLine("PASS: " + checks + " aquatic navigation, depth, fence, bank and compatibility checks.");
    }
}

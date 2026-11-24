using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class SpecialEnclosureChecks
{
    private static int checks;
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
    private static Vector2Int C(int x, int y) => new Vector2Int(x, y);
    private static HashSet<Vector2Int> Box(int x, int y, int width, int depth)
    { var result = new HashSet<Vector2Int>(); for (int z = y; z < y + depth; z++) for (int a = x; a < x + width; a++) result.Add(C(a, z)); return result; }
    private static List<FenceEdge> Ring(FenceLayout fences, HashSet<Vector2Int> cells, FenceKind kind)
    {
        var result = new List<FenceEdge>();
        foreach (var cell in cells) foreach (var direction in Directions)
        {
            var next = cell + direction; if (cells.Contains(next)) continue;
            var edge = cell.x != next.x ? new FenceEdge(C(Math.Max(cell.x, next.x), cell.y), false)
                : new FenceEdge(C(cell.x, Math.Max(cell.y, next.y)), true);
            fences.TryAdd(edge, kind); result.Add(edge);
        }
        return result;
    }
    public static void Main()
    {
        var terrain = new GridHeightField(22, 18, 2);
        var grid = new SquareBuildGrid(new Vector3(140000, 20, -140000), 2, 22, 18, terrain);
        var fences = new FenceLayout(grid);
        var rules = new SpecialEnclosureRules();
        var map = new SpecialEnclosureMap(grid, fences, rules);
        Check((int)FenceKind.Aviary == 5 && (int)FenceKind.Aquarium == 6, "Special kinds append saved fence enum values without reordering old types.");
        Check(map.Records.Count == 0 && map.CanFlyAcross(C(0, 0), C(1, 0)) && float.IsPositiveInfinity(map.FlightCeiling(C(0, 0))),
            "Open ground has no invisible ceiling or enclosure assignment.");
        Check(!map.CanFlyAcross(C(0, 0), C(-1, 0)), "Air movement remains within map bounds.");
        var first = Box(3, 3, 4, 4); var perimeter = Ring(fences, first, FenceKind.Aviary);
        Check(map.Records.Count == 1 && map.IsAviary(C(4, 4)) && !map.IsAviary(C(2, 4)), "A complete special ring creates exactly its physical cell footprint.");
        Check(Math.Abs(map.FlightCeiling(C(4, 4)) - 28) < .001f, "Roof elevation is in world coordinates above the authored ground.");
        Check(map.CanFlyAcross(C(4, 4), C(5, 4)) && !map.CanFlyAcross(C(3, 3), C(2, 3)) && !map.CanFlyAcross(C(2, 3), C(3, 3)),
            "Contained flight crosses interior cells but cannot enter or leave through an intact cage.");
        int stable = map.Revision;
        for (int i = 0; i < 1000; i++) map.CanFlyAcross(C(4, 4), C(5, 4));
        Check(map.Revision == stable, "Repeated creature queries reuse topology instead of flood-filling each frame.");
        fences.TrySetGate(perimeter[0]);
        Check(map.IsAviary(C(4, 4)) && !map.CanFlyAcross(perimeter[0].CellA, perimeter[0].CellB), "A closed staff gate inherits its cage boundary.");
        fences.TryRemove(perimeter[1]);
        Check(!map.IsAviary(C(4, 4)) && float.IsPositiveInfinity(map.FlightCeiling(C(4, 4))), "Removing one boundary immediately removes the derived roof.");
        fences.TryAdd(perimeter[1], FenceKind.Wooden);
        Check(!map.IsAviary(C(4, 4)), "Ordinary wood cannot complete a missing special mesh wall.");
        fences.TrySetKind(perimeter[1], FenceKind.Aviary);
        Check(map.IsAviary(C(4, 4)), "Repairing the matching mesh restores containment.");
        fences.TryAdd(new FenceEdge(C(5, 4), false), FenceKind.Wooden);
        Check(map.At(C(4, 4), FenceKind.Aviary).Cells.Count == first.Count, "An ordinary internal partition does not disable a closed outer cage.");
        rules.aviaryHeight = 12;
        Check(Math.Abs(map.FlightCeiling(C(4, 4)) - 32) < .001f && map.Revision > stable, "Changing the shared height rule refreshes the ceiling.");
        var values = terrain.Capture().heights; values[5 * (terrain.Width + 1) + 5] = 7; terrain.ImportHeights(values);
        Check(Math.Abs(map.FlightCeiling(C(4, 4)) - 39) < .001f, "An interior hill cannot stick through a roof based only on the fence rim.");

        var concave = Box(11, 3, 4, 4); concave.Remove(C(13, 5)); concave.Remove(C(14, 5)); concave.Remove(C(13, 6)); concave.Remove(C(14, 6));
        Ring(fences, concave, FenceKind.Aviary);
        Check(map.Records.Count(record => record.Kind == FenceKind.Aviary) == 2 && map.At(C(11, 3), FenceKind.Aviary).Cells.Count == concave.Count,
            "Separate concave cages keep their real masks rather than filling a bounding rectangle.");
        Check(!map.IsAviary(C(14, 6)), "A concave cut-out remains outside the cage.");
        var nested = Box(4, 4, 2, 2); var inner = Ring(fences, nested, FenceKind.Aviary);
        Check(map.At(C(4, 4), FenceKind.Aviary).Cells.Count == 4 && map.At(C(3, 3), FenceKind.Aviary).Cells.Count == 12,
            "Nested rings form a bounded inner cage and annular outside cage.");
        Check(!map.CanFlyAcross(C(3, 4), C(4, 4)), "A shared intact special wall separates neighbouring cages.");
        fences.TryRemove(inner[0]);
        Check(map.CanFlyAcross(inner[0].CellA, inner[0].CellB) && map.At(C(3, 3), FenceKind.Aviary).Cells.Count == 16,
            "Opening an internal divider merges connected air space beneath an intact outer roof.");

        var edgeGrid = new SquareBuildGrid(Vector3.zero, 2, 5, 5); var edgeFences = new FenceLayout(edgeGrid);
        var edgeMap = new SpecialEnclosureMap(edgeGrid, edgeFences); var edgeMask = Box(0, 0, 2, 2);
        var edgeRing = Ring(edgeFences, edgeMask, FenceKind.Aviary);
        foreach (var edge in edgeRing.Where(edge => !edgeGrid.Contains(edge.CellA) || !edgeGrid.Contains(edge.CellB)).ToArray()) edgeFences.TryRemove(edge);
        Check(!edgeMap.IsAviary(C(0, 0)), "A map edge never supplies missing cage walls.");
        Ring(edgeFences, edgeMask, FenceKind.Aviary);
        Check(edgeMap.IsAviary(C(0, 0)), "Explicitly built special walls at map edges still make a real complete cage.");

        var poolTerrain = new GridHeightField(12, 12, 2); var poolGrid = new SquareBuildGrid(new Vector3(0, 7, 0), 2, 12, 12, poolTerrain);
        var poolFences = new FenceLayout(poolGrid); var poolMap = new SpecialEnclosureMap(poolGrid, poolFences);
        var poolCells = Box(3, 3, 4, 4); var glass = Ring(poolFences, poolCells, FenceKind.Aquarium);
        Check(poolMap.IsAquarium(C(4, 4)) && !poolMap.IsAviary(C(4, 4)) && float.IsPositiveInfinity(poolMap.FlightCeiling(C(4, 4))),
            "A complete pool wall is separate from an aviary roof.");
        var before = poolTerrain.Capture();
        Check(poolTerrain.TryPlanEnclosedBasin(poolGrid, poolCells, .08f, 1.2f, null, out var plan, out _), "Exact pool earthworks can be planned on clear terrain.");
        Check(poolTerrain.Capture().heights.SequenceEqual(before.heights) && plan.Changes.Count == 9, "Planning is non-mutating and only fully interior vertices move.");
        Check(poolTerrain.TryApplyExcavation(plan), "A validated pool plan applies atomically.");
        Check(Math.Abs(poolTerrain.Node(3, 3)) < .0001f && Math.Abs(poolTerrain.Node(7, 7)) < .0001f
            && Math.Abs(poolTerrain.Node(4, 4) + 1.12f) < .0001f, "The structural rim and neighbouring ground stay in place around a real depressed bed.");
        var edits = poolCells.Select(cell => new WaterSurfaceEdit(cell, true, 7.08f)).ToList();
        int notifications = 0; poolGrid.Habitat.Changed += () => notifications++;
        Check(poolGrid.Habitat.TryApplyWaterSurfaces(edits, out _) && notifications == 1, "A complete pool fills with one water-render/navigation notification.");
        Check(poolCells.All(poolGrid.Habitat.IsWater) && !poolGrid.Habitat.IsWater(C(2, 3)), "Water stays in the closed footprint and has real swim depth.");
        Check(poolCells.All(cell => poolGrid.Habitat.WaterDepth(cell) >= .35f), "Even boundary pool cells meet the amphibious swimming depth.");
        poolFences.TryRemove(glass[0]); poolMap.Refresh();
        Check(!poolMap.IsAquarium(C(4, 4)) && poolCells.All(poolGrid.Habitat.IsWater), "A breach loses containment while water and the basin persist.");
        Check(!poolTerrain.TryApplyExcavation(plan), "An already applied/stale plan cannot blindly overwrite subsequent terrain revisions.");
        var invalidEdits = new[] { new WaterSurfaceEdit(C(4, 4), false), new WaterSurfaceEdit(C(-1, 0), true, 7) };
        Check(!poolGrid.Habitat.TryApplyWaterSurfaces(invalidEdits, out _) && poolGrid.Habitat.IsWater(C(4, 4)), "Invalid batched water changes leave earlier cells untouched.");
        var removal = new[] { new WaterSurfaceEdit(C(4, 4), false), new WaterSurfaceEdit(C(5, 4), false) };
        notifications = 0;
        Check(poolGrid.Habitat.TryApplyWaterSurfaces(removal, out _) && notifications == 1
            && !poolGrid.Habitat.HasWaterSurface(C(4, 4)), "Exact-cell removal remains available to ordinary authoring with one notification.");

        var protectedTerrain = new GridHeightField(10, 10, 2); var protectedGrid = new SquareBuildGrid(Vector3.zero, 2, 10, 10, protectedTerrain);
        var protectedCells = Box(2, 2, 4, 4); var protectedBefore = protectedTerrain.Capture();
        protectedGrid.TryOccupy(C(3, 3), BuildCellContent.Path);
        Check(!protectedTerrain.TryPlanEnclosedBasin(protectedGrid, protectedCells, .08f, 1.2f, null, out _, out _)
            && protectedTerrain.Capture().heights.SequenceEqual(protectedBefore.heights), "A path in the footprint prevents all automatic earthworks.");
        protectedGrid.TryRemove(C(3, 3), BuildCellContent.Path);
        var footprint = new BuildingFootprint(new BuildingDefinition { width = 1, depth = 1, kind = BuildingKind.Kiosk }, C(3, 3), 0);
        Check(protectedTerrain.RegisterFoundation(new object(), footprint), "Foundation reservations can protect otherwise empty ground.");
        Check(!protectedTerrain.TryPlanEnclosedBasin(protectedGrid, protectedCells, .08f, 1.2f, null, out _, out _), "Foundation and entrance locks survive rejected pool creation.");
        Check(!protectedTerrain.TryPlanEnclosedBasin(protectedGrid, protectedCells, float.NaN, 1.2f, null, out _, out _), "Invalid elevations cannot generate pool edits.");

        var saved = new SavedSpecialEnclosures(); saved.completedPoolCells.Add(C(2, 2));
        saved.terrain.Add(new TerrainVertexChange { node = C(3, 3), original = 0, applied = -1.2f });
        saved.water.Add(new SavedPoolWaterReceipt { cell = C(2, 2), originalLevel = 0, appliedLevel = .08f });
        SpecialEnclosureConstruction.ValidateSave(saved, 10, 10); Check(true, "Sparse completion receipts are valid independent of ordinary terrain snapshots.");
        saved.completedPoolCells.Add(C(2, 2));
        bool rejected = false; try { SpecialEnclosureConstruction.ValidateSave(saved, 10, 10); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Duplicate completion cells are rejected instead of growing state on repeated load.");
        Console.WriteLine("PASS: " + checks + " completed special-enclosure topology, exact pool earthworks, batched water and sparse-save checks.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Called by a prepared disposable Play-mode fixture; does not launch or save a player park.</summary>
public static class SpecialEnclosureNativeChecks
{
    private static int checks;
    private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    private static void Check(bool value, string why) { if (!value) throw new Exception("Special enclosures: " + why); checks++; }
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
    public static int RunChecks()
    {
        checks = 0;
        var imported = Resources.Load<SpecialEnclosureDefinition>("Enclosures/DefaultSpecialEnclosures");
        Check(imported != null && imported.rules != null && imported.rules.aviaryHeight >= 6
            && imported.rules.aquariumDepth >= 1 && imported.rules.minimumPoolCells >= 4,
            "the editable cage and aquarium rules import as a real resource");
        var settings = Object.Instantiate(imported);
        var root = new GameObject("Special enclosure native fixture");
        root.transform.SetPositionAndRotation(new Vector3(5, 0, 8), Quaternion.Euler(0, 20, 0));
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        var terrain = new GridHeightField(26, 24, 2);
        var origin = new Vector3(144000, 30, -144000);
        var grid = new SquareBuildGrid(origin, 2, 26, 24, terrain);
        var fences = new FenceLayout(grid);
        var map = new SpecialEnclosureMap(grid, fences, settings.rules);
        bool temporaryBlock = true;
        var structures = new SpecialEnclosureConstruction(grid, map, root.transform, material, settings,
            cell => !temporaryBlock || cell != C(15, 14));
        HabitatTerrainSurface surface = null;
        SpecialEnclosureConstruction restored = null;
        try
        {
            var cage = Box(3, 3, 6, 6); var aviary = Ring(fences, cage, FenceKind.Aviary); var gap = aviary[0];
            fences.TryRemove(gap); structures.Refresh();
            Check(!map.IsAviary(C(5, 5)) && !ActiveMeshes(root).Any(item => item.name.StartsWith("Complete aviary roof")),
                "an unfinished mesh ring has neither roof geometry nor flight containment");
            fences.TryAdd(gap, FenceKind.Aviary); structures.Refresh();
            var roof = ActiveMeshes(root).Single(item => item.name.StartsWith("Complete aviary roof"));
            var mesh = roof.sharedMesh;
            Check(map.IsAviary(C(5, 5)) && mesh != null && mesh.vertexCount > 100 && mesh.triangles.Length > 0,
                "closing the final segment creates an actual large cage mesh");
            Check(roof.GetComponent<MeshRenderer>().bounds.max.y >= map.FlightCeiling(C(5, 5)) - .01f
                && Math.Abs(roof.GetComponent<MeshRenderer>().bounds.min.y - origin.y) < .1f,
                "support posts and roof use world terrain coordinates even under a transformed parent");
            Check(mesh.normals.Length == mesh.vertexCount && mesh.normals.All(normal => normal.sqrMagnitude > .5f)
                && roof.GetComponent<Collider>() == null && roof.gameObject.layer == 2,
                "cage normals face real surfaces and the visual roof cannot block flight Physics queries or selection");
            int roofIdentity = roof.gameObject.GetInstanceID();
            for (int i = 0; i < 10; i++) structures.Refresh();
            Check(ActiveMeshes(root).Single(item => item.name.StartsWith("Complete aviary roof")).gameObject.GetInstanceID() == roofIdentity,
                "unchanged refreshes reuse the same cage instead of rebuilding meshes every frame");
            fences.TrySetGate(gap); structures.Refresh();
            Check(map.IsAviary(C(5, 5)) && !map.CanFlyAcross(gap.CellA, gap.CellB), "a staff gate retains the roof and its underlying mesh protection");
            fences.TryRemove(gap); structures.Refresh();
            Check(!map.IsAviary(C(5, 5)) && !ActiveMeshes(root).Any(item => item.name.StartsWith("Complete aviary roof")),
                "a real breach disables the roof mesh immediately and releases air routes");
            fences.TryAdd(gap, FenceKind.Aviary); structures.Refresh();

            var poolCells = Box(14, 3, 4, 4); var poolRing = Ring(fences, poolCells, FenceKind.Aquarium); var opening = poolRing[0];
            fences.TryRemove(opening); structures.Refresh();
            Check(!poolCells.Any(grid.Habitat.HasWaterSurface), "an unfinished aquarium never mutates water or ground");
            var untouchedOutside = terrain.Node(13, 4); fences.TryAdd(opening, FenceKind.Aquarium); structures.Refresh();
            Check(poolCells.All(grid.Habitat.IsWater) && poolCells.All(cell => grid.Habitat.WaterDepth(cell) >= .35f)
                && terrain.Node(15, 4) < -.5f && terrain.Node(13, 4) == untouchedOutside,
                "closing a glass loop excavates and fills a real pool without moving outside rim terrain");
            Check(ActiveMeshes(root).Any(item => item.name.StartsWith("Complete aquarium walls")), "a completed pool has visible retaining glass as well as water data");
            surface = new HabitatTerrainSurface(grid, null, root.transform); surface.Refresh();
            var water = ActiveMeshes(root).Where(item => item.name.StartsWith("Pond water")).ToArray();
            Check(water.Length > 0 && water.All(item => item.sharedMesh != null && item.sharedMesh.triangles.Length > 0),
                "the ordinary habitat surface renderer draws the generated pool");
            var groundBefore = terrain.Capture().heights; var waterBefore = grid.Habitat.Capture(); var receipt = structures.Capture();
            Check(receipt.completedPoolCells.Count == poolCells.Count && receipt.terrain.Count == 9 && receipt.water.Count == poolCells.Count,
                "one-time completion receipts store only affected cells and interior vertices");
            fences.TryRemove(opening); structures.Refresh();
            Check(!map.IsAquarium(C(15, 4)) && poolCells.All(grid.Habitat.IsWater)
                && terrain.Capture().heights.SequenceEqual(groundBefore), "demolition loses containment while retaining all excavated ground and water");
            var editCell = C(15, 4);
            float requestedWater = origin.y + .4f;
            Check(grid.Habitat.TrySetWater(editCell, requestedWater), "a later authored water edit is accepted before repairing the pool");
            float authoredWater = grid.Habitat.WaterLevel(editCell);
            Check(Mathf.Approximately(authoredWater, requestedWater),
                $"the habitat stores the requested authored water level (requested {requestedWater:R}, actual {authoredWater:R})");
            var edits = terrain.Capture().heights; edits[4 * (terrain.Width + 1) + 15] += .1f; terrain.ImportHeights(edits);
            var authoredGround = terrain.Capture().heights;
            fences.TryAdd(opening, FenceKind.Aquarium); structures.Refresh();
            float repairedWater = grid.Habitat.WaterLevel(editCell); var repairedGround = terrain.Capture().heights;
            var groundChanges = repairedGround.Select((height, index) => new { height, index })
                .Where(item => item.height != authoredGround[item.index]).Take(5)
                .Select(item => $"{item.index}: {authoredGround[item.index]:R}->{item.height:R}");
            Check(Mathf.Approximately(repairedWater, authoredWater) && Mathf.Approximately(repairedWater, requestedWater)
                && repairedGround.SequenceEqual(authoredGround),
                $"repair and reclosure never refill or re-excavate over later authored edits (water {authoredWater:R}->{repairedWater:R}; ground diffs {string.Join(", ", groundChanges)})");
            var json = JsonUtility.ToJson(structures.Capture());
            var snapshot = JsonUtility.FromJson<SavedSpecialEnclosures>(json);
            SpecialEnclosureConstruction.ValidateSave(snapshot, grid.Width, grid.Depth);
            Check(snapshot.completedPoolCells.Count == poolCells.Count && snapshot.terrain.Count == 9, "native JSON retains sparse completion and earthwork metadata");
            var terrainSave = terrain.Capture(); var habitatSave = grid.Habitat.Capture();
            var otherTerrain = new GridHeightField(grid.Width, grid.Depth, grid.CellSize); otherTerrain.Restore(terrainSave);
            var otherGrid = new SquareBuildGrid(origin, grid.CellSize, grid.Width, grid.Depth, otherTerrain); otherGrid.Habitat.Restore(habitatSave);
            var otherFences = new FenceLayout(otherGrid);
            foreach (var edge in fences.Edges) { otherFences.TryAdd(edge, fences.KindAt(edge)); if (fences.IsGate(edge)) otherFences.TrySetGate(edge); }
            var otherMap = new SpecialEnclosureMap(otherGrid, otherFences, settings.rules);
            restored = new SpecialEnclosureConstruction(otherGrid, otherMap, root.transform, material, settings);
            restored.Restore(snapshot); int waterRevision = otherGrid.Habitat.Revision, terrainRevision = otherTerrain.Revision;
            restored.Refresh(); restored.Refresh();
            Check(otherGrid.Habitat.Revision == waterRevision && otherTerrain.Revision == terrainRevision
                && otherTerrain.Capture().heights.SequenceEqual(authoredGround)
                && Mathf.Approximately(otherGrid.Habitat.WaterLevel(editCell), authoredWater),
                $"restoring ordinary terrain plus receipts is idempotent and preserves later manual water levels (water {authoredWater:R}->{otherGrid.Habitat.WaterLevel(editCell):R}; water revision {waterRevision}->{otherGrid.Habitat.Revision}; terrain revision {terrainRevision}->{otherTerrain.Revision})");
            Check(otherMap.IsAviary(C(5, 5)) && otherMap.IsAquarium(C(15, 4)), "roof and pool containment reconstruct from saved fence topology");
            otherFences.TryRemove(opening); restored.Refresh();
            Check(otherGrid.Habitat.IsWater(editCell), "a loaded pool also keeps its water after another breach");

            var blockedCells = Box(14, 12, 4, 4); Ring(fences, blockedCells, FenceKind.Aquarium);
            var beforeBlocked = terrain.Capture().heights; structures.Refresh();
            Check(structures.HasBlockedPools && !blockedCells.Any(grid.Habitat.HasWaterSurface)
                && terrain.Capture().heights.SequenceEqual(beforeBlocked), "temporary protected occupancy rejects the entire automatic pool transaction");
            temporaryBlock = false; structures.Tick(settings.rules.blockedPoolRetrySeconds + .1f);
            Check(!structures.HasBlockedPools && blockedCells.All(grid.Habitat.IsWater), "a bounded retry activates a pool after its temporary obstruction moves");
            var pondCells = Box(3, 14, 4, 4); grid.Habitat.TrySetWater(C(4, 15), origin.y + .9f);
            var pondGround = terrain.Capture().heights; var pondWater = grid.Habitat.Capture();
            Ring(fences, pondCells, FenceKind.Aquarium); structures.Refresh();
            Check(map.IsAquarium(C(4, 15)) && terrain.Capture().heights.SequenceEqual(pondGround)
                && JsonUtility.ToJson(grid.Habitat.Capture()) == JsonUtility.ToJson(pondWater),
                "closing walls around an existing authored pond preserves its whole bed and water data");
            var independent = structures.Capture(); structures.Restore(independent); independent.completedPoolCells.Clear();
            Check(structures.Capture().completedPoolCells.Count >= poolCells.Count + blockedCells.Count + pondCells.Count,
                "save restoration takes copies and does not retain mutable caller-owned receipt lists");
        }
        finally
        {
            root.SetActive(false); restored?.Dispose(); surface?.Dispose(); structures.Dispose();
            Object.Destroy(root); Object.Destroy(material); Object.Destroy(settings);
        }
        return checks;
    }
    private static IEnumerable<MeshFilter> ActiveMeshes(GameObject parent)
        => parent.GetComponentsInChildren<MeshFilter>(true).Where(item => item.gameObject.activeInHierarchy);
}

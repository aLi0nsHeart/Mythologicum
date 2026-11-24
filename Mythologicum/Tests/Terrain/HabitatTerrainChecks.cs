using System;
using System.IO;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class HabitatTerrainChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .002f;
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static SquareBuildGrid Grid() => new SquareBuildGrid(new Vector3(-4, 3, 8), 2, 24, 24, new GridHeightField(24, 24, 2));
    private static void Reject(Action action, string reason)
    { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, reason); }

    public static void Main()
    {
        var grid = Grid(); var habitat = grid.Habitat;
        Check(habitat.BiomeAt(V(10, 10)) == HabitatBiome.Grass && !habitat.HasBiomeOverride(V(10, 10)), "Unpainted and old parks retain grass habitat");
        Check(!habitat.IsWater(V(10, 10)) && habitat.WaterDepth(V(10, 10)) == 0 && grid.GroundWalkable(V(10, 10)), "Dry terrain remains normal land");
        int revision = grid.Revision;
        foreach (HabitatBiome biome in Enum.GetValues(typeof(HabitatBiome)))
        {
            Check(habitat.PaintBiome(new Vector2(10.5f, 10.5f), .4f, biome, null, out _), "Every requested biome can be painted");
            Check(habitat.BiomeAt(V(10, 10)) == biome && habitat.HasBiomeOverride(V(10, 10)), "Painting stores the gameplay biome independently of height and enclosures");
        }
        Check(grid.Revision > revision, "Biome edits invalidate environment and navigation snapshots");
        Check(!habitat.PaintBiome(new Vector2(10.5f, 10.5f), .4f, HabitatBiome.Sand, null, out _), "Holding an unchanged biome avoids redundant revisions");
        Check(!habitat.PaintBiome(new Vector2(-2, 1), 1, HabitatBiome.Dirt, null, out _)
            && !habitat.PaintBiome(new Vector2(10, 10), float.NaN, HabitatBiome.Dirt, null, out _), "Invalid and off-map brushes fail safely");
        Check(!habitat.PaintBiome(new Vector2(10, 10), 1, (HabitatBiome)99, null, out _), "Unknown biome IDs cannot enter game state");

        Check(habitat.TrySetWater(V(6, 6), 4.2f) && Near(habitat.WaterDepth(V(6, 6)), 1.2f), "Water surfaces and bed depths use the map's actual world elevation");
        Check(habitat.IsWater(V(6, 6)) && !grid.GroundWalkable(V(6, 6)) && !grid.IsAvailable(V(6, 6)), "Water blocks ordinary land construction and movement");
        Check(!new CreatureNavigation(grid, new FenceLayout(grid)).IsWalkable(V(6, 6)), "Land creature navigation does not enter painted water");
        Check(!grid.TryOccupy(V(6, 6), BuildCellContent.Path), "Paths cannot silently cover a pond");
        Check(Near(grid.CellCenter(V(6, 6)).y, 3), "Surface water does not replace the seabed height sampler");
        Check(habitat.TrySetWater(V(6, 6), 4.8f) && Near(habitat.WaterLevel(V(6, 6)), 4.8f), "An existing water surface can change level");
        Check(habitat.PaintBiome(new Vector2(6.5f, 6.5f), .4f, HabitatBiome.Sand, cell => true, out _), "Underwater ground can have its own biome for future aquatic habitats");

        var pond = Grid(); int before = pond.Terrain.Revision;
        Check(pond.Habitat.AddWater(new Vector2(10.5f, 10.5f), 3, 3, 1.2f, cell => true, out _), "The water brush excavates a real bounded pond");
        Check(pond.Terrain.Revision > before && pond.CellCenter(V(10, 10)).y < 2.5f && pond.Habitat.WaterDepth(V(10, 10)) > .5f,
            "A pond has a real lower bed and a separate water depth");
        Check(pond.Habitat.WaterCells.Any() && pond.Habitat.WaterCells.All(cell => Near(pond.Habitat.WaterLevel(cell), 3)), "One pond brush uses a level water surface");
        Check(!pond.Habitat.IsWater(V(15, 10)) && Near(pond.CellCenter(V(15, 10)).y, 3), "Pond creation does not flood or lower distant land");
        Check(pond.Habitat.AddWater(new Vector2(11.5f, 10.5f), 3, 3, 1.2f, cell => true, out _), "A pond can be extended through its existing wet cells");
        float excavated = pond.CellCenter(V(10, 10)).y;
        Check(pond.Habitat.RemoveWater(new Vector2(10.5f, 10.5f), 6, null, out _) && !pond.Habitat.WaterCells.Any(), "The drain brush removes the water surface");
        Check(Near(pond.CellCenter(V(10, 10)).y, excavated), "Draining leaves the dug terrain available for further sculpting");

        var blocked = Grid(); var originalHeights = blocked.Terrain.Capture().heights;
        Check(!blocked.Habitat.AddWater(new Vector2(10.5f, 10.5f), 1, 3, 1.2f, cell => cell != V(9, 9), out _),
            "Shared-corner excavation respects a protected neighbour outside the selected cell");
        Check(originalHeights.SequenceEqual(blocked.Terrain.Capture().heights) && !blocked.Habitat.WaterSurfaceCells.Any(), "Blocked pond brushes leave both terrain and water unchanged");
        blocked.TryOccupy(V(10, 10), BuildCellContent.Path);
        Check(!blocked.Habitat.AddWater(new Vector2(10.5f, 10.5f), 2, 3, 1.2f, cell => true, out _), "An existing public path prevents pond creation");
        Check(originalHeights.SequenceEqual(blocked.Terrain.Capture().heights), "Rejected path flooding does not perform partial earthworks");
        var locked = Grid(); var owner = new object();
        Check(locked.Terrain.RegisterFoundation(owner, new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.Kiosk, width = 1, depth = 1 }, V(12, 12), 0)), "The water fixture protects a real building foundation");
        Check(!locked.Habitat.AddWater(new Vector2(10.5f, 10.5f), 3, 3, 1.2f, cell => true, out _), "Foundation nodes cannot be excavated by water brushes");
        Check(!locked.Habitat.AddWater(new Vector2(10.5f, 10.5f), 3, 3, -1, cell => true, out _), "Negative pond depths fail before mutation");

        var captured = grid.Habitat.Capture(); var restored = new SquareBuildGrid(new Vector3(4, 11, -8), 2, 24, 24, new GridHeightField(24, 24, 2));
        restored.Habitat.Restore(captured);
        Check(restored.Habitat.BiomeAt(V(10, 10)) == HabitatBiome.Sand && restored.Habitat.IsWater(V(6, 6)), "Habitat capture restores painted ground and water");
        Check(Near(restored.Habitat.WaterDepth(V(6, 6)), 1.8f) && Near(restored.Habitat.WaterLevel(V(6, 6)), 12.8f), "Saved pond surfaces are relative to the map origin");
        var raised = Enumerable.Repeat(2f, 25 * 25).ToArray(); restored.Terrain.ImportHeights(raised);
        Check(!restored.Habitat.IsWater(V(6, 6)) && restored.Habitat.WaterDepth(V(6, 6)) == 0, "Raised ground can dry a previously painted pond surface");
        var dryCapture = restored.Habitat.Capture(); restored.Habitat.Restore(dryCapture);
        Check(!restored.Habitat.IsWater(V(6, 6)) && restored.Habitat.HasWaterSurface(V(6, 6)), "Dry, raised pond beds remain saveable without discarding their surface level");
        restored.Terrain.ImportHeights(new float[25 * 25]);
        Check(restored.Habitat.IsWater(V(6, 6)), "Lowering a retained pond bed exposes water again");
        var duplicate = grid.Habitat.Capture(); duplicate.water.Add(duplicate.water[0]);
        Reject(() => restored.Habitat.Restore(duplicate), "Duplicate water-cell records are rejected before restoring");
        Check(restored.Habitat.IsWater(V(6, 6)), "Invalid restore leaves the existing pond intact");
        var invalid = new SavedHabitatTerrain(); invalid.water.Add(new SavedWaterCell { cell = V(4, 4), level = float.NaN });
        Reject(() => HabitatTerrain.ValidateSave(invalid, 24, 24), "Nonfinite surface levels cannot enter saves");
        invalid = new SavedHabitatTerrain(); invalid.biomes.Add(new SavedBiomeCell { cell = V(24, 4), biome = HabitatBiome.Stone });
        Reject(() => HabitatTerrain.ValidateSave(invalid, 24, 24), "Out-of-map biome records cannot enter saves");
        invalid = new SavedHabitatTerrain(); invalid.biomes.Add(new SavedBiomeCell { cell = V(4, 4), biome = (HabitatBiome)99 });
        Reject(() => HabitatTerrain.ValidateSave(invalid, 24, 24), "Unknown saved biome types are rejected");
        restored.Habitat.Restore(null);
        Check(!restored.Habitat.WaterSurfaceCells.Any() && restored.Habitat.BiomeAt(V(10, 10)) == HabitatBiome.Grass, "Older saves without habitat data load grass and no added water");
        Check(!restored.Terrain.TryEdit(restored, new Vector2(10, 10), 3, 1, TerrainBrushMode.Grass, 0, out _), "Biome modes cannot accidentally enter the height-sculpting fallback");
        Console.WriteLine("PASS: " + checks + " habitat biome, bounded pond, bank protection, navigation and save checks.");
    }
}

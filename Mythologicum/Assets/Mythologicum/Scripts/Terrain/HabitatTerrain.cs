using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mythologicum
{
    public enum HabitatBiome { Grass, Dirt, Thicket, Stone, Sand }

    public readonly struct WaterSurfaceEdit
    {
        public Vector2Int Cell { get; }
        public bool HasSurface { get; }
        public float WorldLevel { get; }
        public WaterSurfaceEdit(Vector2Int cell, bool hasSurface, float worldLevel = 0)
        { Cell = cell; HasSurface = hasSurface; WorldLevel = worldLevel; }
    }

    [Serializable] public sealed class SavedBiomeCell
    {
        public Vector2Int cell;
        public HabitatBiome biome;
    }
    [Serializable] public sealed class SavedWaterCell
    {
        public Vector2Int cell;
        // Surface elevation relative to the map origin; the seabed remains ordinary terrain.
        public float level;
    }
    [Serializable] public sealed class SavedHabitatTerrain
    {
        public int version = 1;
        public List<SavedBiomeCell> biomes = new List<SavedBiomeCell>();
        public List<SavedWaterCell> water = new List<SavedWaterCell>();
    }

    /// <summary>Editable ground types and bounded pond surfaces, independent of creature/enclosure ownership.</summary>
    public sealed class HabitatTerrain
    {
        public const float ActiveWaterDepth = .05f;
        private readonly SquareBuildGrid grid;
        private readonly HabitatBiome[] biomes;
        private readonly bool[] painted, water;
        private readonly float[] levels;
        public int Revision { get; private set; }
        public Vector2Int ChangedMinimum { get; private set; }
        public Vector2Int ChangedMaximum { get; private set; }
        public event Action Changed;
        public HabitatTerrain(SquareBuildGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            int count = checked(grid.Width * grid.Depth);
            biomes = new HabitatBiome[count]; painted = new bool[count]; water = new bool[count]; levels = new float[count];
            if (grid.Terrain != null) grid.Terrain.Changed += () => Revision++;
        }
        private int Index(Vector2Int cell) => cell.y * grid.Width + cell.x;
        public HabitatBiome BiomeAt(Vector2Int cell) => grid.Contains(cell) ? biomes[Index(cell)] : HabitatBiome.Grass;
        public bool HasBiomeOverride(Vector2Int cell) => grid.Contains(cell) && painted[Index(cell)];
        public bool HasWaterSurface(Vector2Int cell) => grid.Contains(cell) && water[Index(cell)];
        public float WaterLevel(Vector2Int cell) => HasWaterSurface(cell) ? grid.Origin.y + levels[Index(cell)] : float.NaN;
        public float WaterDepth(Vector2Int cell) => HasWaterSurface(cell) ? Math.Max(0, WaterLevel(cell) - grid.CellCenter(cell).y) : 0f;
        public bool IsWater(Vector2Int cell) => HasWaterSurface(cell) && WaterDepth(cell) > ActiveWaterDepth;
        public bool HasWater(Vector2Int cell) => IsWater(cell);
        public IEnumerable<Vector2Int> WaterCells
        {
            get { for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++) { var cell = new Vector2Int(x, z); if (IsWater(cell)) yield return cell; } }
        }
        public IEnumerable<Vector2Int> WaterSurfaceCells
        {
            get { for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++) { var cell = new Vector2Int(x, z); if (HasWaterSurface(cell)) yield return cell; } }
        }

        public bool PaintBiome(Vector2 center, float radius, HabitatBiome biome, Predicate<Vector2Int> canEdit, out string reason)
        {
            reason = "";
            if (!ValidBrush(center, radius) || !Enum.IsDefined(typeof(HabitatBiome), biome))
            { reason = Loc.Text("habitat.terrain.invalid_brush"); return false; }
            bool changed = false; var minimum = new Vector2Int(grid.Width, grid.Depth); var maximum = new Vector2Int(-1, -1);
            foreach (var cell in BrushCells(center, radius))
            {
                if (!(canEdit?.Invoke(cell) ?? (grid.ContentAt(cell) == BuildCellContent.Empty))) continue;
                int index = Index(cell);
                if (painted[index] && biomes[index] == biome) continue;
                painted[index] = true; biomes[index] = biome; changed = true; Include(cell, ref minimum, ref maximum);
            }
            if (changed) Touch(minimum, maximum);
            return changed;
        }

        public bool AddWater(Vector2 center, float radius, float worldLevel, float depth, Predicate<Vector2Int> canEdit, out string reason)
        {
            reason = "";
            if (!ValidBrush(center, radius) || !Finite(worldLevel) || !Finite(depth) || depth <= ActiveWaterDepth || depth > 100f
                || Math.Abs(worldLevel - grid.Origin.y) > 10000f || grid.Terrain == null)
            { reason = Loc.Text("habitat.terrain.invalid_water"); return false; }
            var selected = new List<Vector2Int>(BrushCells(center, radius));
            foreach (var cell in selected)
                if (grid.ContentAt(cell) != BuildCellContent.Empty || !(canEdit?.Invoke(cell) ?? true))
                { reason = Loc.Text("habitat.terrain.water_blocked"); return false; }
            if (selected.Count == 0) return false;
            int before = grid.Terrain.Revision;
            if (!grid.Terrain.TryExcavateBasin(grid, center, radius, worldLevel - grid.Origin.y, depth,
                cell => grid.ContentAt(cell) == BuildCellContent.Empty && (canEdit?.Invoke(cell) ?? true), out reason)) return false;
            bool changed = grid.Terrain.Revision != before;
            var minimum = new Vector2Int(grid.Width, grid.Depth); var maximum = new Vector2Int(-1, -1);
            float relative = worldLevel - grid.Origin.y;
            foreach (var cell in selected)
            {
                // Retain only surfaces intersecting the terrain; banks stay dry.
                if (grid.CellCenter(cell).y >= worldLevel - ActiveWaterDepth) continue;
                int index = Index(cell);
                if (water[index] && Math.Abs(levels[index] - relative) < .0001f) continue;
                water[index] = true; levels[index] = relative; changed = true; Include(cell, ref minimum, ref maximum);
            }
            if (changed)
            {
                if (maximum.x < 0) { minimum = Vector2Int.zero; maximum = new Vector2Int(grid.Width - 1, grid.Depth - 1); }
                Touch(minimum, maximum);
            }
            return changed;
        }

        public bool RemoveWater(Vector2 center, float radius, Predicate<Vector2Int> canEdit, out string reason)
        {
            reason = "";
            if (!ValidBrush(center, radius)) { reason = Loc.Text("habitat.terrain.invalid_brush"); return false; }
            bool changed = false; var minimum = new Vector2Int(grid.Width, grid.Depth); var maximum = new Vector2Int(-1, -1);
            foreach (var cell in BrushCells(center, radius))
            {
                if (!(canEdit?.Invoke(cell) ?? true) || !HasWaterSurface(cell)) continue;
                water[Index(cell)] = false; levels[Index(cell)] = 0; changed = true; Include(cell, ref minimum, ref maximum);
            }
            if (changed) Touch(minimum, maximum);
            return changed;
        }

        /// <summary>Fixture/import helper; the player tool uses AddWater to create a contained basin first.</summary>
        public bool TrySetWater(Vector2Int cell, float worldLevel)
        {
            if (!grid.Contains(cell) || grid.ContentAt(cell) != BuildCellContent.Empty || !Finite(worldLevel) || Math.Abs(worldLevel - grid.Origin.y) > 10000f) return false;
            int index = Index(cell); float relative = worldLevel - grid.Origin.y;
            if (water[index] && Math.Abs(levels[index] - relative) < .0001f) return false;
            water[index] = true; levels[index] = relative; Touch(cell, cell); return true;
        }

        /// <summary>Applies exact-cell automatic pool edits with one render/navigation notification.</summary>
        public bool TryApplyWaterSurfaces(IReadOnlyList<WaterSurfaceEdit> edits, out string reason)
        {
            reason = "";
            if (edits == null) { reason = Loc.Text("habitat.terrain.invalid_water"); return false; }
            foreach (var edit in edits)
                if (!grid.Contains(edit.Cell) || edit.HasSurface && (grid.ContentAt(edit.Cell) != BuildCellContent.Empty
                    || !Finite(edit.WorldLevel) || Math.Abs(edit.WorldLevel - grid.Origin.y) > 10000f))
                { reason = Loc.Text("habitat.terrain.water_blocked"); return false; }
            bool changed = false; var minimum = new Vector2Int(grid.Width, grid.Depth); var maximum = new Vector2Int(-1, -1);
            foreach (var edit in edits)
            {
                int index = Index(edit.Cell); float level = edit.HasSurface ? edit.WorldLevel - grid.Origin.y : 0;
                if (water[index] == edit.HasSurface && (!edit.HasSurface || Math.Abs(levels[index] - level) < .00001f)) continue;
                water[index] = edit.HasSurface; levels[index] = level; changed = true; Include(edit.Cell, ref minimum, ref maximum);
            }
            if (changed) Touch(minimum, maximum);
            return true;
        }

        public SavedHabitatTerrain Capture()
        {
            var saved = new SavedHabitatTerrain();
            for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++)
            {
                var cell = new Vector2Int(x, z); int index = Index(cell);
                if (painted[index]) saved.biomes.Add(new SavedBiomeCell { cell = cell, biome = biomes[index] });
                if (water[index]) saved.water.Add(new SavedWaterCell { cell = cell, level = levels[index] });
            }
            return saved;
        }
        public static void ValidateSave(SavedHabitatTerrain saved, int width, int depth)
        {
            if (saved == null) return;
            if (saved.version != 1 || width < 1 || depth < 1 || saved.biomes == null || saved.water == null
                || saved.biomes.Count > width * depth || saved.water.Count > width * depth)
                throw new InvalidDataException(Loc.Text("habitat.terrain.invalid_save"));
            bool Contains(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;
            var unique = new HashSet<Vector2Int>();
            foreach (var item in saved.biomes)
                if (item == null || !Contains(item.cell) || !Enum.IsDefined(typeof(HabitatBiome), item.biome) || !unique.Add(item.cell))
                    throw new InvalidDataException(Loc.Text("habitat.terrain.invalid_save"));
            unique.Clear();
            foreach (var item in saved.water)
                if (item == null || !Contains(item.cell) || !Finite(item.level) || Math.Abs(item.level) > 10000f || !unique.Add(item.cell))
                    throw new InvalidDataException(Loc.Text("habitat.terrain.invalid_save"));
        }
        public void Restore(SavedHabitatTerrain saved)
        {
            ValidateSave(saved, grid.Width, grid.Depth);
            Array.Clear(painted, 0, painted.Length); Array.Clear(biomes, 0, biomes.Length); Array.Clear(water, 0, water.Length); Array.Clear(levels, 0, levels.Length);
            if (saved != null)
            {
                foreach (var item in saved.biomes) { int index = Index(item.cell); painted[index] = true; biomes[index] = item.biome; }
                foreach (var item in saved.water) { int index = Index(item.cell); water[index] = true; levels[index] = item.level; }
            }
            Touch(Vector2Int.zero, new Vector2Int(grid.Width - 1, grid.Depth - 1));
        }
        private bool ValidBrush(Vector2 center, float radius) => Finite(center.x) && Finite(center.y) && Finite(radius) && radius > 0f
            && radius <= Math.Max(grid.Width, grid.Depth) * 2 && center.x >= 0 && center.y >= 0 && center.x <= grid.Width && center.y <= grid.Depth;
        private IEnumerable<Vector2Int> BrushCells(Vector2 center, float radius)
        {
            for (int z = Math.Max(0, (int)Math.Floor(center.y - radius)); z <= Math.Min(grid.Depth - 1, (int)Math.Floor(center.y + radius)); z++)
            for (int x = Math.Max(0, (int)Math.Floor(center.x - radius)); x <= Math.Min(grid.Width - 1, (int)Math.Floor(center.x + radius)); x++)
            {
                float dx = x + .5f - center.x, dz = z + .5f - center.y;
                if (dx * dx + dz * dz < radius * radius) yield return new Vector2Int(x, z);
            }
        }
        private static void Include(Vector2Int cell, ref Vector2Int minimum, ref Vector2Int maximum)
        { minimum = Vector2Int.Min(minimum, cell); maximum = Vector2Int.Max(maximum, cell); }
        private void Touch(Vector2Int minimum, Vector2Int maximum)
        { ChangedMinimum = minimum; ChangedMaximum = maximum; Revision++; Changed?.Invoke(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

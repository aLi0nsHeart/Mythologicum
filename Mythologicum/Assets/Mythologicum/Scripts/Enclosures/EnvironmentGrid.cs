using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public readonly struct EnvironmentStats
    {
        public int Tiles { get; }
        public float Area { get; }
        public float ForestArea { get; }
        public float ForestPercent => Area > 0f ? ForestArea / Area * 100f : 0f;
        public EnvironmentStats(int tiles, float area, float forestArea)
        { Tiles = tiles; Area = area; ForestArea = forestArea; }
    }

    /// <summary>Environment exists across the whole map, independently of fences or creature ownership.</summary>
    public sealed class EnvironmentGrid
    {
        private readonly SquareBuildGrid grid;
        private readonly Dictionary<Vector2Int, float> forest = new Dictionary<Vector2Int, float>();
        private readonly HashSet<Vector2Int> trees = new HashSet<Vector2Int>();
        public int Revision { get; private set; }
        public EnvironmentGrid(SquareBuildGrid grid) => this.grid = grid;

        public int TreesAt(Vector2Int cell) => trees.Contains(cell) ? 1 : 0;

        public int TreeCount(IEnumerable<Vector2Int> cells)
        {
            int count = 0;
            var visited = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cells)
                if (visited.Add(cell) && trees.Contains(cell)) count++;
            return count;
        }

        public bool AddTree(Vector2Int cell)
        {
            if (!grid.Contains(cell) || !trees.Add(cell)) return false;
            Revision++;
            return true;
        }

        public bool RemoveTree(Vector2Int cell)
        {
            if (!trees.Remove(cell)) return false;
            Revision++;
            return true;
        }

        public void SetForestCoverage(Vector2Int cell, float fraction)
        {
            if (!grid.Contains(cell) || float.IsNaN(fraction) || fraction < 0f || fraction > 1f)
                throw new ArgumentOutOfRangeException(nameof(fraction));
            if (forest.TryGetValue(cell, out float old) && old == fraction) return;
            forest[cell] = fraction;
            Revision++;
        }

        public EnvironmentStats Sample(IEnumerable<Vector2Int> cells)
        {
            int count = 0;
            float forestTiles = 0f;
            foreach (Vector2Int cell in cells)
            {
                if (!grid.Contains(cell)) continue;
                count++;
                forest.TryGetValue(cell, out float value);
                // Authored ground coverage and constructed trees are independent layers.
                // Demolishing a tree must restore the underlying environment exactly.
                forestTiles += Math.Min(1f, value + (trees.Contains(cell) ? 0.5f : 0f));
            }
            float cellArea = grid.CellSize * grid.CellSize;
            return new EnvironmentStats(count, count * cellArea, forestTiles * cellArea);
        }

        // Future creatures can sample their surroundings anywhere, even outside all enclosures.
        public EnvironmentStats SampleAround(Vector2Int center, int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            var cells = new List<Vector2Int>();
            for (int x = Math.Max(0, center.x - radius); x <= Math.Min(grid.Width - 1, center.x + radius); x++)
            for (int z = Math.Max(0, center.y - radius); z <= Math.Min(grid.Depth - 1, center.y + radius); z++)
                if ((long)(x - center.x) * (x - center.x) + (long)(z - center.y) * (z - center.y) <= (long)radius * radius)
                    cells.Add(new Vector2Int(x, z));
            return Sample(cells);
        }
    }
}

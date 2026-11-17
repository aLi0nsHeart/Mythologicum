using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Reverts only the habitat cells edited by one action, preserving later changes elsewhere.</summary>
    public sealed class HabitatUndoPatch
    {
        private readonly struct CellState
        {
            public readonly bool Painted, Water;
            public readonly HabitatBiome Biome;
            public readonly float Level;
            public CellState(bool painted, HabitatBiome biome, bool water, float level)
            { Painted = painted; Biome = biome; Water = water; Level = level; }
            public bool Same(CellState other) => Painted == other.Painted && (!Painted || Biome == other.Biome)
                && Water == other.Water && (!Water || Math.Abs(Level - other.Level) < .0001f);
            public bool Empty => !Painted && !Water;
        }
        private readonly Dictionary<Vector2Int, CellState> before = new Dictionary<Vector2Int, CellState>();
        private readonly Dictionary<Vector2Int, CellState> after = new Dictionary<Vector2Int, CellState>();
        public int Count => after.Count;
        public IEnumerable<Vector2Int> Cells => after.Keys;

        public HabitatUndoPatch(SavedHabitatTerrain original, SavedHabitatTerrain changed)
        {
            var old = Read(original); var current = Read(changed);
            var cells = new HashSet<Vector2Int>(old.Keys); cells.UnionWith(current.Keys);
            foreach (var cell in cells)
            {
                old.TryGetValue(cell, out var a); current.TryGetValue(cell, out var b);
                if (a.Same(b)) continue;
                before.Add(cell, a); after.Add(cell, b);
            }
        }
        public bool TryRevert(SavedHabitatTerrain current, out SavedHabitatTerrain restored)
        {
            restored = null; var values = Read(current);
            foreach (var pair in after)
            { values.TryGetValue(pair.Key, out var value); if (!value.Same(pair.Value)) return false; }
            foreach (var pair in before)
                if (pair.Value.Empty) values.Remove(pair.Key); else values[pair.Key] = pair.Value;
            restored = new SavedHabitatTerrain();
            var cells = new List<Vector2Int>(values.Keys);
            cells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            foreach (var cell in cells)
            {
                var state = values[cell];
                if (state.Painted) restored.biomes.Add(new SavedBiomeCell { cell = cell, biome = state.Biome });
                if (state.Water) restored.water.Add(new SavedWaterCell { cell = cell, level = state.Level });
            }
            return true;
        }
        private static Dictionary<Vector2Int, CellState> Read(SavedHabitatTerrain data)
        {
            var values = new Dictionary<Vector2Int, CellState>();
            if (data?.biomes != null) foreach (var item in data.biomes)
                values[item.cell] = new CellState(true, item.biome, false, 0);
            if (data?.water != null) foreach (var item in data.water)
            {
                values.TryGetValue(item.cell, out var old);
                values[item.cell] = new CellState(old.Painted, old.Biome, true, item.level);
            }
            return values;
        }
    }
}

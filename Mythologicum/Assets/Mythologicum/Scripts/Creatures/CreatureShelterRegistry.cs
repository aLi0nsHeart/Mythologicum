using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mythologicum
{
    public enum ConstructedShelterKind { Cave, Burrow }
    [Serializable] public sealed class SavedShelter { public Vector2Int cell; public ConstructedShelterKind kind; }

    /// <summary>Persistent physical sites. Creatures can use any reachable site rather than owning an enclosure.</summary>
    public sealed class CreatureShelterRegistry
    {
        private readonly SquareBuildGrid grid;
        private readonly Dictionary<Vector2Int, ConstructedShelterKind> entries = new Dictionary<Vector2Int, ConstructedShelterKind>();
        public int Revision { get; private set; }
        public int CaveCapacity { get; }
        public int BurrowCapacity { get; }
        public IEnumerable<Vector2Int> Cells => entries.Keys;
        public CreatureShelterRegistry(SquareBuildGrid grid, int caveCapacity = 3, int burrowCapacity = 1)
        { this.grid = grid ?? throw new ArgumentNullException(nameof(grid)); CaveCapacity = Math.Max(1, caveCapacity); BurrowCapacity = Math.Max(1, burrowCapacity); }
        public bool Occupies(Vector2Int cell) => entries.ContainsKey(cell);
        public bool Contains(Vector2Int cell, ConstructedShelterKind kind) => entries.TryGetValue(cell, out var actual) && actual == kind;
        public bool TryAdd(Vector2Int cell, ConstructedShelterKind kind)
        {
            if (!Enum.IsDefined(typeof(ConstructedShelterKind), kind) || !grid.Contains(cell) || !grid.IsAvailable(cell)
                || !grid.GroundWalkable(cell) || entries.ContainsKey(cell)) return false;
            entries.Add(cell, kind); Revision++; return true;
        }
        public bool TryRemove(Vector2Int cell) { if (!entries.Remove(cell)) return false; Revision++; return true; }
        public List<SavedShelter> Capture()
        {
            var result = new List<SavedShelter>();
            foreach (var entry in entries) result.Add(new SavedShelter { cell = entry.Key, kind = entry.Value });
            result.Sort((a,b) => a.cell.y != b.cell.y ? a.cell.y.CompareTo(b.cell.y) : a.cell.x.CompareTo(b.cell.x));
            return result;
        }
        public static void ValidateSaved(IEnumerable<SavedShelter> saved, SquareBuildGrid grid)
        {
            if (saved == null) return;
            var cells = new HashSet<Vector2Int>();
            foreach (var entry in saved)
                if (entry == null || !Enum.IsDefined(typeof(ConstructedShelterKind), entry.kind) || !grid.Contains(entry.cell)
                    || !grid.IsAvailable(entry.cell) || !grid.GroundWalkable(entry.cell) || !cells.Add(entry.cell))
                    throw new InvalidDataException(Loc.Text("habitat.shelter.invalid_save"));
        }
    }
}

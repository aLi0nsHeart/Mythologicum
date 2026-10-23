using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class EnclosureRecord
    {
        public int Id { get; }
        public HashSet<Vector2Int> Cells { get; internal set; }
        public bool IsSecured { get; internal set; }
        public int GateCount { get; internal set; }
        public bool HasStaffAccess { get; internal set; }
        public EnvironmentStats Environment { get; internal set; }
        public EnclosureRecord(int id, HashSet<Vector2Int> cells) { Id = id; Cells = cells; }
    }

    /// <summary>Spatial regions describe the park. They never own or constrain creatures.</summary>
    public sealed class EnclosureMap
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly EnvironmentGrid environment;
        private readonly List<EnclosureRecord> records = new List<EnclosureRecord>();
        public IReadOnlyList<EnclosureRecord> Records => records;
        public int Revision { get; private set; }
        private int fenceRevision = -1, environmentRevision = -1, nextId = 1;

        public EnclosureMap(SquareBuildGrid grid, FenceLayout fences, EnvironmentGrid environment)
        { this.grid = grid; this.fences = fences; this.environment = environment; }

        public void Refresh(Func<Vector2Int, bool> pathConnected)
        {
            bool geometryChanged = fenceRevision != fences.Revision;
            if (geometryChanged)
            {
                Rebuild();
                fenceRevision = fences.Revision;
                Revision++;
            }
            bool statsChanged = geometryChanged || environmentRevision != environment.Revision;
            environmentRevision = environment.Revision;
            foreach (EnclosureRecord record in records)
            {
                if (statsChanged) record.Environment = environment.Sample(record.Cells);
                record.GateCount = 0;
                record.HasStaffAccess = false;
                foreach (FenceEdge gate in fences.Gates)
                {
                    bool a = record.Cells.Contains(gate.CellA), b = record.Cells.Contains(gate.CellB);
                    if (a == b) continue;
                    record.GateCount++;
                    Vector2Int outside = a ? gate.CellB : gate.CellA;
                    if (record.IsSecured && pathConnected(outside)) record.HasStaffAccess = true;
                }
            }
        }

        private void Rebuild()
        {
            var closed = new List<HashSet<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();
            for (int x = 0; x < grid.Width; x++)
            for (int z = 0; z < grid.Depth; z++)
            {
                var seed = new Vector2Int(x, z);
                if (!visited.Add(seed)) continue;
                var region = new HashSet<Vector2Int> { seed };
                var pending = new Queue<Vector2Int>();
                pending.Enqueue(seed);
                bool leaksOutside = false;
                while (pending.Count > 0)
                {
                    Vector2Int cell = pending.Dequeue();
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int next = cell + direction;
                        var boundary = direction.x != 0
                            ? new FenceEdge(new Vector2Int(Math.Max(cell.x, next.x), cell.y), false)
                            : new FenceEdge(new Vector2Int(cell.x, Math.Max(cell.y, next.y)), true);
                        // Gates are closed barriers for containment; map edges are never implicit walls.
                        if (fences.HasFence(boundary)) continue;
                        if (!grid.Contains(next)) { leaksOutside = true; continue; }
                        if (visited.Add(next)) { region.Add(next); pending.Enqueue(next); }
                    }
                }
                if (!leaksOutside) closed.Add(region);
            }
            var old = new List<EnclosureRecord>(records);
            records.Clear();
            var used = new HashSet<EnclosureRecord>();
            foreach (HashSet<Vector2Int> region in closed)
            {
                EnclosureRecord match = null;
                int best = 0;
                foreach (EnclosureRecord candidate in old)
                {
                    if (used.Contains(candidate)) continue;
                    int overlap = 0;
                    foreach (Vector2Int cell in region) if (candidate.Cells.Contains(cell)) overlap++;
                    if (overlap > best) { match = candidate; best = overlap; }
                }
                if (match == null) match = new EnclosureRecord(nextId++, region);
                used.Add(match);
                match.Cells = region;
                match.IsSecured = true;
                records.Add(match);
            }
            foreach (EnclosureRecord previous in old)
            {
                if (used.Contains(previous)) continue;
                bool merged = false;
                foreach (HashSet<Vector2Int> region in closed)
                    if (region.Overlaps(previous.Cells)) { merged = true; break; }
                if (merged) continue;
                // Retain the last known area so a breach remains visible instead of silently disappearing.
                previous.IsSecured = false;
                records.Add(previous);
            }
        }

        public EnclosureRecord At(Vector2Int cell)
        {
            foreach (EnclosureRecord record in records)
                if (record.IsSecured && record.Cells.Contains(cell)) return record;
            foreach (EnclosureRecord record in records)
                if (record.Cells.Contains(cell)) return record;
            return null;
        }
    }
}

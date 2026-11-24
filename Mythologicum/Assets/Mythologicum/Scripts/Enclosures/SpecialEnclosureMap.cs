using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class SpecialEnclosureRecord
    {
        private readonly HashSet<Vector2Int> cells;
        public FenceKind Kind { get; }
        public Vector2Int Anchor { get; }
        public IReadOnlyCollection<Vector2Int> Cells => cells;
        public IReadOnlyCollection<FenceEdge> Boundary { get; }
        public float HighestBoundaryGround { get; }
        public float RoofHeight { get; }
        public string Key => ((int)Kind) + ":" + Anchor.x + ":" + Anchor.y;

        internal SpecialEnclosureRecord(FenceKind kind, Vector2Int anchor, HashSet<Vector2Int> cells,
            List<FenceEdge> boundary, float ground, float roofHeight)
        {
            Kind = kind; Anchor = anchor; this.cells = cells; Boundary = boundary;
            HighestBoundaryGround = ground; RoofHeight = roofHeight;
        }
        public bool Contains(Vector2Int cell) => cells.Contains(cell);
    }

    /// <summary>
    /// Complete special-fence loops create physical containment. Creatures are never assigned
    /// to a region; air routes simply cannot cross the boundary of an intact cage.
    /// </summary>
    public sealed class SpecialEnclosureMap
    {
        private static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly SpecialEnclosureRules rules;
        private readonly List<SpecialEnclosureRecord> records = new List<SpecialEnclosureRecord>();
        private readonly SpecialEnclosureRecord[] aviaries, aquariums;
        private int fenceRevision = -1, terrainRevision = -1;
        private float roofHeight = float.NaN;
        public int Revision { get; private set; }
        public IReadOnlyList<SpecialEnclosureRecord> Records { get { Refresh(); return records; } }

        public SpecialEnclosureMap(SquareBuildGrid grid, FenceLayout fences, SpecialEnclosureRules rules = null)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
            this.rules = rules ?? new SpecialEnclosureRules(); this.rules.Validate();
            aviaries = new SpecialEnclosureRecord[checked(grid.Width * grid.Depth)];
            aquariums = new SpecialEnclosureRecord[aviaries.Length];
        }
        private int Index(Vector2Int cell) => cell.y * grid.Width + cell.x;

        public void Refresh()
        {
            int terrain = grid.Terrain?.Revision ?? 0;
            if (fenceRevision == fences.Revision && terrainRevision == terrain && roofHeight == rules.aviaryHeight) return;
            fenceRevision = fences.Revision; terrainRevision = terrain; roofHeight = rules.aviaryHeight;
            records.Clear(); Array.Clear(aviaries, 0, aviaries.Length); Array.Clear(aquariums, 0, aquariums.Length);
            Build(FenceKind.Aviary, aviaries); Build(FenceKind.Aquarium, aquariums); Revision++;
        }

        private void Build(FenceKind kind, SpecialEnclosureRecord[] lookup)
        {
            int minX = grid.Width, minZ = grid.Depth, maxX = -1, maxZ = -1;
            foreach (var edge in fences.Edges)
            {
                if (fences.KindAt(edge) != kind) continue;
                minX = Math.Min(minX, edge.Start.x - 1); minZ = Math.Min(minZ, edge.Start.y - 1);
                maxX = Math.Max(maxX, edge.End.x); maxZ = Math.Max(maxZ, edge.End.y);
            }
            if (maxX < 0) return;
            minX = Math.Max(0, minX); minZ = Math.Max(0, minZ);
            maxX = Math.Min(grid.Width - 1, maxX); maxZ = Math.Min(grid.Depth - 1, maxZ);
            var visited = new HashSet<Vector2Int>();
            for (int z = minZ; z <= maxZ; z++) for (int x = minX; x <= maxX; x++)
            {
                var seed = new Vector2Int(x, z);
                if (!visited.Add(seed)) continue;
                var cells = new HashSet<Vector2Int> { seed };
                var pending = new Queue<Vector2Int>(); pending.Enqueue(seed);
                bool leaks = false;
                while (pending.Count > 0)
                {
                    var cell = pending.Dequeue();
                    foreach (var direction in Directions)
                    {
                        var next = cell + direction; var edge = Between(cell, next);
                        if (fences.HasFence(edge) && fences.KindAt(edge) == kind) continue;
                        // Other fence materials do not supply the missing special wall.
                        // They may form ordinary partitions inside a completed outer cage.
                        if (!grid.Contains(next) || next.x < minX || next.x > maxX || next.y < minZ || next.y > maxZ)
                        { leaks = true; continue; }
                        if (visited.Add(next)) { cells.Add(next); pending.Enqueue(next); }
                    }
                }
                if (leaks) continue;
                var boundary = new List<FenceEdge>(); var unique = new HashSet<FenceEdge>();
                float ground = float.NegativeInfinity;
                foreach (var cell in cells) foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (cells.Contains(next)) continue;
                    var edge = Between(cell, next);
                    if (!unique.Add(edge)) continue;
                    boundary.Add(edge);
                    ground = Math.Max(ground, Math.Max(grid.NodePosition(edge.Start).y, grid.NodePosition(edge.End).y));
                }
                if (boundary.Count == 0) continue;
                float roofGround = ground;
                foreach (var cell in cells)
                {
                    roofGround = Math.Max(roofGround, grid.NodePosition(cell).y);
                    roofGround = Math.Max(roofGround, grid.NodePosition(cell + Vector2Int.right).y);
                    roofGround = Math.Max(roofGround, grid.NodePosition(cell + Vector2Int.up).y);
                    roofGround = Math.Max(roofGround, grid.NodePosition(cell + Vector2Int.one).y);
                }
                boundary.Sort((a, b) => a.Start.y != b.Start.y ? a.Start.y.CompareTo(b.Start.y)
                    : a.Start.x != b.Start.x ? a.Start.x.CompareTo(b.Start.x) : a.AlongX.CompareTo(b.AlongX));
                var record = new SpecialEnclosureRecord(kind, seed, cells, boundary, ground, roofGround + rules.aviaryHeight);
                records.Add(record); foreach (var cell in cells) lookup[Index(cell)] = record;
            }
        }

        private static FenceEdge Between(Vector2Int a, Vector2Int b) => a.x != b.x
            ? new FenceEdge(new Vector2Int(Math.Max(a.x, b.x), a.y), false)
            : new FenceEdge(new Vector2Int(a.x, Math.Max(a.y, b.y)), true);

        public SpecialEnclosureRecord At(Vector2Int cell, FenceKind kind)
        {
            Refresh(); if (!grid.Contains(cell)) return null;
            return kind == FenceKind.Aviary ? aviaries[Index(cell)] : kind == FenceKind.Aquarium ? aquariums[Index(cell)] : null;
        }
        public bool IsAviary(Vector2Int cell) => At(cell, FenceKind.Aviary) != null;
        public bool IsAquarium(Vector2Int cell) => At(cell, FenceKind.Aquarium) != null;
        public bool CanFlyAcross(Vector2Int from, Vector2Int to)
        {
            Refresh();
            if (!grid.Contains(from) || !grid.Contains(to)) return false;
            return ReferenceEquals(aviaries[Index(from)], aviaries[Index(to)]);
        }
        public bool BlocksFlight(Vector2Int from, Vector2Int to) => !CanFlyAcross(from, to);
        public float FlightCeiling(Vector2Int cell) => At(cell, FenceKind.Aviary)?.RoofHeight ?? float.PositiveInfinity;
        public float RoofHeightAt(Vector2Int cell) => FlightCeiling(cell);
    }
}

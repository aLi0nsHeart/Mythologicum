using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class ParkBuildingRecord
    {
        public BuildingFootprint Footprint { get; }
        public bool IsOpen { get; internal set; }
        public ParkBuildingRecord(BuildingFootprint footprint) => Footprint = footprint;
    }

    /// <summary>Footprint reservations and path reachability, without scene dependencies.</summary>
    public sealed class ParkBuildingNetwork
    {
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly List<ParkBuildingRecord> records = new List<ParkBuildingRecord>();
        public IReadOnlyList<ParkBuildingRecord> Records => records;
        public ParkBuildingRecord Entrance { get; private set; }
        public bool HasEntrance => Entrance != null;
        private int lastGridRevision = -1, lastFenceRevision = -1;
        private readonly HashSet<Vector2Int> connected = new HashSet<Vector2Int>();
        public bool IsPathConnected(Vector2Int cell) => connected.Contains(cell);

        public ParkBuildingNetwork(SquareBuildGrid grid, FenceLayout fences)
        { this.grid = grid; this.fences = fences; }

        public bool CanPlace(BuildingFootprint footprint)
        {
            if ((footprint.Definition.kind == BuildingKind.ParkEntrance) == HasEntrance) return false;
            if (!grid.Contains(footprint.AccessCell) || grid.ContentAt(footprint.AccessCell) == BuildCellContent.Building) return false;
            foreach (Vector2Int cell in footprint.Cells()) if (!grid.IsAvailable(cell)) return false;
            foreach (ParkBuildingRecord existing in records)
                if (footprint.Contains(existing.Footprint.AccessCell)) return false;
            // Boundary fences may border buildings; internal fences cannot run through them.
            for (int x = footprint.Anchor.x; x < footprint.Anchor.x + footprint.Width; x++)
            for (int z = footprint.Anchor.y; z < footprint.Anchor.y + footprint.Depth; z++)
            {
                if (x > footprint.Anchor.x && fences.HasFence(new FenceEdge(new Vector2Int(x, z), false))) return false;
                if (z > footprint.Anchor.y && fences.HasFence(new FenceEdge(new Vector2Int(x, z), true))) return false;
            }
            return true;
        }

        public ParkBuildingRecord TryPlace(BuildingFootprint footprint)
        {
            if (!CanPlace(footprint)) return null;
            foreach (Vector2Int cell in footprint.Cells()) grid.TryOccupy(cell);
            var record = new ParkBuildingRecord(footprint);
            records.Add(record);
            if (footprint.Definition.kind == BuildingKind.ParkEntrance) Entrance = record;
            Refresh();
            return record;
        }

        public bool Remove(ParkBuildingRecord record)
        {
            if (!records.Remove(record)) return false;
            foreach (Vector2Int cell in record.Footprint.Cells()) grid.TryRemove(cell, BuildCellContent.Building);
            if (Entrance == record) Entrance = null;
            record.IsOpen = false;
            Refresh();
            return true;
        }

        public bool Refresh()
        {
            if (lastGridRevision == grid.Revision && lastFenceRevision == fences.Revision) return false;
            lastGridRevision = grid.Revision;
            lastFenceRevision = fences.Revision;
            connected.Clear();
            var pending = new Queue<Vector2Int>();
            if (HasEntrance && HasUsableDoor(Entrance))
            { connected.Add(Entrance.Footprint.AccessCell); pending.Enqueue(Entrance.Footprint.AccessCell); }
            while (pending.Count > 0)
                foreach (Vector2Int next in ConstructionRouteGraph.PathNeighbors(pending.Dequeue(), grid, fences))
                    if (grid.ContentAt(next) == BuildCellContent.Path && connected.Add(next)) pending.Enqueue(next);
            foreach (ParkBuildingRecord record in records)
                record.IsOpen = !record.Footprint.Definition.RequiresPathConnection
                    || (HasUsableDoor(record) && connected.Contains(record.Footprint.AccessCell));
            return true;
        }

        private bool HasUsableDoor(ParkBuildingRecord record) =>
            grid.ContentAt(record.Footprint.AccessCell) == BuildCellContent.Path && !fences.BlocksPath(record.Footprint.EntranceEdge);
    }
}

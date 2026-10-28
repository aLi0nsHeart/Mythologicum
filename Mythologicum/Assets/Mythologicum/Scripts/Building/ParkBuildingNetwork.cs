using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class ParkBuildingRecord
    {
        private static int nextId;
        private int hiredCount;
        public BuildingFootprint Footprint { get; }
        public bool IsOpen { get; internal set; }
        public int RuntimeId { get; }
        public float Price { get; private set; }
        public float Revenue { get; private set; }
        public float Expenses { get; private set; }
        public float Profit => Revenue - Expenses;
        public const int EmployeeCapacity = 5;
        public int HiredCount
        {
            get => hiredCount;
            internal set => hiredCount = Footprint.Definition.IsEmployeeBuilding ? Math.Max(0, Math.Min(EmployeeCapacity, value)) : 0;
        }
        public float UpkeepPerSecond => Bounded((double)Amount(Footprint.Definition.operatingCostPerSecond)
            + HiredCount * (double)Amount(Footprint.Definition.staffSalaryPerSecond));
        public int FeedStock { get; private set; }
        public void RestoreAccounts(float price, float revenue, float expenses, int stock)
        { SetPrice(price); Revenue = Amount(revenue); Expenses = Amount(expenses); FeedStock = Math.Max(0, Math.Min(Footprint.Definition.feederCapacity, stock)); }
        public ParkBuildingRecord(BuildingFootprint footprint)
        {
            Footprint = footprint;
            RuntimeId = System.Threading.Interlocked.Increment(ref nextId);
            Price = footprint.Definition.kind == BuildingKind.ParkEntrance ? 15f
                : footprint.Definition.kind == BuildingKind.Kiosk ? Amount(footprint.Definition.foodPrice) : 0f;
            if (footprint.Definition.kind == BuildingKind.GoatDispenser) RestockFeed();
        }
        public bool SetPrice(float value)
        {
            if (!Footprint.Definition.CanSetPrice || !Valid(value)) return false;
            Price = value; return true;
        }
        public void RecordRevenue(float amount) { if (Valid(amount)) Revenue = Bounded((double)Revenue + amount); }
        public void RecordExpense(float amount) { if (Valid(amount)) Expenses = Bounded((double)Expenses + amount); }
        public bool ConsumeFeed()
        {
            if (Footprint.Definition.kind != BuildingKind.GoatDispenser || FeedStock <= 0) return false;
            FeedStock--; return true;
        }
        public void RestockFeed()
        {
            if (Footprint.Definition.kind == BuildingKind.GoatDispenser) FeedStock = Math.Max(1, Footprint.Definition.feederCapacity);
        }
        private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static float Amount(float value) => Valid(value) ? value : 0f;
        private static float Bounded(double value) => (float)Math.Min(float.MaxValue, Math.Max(0d, value));
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
        internal bool RestoringSave { get; set; }
        private int lastGridRevision = -1, lastFenceRevision = -1;
        private readonly HashSet<Vector2Int> connected = new HashSet<Vector2Int>();
        public bool IsPathConnected(Vector2Int cell) => connected.Contains(cell);
        public bool Contains(ParkBuildingRecord record) => record != null && records.Contains(record);

        public ParkBuildingNetwork(SquareBuildGrid grid, FenceLayout fences)
        { this.grid = grid; this.fences = fences; }

        public bool CanPlace(BuildingFootprint footprint)
        {
            if (RestoringSave)
            {
                if (footprint.Definition.kind == BuildingKind.ParkEntrance && HasEntrance) return false;
            }
            else if ((footprint.Definition.kind == BuildingKind.ParkEntrance) == HasEntrance) return false;
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

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class SavedPoolWaterReceipt
    {
        public Vector2Int cell;
        public bool originallyHadSurface;
        public float originalLevel, appliedLevel;
    }
    [Serializable]
    public sealed class SavedSpecialEnclosures
    {
        public int version = 1;
        public List<Vector2Int> completedPoolCells = new List<Vector2Int>();
        public List<TerrainVertexChange> terrain = new List<TerrainVertexChange>();
        public List<SavedPoolWaterReceipt> water = new List<SavedPoolWaterReceipt>();
    }

    /// <summary>
    /// Generates complete cages and one-time pool earthworks. Water and excavated ground
    /// become ordinary habitat and persist after a breach; sparse receipts prevent repair
    /// or scene loading from excavating again or overwriting later player edits.
    /// </summary>
    public sealed class SpecialEnclosureConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly SpecialEnclosureMap map;
        private readonly SpecialEnclosureRules rules;
        private readonly Predicate<Vector2Int> canExcavate;
        private readonly HashSet<Vector2Int> completed = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, TerrainVertexChange> earthworks = new Dictionary<Vector2Int, TerrainVertexChange>();
        private readonly Dictionary<Vector2Int, SavedPoolWaterReceipt> waterReceipts = new Dictionary<Vector2Int, SavedPoolWaterReceipt>();
        private readonly Dictionary<string, string> blocked = new Dictionary<string, string>();
        private readonly SpecialEnclosureVisuals visuals;
        private int mapRevision = -1, gridRevision = -1;
        private float retryClock;
        public SpecialEnclosureMap Map => map;
        public bool HasBlockedPools => blocked.Count > 0;

        public SpecialEnclosureConstruction(SquareBuildGrid grid, SpecialEnclosureMap map, Transform parent,
            Material material, SpecialEnclosureDefinition definition = null, Predicate<Vector2Int> canExcavate = null)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            rules = (definition ?? SpecialEnclosureDefinition.Current).rules ?? new SpecialEnclosureRules(); rules.Validate();
            this.canExcavate = canExcavate;
            visuals = new SpecialEnclosureVisuals(grid, parent, material, rules);
        }

        public void Refresh(bool force = false)
        {
            map.Refresh();
            if (!force && mapRevision == map.Revision && gridRevision == grid.Revision)
            { visuals.Refresh(map); return; }
            blocked.Clear();
            // Pool planning changes terrain revisions; keep this enumeration independent
            // from the model's lazy re-evaluation during other containment queries.
            var candidates = new List<SpecialEnclosureRecord>(map.Records);
            foreach (var record in candidates)
            {
                if (record.Kind != FenceKind.Aquarium || WasCompleted(record)) continue;
                if (!TryCreatePool(record, out string reason)) blocked[record.Key] = reason;
            }
            map.Refresh(); mapRevision = map.Revision; gridRevision = grid.Revision;
            visuals.Refresh(map);
        }
        public void Tick(float seconds)
        {
            Refresh();
            if (!HasBlockedPools || !Finite(seconds) || seconds <= 0) return;
            retryClock += seconds;
            if (retryClock < rules.blockedPoolRetrySeconds) return;
            retryClock = 0; Refresh(true);
        }
        private bool WasCompleted(SpecialEnclosureRecord record)
        { foreach (var cell in record.Cells) if (!completed.Contains(cell)) return false; return true; }
        private void MarkCompleted(SpecialEnclosureRecord record)
        { foreach (var cell in record.Cells) completed.Add(cell); }

        private bool TryCreatePool(SpecialEnclosureRecord record, out string reason)
        {
            reason = "";
            bool existingWater = false;
            foreach (var cell in record.Cells) if (grid.Habitat.HasWaterSurface(cell)) { existingWater = true; break; }
            if (existingWater)
            {
                // An already landscaped pond is a valid aquatic habitat. Never flatten,
                // fill over, or unify its authored shorelines and water levels.
                MarkCompleted(record); return true;
            }
            if (record.Cells.Count < rules.minimumPoolCells || grid.Terrain == null)
            { reason = Loc.Text("special.enclosure.pool.too_small"); return false; }
            foreach (var cell in record.Cells)
                if (grid.ContentAt(cell) != BuildCellContent.Empty || !(canExcavate?.Invoke(cell) ?? true))
                { reason = Loc.Text("special.enclosure.pool.blocked"); return false; }
            float surface = record.HighestBoundaryGround + rules.aquariumWaterLift;
            if (!Finite(surface) || Math.Abs(surface - grid.Origin.y) > 10000)
            { reason = Loc.Text("habitat.terrain.invalid_water"); return false; }
            if (!grid.Terrain.TryPlanEnclosedBasin(grid, record.Cells, surface - grid.Origin.y,
                rules.aquariumDepth, canExcavate, out var plan, out reason)) return false;
            var edits = new List<WaterSurfaceEdit>();
            foreach (var cell in record.Cells)
            {
                float depth = surface - grid.Origin.y - plan.CenterHeight(cell);
                if (depth >= rules.minimumPoolDepth) edits.Add(new WaterSurfaceEdit(cell, true, surface));
            }
            if (edits.Count < rules.minimumPoolCells)
            { reason = Loc.Text("special.enclosure.pool.too_small"); return false; }
            if (!grid.Terrain.TryApplyExcavation(plan))
            { reason = Loc.Text("special.enclosure.pool.blocked"); return false; }
            foreach (var change in plan.Changes)
            {
                if (earthworks.TryGetValue(change.node, out var previous)) previous.applied = change.applied;
                else earthworks.Add(change.node, new TerrainVertexChange { node = change.node, original = change.original, applied = change.applied });
            }
            if (!grid.Habitat.TryApplyWaterSurfaces(edits, out reason)) return false;
            foreach (var edit in edits)
                if (!waterReceipts.ContainsKey(edit.Cell)) waterReceipts.Add(edit.Cell, new SavedPoolWaterReceipt {
                    cell = edit.Cell, originallyHadSurface = false, originalLevel = 0, appliedLevel = surface - grid.Origin.y
                });
            MarkCompleted(record); return true;
        }

        public string StatusAt(Vector2Int cell)
        {
            Refresh(); var record = map.At(cell, FenceKind.Aquarium);
            return record != null && blocked.TryGetValue(record.Key, out string reason) ? reason : "";
        }
        public bool HasCompletedPool(Vector2Int cell) => completed.Contains(cell);
        public SavedSpecialEnclosures Capture()
        {
            Refresh(); var result = new SavedSpecialEnclosures();
            result.completedPoolCells.AddRange(completed);
            result.completedPoolCells.Sort(CompareCells);
            foreach (var item in earthworks.Values) result.terrain.Add(new TerrainVertexChange { node = item.node, original = item.original, applied = item.applied });
            result.terrain.Sort((a, b) => CompareCells(a.node, b.node));
            foreach (var item in waterReceipts.Values) result.water.Add(new SavedPoolWaterReceipt {
                cell = item.cell, originallyHadSurface = item.originallyHadSurface, originalLevel = item.originalLevel, appliedLevel = item.appliedLevel
            });
            result.water.Sort((a, b) => CompareCells(a.cell, b.cell)); return result;
        }
        public void Restore(SavedSpecialEnclosures saved)
        {
            ValidateSave(saved, grid.Width, grid.Depth);
            completed.Clear(); earthworks.Clear(); waterReceipts.Clear(); blocked.Clear();
            if (saved != null)
            {
                foreach (var cell in saved.completedPoolCells) completed.Add(cell);
                foreach (var item in saved.terrain) earthworks.Add(item.node, new TerrainVertexChange { node = item.node, original = item.original, applied = item.applied });
                foreach (var item in saved.water) waterReceipts.Add(item.cell, new SavedPoolWaterReceipt {
                    cell = item.cell, originallyHadSurface = item.originallyHadSurface, originalLevel = item.originalLevel, appliedLevel = item.appliedLevel
                });
            }
            // Ordinary terrain/water save restoration is authoritative. No mutations here.
            mapRevision = gridRevision = -1; retryClock = 0;
        }
        public static void ValidateSave(SavedSpecialEnclosures saved, int width, int depth)
        {
            if (saved == null) return;
            int cells = checked(width * depth), nodes = checked((width + 1) * (depth + 1));
            if (saved.version != 1 || saved.completedPoolCells == null || saved.terrain == null || saved.water == null
                || saved.completedPoolCells.Count > cells || saved.terrain.Count > nodes || saved.water.Count > cells) Invalid();
            bool Cell(Vector2Int cell) => cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < depth;
            var known = new HashSet<Vector2Int>();
            foreach (var cell in saved.completedPoolCells) if (!Cell(cell) || !known.Add(cell)) Invalid();
            known.Clear();
            foreach (var item in saved.terrain)
                if (item == null || item.node.x < 0 || item.node.y < 0 || item.node.x > width || item.node.y > depth
                    || !Finite(item.original) || !Finite(item.applied) || Math.Abs(item.original) > 10000 || Math.Abs(item.applied) > 10000 || !known.Add(item.node)) Invalid();
            known.Clear();
            foreach (var item in saved.water)
                if (item == null || !Cell(item.cell) || !Finite(item.originalLevel) || !Finite(item.appliedLevel)
                    || Math.Abs(item.originalLevel) > 10000 || Math.Abs(item.appliedLevel) > 10000 || !known.Add(item.cell)) Invalid();
        }
        private static void Invalid() => throw new InvalidDataException(Loc.Text("special.enclosure.invalid_save"));
        private static int CompareCells(Vector2Int a, Vector2Int b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Dispose() => visuals.Dispose();
    }
}

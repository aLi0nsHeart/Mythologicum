using UnityEngine;

namespace Mythologicum
{
    public sealed partial class StaffWorld
    {
        private WaterTroughConstruction waterTroughs;
        private static readonly Vector2Int[] TroughDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        public void ConfigureWaterTroughs(WaterTroughConstruction troughs) => waterTroughs = troughs;

        private Vector2Int? WaterTroughApproach(StaffAgent worker, WaterTroughRecord trough, bool prepare = true)
        {
            if (prepare) ReachableWorkCells(worker.Actor.Cell);
            Vector2Int? best = null; int distance = int.MaxValue;
            foreach (var direction in TroughDirections)
            {
                var cell = trough.Cell + direction;
                if (!workVisited.Contains(cell)) continue;
                var edge = cell.x == trough.Cell.x
                    ? new FenceEdge(new Vector2Int(cell.x, Mathf.Max(cell.y, trough.Cell.y)), true)
                    : new FenceEdge(new Vector2Int(Mathf.Max(cell.x, trough.Cell.x), cell.y), false);
                if (fences.HasFence(edge) && !fences.IsGate(edge)) continue;
                int travel = ConstructionRouteGraph.CellDistance(worker.Actor.Cell, cell);
                if (travel < distance) { distance = travel; best = cell; }
            }
            return best;
        }
        private bool TryAssignWaterRefill(StaffAgent worker)
        {
            if (waterTroughs == null) return false;
            bool prepared = false;
            foreach (var trough in waterTroughs.Records)
            {
                if (trough.Stock > trough.Definition.refillThreshold || trough.Stock >= trough.Capacity) continue;
                bool reserved = false;
                foreach (var other in workers) if (other != worker && other.WaterTrough == trough) { reserved = true; break; }
                if (reserved) continue;
                if (!prepared) { ReachableWorkCells(worker.Actor.Cell); prepared = true; }
                var approach = WaterTroughApproach(worker, trough, false);
                if (!approach.HasValue || !SetGoal(worker, approach.Value)) continue;
                worker.WaterTrough = trough;
                worker.Destination = approach;
                SetStatus(worker, StaffTask.RefillingWater, "staff.status.taking_water_to_trough"); return true;
            }
            return false;
        }
        private bool WorkWaterRefill(StaffAgent worker, float dt)
        {
            if (worker.Task != StaffTask.RefillingWater) return false;
            var trough = worker.WaterTrough;
            if (waterTroughs == null || !waterTroughs.Contains(trough) || trough.Stock >= trough.Capacity)
            { ClearJob(worker); return true; }
            var approach = worker.Destination;
            if (!approach.HasValue || !ValidTroughApproach(approach.Value, trough))
            { approach = WaterTroughApproach(worker, trough); worker.Destination = approach; }
            if (!approach.HasValue || !SetGoal(worker, approach.Value))
            { ClearJob(worker); Block(worker, "staff.status.trough_unreachable"); return true; }
            if (AtNode(worker) && worker.Actor.Route.Count == 0)
            {
                worker.Status = "staff.status.refilling_water_trough"; worker.ActionTimer += dt;
                if (worker.ActionTimer >= Mathf.Max(.1f, trough.Definition.refillSeconds))
                {
                    if (economy.TrySpend(trough.Definition.refillCost, ParkTransactionCategory.Supplies))
                    { trough.RecordExpense(trough.Definition.refillCost); trough.Refill(); waterTroughs.Refresh(); ClearJob(worker); }
                    else worker.Status = "staff.status.waiting_for_trough_funds";
                }
            }
            return true;
        }
        private bool ValidTroughApproach(Vector2Int cell, WaterTroughRecord trough)
        {
            if (!navigation.IsWalkable(cell) || ConstructionRouteGraph.CellDistance(cell, trough.Cell) != 1) return false;
            var edge = cell.x == trough.Cell.x
                ? new FenceEdge(new Vector2Int(cell.x, Mathf.Max(cell.y, trough.Cell.y)), true)
                : new FenceEdge(new Vector2Int(Mathf.Max(cell.x, trough.Cell.x), cell.y), false);
            return !fences.HasFence(edge) || fences.IsGate(edge);
        }
    }
}

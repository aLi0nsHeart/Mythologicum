using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private sealed class DrinkingTarget
        {
            public Vector2Int Source, Approach;
            public WaterTroughRecord Trough;
            public float Remaining;
            public bool LeavingShelter;
            public Vector2Int? ShelterHome;
        }
        private static readonly Vector2Int[] WaterDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly List<DrinkingTarget> waterBanks = new List<DrinkingTarget>();
        private int waterGridRevision = -1, waterFenceRevision = -1;
        private readonly Dictionary<CreatureAgent, DrinkingTarget> drinkingTargets = new Dictionary<CreatureAgent, DrinkingTarget>();
        private readonly List<CreatureAgent> obsoleteDrinkers = new List<CreatureAgent>();
        private WaterTroughConstruction waterTroughs;
        private Func<Vector2Int, bool> naturalWater;
        private float drinkingCleanupAfter;
        public void ConfigureDrinking(WaterTroughConstruction troughs, Func<Vector2Int, bool> naturalWater)
        { waterTroughs = troughs; this.naturalWater = naturalWater; waterGridRevision = waterFenceRevision = -1; }

        private static FenceEdge WaterEdge(Vector2Int from, Vector2Int to) => from.x == to.x
            ? new FenceEdge(new Vector2Int(from.x, Math.Max(from.y, to.y)), true)
            : new FenceEdge(new Vector2Int(Math.Max(from.x, to.x), from.y), false);

        private bool WaterSourceAvailable(DrinkingTarget target) => target != null && grid.Contains(target.Source)
            && navigation.IsWalkable(target.Approach) && !fences.HasFence(WaterEdge(target.Approach, target.Source))
            && (target.Trough != null ? waterTroughs != null && waterTroughs.Contains(target.Trough) && target.Trough.Stock > 0
                : naturalWater?.Invoke(target.Source) == true);

        private void RefreshWaterBanks()
        {
            if (waterGridRevision == grid.Revision && waterFenceRevision == fences.Revision) return;
            waterGridRevision = grid.Revision; waterFenceRevision = fences.Revision; waterBanks.Clear();
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Depth; y++)
            {
                var source = new Vector2Int(x, y); var trough = waterTroughs?.RecordAt(source);
                if (trough == null && naturalWater?.Invoke(source) != true) continue;
                foreach (var direction in WaterDirections)
                {
                    var approach = source + direction;
                    if (navigation.IsWalkable(approach) && !fences.HasFence(WaterEdge(approach, source)))
                        waterBanks.Add(new DrinkingTarget { Source = source, Approach = approach, Trough = trough });
                }
            }
        }

        private bool HasWaterJourney(CreatureAgent actor) => drinkingTargets.TryGetValue(actor, out var target)
            && (target.LeavingShelter || actor.Activity == CreatureActivity.SeekingWater || actor.Activity == CreatureActivity.Drinking);

        private bool TrySeekWater(CreatureAgent actor)
        {
            var need = actor.Definition.Thirst;
            if (!need.needsWater) return false;
            if (drinkingTargets.TryGetValue(actor, out var existing)
                && (existing.LeavingShelter || actor.Activity == CreatureActivity.SeekingWater || actor.Activity == CreatureActivity.Drinking)
                && WaterSourceAvailable(existing) && actor.Vitals.Thirst > need.satisfiedThirst)
            {
                if (actor.Activity == CreatureActivity.Drinking && existing.Remaining > 0) return true;
                if (ContinueWaterJourney(actor, existing)) return true;
            }
            drinkingTargets.Remove(actor);
            if (actor.Vitals.Thirst < need.drinkThreshold) return false;
            RefreshNavigation(); RefreshWaterBanks();
            if (waterBanks.Count == 0) return false;
            DrinkingTarget best = null; int bestDistance = int.MaxValue;
            float radius = Mathf.Max(.1f, need.searchRadius);
            var reachable = ReachableRegion(actor.Cell);
            foreach (var bank in waterBanks)
            {
                var approach = bank.Approach;
                if (!reachable.Contains(approach) || !WaterSourceAvailable(bank)) continue;
                if (HorizontalDistance(grid.CellCenter(actor.Cell), grid.CellCenter(approach)) > radius) continue;
                int distance = ConstructionRouteGraph.CellDistance(actor.Cell, approach);
                if (distance > bestDistance) continue;
                if (distance == bestDistance && best != null && (bank.Source.y > best.Source.y
                    || bank.Source.y == best.Source.y && bank.Source.x >= best.Source.x)) continue;
                best = bank; bestDistance = distance;
            }
            if (best == null) return false;
            var target = new DrinkingTarget { Source = best.Source, Approach = best.Approach, Trough = best.Trough,
                ShelterHome = actor.SleepTree ?? actor.ShelterCell };
            drinkingTargets[actor] = target;
            if (ContinueWaterJourney(actor, target)) return true;
            drinkingTargets.Remove(actor); return false;
        }

        private bool ContinueWaterJourney(CreatureAgent actor, DrinkingTarget target)
        {
            ReleaseClaim(actor); actor.ShelterRetreat = false;
            // Reachable water is selected before waking. Finish emerging before any walking or drinking.
            if (actor.InBurrow || Mathf.Abs(actor.Elevation) > .01f)
            {
                target.LeavingShelter = true;
                if (actor.InBurrow) BeginSleepingBurrow(actor, false);
                else BeginTreeTraversal(actor, false);
                return true;
            }
            if (!SetGoal(actor, target.Approach, true)) return false;
            target.LeavingShelter = false; actor.SleepTree = null;
            if (target.ShelterHome.HasValue && SleepSitesAt(actor, target.ShelterHome.Value) > 0)
                actor.ShelterCell = target.ShelterHome;
            actor.Activity = CreatureActivity.SeekingWater;
            if (actor.Cell == target.Approach && actor.Route.Count == 0) BeginDrink(actor, target);
            return true;
        }

        private void BeginDrink(CreatureAgent actor, DrinkingTarget target)
        {
            actor.Route.Clear(); actor.Goal = null; actor.GrazeTimer = 0;
            actor.Activity = CreatureActivity.Drinking; target.Remaining = Mathf.Max(.1f, actor.Definition.Thirst.drinkSeconds);
            actor.StateTimer = target.Remaining;
            var direction = grid.CellCenter(target.Source) - actor.transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void TickDrinking(CreatureAgent actor, float dt)
        {
            if (time >= drinkingCleanupAfter)
            {
                drinkingCleanupAfter = time + 1; obsoleteDrinkers.Clear();
                foreach (var pair in drinkingTargets) if (pair.Key == null || !pair.Key.Alive) obsoleteDrinkers.Add(pair.Key);
                foreach (var obsolete in obsoleteDrinkers) drinkingTargets.Remove(obsolete);
            }
            if (!drinkingTargets.TryGetValue(actor, out var target)) return;
            if (target.LeavingShelter) return;
            if (actor.Activity != CreatureActivity.Drinking && actor.Activity != CreatureActivity.SeekingWater)
            { drinkingTargets.Remove(actor); return; }
            if (!WaterSourceAvailable(target)) { StopDrinking(actor); return; }
            if (actor.Activity == CreatureActivity.SeekingWater)
            {
                if (actor.Cell == target.Approach && actor.Route.Count == 0) BeginDrink(actor, target);
                else return;
            }
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            var need = actor.Definition.Thirst;
            float relief = Mathf.Min(actor.Vitals.Thirst, Mathf.Max(0, need.drinkReliefPerSecond) * dt);
            if (target.Trough != null)
            {
                float unitsPerPoint = Mathf.Max(.001f, target.Trough.Definition.waterUnitsPerThirstPoint);
                relief = target.Trough.Consume(relief * unitsPerPoint) / unitsPerPoint;
            }
            actor.Vitals.Drink(relief); actor.GrazeTimer += dt; target.Remaining -= dt;
            if (target.Remaining <= 0 || actor.Vitals.Thirst <= need.satisfiedThirst || target.Trough != null && target.Trough.Stock <= 0)
                StopDrinking(actor);
        }
        private void StopDrinking(CreatureAgent actor)
        {
            drinkingTargets.Remove(actor); actor.Activity = CreatureActivity.Resting;
            actor.Route.Clear(); actor.Goal = null; actor.StateTimer = PauseDuration(actor); actor.GrazeTimer = 0;
        }
    }
}

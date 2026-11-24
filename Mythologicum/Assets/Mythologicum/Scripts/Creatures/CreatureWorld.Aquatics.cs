using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private readonly Dictionary<float, Dictionary<int, CreatureNavigation>> aquaticNavigation = new Dictionary<float, Dictionary<int, CreatureNavigation>>();
        private readonly Dictionary<float, Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>>> aquaticRegions
            = new Dictionary<float, Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>>>();
        private int aquaticGridRevision = -1, aquaticFenceRevision = -1;
        private readonly Dictionary<Vector2Int, bool> aquaticClearance = new Dictionary<Vector2Int, bool>();

        private void RefreshAquaticRegions()
        {
            if (aquaticGridRevision == grid.Revision && aquaticFenceRevision == fences.Revision) return;
            aquaticRegions.Clear(); aquaticClearance.Clear(); aquaticGridRevision = grid.Revision; aquaticFenceRevision = fences.Revision;
        }
        private bool IsAquaticPhysicallyClear(Vector2Int cell)
        {
            if (!grid.Habitat.IsWater(cell)) return IsPhysicallyClear(cell);
            RefreshAquaticRegions();
            if (aquaticClearance.TryGetValue(cell, out bool clear)) return clear;
            var center = grid.CellCenter(cell); center.y = grid.Habitat.WaterLevel(cell) + .65f;
            clear = !Physics.CheckBox(center, new Vector3(grid.CellSize * .3f, .6f, grid.CellSize * .3f),
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            aquaticClearance[cell] = clear; return clear;
        }
        private CreatureNavigation SwimmingNavigation(CreatureAgent actor, bool abilities)
        {
            float depth = actor.Definition.Aquatic.minimumSwimDepth;
            int key = (abilities ? AbilityKey(actor) : 0) | (actor.Definition.Aquatic.canWadeShallows ? 16 : 0);
            if (!aquaticNavigation.TryGetValue(depth, out var variants))
                aquaticNavigation[depth] = variants = new Dictionary<int, CreatureNavigation>();
            if (!variants.TryGetValue(key, out var result))
                variants[key] = result = new CreatureNavigation(grid, fences, IsAquaticPhysicallyClear,
                    (key & 1) != 0, (key & 2) != 0, (key & 4) != 0, (key & 8) != 0, true, depth,(key & 16)!=0);
            return result;
        }
        private HashSet<Vector2Int> SwimmingRegion(CreatureAgent actor, Vector2Int start, bool abilities)
        {
            RefreshAquaticRegions();
            float depth = actor.Definition.Aquatic.minimumSwimDepth;
            int key = (abilities ? AbilityKey(actor) : 0) | (actor.Definition.Aquatic.canWadeShallows ? 16 : 0);
            if (!aquaticRegions.TryGetValue(depth, out var variants))
                aquaticRegions[depth] = variants = new Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>>();
            if (!variants.TryGetValue(key, out var regions)) variants[key] = regions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
            if (regions.TryGetValue(start, out var result)) return result;
            using var profile = Profiles.RegionSearch.Auto();
            result = new HashSet<Vector2Int>(SwimmingNavigation(actor, abilities).Reachable(start, grid.Width * grid.Depth));
            foreach (var cell in result) regions[cell] = result;
            return result;
        }
        private CreatureNavigation StrictNavigation(CreatureAgent actor) => actor.Definition.Flight.canFly ? FlyingNavigation(actor)
            : actor.Definition.Aquatic.canSwim ? SwimmingNavigation(actor, false) : navigation;
        private HashSet<Vector2Int> StrictRegion(CreatureAgent actor) => StrictRegion(actor, actor.Cell);
        private HashSet<Vector2Int> StrictRegion(CreatureAgent actor, Vector2Int start)
            => actor.Definition.Flight.canFly ? FlyingRegion(actor, start)
                : actor.Definition.Aquatic.canSwim ? SwimmingRegion(actor, start, false) : ReachableRegion(start);

        public bool CanOccupy(CreatureDefinition definition, Vector2Int cell)
        {
            if (definition == null) return false;
            RefreshNavigation();
            if (definition.Flight.canFly) return FlightCache(definition).Navigation.IsWalkable(cell);
            return definition.Aquatic.CanOccupy(grid, cell)
                && (definition.Aquatic.canSwim ? IsAquaticPhysicallyClear(cell) : IsPhysicallyClear(cell));
        }
        public bool CanApproach(CreatureAgent actor, Vector2Int cell)
        {
            if (actor == null || !actor.Alive) return false;
            RefreshNavigation(); return StrictRegion(actor).Contains(cell);
        }
        public bool IsSwimming(CreatureAgent actor) => actor != null && actor.Definition.Aquatic.IsSwimmable(grid, grid.WorldToCell(actor.transform.position));
        public Vector3 ProjectAquaticPosition(CreatureAgent actor, Vector3 point)
        {
            if (actor.Definition.Flight.canFly) return ProjectFlightPosition(actor, point);
            var cell = grid.WorldToCell(point);
            if (actor.Definition.Aquatic.IsSwimmable(grid, cell))
            {
                var water = actor.Definition.Aquatic;
                point.y = grid.Habitat.WaterLevel(cell) + actor.Definition.bodyScale.y
                    + (actor.Activity == CreatureActivity.Sleeping ? water.restingBodyOffset : water.swimmingBodyOffset);
                return point;
            }
            return grid.ProjectToGround(point, actor.Definition.bodyScale.y);
        }
        public float WaterArea(CreatureAgent actor)
        {
            if (actor == null || !actor.Definition.Aquatic.canSwim) return 0;
            var region = StrictRegion(actor); float area = 0;
            float radius = actor.Definition.Aquatic.waterSearchRadius;
            TryLocalSampleBounds(grid, actor.transform.position, radius, out var minimum, out var maximum);
            for (int z = minimum.y; z <= maximum.y; z++) for (int x = minimum.x; x <= maximum.x; x++)
            {
                var cell = new Vector2Int(x, z);
                if (region.Contains(cell) && actor.Definition.Aquatic.IsSwimmable(grid, cell)
                    && HorizontalDistance(grid.CellCenter(cell), actor.transform.position) <= radius) area += grid.CellSize * grid.CellSize;
            }
            return area;
        }
        public float WaterSatisfaction(CreatureAgent actor)
        {
            actor.ReachableWaterArea = WaterArea(actor);
            float required = actor.Definition.Aquatic.preferredWaterArea;
            return !actor.Definition.Aquatic.canSwim || required <= 0 ? 100f : Mathf.Clamp01(actor.ReachableWaterArea / required) * 100f;
        }
        private bool TryFindWater(CreatureAgent actor, bool retreat, out Vector2Int site)
        {
            site = default; var settings = actor.Definition.Aquatic;
            if (!settings.canSwim) return false;
            var region = StrictRegion(actor); float best = float.MaxValue;
            TryLocalSampleBounds(grid, actor.transform.position, settings.waterSearchRadius, out var minimum, out var maximum);
            for (int x = minimum.x; x <= maximum.x; x++) for (int z = minimum.y; z <= maximum.y; z++)
            {
                var cell = new Vector2Int(x, z);
                if (!region.Contains(cell) || !settings.IsSwimmable(grid, cell)
                    || SiteReserved(actor, cell, CreatureShelterMode.Water)
                    || HorizontalDistance(grid.CellCenter(cell), actor.transform.position) > settings.waterSearchRadius) continue;
                if (retreat && actor.HasDanger && HorizontalDistance(grid.CellCenter(cell), actor.LastDanger) < actor.Definition.Shelter.retreatSafeDistance) continue;
                float distance = ConstructionRouteGraph.CellDistance(actor.Cell, cell);
                if (distance >= best) continue;
                best = distance; site = cell;
            }
            return best < float.MaxValue;
        }
        private bool TryPreferWater(CreatureAgent actor)
        {
            if (!actor.Definition.Aquatic.canSwim || !actor.Definition.Aquatic.preferWater) return false;
            if (IsSwimming(actor))
            {
                // Idle swimming stays within the pond. Needs and hunting can still choose land routes.
                var choices = new List<Vector2Int>(4);
                foreach (var direction in ShelterDirections)
                {
                    var next = actor.Cell + direction;
                    if (actor.Definition.Aquatic.IsSwimmable(grid, next) && StrictNavigation(actor).CanCross(actor.Cell, next)) choices.Add(next);
                }
                if (choices.Count > 0 && SetGoal(actor, choices[UnityEngine.Random.Range(0, choices.Count)], true))
                    actor.Activity = CreatureActivity.Wandering;
                else { actor.Activity = CreatureActivity.Resting; actor.StateTimer = PauseDuration(actor); }
                return true;
            }
            if (!TryFindWater(actor, false, out var site)) return false;
            if (!SetGoal(actor, site, true)) return false;
            actor.Activity = CreatureActivity.Wandering; return true;
        }
        private void TickAquaticHydration(CreatureAgent actor, float dt)
        {
            if (dt <= 0 || !actor.Definition.Thirst.needsWater || !IsSwimming(actor)) return;
            actor.Vitals.Drink(actor.Definition.Thirst.drinkReliefPerSecond * dt);
        }
    }
}

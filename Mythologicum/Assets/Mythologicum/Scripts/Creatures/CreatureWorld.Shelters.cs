using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private ShelterConstruction shelterConstruction;
        private CreatureShelterRegistry fallbackShelters;
        private static readonly Vector2Int[] ShelterDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        public CreatureShelterRegistry ShelterRegistry => shelterConstruction != null ? shelterConstruction.Registry
            : fallbackShelters ?? (fallbackShelters = new CreatureShelterRegistry(grid));
        public void ConfigureShelters(ShelterConstruction construction) => shelterConstruction = construction;

        public bool IsHiddenInShelter(CreatureAgent actor)
        {
            if (actor == null || !actor.Alive) return false;
            var mode = actor.Definition.Shelter.mode;
            return actor.InBurrow || actor.Activity == CreatureActivity.Sleeping && actor.SleepTree.HasValue && HasSleepSite(actor)
                && (mode == CreatureShelterMode.Cave || mode == CreatureShelterMode.Gravestone);
        }
        public bool IsShelterProtected(CreatureAgent actor) => actor != null && actor.Alive && HasSleepSite(actor)
            && (IsHiddenInShelter(actor) || actor.Definition.Shelter.mode == CreatureShelterMode.TreeCanopy && actor.Elevation > .01f);

        public bool HasShelterAvailable(CreatureAgent actor)
        {
            if (actor == null || !actor.Alive) return false;
            var settings = actor.Definition.Shelter;
            bool available = !settings.requiredForSleep || settings.mode == CreatureShelterMode.Ground
                || settings.mode == CreatureShelterMode.Burrow && FindBurrowSite(actor, out _)
                || FindShelterSite(actor, false, out _, out _);
            actor.HasShelter = available; return available;
        }
        public float ShelterSatisfaction(CreatureAgent actor) => HasShelterAvailable(actor) ? 100f : 0f;
        public void ReleaseShelterRetreat(CreatureAgent actor)
        { if (actor != null) { actor.ShelterRetreat = false; actor.ShelterCell = null; } }

        public Vector2Int? CaptureValidShelterHome(CreatureAgent actor)
        {
            if (actor == null || !actor.Alive || actor.ExternallyControlled) return null;
            var site = actor.SleepTree ?? actor.ShelterCell;
            if (!site.HasValue || !grid.Contains(site.Value)) return null;
            var mode = actor.Definition.Shelter.mode;
            if (mode == CreatureShelterMode.Ground || SleepSitesAt(actor, site.Value) == 0
                || mode == CreatureShelterMode.Burrow && !CanDigAt(site.Value)) return null;
            return site;
        }

        public bool RestoreShelterState(CreatureAgent actor, Vector2Int? home, bool sleeping, bool retreat)
        {
            if (actor == null || !actor.Alive || actor.ExternallyControlled) return false;
            actor.SleepTree = actor.ShelterCell = null; actor.ShelterRetreat = false;
            actor.InBurrow = false; actor.Elevation = 0;
            if (!home.HasValue) return !sleeping && !retreat;
            var mode = actor.Definition.Shelter.mode;
            if (!grid.Contains(home.Value) || mode == CreatureShelterMode.Ground || SleepSitesAt(actor, home.Value) == 0
                || mode == CreatureShelterMode.Burrow && !CanDigAt(home.Value)) return false;
            if (sleeping)
            {
                bool legacyShade = mode == CreatureShelterMode.TreeShade
                    && (actor.Definition.shelter == null || actor.Definition.shelter.mode == CreatureShelterMode.Legacy);
                if (mode == CreatureShelterMode.TreeShade ? ConstructionRouteGraph.CellDistance(actor.Cell, home.Value) != 1
                    && !(legacyShade && actor.Cell == home.Value) : actor.Cell != home.Value) return false;
            }
            actor.SleepTree = actor.ShelterCell = home; actor.HasShelter = true; actor.ShelterRetreat = retreat;
            if (!sleeping) return true;
            actor.Route.Clear(); actor.Goal = null; actor.WantsSleep = true; actor.Activity = CreatureActivity.Sleeping;
            actor.InBurrow = mode == CreatureShelterMode.Burrow;
            actor.Elevation = mode == CreatureShelterMode.TreeCanopy ? actor.Definition.Shelter.treeHeight
                : mode == CreatureShelterMode.Gravestone ? -.9f : actor.InBurrow ? -actor.Definition.burrowDepth : 0;
            actor.transform.position = GroundPosition(actor, actor.Cell) + Vector3.up * actor.Elevation;
            actor.DecisionTimer = 0; RefreshVisual(actor); return true;
        }

        private bool SiteReserved(CreatureAgent actor, Vector2Int site, CreatureShelterMode mode)
        {
            int capacity = mode == CreatureShelterMode.Cave ? ShelterRegistry.CaveCapacity
                : mode == CreatureShelterMode.Burrow ? ShelterRegistry.BurrowCapacity : mode == CreatureShelterMode.TreeShade ? ShelterDirections.Length : 1;
            int count = 0;
            foreach (var other in agents)
                if (other != actor && other.Alive && !other.ExternallyControlled && other.SleepTree == site
                    && other.Definition.Shelter.mode == mode && ++count >= capacity) return true;
            return false;
        }

        private bool FindShelterSite(CreatureAgent actor, bool retreat, out Vector2Int source, out Vector2Int stand)
        {
            source = stand = default;
            var settings = actor.Definition.Shelter; var mode = settings.mode;
            if (mode == CreatureShelterMode.Ground || mode == CreatureShelterMode.Burrow) return false;
            HashSet<Vector2Int> reachable = retreat ? ReachableRegion(actor.Cell) : AbilityRegion(actor);
            float nearest = float.MaxValue;
            int radius = Mathf.Min(Mathf.Max(grid.Width, grid.Depth), Mathf.CeilToInt(settings.searchRadius / grid.CellSize));
            for (int x = Mathf.Max(0, actor.Cell.x - radius); x <= Mathf.Min(grid.Width - 1, actor.Cell.x + radius); x++)
            for (int z = Mathf.Max(0, actor.Cell.y - radius); z <= Mathf.Min(grid.Depth - 1, actor.Cell.y + radius); z++)
            {
                var cell = new Vector2Int(x, z);
                if (!reachable.Contains(cell)) continue;
                float distance = HorizontalDistance(grid.CellCenter(cell), actor.transform.position);
                if (distance > settings.searchRadius || SiteReserved(actor, cell, mode)) continue;
                bool found = mode == CreatureShelterMode.TreeCanopy || mode == CreatureShelterMode.TreeShade ? environment.TreesAt(cell) > 0
                    : mode == CreatureShelterMode.Gravestone ? environment.GravestonesAt(cell) > 0
                    : mode == CreatureShelterMode.Cave && ShelterRegistry.Contains(cell, ConstructedShelterKind.Cave);
                if (!found) continue;
                if (mode == CreatureShelterMode.TreeShade)
                {
                    foreach (var direction in ShelterDirections)
                    {
                        var shade = cell + direction;
                        if (!reachable.Contains(shade) || environment.TreesAt(shade) > 0 || environment.GravestonesAt(shade) > 0
                            || environment.ScarecrowsAt(shade) > 0 || ShelterRegistry.Occupies(shade)) continue;
                        bool occupied = false;
                        foreach (var other in agents)
                            if (other != actor && other.Alive && other.SleepTree == cell
                                && (other.Cell == shade || other.Goal == shade)) { occupied = true; break; }
                        if (occupied) continue;
                        if (retreat && actor.HasDanger && HorizontalDistance(grid.CellCenter(shade), actor.LastDanger)
                            < Mathf.Max(settings.retreatSafeDistance, actor.Definition.fearRange)) continue;
                        float value = ConstructionRouteGraph.CellDistance(actor.Cell, shade);
                        if (value >= nearest) continue;
                        nearest = value; source = cell; stand = shade;
                    }
                }
                else
                {
                    float value = ConstructionRouteGraph.CellDistance(actor.Cell, cell);
                    if (value >= nearest) continue;
                    nearest = value; source = stand = cell;
                }
            }
            return nearest < float.MaxValue;
        }

        private bool FindBurrowSite(CreatureAgent actor, out Vector2Int site)
        {
            site = default; int nearest = int.MaxValue;
            var local = ReachableRegion(actor.Cell);
            // Existing homes take priority over digging another hole at the current position.
            foreach (var cell in ShelterRegistry.Cells)
            {
                if (!ShelterRegistry.Contains(cell, ConstructedShelterKind.Burrow) || !local.Contains(cell) || !CanDigAt(cell)
                    || SiteReserved(actor, cell, CreatureShelterMode.Burrow)
                    || HorizontalDistance(grid.CellCenter(cell), actor.transform.position) > actor.Definition.Shelter.searchRadius) continue;
                int distance = ConstructionRouteGraph.CellDistance(actor.Cell, cell);
                if (distance < nearest) { nearest = distance; site = cell; }
            }
            if (nearest < int.MaxValue) return true;
            int radius = Mathf.Min(Mathf.Max(grid.Width, grid.Depth), Mathf.CeilToInt(actor.Definition.Shelter.searchRadius / grid.CellSize));
            for (int x = Mathf.Max(0, actor.Cell.x - radius); x <= Mathf.Min(grid.Width - 1, actor.Cell.x + radius); x++)
            for (int z = Mathf.Max(0, actor.Cell.y - radius); z <= Mathf.Min(grid.Depth - 1, actor.Cell.y + radius); z++)
            {
                var cell = new Vector2Int(x, z);
                if (!local.Contains(cell)) continue;
                if (!CanDigAt(cell) || ShelterRegistry.Occupies(cell)
                    || HorizontalDistance(grid.CellCenter(cell), actor.transform.position) > actor.Definition.Shelter.searchRadius) continue;
                int distance = ConstructionRouteGraph.CellDistance(actor.Cell, cell);
                if (distance < nearest) { nearest = distance; site = cell; }
            }
            return nearest < int.MaxValue;
        }

        private bool EnsureBurrow(Vector2Int cell) => ShelterRegistry.Contains(cell, ConstructedShelterKind.Burrow)
            || (shelterConstruction != null ? shelterConstruction.TryEnsureBurrow(cell) : ShelterRegistry.TryAdd(cell, ConstructedShelterKind.Burrow));

        private bool ShelterThreatNearby(CreatureAgent actor)
        {
            var local = ReachableRegion(actor.Cell);
            foreach (var other in agents)
                if (other != actor && other.Alive && !other.IsSedated && !other.IsUnderground && !IsHiddenInShelter(other)
                    && other.Definition.threat > actor.Definition.threat && local.Contains(other.Cell)
                    && HorizontalDistance(actor.transform.position, other.transform.position) <= actor.Definition.fearRange
                    && navigation.HasLineOfSight(actor.Cell, other.Cell)) return true;
            return false;
        }

        private bool TrySeekFearShelter(CreatureAgent actor)
        {
            var settings = actor.Definition.Shelter;
            if (!settings.retreatWhenAfraid || settings.mode == CreatureShelterMode.Ground) return false;
            actor.ShelterRetreat = true;
            ReleaseClaim(actor);
            if (IsShelterProtected(actor)) { actor.Activity = CreatureActivity.Sleeping; actor.Route.Clear(); actor.Goal = null; return true; }
            if (settings.mode == CreatureShelterMode.Burrow)
            {
                if (!FindBurrowSite(actor, out _)) { actor.ShelterRetreat = false; return false; }
                SeekBurrowSleep(actor); return true;
            }
            if (!FindShelterSite(actor, true, out var source, out var stand)) { actor.ShelterRetreat = false; return false; }
            actor.SleepTree = source; actor.ShelterCell = source; actor.HasShelter = true;
            if (actor.Cell == stand && actor.Route.Count == 0)
            {
                if (settings.mode == CreatureShelterMode.TreeCanopy)
                { BeginTreeTraversal(actor, true); return true; }
                if (settings.mode == CreatureShelterMode.Gravestone)
                { BeginTreeTraversal(actor, true); return true; }
                actor.Activity = CreatureActivity.Sleeping; return true;
            }
            actor.Activity = CreatureActivity.Fleeing;
            return SetGoal(actor, stand, true);
        }

        private void SeekShelterSleep(CreatureAgent actor)
        {
            ReleaseClaim(actor);
            var settings = actor.Definition.Shelter;
            if (settings.mode == CreatureShelterMode.Burrow) { SeekBurrowSleep(actor); return; }
            if (actor.Activity == CreatureActivity.Sleeping)
            {
                if (!HasSleepSite(actor))
                {
                    actor.ShelterRetreat = false;
                    if (Mathf.Abs(actor.Elevation) > .01f) BeginTreeTraversal(actor, false);
                    else { actor.Activity = CreatureActivity.SeekingSleep; actor.SleepTree = actor.ShelterCell = null; }
                }
                else if (actor.ShelterRetreat && (ShelterThreatNearby(actor) || actor.Vitals.Fear > settings.calmFearThreshold)) return;
                else if (actor.Energy.Energy >= actor.Definition.wakeThreshold)
                {
                    actor.ShelterRetreat = false; actor.WantsSleep = false;
                    if (Mathf.Abs(actor.Elevation) > .01f) BeginTreeTraversal(actor, false);
                    else { actor.Activity = CreatureActivity.Resting; actor.SleepTree = actor.ShelterCell = null; actor.StateTimer = PauseDuration(actor); }
                }
                return;
            }
            // Keep legacy optional shade profiles compatible; authored profiles use adjacent ground below.
            if ((actor.Definition.shelter == null || actor.Definition.shelter.mode == CreatureShelterMode.Legacy) && TrySeekShadeSleep(actor)) return;
            if (settings.mode == CreatureShelterMode.Ground)
            { FinishSegment(actor); actor.Activity = actor.Route.Count > 0 ? CreatureActivity.SeekingSleep : CreatureActivity.Sleeping; return; }
            if (!FindShelterSite(actor, false, out var source, out var stand))
            {
                actor.SleepTree = actor.ShelterCell = null; actor.HasShelter = !settings.requiredForSleep;
                FinishSegment(actor); actor.Activity = settings.requiredForSleep ? CreatureActivity.SeekingSleep : CreatureActivity.Sleeping;
                return;
            }
            actor.SleepTree = source; actor.ShelterCell = source; actor.HasShelter = true;
            if (actor.Cell == stand && actor.Route.Count == 0)
            {
                if (settings.mode == CreatureShelterMode.TreeCanopy || settings.mode == CreatureShelterMode.Gravestone) BeginTreeTraversal(actor, true);
                else actor.Activity = CreatureActivity.Sleeping;
            }
            else { actor.Activity = CreatureActivity.SeekingSleep; SetGoal(actor, stand); }
        }
    }
}

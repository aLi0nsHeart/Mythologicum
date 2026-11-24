using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class AquaticHuntSave
    {
        public CreatureActivity phase;
        public Vector2Int waterCell, releaseCell;
        public float remaining;
    }

    public sealed partial class CreatureWorld
    {
        private bool UsesAquaticHunt(CreatureAgent actor) => actor != null && actor.Definition.Aquatic.canSwim
            && actor.Definition.Aquatic.dragPreyToWater && actor.Definition.diet == CreatureDiet.Carnivore
            && !actor.Definition.feedsOnFear;

        private bool AquaticPreyAvailable(CreatureAgent hunter, CreatureAgent prey) => prey != null && prey != hunter
            && prey.Alive && prey.Health.Alive && !prey.IsSedated && prey.CarriedBy == null && !prey.IsUnderground
            && !IsHiddenInShelter(prey) && (prey.CapturedBy == null || prey.CapturedBy == hunter)
            && (hunter.Definition.preyTags & prey.Definition.tags) != 0;

        private bool HuntingWaterValid(CreatureAgent actor, Vector2Int cell) => actor.Definition.Aquatic.IsSwimmable(grid,cell)
            && StrictNavigation(actor).IsWalkable(cell);

        private bool TryHuntingWater(CreatureAgent actor, Vector2Int near, out Vector2Int water)
        {
            water = default; int nearest = int.MaxValue;
            var reachable = StrictRegion(actor);
            foreach (var cell in grid.Habitat.WaterCells)
            {
                if (!reachable.Contains(cell) || !HuntingWaterValid(actor, cell)
                    || HorizontalDistance(grid.CellCenter(cell), actor.transform.position) > actor.Definition.Aquatic.waterSearchRadius) continue;
                int distance = ConstructionRouteGraph.CellDistance(near, cell);
                if (distance > nearest || distance == nearest && (cell.y > water.y || cell.y == water.y && cell.x >= water.x)) continue;
                nearest = distance; water = cell;
            }
            return nearest < int.MaxValue;
        }

        private bool HuntAquatic(CreatureAgent actor)
        {
            if (actor.WaterHuntActive) return true;
            if (!TryHuntingWater(actor, actor.Cell, out _)) { ReleaseClaim(actor); return false; }
            if (AquaticPreyAvailable(actor, actor.Prey) && CanSee(actor, actor.Prey, actor.Definition.visionRange)
                && StrictRegion(actor).Contains(actor.Prey.Cell) && SetGoal(actor, PursuitCell(actor.Prey), true))
            { actor.Activity = CreatureActivity.Hunting; return true; }
            ReleaseClaim(actor);
            var candidates = Visible(actor, actor.Definition.preyTags, actor.Definition.visionRange);
            var preferred = actor.Definition.Aquatic.preferredPreyTags;
            candidates.Sort((a, b) =>
            {
                int priority = ((b.Definition.tags & preferred) != 0).CompareTo((a.Definition.tags & preferred) != 0);
                return priority != 0 ? priority : (a.transform.position - actor.transform.position).sqrMagnitude
                    .CompareTo((b.transform.position - actor.transform.position).sqrMagnitude);
            });
            var reachable = StrictRegion(actor);
            foreach (var prey in candidates)
            {
                if (!AquaticPreyAvailable(actor, prey) || !reachable.Contains(prey.Cell)
                    || !TryHuntingWater(actor, prey.Cell, out _) || !SetGoal(actor, PursuitCell(prey), true)) continue;
                actor.Prey = prey; actor.LastSeenPreyCell = prey.Cell; actor.PreyLastSeen = time;
                actor.Activity = CreatureActivity.Hunting; return true;
            }
            return false;
        }

        private bool FeedAquaticHunter(CreatureAgent actor)
        {
            if (!UsesAquaticHunt(actor)) return false;
            var prey = actor.Prey;
            if (actor.WaterHuntActive || actor.Activity != CreatureActivity.Hunting || actor.Definition.damage <= 0
                || actor.Threat != null || !AquaticPreyAvailable(actor, prey) || !CanInteract(actor, prey)
                || (actor.transform.position - GroundPosition(actor, actor.Cell)).sqrMagnitude >= .0025f
                || !TryHuntingWater(actor, prey.Cell, out var water)) return true;
            Vector2Int release = prey.Cell;
            if (!navigation.IsWalkable(release))
            {
                bool found = false; int best = int.MaxValue;
                foreach (var cell in StrictRegion(actor))
                {
                    if (!navigation.IsWalkable(cell)) continue;
                    int distance = ConstructionRouteGraph.CellDistance(prey.Cell, cell);
                    if (distance >= best) continue;
                    release = cell; best = distance; found = true;
                }
                if (!found) return true;
            }
            actor.WaterHuntActive = true; actor.WaterHuntWaterCell = water; actor.WaterHuntReleaseCell = release;
            actor.Route.Clear(); actor.Goal = null; actor.ShelterRetreat = false; actor.SleepTree = null;
            actor.Activity = CreatureActivity.Attacking; actor.StateTimer = Mathf.Max(.1f, actor.Definition.Aquatic.captureSeconds);
            ReleaseClaim(prey);
            prey.SleepTree=null; prey.ShelterCell=null; prey.ShelterRetreat=false; prey.WantsSleep=false;
            prey.Activity=CreatureActivity.Fleeing;
            prey.CapturedBy = actor; prey.Route.Clear(); prey.Goal = null; prey.GrazeTimer = 0;
            prey.LastDanger = actor.transform.position; prey.HasDanger = true;
            return true;
        }

        private void FollowAquaticCaptor(CreatureAgent prey, CreatureAgent hunter, float dt)
        {
            prey.Route.Clear(); prey.Goal = null;
            var target = hunter.transform.position;
            target.y = grid.Habitat.IsWater(hunter.Cell) ? grid.Habitat.WaterLevel(hunter.Cell) + prey.Definition.bodyScale.y * .15f
                : grid.ActorPosition(hunter.Cell, prey.Definition.bodyScale.y).y;
            prey.transform.position = Vector3.MoveTowards(prey.transform.position, target,
                Mathf.Max(hunter.Definition.walkSpeed, hunter.Definition.Aquatic.swimSpeed) * Mathf.Max(.05f, dt) * 2);
            // Logical containment follows the captor. Only this live relationship permits a land actor in water.
            prey.Cell = hunter.Cell;
            Vector3 direction = hunter.transform.position - prey.transform.position; direction.y = 0;
            if (direction.sqrMagnitude > .001f) prey.transform.rotation = Quaternion.LookRotation(direction);
        }

        private bool TickAquaticHunt(CreatureAgent actor, float dt)
        {
            if (!actor.WaterHuntActive) return false;
            if (!UsesAquaticHunt(actor) || !actor.Alive || actor.IsSedated || actor.CarriedBy != null
                || !HuntingWaterValid(actor, actor.WaterHuntWaterCell))
            { StopAquaticHunt(actor); return true; }
            if (actor.Activity == CreatureActivity.Feeding)
            {
                if (!grid.Habitat.IsWater(actor.Cell)) { StopAquaticHunt(actor); return true; }
                if (actor.StateTimer <= 0)
                {
                    actor.Vitals.Eat(actor.Definition.foodRelief);
                    actor.WaterHuntActive = false; actor.Prey = null; actor.Activity = CreatureActivity.Resting;
                    actor.StateTimer = PauseDuration(actor); actor.DecisionTimer = 0;
                }
                return true;
            }
            var prey = actor.Prey;
            if (!AquaticPreyAvailable(actor, prey) || prey.CapturedBy != actor || prey.IsSedated)
            { StopAquaticHunt(actor); return true; }
            var threat = FindThreat(actor, actor.Definition.fearRange);
            if (threat != null)
            { actor.LastDanger = threat.transform.position; actor.HasDanger = true; StopAquaticHunt(actor); return true; }
            if (actor.Activity == CreatureActivity.Attacking)
            {
                if (!CanInteract(actor, prey)) { StopAquaticHunt(actor); return true; }
                if (actor.StateTimer > 0) return true;
                if (!SetGoal(actor, actor.WaterHuntWaterCell, true)) { StopAquaticHunt(actor); return true; }
                actor.Activity = CreatureActivity.DraggingPrey;
            }
            if (actor.Activity == CreatureActivity.DraggingPrey)
            {
                if (actor.Route.Count == 0 && actor.Cell != actor.WaterHuntWaterCell
                    && !SetGoal(actor, actor.WaterHuntWaterCell, true)) { StopAquaticHunt(actor); return true; }
                if (actor.Route.Count > 0)
                {
                    var next = actor.Route[0]; var nav = StrictNavigation(actor);
                    if (!nav.CanCross(actor.Cell, next)) { StopAquaticHunt(actor); return true; }
                    Vector3 goal = GroundPosition(actor, next);
                    float speed = (grid.Habitat.IsWater(next) ? actor.Definition.Aquatic.swimSpeed : actor.Definition.walkSpeed)
                        * Mathf.Max(.05f, actor.Definition.Aquatic.dragSpeedMultiplier);
                    Vector3 direction = goal - actor.transform.position; direction.y = 0;
                    if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
                    actor.transform.position = Vector3.MoveTowards(actor.transform.position, goal, speed * dt);
                    if ((actor.transform.position - goal).sqrMagnitude < .0001f) { actor.Cell = next; actor.Route.RemoveAt(0); }
                }
                FollowAquaticCaptor(prey, actor, dt);
                if (actor.Cell == actor.WaterHuntWaterCell && actor.Route.Count == 0)
                { actor.Activity = CreatureActivity.DrowningPrey; actor.StateTimer = Mathf.Max(.1f, actor.Definition.Aquatic.drowningSeconds); }
                return true;
            }
            if (actor.Activity == CreatureActivity.DrowningPrey)
            {
                FollowAquaticCaptor(prey, actor, dt);
                if (!grid.Habitat.IsWater(actor.Cell)) { StopAquaticHunt(actor); return true; }
                if (actor.StateTimer > 0) return true;
                // The kill happens only after physically reaching water and completing the struggle.
                prey.Health.Damage(prey.Health.Current); prey.CapturedBy = null;
                ReleaseClaim(prey); prey.Alive = false; prey.Activity = CreatureActivity.Dying;
                prey.Route.Clear(); prey.Goal = null;
                prey.CorpseTimer = actor.Definition.feedingSeconds + simulation.corpseAfterFeedingSeconds;
                var collider = prey.GetComponent<Collider>(); if (collider != null) collider.enabled = false;
                actor.Prey = null; actor.Activity = CreatureActivity.Feeding; actor.StateTimer = actor.Definition.feedingSeconds;
                return true;
            }
            StopAquaticHunt(actor); return true;
        }

        private void StopAquaticHunt(CreatureAgent actor)
        {
            ReleaseAquaticCapture(actor); actor.Prey = null; actor.LastSeenPreyCell = null;
            actor.Route.Clear(); actor.Goal = null;
            if (actor.Alive) { actor.Activity = CreatureActivity.Resting; actor.StateTimer = PauseDuration(actor); actor.DecisionTimer = 0; }
        }

        private void ReleaseAquaticCapture(CreatureAgent hunter)
        {
            if (hunter == null || !hunter.WaterHuntActive) return;
            hunter.WaterHuntActive = false;
            var prey = hunter.Prey;
            if (prey == null) return;
            if (prey.CapturedBy == hunter) prey.CapturedBy = null;
            if (!prey.Alive) return;
            // Rescue uses a bank recorded before capture, never an arbitrary tile across a fence.
            var bank = hunter.WaterHuntReleaseCell;
            if (!navigation.IsWalkable(bank))
            {
                int nearest = int.MaxValue;
                // A later obstruction can invalidate the original bank. Restrict the replacement
                // to the captor's current component so rescue never crosses an intact fence.
                foreach (var cell in StrictRegion(hunter))
                {
                    if (!navigation.IsWalkable(cell)) continue;
                    int distance = ConstructionRouteGraph.CellDistance(cell, hunter.WaterHuntReleaseCell);
                    if (distance >= nearest) continue;
                    nearest = distance; bank = cell;
                }
            }
            if (!navigation.IsWalkable(prey.Cell) && navigation.IsWalkable(bank))
            { prey.Cell = bank; prey.transform.position = GroundPosition(prey, bank); }
            if(navigation.IsWalkable(prey.Cell))prey.transform.position=GroundPosition(prey,prey.Cell);
            prey.Route.Clear(); prey.Goal = null; prey.DecisionTimer = 0; prey.HasDanger = true;
            prey.LastDanger = hunter.transform.position; prey.Activity = prey.IsSedated ? CreatureActivity.Sedated : CreatureActivity.Fleeing;
        }

        public AquaticHuntSave CaptureAquaticHunt(CreatureAgent actor) => actor != null && actor.WaterHuntActive
            ? new AquaticHuntSave { phase = actor.Activity, waterCell = actor.WaterHuntWaterCell,
                releaseCell = actor.WaterHuntReleaseCell, remaining = Mathf.Max(0, actor.StateTimer) } : null;

        /// <summary>Resolve a construction interruption even if the player paused before the next simulation tick.</summary>
        public void PrepareAquaticSnapshot()
        {
            RefreshNavigation();
            foreach(var actor in agents)
            {
                if(!actor.Alive || !actor.WaterHuntActive)continue;
                if(!HuntingWaterValid(actor,actor.WaterHuntWaterCell)
                    || actor.Activity==CreatureActivity.DraggingPrey && !StrictRegion(actor).Contains(actor.WaterHuntWaterCell)
                    || !navigation.IsWalkable(actor.WaterHuntReleaseCell)
                    || actor.Activity!=CreatureActivity.Feeding && !AquaticPreyAvailable(actor,actor.Prey))
                    StopAquaticHunt(actor);
            }
        }

        public bool RestoreAquaticHunt(CreatureAgent hunter, CreatureAgent prey, AquaticHuntSave saved)
        {
            if (hunter == null || !hunter.Alive || !UsesAquaticHunt(hunter) || !ValidAquaticHuntSave(grid, hunter.Definition, saved)
                || hunter.IsSedated || hunter.CarriedBy != null || saved.phase != CreatureActivity.Feeding
                    && (!AquaticPreyAvailable(hunter, prey) || prey.CapturedBy != null)) return false;
            if (saved.phase == CreatureActivity.Feeding && prey != null) return false;
            hunter.WaterHuntActive = true; hunter.WaterHuntWaterCell = saved.waterCell; hunter.WaterHuntReleaseCell = saved.releaseCell;
            hunter.Activity = saved.phase; hunter.StateTimer = saved.remaining; hunter.Route.Clear(); hunter.Goal = null; hunter.Prey = prey;
            if (prey != null)
            {
                prey.CapturedBy = hunter; prey.Route.Clear(); prey.Goal = null; prey.Cell=hunter.Cell;
                var position=hunter.transform.position;
                position.y=grid.Habitat.IsWater(hunter.Cell)
                    ? grid.Habitat.WaterLevel(hunter.Cell)+prey.Definition.bodyScale.y*.15f
                    : grid.ActorPosition(hunter.Cell,prey.Definition.bodyScale.y).y;
                prey.transform.position=position; RefreshVisual(prey);
            }
            if (saved.phase == CreatureActivity.DraggingPrey && !SetGoal(hunter, saved.waterCell, true))
            { StopAquaticHunt(hunter); return true; }
            return true;
        }

        public static bool ValidAquaticHuntSave(SquareBuildGrid map, CreatureDefinition definition, AquaticHuntSave saved)
            => map != null && !ReferenceEquals(definition, null) && definition.Aquatic.canSwim && definition.Aquatic.dragPreyToWater && saved != null
                && (saved.phase == CreatureActivity.Attacking || saved.phase == CreatureActivity.DraggingPrey
                    || saved.phase == CreatureActivity.DrowningPrey || saved.phase == CreatureActivity.Feeding)
                && !float.IsNaN(saved.remaining) && !float.IsInfinity(saved.remaining) && saved.remaining >= 0
                && definition.Aquatic.IsSwimmable(map,saved.waterCell) && map.GroundWalkable(saved.releaseCell)
                && map.ContentAt(saved.releaseCell) != BuildCellContent.Building;
    }
}

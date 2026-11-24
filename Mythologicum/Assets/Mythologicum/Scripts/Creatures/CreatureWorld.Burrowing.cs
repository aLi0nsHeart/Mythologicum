using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private bool CanBurrow(CreatureAgent actor) => actor.Definition.canBurrowFences
            && actor.EscapeIntent && time < actor.EscapeUntil && time >= actor.AttemptAfter;

        private bool CanDigAt(Vector2Int cell) => CreatureBurrowing.CanDigGround(grid, cell)
            && navigation.IsWalkable(cell) && environment.TreesAt(cell) == 0
            && environment.GravestonesAt(cell) == 0 && environment.ScarecrowsAt(cell) == 0
            && !ShelterRegistry.Contains(cell, ConstructedShelterKind.Cave);

        private static bool BurrowTraversal(CreatureActivity activity) => activity == CreatureActivity.EnteringBurrow
            || activity == CreatureActivity.LeavingBurrow || activity == CreatureActivity.BurrowingFence
            || activity == CreatureActivity.FailedBurrow;

        private void SeekBurrowSleep(CreatureAgent actor)
        {
            if (actor.InBurrow)
            {
                if (actor.ShelterRetreat && CanDigAt(actor.Cell) && ShelterRegistry.Contains(actor.Cell, ConstructedShelterKind.Burrow)
                    && (ShelterThreatNearby(actor) || actor.Vitals.Fear > actor.Definition.Shelter.calmFearThreshold)) return;
                if (actor.Energy.Energy >= actor.Definition.wakeThreshold || !CanDigAt(actor.Cell)
                    || !ShelterRegistry.Contains(actor.Cell, ConstructedShelterKind.Burrow))
                {
                    actor.ShelterRetreat = false;
                    if (actor.Energy.Energy >= actor.Definition.wakeThreshold) actor.WantsSleep = false;
                    BeginSleepingBurrow(actor, false);
                }
                return;
            }
            if (!FindBurrowSite(actor, out var site))
            {
                FinishSegment(actor); actor.Activity = CreatureActivity.SeekingSleep;
                return;
            }
            actor.SleepTree = actor.ShelterCell = site; actor.HasShelter = true;
            if (actor.Cell == site && actor.Route.Count == 0) BeginSleepingBurrow(actor, true);
            else
            {
                actor.Activity = CreatureActivity.SeekingSleep;
                SetGoal(actor, site, true);
            }
        }

        private void BeginSleepingBurrow(CreatureAgent actor, bool entering)
        {
            if (entering && !EnsureBurrow(actor.Cell))
            { actor.Activity = CreatureActivity.SeekingSleep; actor.SleepTree = actor.ShelterCell = null; return; }
            actor.Route.Clear(); actor.Goal = null;
            actor.TraversalStartElevation = actor.Elevation;
            actor.TraversalDuration = Mathf.Max(.1f, actor.Definition.burrowSeconds);
            actor.StateTimer = actor.TraversalDuration;
            actor.Activity = entering ? CreatureActivity.EnteringBurrow : CreatureActivity.LeavingBurrow;
            if (!entering) actor.InBurrow = false;
            if (entering) actor.Energy.Spend(actor.Definition.burrowEnergyCost);
        }

        private void BeginFenceBurrow(CreatureAgent actor, Vector2Int next)
        {
            actor.TraversalFrom = actor.Cell; actor.TraversalTo = next;
            actor.Activity = CreatureActivity.BurrowingFence;
            actor.TraversalDuration = Mathf.Max(.1f, actor.Definition.burrowSeconds);
            actor.StateTimer = actor.TraversalDuration;
            actor.Energy.Spend(actor.Definition.burrowEnergyCost);
            Vector3 direction = GroundPosition(actor, next) - GroundPosition(actor, actor.Cell);
            direction.y = 0f;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void BeginFailedBurrow(CreatureAgent actor)
        {
            actor.Route.Clear(); actor.Goal = null;
            actor.Activity = CreatureActivity.FailedBurrow;
            actor.TraversalDuration = Mathf.Max(.1f, actor.Definition.burrowSeconds);
            actor.StateTimer = actor.TraversalDuration;
            actor.AttemptAfter = time + actor.TraversalDuration + actor.Definition.climbRetrySeconds;
            actor.Energy.Spend(actor.Definition.burrowEnergyCost);
        }

        private bool TickBurrowTraversal(CreatureAgent actor)
        {
            if (!BurrowTraversal(actor.Activity)) return false;
            float progress = 1f - Mathf.Clamp01(actor.StateTimer / Mathf.Max(.1f, actor.TraversalDuration));
            if (actor.Activity == CreatureActivity.FailedBurrow)
            {
                if (actor.StateTimer <= 0f)
                {
                    if (actor.AttemptFence.HasValue && fences.HasFence(actor.AttemptFence.Value) && CanAttackFences(actor))
                        BeginFenceAttack(actor, actor.AttemptFence.Value);
                    else FinishFenceAttempt(actor, "creature.escape.burrow_blocked");
                }
                return true;
            }
            if (actor.Activity == CreatureActivity.BurrowingFence)
            {
                var edge = CrossingEdge(actor.TraversalFrom, actor.TraversalTo);
                if (!CanBurrow(actor) || !CanDigAt(actor.TraversalFrom) || !CanDigAt(actor.TraversalTo)
                    || !grid.CanTraverse(actor.TraversalFrom, actor.TraversalTo)
                    || fences.HasFence(edge) && !fences.CanBurrow(edge))
                {
                    actor.transform.position = GroundPosition(actor, actor.TraversalFrom);
                    actor.AttemptFence = edge; BeginFailedBurrow(actor);
                    return true;
                }
                var a = GroundPosition(actor, actor.TraversalFrom);
                var b = GroundPosition(actor, actor.TraversalTo);
                actor.transform.position = Vector3.Lerp(a, b, progress)
                    - Vector3.up * Mathf.Sin(progress * Mathf.PI) * actor.Definition.burrowDepth;
                if (grid.Terrain != null)
                    actor.transform.position = grid.ProjectToGround(actor.transform.position,
                        actor.Definition.bodyScale.y - Mathf.Sin(progress * Mathf.PI) * actor.Definition.burrowDepth);
                if (actor.StateTimer <= 0f)
                {
                    actor.Cell = actor.TraversalTo; actor.transform.position = GroundPosition(actor, actor.Cell);
                    if (actor.Route.Count > 0 && actor.Route[0] == actor.Cell) actor.Route.RemoveAt(0);
                    actor.Activity = CreatureActivity.Wandering; actor.DecisionTimer = 0f;
                    EndEscape(actor, "creature.escape.reached_new_ground");
                }
                return true;
            }
            bool entering = actor.Activity == CreatureActivity.EnteringBurrow;
            if (entering && (!CanDigAt(actor.Cell) || !ShelterRegistry.Contains(actor.Cell, ConstructedShelterKind.Burrow)))
            {
                actor.InBurrow = false; actor.Elevation = 0f;
                actor.transform.position = GroundPosition(actor, actor.Cell);
                actor.Activity = CreatureActivity.SeekingSleep; actor.DecisionTimer = 0f;
                return true;
            }
            actor.Elevation = Mathf.Lerp(actor.TraversalStartElevation, entering ? -actor.Definition.burrowDepth : 0f,
                Mathf.SmoothStep(0f, 1f, progress));
            actor.transform.position = GroundPosition(actor, actor.Cell) + Vector3.up * actor.Elevation;
            if (actor.StateTimer <= 0f)
            {
                actor.InBurrow = entering;
                if (!entering) actor.SleepTree = actor.ShelterCell = null;
                actor.Activity = entering ? CreatureActivity.Sleeping : CreatureActivity.Resting;
                actor.DecisionTimer = 0f; actor.StateTimer = PauseDuration(actor);
            }
            return true;
        }

        private bool AnimateBurrow(CreatureAgent actor)
        {
            if (!BurrowTraversal(actor.Activity)) return false;
            float progress = 1f - Mathf.Clamp01(actor.StateTimer / Mathf.Max(.1f, actor.TraversalDuration));
            float effort = Mathf.Sin(progress * Mathf.PI);
            actor.Body.localPosition = actor.HasVisualModel ? Vector3.down * actor.Definition.bodyScale.y : Vector3.zero;
            actor.Body.localPosition += Vector3.forward * Mathf.Sin(progress * Mathf.PI * 6f) * .06f * effort;
            actor.Body.localRotation = Quaternion.Euler(effort * 30f, 0f,
                actor.Activity == CreatureActivity.FailedBurrow ? Mathf.Sin(progress * Mathf.PI * 4f) * effort * 12f : 0f);
            return true;
        }
    }
}

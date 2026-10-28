using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private readonly Dictionary<int, CreatureNavigation> abilityNavigation = new Dictionary<int, CreatureNavigation>();
        private readonly Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>> abilityRegions = new Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>>();

        private bool CanPhase(CreatureAgent agent) => agent.Definition.canPhaseFences
            && agent.Vitals.Hunger >= agent.Definition.phaseHungerThreshold;
        private bool CanAttackFences(CreatureAgent agent) => agent.Definition.damage > 0f
            && (agent.Definition.canBreakFences || agent.EscapeIntent || agent.Vitals.Hunger >= 80f
                || agent.Activity == CreatureActivity.Fleeing);
        private int AbilityKey(CreatureAgent agent) => (CanClimbForNeeds(agent) ? 1 : 0)
            | (CanPhase(agent) ? 2 : 0) | (CanAttackFences(agent) ? 4 : 0);
        private CreatureNavigation AbilityNavigation(CreatureAgent agent)
        {
            int key = AbilityKey(agent);
            if (key == 0) return navigation;
            if (!abilityNavigation.TryGetValue(key, out var nav))
                abilityNavigation[key] = nav = new CreatureNavigation(grid, fences, IsPhysicallyClear,
                    (key & 1) != 0, (key & 2) != 0, (key & 4) != 0);
            return nav;
        }
        private HashSet<Vector2Int> AbilityRegion(CreatureAgent agent)
        {
            int key = AbilityKey(agent);
            if (key == 0) return ReachableRegion(agent.Cell);
            if (!abilityRegions.TryGetValue(key, out var regions))
                abilityRegions[key] = regions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
            if (regions.TryGetValue(agent.Cell, out var result)) return result;
            result = new HashSet<Vector2Int>(Navigation(agent).Reachable(agent.Cell, grid.Width * grid.Depth));
            foreach (Vector2Int cell in result) regions[cell] = result;
            return result;
        }

        private int SleepSitesAt(CreatureAgent agent, Vector2Int cell) => agent.Definition.requiresGravestoneForSleep
            ? environment.GravestonesAt(cell) : environment.TreesAt(cell);
        private bool HasSleepSite(CreatureAgent agent) => !agent.Definition.requiresTreeForSleep
            && !agent.Definition.requiresGravestoneForSleep || agent.SleepTree.HasValue && SleepSitesAt(agent, agent.SleepTree.Value) > 0;

        /// <summary>Threat is independent of damage; physical containment still makes exhibits safe to observe.</summary>
        public CreatureAgent FindThreat(CreatureAgent observer, float range)
        {
            if (observer == null || !observer.Alive) return null;
            CreatureAgent closest = null; float best = float.MaxValue;
            foreach (CreatureAgent other in agents)
            {
                if (other == observer || !other.Alive || other.IsSedated || other.Definition.threat <= observer.Definition.threat
                    || !CanSee(observer, other, range) || !ReachableRegion(observer.Cell).Contains(other.Cell)) continue;
                float distance = HorizontalDistance(observer.transform.position, other.transform.position);
                if (distance < best) { best = distance; closest = other; }
            }
            return closest;
        }

        private bool SeekScare(CreatureAgent agent)
        {
            if (time < agent.ScareAfter) return false;
            var guests = Visible(agent, CreatureTags.Visitor, agent.Definition.visionRange);
            guests.Sort((a,b) => (a.transform.position - agent.transform.position).sqrMagnitude
                .CompareTo((b.transform.position - agent.transform.position).sqrMagnitude));
            foreach (CreatureAgent guest in guests)
            {
                if (guest.CapturedBy != null || guest.Definition.threat >= agent.Definition.threat
                    || !AbilityRegion(agent).Contains(guest.Cell)) continue;
                agent.Prey = guest;
                if (HorizontalDistance(agent.transform.position, guest.transform.position) <= agent.Definition.scareRange
                    && ReachableRegion(agent.Cell).Contains(guest.Cell))
                {
                    FinishSegment(agent);
                    if (agent.Route.Count > 0) { agent.Activity = CreatureActivity.Hunting; return true; }
                    agent.Activity = CreatureActivity.Scaring; agent.StateTimer = Mathf.Max(.1f, agent.Definition.scareSeconds);
                    // A scare is a non-damaging action. The victim remains free to run throughout it.
                    guest.Vitals.Tick(.25f, 0f, 60f, 0f, true);
                    agent.ScareSucceeded = guest.Vitals.Fear > 0f;
                    guest.LastDanger = agent.transform.position; guest.HasDanger = true; guest.DecisionTimer = 0f;
                    return true;
                }
                if (SetGoal(agent, guest.Cell)) { agent.Activity = CreatureActivity.Hunting; return true; }
            }
            ReleaseClaim(agent);
            return false;
        }

        private bool TickScare(CreatureAgent agent)
        {
            if (agent.StateTimer > 0f) return true;
            // The visitor fleeing after being frightened is a successful scare, not a failed capture.
            if (agent.ScareSucceeded)
                agent.Vitals.Eat(agent.Definition.foodRelief);
            agent.ScareSucceeded = false;
            ReleaseClaim(agent); agent.ScareAfter = time + Mathf.Max(.1f, agent.Definition.scareCooldownSeconds);
            agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent);
            return false;
        }

        private void BeginFenceAttack(CreatureAgent agent, FenceEdge edge)
        {
            agent.AttackedFence = edge; agent.FenceStrikes = 0;
            agent.Activity = CreatureActivity.AttackingFence;
            agent.StateTimer = Mathf.Max(.2f, agent.Definition.attackSeconds);
            Vector3 direction = fences.Center(edge) - grid.CellCenter(agent.Cell); direction.y = 0f;
            if (direction.sqrMagnitude > .001f) agent.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void BeginPhase(CreatureAgent agent, Vector2Int next)
        {
            agent.TraversalFrom = agent.Cell; agent.TraversalTo = next;
            agent.Activity = CreatureActivity.PhasingFence;
            agent.TraversalDuration = 1.5f; agent.StateTimer = agent.TraversalDuration;
            agent.transform.rotation = Quaternion.LookRotation(GroundPosition(agent,next) - GroundPosition(agent,agent.Cell));
        }

        private bool TickSpecialTraversal(CreatureAgent agent)
        {
            if (agent.Activity == CreatureActivity.AttackingFence)
            {
                FenceEdge? edge = agent.AttackedFence;
                if (!edge.HasValue || !fences.HasFence(edge.Value))
                { agent.AttackedFence = null; agent.Activity = CreatureActivity.Wandering; agent.DecisionTimer = 0f; return true; }
                if (agent.StateTimer > 0f) return true;
                if (agent.Energy.Energy <= agent.Definition.sleepThreshold || FindThreat(agent,
                    Mathf.Min(agent.Definition.visionRange, agent.Definition.fearRange)) != null)
                {
                    agent.AttackedFence = null; agent.Route.Clear(); agent.Goal = null;
                    agent.Activity = CreatureActivity.Resting; agent.DecisionTimer = 0f;
                    return false; // Reconsider danger and exhaustion between strikes, just like other activities.
                }
                float damage = fences.TryDamage(edge.Value, agent.Definition.damage);
                agent.FenceStrikes++; agent.Energy.Spend(1f);
                if (damage <= 0f || agent.FenceStrikes >= 30 || (agent.EscapeIntent && time >= agent.EscapeUntil))
                {
                    agent.AttackedFence = null; agent.AttemptAfter = time + agent.Definition.climbRetrySeconds;
                    agent.Route.Clear(); agent.Goal = null; agent.Activity = CreatureActivity.Resting;
                    agent.EscapeStatus = damage <= 0f ? "Fence resists the attack" : "Pausing fence attacks";
                    agent.StateTimer = PauseDuration(agent); agent.DecisionTimer = 0f;
                    if (agent.EscapeIntent) EndEscape(agent, agent.EscapeStatus);
                }
                else agent.StateTimer = Mathf.Max(.2f, agent.Definition.attackSeconds);
                return true;
            }
            bool phase = agent.Activity == CreatureActivity.PhasingFence;
            bool entering = agent.Activity == CreatureActivity.EnteringGrave;
            bool leaving = agent.Activity == CreatureActivity.LeavingGrave;
            if (!phase && !entering && !leaving) return false;
            float progress = 1f - Mathf.Clamp01(agent.StateTimer / Mathf.Max(.1f, agent.TraversalDuration));
            if (phase)
            {
                FenceEdge edge = CrossingEdge(agent.TraversalFrom, agent.TraversalTo);
                if ((fences.HasFence(edge) && (!CanPhase(agent) || !fences.CanPhase(edge))) || !navigation.IsWalkable(agent.TraversalTo))
                {
                    agent.transform.position = GroundPosition(agent, agent.TraversalFrom);
                    agent.AttemptFence = edge; BeginFailedClimb(agent); return true;
                }
                agent.transform.position = Vector3.Lerp(GroundPosition(agent, agent.TraversalFrom), GroundPosition(agent, agent.TraversalTo), progress)
                    + Vector3.up * Mathf.Sin(progress * Mathf.PI) * .12f;
                if (agent.StateTimer <= 0f)
                {
                    agent.Cell = agent.TraversalTo; agent.transform.position = GroundPosition(agent, agent.Cell);
                    if (agent.Route.Count > 0 && agent.Route[0] == agent.Cell) agent.Route.RemoveAt(0);
                    agent.Activity = CreatureActivity.Wandering; agent.DecisionTimer = 0f;
                }
                return true;
            }
            if (entering && !HasSleepSite(agent)) { BeginTreeTraversal(agent, false); return true; }
            agent.Elevation = Mathf.Lerp(agent.TraversalStartElevation, entering ? -.9f : 0f, Mathf.SmoothStep(0f,1f,progress));
            agent.transform.position = GroundPosition(agent, agent.Cell) + Vector3.up * agent.Elevation;
            if (agent.StateTimer <= 0f)
            {
                agent.Activity = entering ? CreatureActivity.Sleeping : CreatureActivity.Resting;
                if (leaving) agent.SleepTree = null;
                agent.DecisionTimer = 0f; agent.StateTimer = PauseDuration(agent);
            }
            return true;
        }
    }
}

using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private bool SeekScare(CreatureAgent agent)
        {
            if (time < agent.ScareAfter) return false;
            agent.ScarecrowTarget = null;
            var people = Visible(agent, CreatureTags.Visitor | CreatureTags.Staff, agent.Definition.visionRange);
            people.Sort((a, b) => (a.transform.position - agent.transform.position).sqrMagnitude
                .CompareTo((b.transform.position - agent.transform.position).sqrMagnitude));
            var local = ReachableRegion(agent.Cell);
            // People sharing the enclosure are interesting even when a practice dummy exists.
            foreach (var person in people)
                if (local.Contains(person.Cell) && TryScarePerson(agent, person, true)) return true;
            // A contained fear-feeder has a safe, reachable source of food before
            // considering people beyond fences. The dummy is enrichment, not prey.
            if (agent.Definition.canUseScarecrows)
            {
                Vector2Int? closest = null;
                float distance = float.MaxValue;
                foreach (var cell in environment.ScarecrowCells)
                {
                    if (!local.Contains(cell) || !navigation.HasLineOfSight(agent.Cell, cell)) continue;
                    float current = HorizontalDistance(agent.transform.position, grid.CellCenter(cell));
                    if (current > agent.Definition.visionRange) continue;
                    if (current < distance || current == distance && (!closest.HasValue || cell.y < closest.Value.y
                        || cell.y == closest.Value.y && cell.x < closest.Value.x))
                    { closest = cell; distance = current; }
                }
                if (closest.HasValue)
                {
                    ReleaseClaim(agent); agent.ScarecrowTarget = closest;
                    if (distance <= ScareDistance(agent))
                    {
                        FinishSegment(agent);
                        if (agent.Route.Count > 0) { agent.Activity = CreatureActivity.Hunting; return true; }
                        BeginScare(agent, grid.CellCenter(closest.Value));
                        agent.ScareSucceeded = true;
                        return true;
                    }
                    if (SetGoal(agent, closest.Value, true)) { agent.Activity = CreatureActivity.Hunting; return true; }
                    agent.ScarecrowTarget = null;
                }
            }
            foreach (var person in people)
                if (!local.Contains(person.Cell) && AbilityRegion(agent).Contains(person.Cell) && TryScarePerson(agent, person)) return true;
            ReleaseClaim(agent);
            return false;
        }

        private static float ScareDistance(CreatureAgent agent) => Mathf.Max(.1f,
            Mathf.Min(agent.Definition.scareRange, agent.Definition.scareApproachDistance));

        private bool TryScarePerson(CreatureAgent agent, CreatureAgent person, bool walkingOnly = false)
        {
            if (person == null || !person.Alive || person.CapturedBy != null || person.CarriedBy != null || person.IsSedated
                || person.Definition.threat >= agent.Definition.threat) return false;
            agent.Prey = person;
            if (HorizontalDistance(agent.transform.position, person.transform.position) <= ScareDistance(agent)
                && ReachableRegion(agent.Cell).Contains(person.Cell))
            {
                FinishSegment(agent);
                if (agent.Route.Count > 0) { agent.Activity = CreatureActivity.Hunting; return true; }
                BeginScare(agent, person.transform.position);
                float impulse = agent.Definition.Behaviour.scareFearImpulse;
                person.Vitals.Tick(1f, 0f, impulse, 0f, true);
                agent.ScareSucceeded = impulse > 0f && person.Vitals.Fear > 0f;
                person.LastDanger = agent.transform.position; person.HasDanger = true; person.DecisionTimer = 0f;
                return true;
            }
            if (SetGoal(agent, person.Cell, walkingOnly)) { agent.Activity = CreatureActivity.Hunting; return true; }
            ReleaseClaim(agent);
            return false;
        }

        private void BeginScare(CreatureAgent agent, Vector3 target)
        {
            agent.Route.Clear(); agent.Goal = null;
            agent.Activity = CreatureActivity.Scaring;
            agent.StateTimer = Mathf.Max(.1f, agent.Definition.scareSeconds);
            agent.ScareSucceeded = false;
            Vector3 direction = target - agent.transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > .001f) agent.transform.rotation = Quaternion.LookRotation(direction);
        }

        private bool TickScare(CreatureAgent agent)
        {
            if (agent.StateTimer > 0f) return true;
            // A frightened person remains free to run. Credit the completed scare,
            // while demolished practice dummies cannot provide food afterwards.
            bool valid = !agent.ScarecrowTarget.HasValue || environment.ScarecrowsAt(agent.ScarecrowTarget.Value) > 0;
            if (agent.ScareSucceeded && valid) agent.Vitals.Eat(agent.Definition.foodRelief);
            agent.ScareSucceeded = false; agent.ScarecrowTarget = null;
            ReleaseClaim(agent);
            agent.ScareAfter = time + Mathf.Max(.1f, agent.Definition.scareCooldownSeconds);
            agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent);
            RestoreScarePose(agent);
            return false;
        }

        private bool AnimateScare(CreatureAgent agent)
        {
            if (!agent.Alive || agent.Activity != CreatureActivity.Scaring || agent.IsSedated)
            { RestoreScarePose(agent); return false; }
            if (!agent.ScarePoseActive)
            {
                agent.ScareBodyScale = agent.Body.localScale; agent.ScarePoseActive = true;
                if (agent.BodyAnimator != null)
                {
                    int idle = Animator.StringToHash("Base Layer.idle");
                    if (agent.BodyAnimator.HasState(0, idle)) agent.BodyAnimator.CrossFadeInFixedTime(idle, .1f, 0);
                    agent.AnimationState = null;
                }
            }
            float progress = 1f - Mathf.Clamp01(agent.StateTimer / Mathf.Max(.1f, agent.Definition.scareSeconds));
            float reach = Mathf.Min(agent.Definition.scareJumpForward, grid.CellSize * .35f);
            var pose = CreatureScareMotion.Sample(progress, agent.Definition.scareJumpHeight, reach, agent.Definition.scareJumpSquash);
            Vector3 origin = agent.HasVisualModel ? Vector3.down * agent.Definition.bodyScale.y : Vector3.zero;
            agent.Body.localPosition = origin + pose.Offset;
            agent.Body.localScale = Vector3.Scale(agent.ScareBodyScale, pose.Scale);
            agent.Body.localRotation = Quaternion.Euler(pose.Tilt, 0f, 0f);
            return true;
        }

        private static void RestoreScarePose(CreatureAgent agent)
        {
            if (!agent.ScarePoseActive || agent.Body == null) return;
            agent.Body.localScale = agent.ScareBodyScale;
            agent.Body.localPosition = agent.HasVisualModel ? Vector3.down * agent.Definition.bodyScale.y : Vector3.zero;
            agent.Body.localRotation = Quaternion.identity;
            agent.ScarePoseActive = false;
        }
    }
}

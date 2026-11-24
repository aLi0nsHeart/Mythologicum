using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private sealed class StalkingIntent
        {
            internal CreatureAgent Target;
            internal Vector2Int? Cover;
            internal float Began, ReachedCover = -1;
            internal bool Rushing;
        }
        private readonly Dictionary<CreatureAgent, StalkingIntent> stalkingIntents = new Dictionary<CreatureAgent, StalkingIntent>();
        private readonly List<CreatureAgent> expiredPredatorIntents = new List<CreatureAgent>();

        private void ClearStalking(CreatureAgent actor) => stalkingIntents.Remove(actor);

        private bool HuntStalking(CreatureAgent actor)
        {
            var settings = actor.Definition.Stalking;
            if (!settings.enabled) return false;
            stalkingIntents.TryGetValue(actor, out var intention);
            var prey = intention?.Target;
            if (prey == null || !prey.Alive || prey.IsSedated || prey.CarriedBy != null || prey.IsUnderground
                || prey.CapturedBy != null && prey.CapturedBy != actor)
            {
                ReleaseClaim(actor);
                prey = null;
                var candidates = Visible(actor, actor.Definition.preyTags, actor.Definition.visionRange);
                candidates.Sort((a, b) => (a.transform.position - actor.transform.position).sqrMagnitude
                    .CompareTo((b.transform.position - actor.transform.position).sqrMagnitude));
                foreach (var candidate in candidates)
                {
                    if (candidate.IsSedated || candidate.CarriedBy != null || candidate.CapturedBy != null
                        || !AbilityRegion(actor).Contains(candidate.Cell)) continue;
                    prey = candidate; break;
                }
                if (prey == null || !prey.Alive) return TryBlockedFence(actor, candidates);
                intention = new StalkingIntent { Target = prey, Cover = FindStalkingCover(actor, prey), Began = time };
                intention.Rushing = !intention.Cover.HasValue;
                stalkingIntents[actor] = intention;
                actor.Prey = prey; actor.PreyLastSeen = time; actor.LastSeenPreyCell = PursuitCell(prey);
            }

            bool visible = CanSee(actor, prey, actor.Definition.visionRange);
            if (visible) { actor.PreyLastSeen = time; actor.LastSeenPreyCell = PursuitCell(prey); }
            if (!visible && time - actor.PreyLastSeen > actor.Definition.preyMemorySeconds)
            { ReleaseClaim(actor); actor.Route.Clear(); actor.Goal = null; return false; }
            float elapsed = time - intention.Began;
            if (!intention.Rushing)
            {
                bool nearby = HorizontalDistance(actor.transform.position, prey.transform.position) <= settings.ambushRange;
                if (intention.Cover.HasValue && !HasStalkingCover(intention.Cover.Value, settings)
                    || elapsed >= settings.maximumStalkSeconds || nearby && elapsed >= settings.minimumStalkSeconds)
                    intention.Rushing = true;
                else if (intention.Cover.HasValue && actor.Cell == intention.Cover.Value && actor.Route.Count == 0)
                {
                    if (intention.ReachedCover < 0) intention.ReachedCover = time;
                    if (elapsed >= settings.minimumStalkSeconds && time - intention.ReachedCover >= settings.coverWaitSeconds)
                        intention.Rushing = true;
                    else { actor.Activity = CreatureActivity.Stalking; actor.Goal = null; return true; }
                }
                else if (intention.Cover.HasValue && SetGoal(actor, intention.Cover.Value, true))
                { actor.Activity = CreatureActivity.Stalking; return true; }
                else intention.Rushing = true;
            }
            if (intention.Rushing && actor.LastSeenPreyCell.HasValue && SetGoal(actor, actor.LastSeenPreyCell.Value))
            { actor.Activity = visible ? CreatureActivity.Hunting : CreatureActivity.Searching; return true; }
            ReleaseClaim(actor);
            return TryBlockedFence(actor, new List<CreatureAgent> { prey });
        }

        private Vector2Int? FindStalkingCover(CreatureAgent actor, CreatureAgent target)
        {
            var settings = actor.Definition.Stalking;
            Vector2Int? best = null; float bestScore = float.NegativeInfinity;
            foreach (var cell in StrictNavigation(actor).Reachable(actor.Cell, settings.coverSearchSteps))
            {
                if (!HasStalkingCover(cell, settings) || cell == target.Cell) continue;
                float distance = ConstructionRouteGraph.CellDistance(cell, target.Cell) * grid.CellSize;
                if (distance < settings.ambushRange) continue;
                float score = -Mathf.Abs(distance - settings.preferredCoverDistance)
                    - ConstructionRouteGraph.CellDistance(actor.Cell, cell) * .1f;
                if (score > bestScore) { bestScore = score; best = cell; }
            }
            return best;
        }

        private bool HasStalkingCover(Vector2Int cell, CreatureStalkingSettings settings)
        {
            if (grid.Habitat.BiomeAt(cell) == HabitatBiome.Thicket) return true;
            int trees = 0;
            for (int x = -settings.coverTreeRadius; x <= settings.coverTreeRadius; x++)
            for (int y = -settings.coverTreeRadius; y <= settings.coverTreeRadius; y++)
                if ((trees += environment.TreesAt(cell + new Vector2Int(x, y))) >= settings.minimumCoverTrees) return true;
            return false;
        }

        private float StalkingSpeed(CreatureAgent actor, float speed) => actor.Activity == CreatureActivity.Stalking
            ? speed * actor.Definition.Stalking.stalkSpeedMultiplier : speed;

        public bool IsConcealedPredator(CreatureAgent actor)
        {
            if (actor == null || !actor.Alive || actor.IsSedated || actor.CarriedBy != null) return false;
            if (IsConcealedLure(actor)) return true;
            return actor.Definition.Stalking.enabled && actor.Activity == CreatureActivity.Stalking
                && HasStalkingCover(actor.Cell, actor.Definition.Stalking)
                && (actor.Prey == null || HorizontalDistance(actor.transform.position, actor.Prey.transform.position) > actor.Definition.Stalking.ambushRange);
        }

        private void TickPredatorIntents()
        {
            expiredPredatorIntents.Clear();
            foreach (var pair in stalkingIntents)
                if (pair.Key == null || !pair.Key.Alive || pair.Key.IsSedated || pair.Key.CarriedBy != null
                    || pair.Value.Target == null || !pair.Value.Target.Alive) expiredPredatorIntents.Add(pair.Key);
            foreach (var actor in expiredPredatorIntents) stalkingIntents.Remove(actor);
            TickLureIntents();
        }

        private void AnimatePredatorIntent(CreatureAgent actor)
        {
            if (actor.Body == null || !actor.Alive || actor.IsSedated || actor.CarriedBy != null) return;
            if (actor.Activity == CreatureActivity.Stalking)
            {
                actor.Body.localPosition += Vector3.down * actor.Definition.Stalking.crouchOffset;
                actor.Body.localRotation *= Quaternion.Euler(14f, 0, 0);
            }
            if (IsConcealedLure(actor))
                actor.Body.localPosition += Vector3.down * (actor.Definition.bodyScale.y + actor.Definition.Lure.submergeOffset);
        }
    }
}

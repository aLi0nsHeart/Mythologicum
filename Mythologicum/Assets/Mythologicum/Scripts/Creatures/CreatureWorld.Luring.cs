using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private sealed class LureIntent
        {
            internal VisitorAgent Visitor;
            internal Vector2Int Bank;
            internal float Until;
            internal bool Approaching;
        }
        private VisitorWorld lureVisitors;
        private readonly Dictionary<CreatureAgent, LureIntent> lureIntents = new Dictionary<CreatureAgent, LureIntent>();
        private readonly Dictionary<CreatureAgent, float> lureAfter = new Dictionary<CreatureAgent, float>();

        public void ConfigureLuring(VisitorWorld visitors)
        {
            if (ReferenceEquals(lureVisitors, visitors)) return;
            foreach (var pair in lureIntents) lureVisitors?.CancelLure(pair.Value.Visitor);
            lureIntents.Clear(); lureVisitors = visitors;
        }

        public float CaptureLureCooldown(CreatureAgent source) => source != null && lureAfter.TryGetValue(source, out float after)
            ? Mathf.Max(0, after - time) : 0;

        public void RestoreLureCooldown(CreatureAgent source, float remaining)
        {
            if (source == null) return;
            CancelLureFor(source);
            if (!source.Definition.Lure.enabled || float.IsNaN(remaining) || float.IsInfinity(remaining) || remaining <= 0)
            { lureAfter.Remove(source); return; }
            lureAfter[source] = time + Mathf.Min(remaining, source.Definition.Lure.cooldownSeconds);
        }

        public bool HasActiveLure(CreatureAgent source, VisitorAgent guest) => source != null && source.Alive
            && !source.IsSedated && source.CarriedBy == null && lureIntents.TryGetValue(source, out var intent)
            && intent.Visitor == guest && intent.Until > time && !source.WaterHuntActive
            && (source.Activity == CreatureActivity.Luring || source.Activity == CreatureActivity.Hunting);

        public bool IsConcealedLure(CreatureAgent source) => source != null && source.Alive && source.Definition.Lure.enabled
            && !source.IsSedated && source.CarriedBy == null && source.CapturedBy == null
            && source.Definition.Aquatic.canSwim && source.Definition.Aquatic.IsSwimmable(grid, source.Cell)
            && lureIntents.TryGetValue(source, out var intent) && intent.Until > time
            && intent.Visitor != null && intent.Visitor.Actor != null && intent.Visitor.Actor.Alive
            && intent.Visitor.LureSource == source
            && intent.Visitor.Actor.CapturedBy == null && !source.WaterHuntActive
            && (source.Activity == CreatureActivity.Luring || source.Activity == CreatureActivity.Hunting);

        private bool TryLurePrey(CreatureAgent source)
        {
            if (!source.Definition.Lure.enabled || lureVisitors == null || !UsesAquaticHunt(source)) return false;
            var settings = source.Definition.Lure;
            if (lureIntents.TryGetValue(source, out var intent))
            {
                if (!LureIntentValid(source, intent)) { CancelLureFor(source); return false; }
                if (intent.Visitor.Actor.Cell == intent.Bank && intent.Visitor.Actor.Route.Count == 0)
                {
                    // Keep the visitor's physical wait at the shore while ordinary aquatic
                    // hunting approaches. Capture and dragging still happen in the shared AI.
                    intent.Approaching = true;
                    source.Prey = intent.Visitor.Actor; source.LastSeenPreyCell = intent.Bank; source.PreyLastSeen = time;
                    return false;
                }
                source.Route.Clear(); source.Goal = null; source.Activity = CreatureActivity.Luring;
                return true;
            }
            if (source.WaterHuntActive || source.Prey != null || source.IsSedated || source.CarriedBy != null
                || settings.visitorTypes == null || settings.visitorTypes.Length == 0
                || !source.Definition.Aquatic.IsSwimmable(grid, source.Cell)
                || lureAfter.TryGetValue(source, out float after) && time < after) return false;

            lureAfter[source] = time + settings.retrySeconds;
            var banks = LureBanks(source);
            if (banks.Count == 0) return false;
            var candidates = new List<VisitorAgent>();
            foreach (var guest in lureVisitors.Visitors)
                if (lureVisitors.CanRespondToLure(guest, source)
                    && HorizontalDistance(source.transform.position, guest.Actor.transform.position) <= settings.range)
                    candidates.Add(guest);
            candidates.Sort((a, b) => (a.Actor.transform.position - source.transform.position).sqrMagnitude
                .CompareTo((b.Actor.transform.position - source.transform.position).sqrMagnitude));
            int tried = 0;
            foreach (var guest in candidates)
            {
                if (tried++ >= settings.maximumVisitorCandidates) break;
                if (!lureVisitors.TryLureVisitor(guest, source, banks, out var bank)) continue;
                ReleaseClaim(source);
                lureIntents[source] = new LureIntent { Visitor = guest, Bank = bank, Until = time + settings.durationSeconds };
                lureAfter[source] = time + settings.cooldownSeconds;
                source.Route.Clear(); source.Goal = null; source.Activity = CreatureActivity.Luring;
                return true;
            }
            return false;
        }

        private HashSet<Vector2Int> LureBanks(CreatureAgent source)
        {
            var result = new HashSet<Vector2Int>();
            var movement = StrictNavigation(source);
            foreach (var cell in movement.Reachable(source.Cell, source.Definition.Lure.shorelineSearchSteps))
            {
                if (!navigation.IsWalkable(cell) || grid.Habitat.IsWater(cell)) continue;
                foreach (var direction in LureDirections)
                {
                    var water = cell + direction;
                    if (source.Definition.Aquatic.IsSwimmable(grid, water) && movement.CanCross(water, cell))
                    { result.Add(cell); break; }
                }
            }
            return result;
        }
        private static readonly Vector2Int[] LureDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        private bool LureIntentValid(CreatureAgent source, LureIntent intent)
        {
            if (lureVisitors == null || source == null || !source.Alive || source.IsSedated || source.CarriedBy != null || source.CapturedBy != null
                || source.WaterHuntActive || !source.Definition.Lure.enabled || time >= intent.Until
                || source.Vitals.Hunger < source.Definition.huntThreshold || intent.Visitor == null
                || !lureVisitors.HasAssignedLure(intent.Visitor, source)
                || !lureVisitors.CanRespondToLure(intent.Visitor, source)
                || !source.Definition.Aquatic.IsSwimmable(grid, source.Cell)
                || !navigation.IsWalkable(intent.Bank)) return false;
            if (source.Activity != CreatureActivity.Luring && !(intent.Approaching && source.Activity == CreatureActivity.Hunting)) return false;
            foreach (var direction in LureDirections)
            {
                var water = intent.Bank + direction;
                if (source.Definition.Aquatic.IsSwimmable(grid, water) && StrictNavigation(source).CanCross(water, intent.Bank)) return true;
            }
            return false;
        }

        private void CancelLureFor(CreatureAgent source)
        {
            if (ReferenceEquals(source, null) || !lureIntents.TryGetValue(source, out var intent)) return;
            lureIntents.Remove(source);
            lureVisitors?.CancelLure(intent.Visitor);
            if (source != null && source.Activity == CreatureActivity.Luring)
            { source.Activity = CreatureActivity.Resting; source.StateTimer = 0; source.DecisionTimer = 0; }
        }

        private void TickLureIntents()
        {
            expiredPredatorIntents.Clear();
            foreach (var pair in lureIntents)
                if (!LureIntentValid(pair.Key, pair.Value)) expiredPredatorIntents.Add(pair.Key);
            foreach (var source in expiredPredatorIntents) CancelLureFor(source);
            expiredPredatorIntents.Clear();
            foreach (var pair in lureAfter)
                if (pair.Key == null || !pair.Key.Alive || pair.Value <= time) expiredPredatorIntents.Add(pair.Key);
            foreach (var source in expiredPredatorIntents) lureAfter.Remove(source);
        }
    }
}

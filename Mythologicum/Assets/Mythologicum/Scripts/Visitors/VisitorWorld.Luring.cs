using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class VisitorWorld
    {
        public bool CanRespondToLure(VisitorAgent guest, CreatureAgent source)
        {
            return guest != null && visitors.Contains(guest) && source != null && source.Alive && !source.IsSedated
                && source.CarriedBy == null && source.Definition.Lure.Permits(guest.Type)
                && guest.Actor != null && guest.Actor.Alive && guest.Actor.CapturedBy == null && !guest.Actor.IsSedated
                && !guest.LeavingPark && !guest.Panicked && guest.Attraction == null
                && guest.Activity != VisitorActivity.Eating && guest.Activity != VisitorActivity.Resting
                && guest.Activity != VisitorActivity.Fleeing && guest.Actor.Vitals.Fear < Definition.panicFearThreshold
                && (guest.LureSource == null || guest.LureSource == source);
        }

        public bool TryLureVisitor(VisitorAgent guest, CreatureAgent source, ISet<Vector2Int> banks, out Vector2Int bank)
        {
            bank = default;
            if (!CanRespondToLure(guest, source) || banks == null || banks.Count == 0) return false;
            var actor = guest.Actor; var settings = source.Definition.Lure;
            bool moving = actor.Route.Count > 0;
            if (moving && !navigation.CanCross(actor.Cell, actor.Route[0], true)) return false;
            if (!moving && Distance(actor.transform.position, Position(actor, actor.Cell)) > .1f) return false;
            Vector2Int start = moving ? actor.Route[0] : actor.Cell;
            var route = FindLureRoute(start, banks, settings.maximumApproachSteps);
            if (route == null || route.Count == 0) return false;
            bank = route[route.Count - 1];
            ReleaseService(guest); guest.Watching = null;
            actor.Route.Clear(); actor.Goal = bank;
            if (moving) actor.Route.Add(start);
            for (int i = 1; i < route.Count; i++) actor.Route.Add(route[i]);
            guest.LureSource = source; guest.LureBank = bank; guest.LureUntil = time + settings.durationSeconds;
            guest.GroundRoute = true; guest.DecisionTimer = 0;
            SetActivity(guest, VisitorActivity.Lured, "visitor.status.following_a_strange_call");
            return true;
        }

        public bool HasAssignedLure(VisitorAgent guest, CreatureAgent source) => guest != null && guest.Actor != null
            && guest.Actor.Alive && guest.LureSource == source;

        public void CancelLure(VisitorAgent guest)
        {
            if (guest == null || ReferenceEquals(guest.LureSource, null)) return;
            guest.LureSource = null; guest.LureUntil = 0;
            if (guest.Actor == null || !guest.Actor.Alive) return;
            // Preserve the physical position; ordinary visitor decisions then find a route
            // back to the public network. A cancelled call never teleports a guest.
            guest.Actor.Route.Clear(); guest.Actor.Goal = null; guest.DecisionTimer = 0;
            guest.GroundRoute = true;
            if (guest.Activity == VisitorActivity.Lured)
                SetActivity(guest, VisitorActivity.Arriving, "visitor.status.the_strange_call_has_stopped");
        }

        private bool TickVisitorLure(VisitorAgent guest)
        {
            if (ReferenceEquals(guest.LureSource, null)) return false;
            var source = guest.LureSource;
            if (!CanRespondToLure(guest, source) || time >= guest.LureUntil || !creatures.HasActiveLure(source, guest)
                || !navigation.IsWalkable(guest.LureBank, true))
            { CancelLure(guest); return false; }
            if (guest.Actor.Route.Count == 0 && guest.Actor.Cell != guest.LureBank)
            {
                var route = FindLureRoute(guest.Actor.Cell, new HashSet<Vector2Int> { guest.LureBank }, source.Definition.Lure.maximumApproachSteps);
                if (route == null || route.Count == 0) { CancelLure(guest); return false; }
                guest.Actor.Goal = guest.LureBank;
                for (int i = 1; i < route.Count; i++) guest.Actor.Route.Add(route[i]);
            }
            SetActivity(guest, VisitorActivity.Lured, "visitor.status.following_a_strange_call");
            return true;
        }

        private List<Vector2Int> FindLureRoute(Vector2Int start, ISet<Vector2Int> banks, int maximumSteps)
        {
            if (!navigation.IsWalkable(start, true)) return null;
            var pending = new Queue<Vector2Int>(); pending.Enqueue(start);
            var distance = new Dictionary<Vector2Int, int> { [start] = 0 };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (banks.Contains(cell))
                {
                    var route = new List<Vector2Int> { cell };
                    while (previous.TryGetValue(cell, out var parent)) { cell = parent; route.Add(cell); }
                    route.Reverse(); return route;
                }
                if (distance[cell] >= maximumSteps) continue;
                foreach (var direction in VisitorLureDirections)
                {
                    var next = cell + direction;
                    if (distance.ContainsKey(next) || !navigation.CanCross(cell, next, true)) continue;
                    distance[next] = distance[cell] + 1; previous[next] = cell; pending.Enqueue(next);
                }
            }
            return null;
        }
        private static readonly Vector2Int[] VisitorLureDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
    }
}

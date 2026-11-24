using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private bool TrySeekFood(CreatureAgent actor)
        {
            var definition = actor.Definition;
            if (!definition.needsFood || actor.Vitals.Hunger < definition.huntThreshold) return false;
            if (definition.feedsOnFear && SeekScare(actor)) return true;
            if (definition.diet == CreatureDiet.Carnivore) return Hunt(actor);
            if (definition.diet != CreatureDiet.Herbivore) return false;
            if (grass.HasFood(actor.Cell, time) && actor.Route.Count == 0)
            { actor.Activity = CreatureActivity.Grazing; actor.Goal = null; return true; }
            if (actor.Activity == CreatureActivity.Searching && actor.Route.Count > 0 && actor.Goal.HasValue
                && grass.HasFood(actor.Goal.Value, time)) return true;
            foreach (var candidate in StrictNavigation(actor).Reachable(actor.Cell, definition.Behaviour.grazingSearchSteps))
                if (grass.HasFood(candidate, time) && SetGoal(actor, candidate))
                { actor.Activity = CreatureActivity.Searching; return true; }
            return false;
        }

        private bool SleepAttemptHasProgress(CreatureAgent actor)
        {
            if (actor.Activity == CreatureActivity.Sleeping || actor.Activity == CreatureActivity.ClimbingTree
                || actor.Activity == CreatureActivity.DescendingTree || actor.Activity == CreatureActivity.EnteringGrave
                || actor.Activity == CreatureActivity.LeavingGrave || actor.Activity == CreatureActivity.EnteringBurrow
                || actor.Activity == CreatureActivity.LeavingBurrow) return true;
            return actor.Activity == CreatureActivity.SeekingSleep && (actor.Route.Count > 0
                || actor.FlightState != null && actor.FlightState.LandingForSleep && actor.IsAirborne);
        }

        private bool TrySleepForNeeds(CreatureAgent actor)
        {
            // A real sleeper keeps its site and completes recovery using the existing shelter rules.
            if (actor.Activity == CreatureActivity.Sleeping)
            { SeekSleep(actor); return true; }
            if (time < actor.SleepRetryAfter) return false;
            SeekSleep(actor);
            if (SleepAttemptHasProgress(actor)) return true;
            // Recheck missing/blocked shelter without performing an impossible search every decision.
            actor.SleepRetryAfter = time + actor.Definition.Behaviour.sleepSearchRetrySeconds;
            return false;
        }
    }
}

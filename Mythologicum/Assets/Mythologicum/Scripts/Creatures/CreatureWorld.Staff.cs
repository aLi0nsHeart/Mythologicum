using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private bool RegisteredLiving(CreatureAgent actor) => actor != null && actor.Alive
            && actor.Health != null && actor.Health.Alive && agents.Contains(actor);

        /// <summary>Applies an external strike through the shared health/death pipeline.</summary>
        public float ApplyDamage(CreatureAgent actor, float amount)
        {
            if (!RegisteredLiving(actor) || actor.CarriedBy != null) return 0f;
            float dealt = actor.Health.Damage(amount);
            if (dealt <= 0f) return 0f;
            if (!actor.Health.Alive)
            {
                CancelCarry(actor);
                ClearCombatLinks(actor);
                actor.Alive = false; actor.IsSedated = false; actor.SedationRemaining = 0f;
                actor.RetirementRequested = false; actor.SedationRequested = false; actor.MoveRequestedDestination = null;
                actor.Route.Clear(); actor.Goal = null; actor.AttackedFence = null;
                actor.Activity = CreatureActivity.Dying; actor.CorpseTimer = 3f;
                actor.StaffStatus = "Deceased";
                Collider collider = actor.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }
            RefreshVisual(actor);
            return dealt;
        }

        /// <summary>Ground-level tranquilization. Sleeping in a tree or grave must finish before a keeper can reach it.</summary>
        public bool Sedate(CreatureAgent actor, float seconds = 90f)
        {
            if (!RegisteredLiving(actor) || actor.ExternallyControlled || actor.CarriedBy != null
                || Mathf.Abs(actor.Elevation) > .01f || actor.Activity == CreatureActivity.ClimbingFence
                || actor.Activity == CreatureActivity.PhasingFence || float.IsNaN(seconds)
                || float.IsInfinity(seconds) || seconds <= 0f) return false;
            ClearCombatLinks(actor);
            actor.IsSedated = true; actor.SedationRemaining = Mathf.Max(actor.SedationRemaining, seconds);
            actor.SedationRequested = false; actor.StaffStatus = "Sedated";
            actor.Route.Clear(); actor.Goal = null; actor.AttackedFence = null; actor.AttemptFence = null;
            actor.EscapeIntent = false; actor.EscapeGoal = null; actor.EscapeBarrier = null;
            actor.HasDanger = false; actor.Threat = null; actor.SocialPartner = null;
            actor.WantsSleep = false; actor.SleepTree = null; actor.GrazeTimer = 0f;
            actor.Activity = CreatureActivity.Sedated; actor.DecisionTimer = 0f;
            RefreshVisual(actor);
            return true;
        }

        public bool CanRelocate(CreatureAgent actor, Vector2Int cell, CreatureAgent ignore = null)
        {
            if (!RegisteredLiving(actor)) return false;
            RefreshNavigation();
            if (!navigation.IsWalkable(cell)) return false;
            Vector3 point = grid.CellCenter(cell);
            foreach (CreatureAgent other in agents)
                if (other != actor && other != ignore && other.Alive
                    && HorizontalDistance(other.transform.position, point) < grid.CellSize * .7f) return false;
            return true;
        }

        public bool BeginCarry(CreatureAgent actor, CreatureAgent carrier)
        {
            if (!RegisteredLiving(actor) || !RegisteredLiving(carrier) || actor == carrier || !actor.IsSedated
                || actor.CarriedBy != null || Mathf.Abs(actor.Elevation) > .01f
                || HorizontalDistance(actor.transform.position, carrier.transform.position) > 2.5f
                || actor.Cell != carrier.Cell && !navigation.CanCross(carrier.Cell, actor.Cell)) return false;
            ClearCombatLinks(actor);
            actor.CarriedBy = carrier; actor.Route.Clear(); actor.Goal = null;
            actor.Activity = CreatureActivity.Carried; actor.StaffStatus = "Being carried by a zookeeper";
            FollowCarrier(actor, carrier);
            RefreshVisual(actor);
            return true;
        }

        public bool DropCarried(CreatureAgent actor, Vector2Int cell)
        {
            if (!RegisteredLiving(actor) || actor.CarriedBy == null || !CanRelocate(actor, cell, actor.CarriedBy)) return false;
            // Staff movement, rather than this method, must bring the animal to its destination.
            if (actor.CarriedBy.Cell != cell) return false;
            actor.CarriedBy = null; actor.Cell = cell; actor.Elevation = 0f;
            actor.transform.position = GroundPosition(actor, cell);
            actor.Activity = CreatureActivity.Sedated; actor.MoveRequestedDestination = null;
            actor.StaffStatus = "Moved; remains sedated until the tranquilizer wears off";
            RefreshVisual(actor);
            return true;
        }

        public void CancelCarry(CreatureAgent actor)
        {
            if (actor == null || actor.CarriedBy == null) return;
            CreatureAgent carrier = actor.CarriedBy;
            actor.Cell = carrier.Cell; actor.CarriedBy = null; actor.Elevation = 0f;
            actor.transform.position = GroundPosition(actor, actor.Cell);
            actor.MoveRequestedDestination = null;
            if (actor.Alive)
            {
                actor.Activity = actor.IsSedated ? CreatureActivity.Sedated : CreatureActivity.Resting;
                actor.StaffStatus = "Transport interrupted; animal placed on the ground";
            }
        }

        private void ClearCombatLinks(CreatureAgent actor)
        {
            ReleaseClaim(actor); actor.CapturedBy = null;
            foreach (CreatureAgent other in agents)
            {
                if (other.Prey == actor)
                {
                    ReleaseClaim(other);
                    if (other.Activity == CreatureActivity.Attacking || other.Activity == CreatureActivity.Feeding
                        || other.Activity == CreatureActivity.Scaring)
                    { other.Activity = CreatureActivity.Resting; other.StateTimer = PauseDuration(other); }
                    other.DecisionTimer = 0f;
                }
                if (other.CapturedBy == actor) { other.CapturedBy = null; other.DecisionTimer = 0f; }
            }
        }

        private void FollowCarrier(CreatureAgent actor, CreatureAgent carrier)
        {
            actor.Cell = carrier.Cell;
            actor.transform.position = carrier.transform.position + Vector3.up * 1.2f + carrier.transform.right * .6f;
            actor.transform.rotation = carrier.transform.rotation;
        }

        private bool TickStaffControl(CreatureAgent actor, float dt)
        {
            if (actor.CarriedBy != null)
            {
                if (actor.CarriedBy.Alive && agents.Contains(actor.CarriedBy))
                {
                    FollowCarrier(actor, actor.CarriedBy);
                    actor.Activity = CreatureActivity.Carried;
                    actor.Vitals.Tick(dt, actor.Definition.hungerPerSecond, 0f, actor.Definition.fearRecoveryPerSecond, false);
                    actor.Energy.Tick(dt, 0f, 1f, true, actor.Definition.sleepRecoveryPerSecond);
                    RefreshVisual(actor); return true;
                }
                CancelCarry(actor);
            }
            if (!actor.IsSedated) return false;
            actor.Activity = CreatureActivity.Sedated;
            actor.Vitals.Tick(dt, actor.Definition.hungerPerSecond, 0f, actor.Definition.fearRecoveryPerSecond, false);
            actor.Energy.Tick(dt, 0f, 1f, true, actor.Definition.sleepRecoveryPerSecond);
            actor.SedationRemaining = Mathf.Max(0f, actor.SedationRemaining - dt);
            if (actor.SedationRemaining <= 0f)
            {
                actor.IsSedated = false; actor.MoveRequestedDestination = null; actor.SedationRequested = false;
                actor.StaffStatus = actor.RetirementRequested ? "Retirement requested" : "Awake";
                actor.Activity = CreatureActivity.Resting; actor.DecisionTimer = 0f;
                actor.StateTimer = PauseDuration(actor); actor.NeedsTimer = 0f;
            }
            RefreshVisual(actor);
            return true;
        }
    }
}

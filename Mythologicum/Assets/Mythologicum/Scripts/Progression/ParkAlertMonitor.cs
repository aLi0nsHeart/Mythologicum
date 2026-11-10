using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Remembers an observed secured area for reporting only; free placement never creates an escape.</summary>
    public sealed class CreatureContainmentObservation
    {
        private HashSet<Vector2Int> containedCells, sourceCells;
        public bool Observe(Vector2Int cell, HashSet<Vector2Int> securedCells, bool transported = false)
        {
            if (transported) { containedCells = sourceCells = null; return false; }
            bool escaped = containedCells != null && !containedCells.Contains(cell);
            if (securedCells != null && sourceCells != securedCells)
            { sourceCells = securedCells; containedCells = new HashSet<Vector2Int>(securedCells); }
            return escaped;
        }
    }

    /// <summary>One report per sustained condition, with a cooldown between separate occurrences.</summary>
    public sealed class ParkAlertLatch
    {
        private sealed class Condition
        {
            public bool active, reported;
            public double since, lastReport = double.NegativeInfinity;
        }
        private readonly Dictionary<string, Condition> conditions = new Dictionary<string, Condition>();
        public double Time { get; private set; }
        public void Advance(float dt)
        { if (dt > 0 && !float.IsNaN(dt) && !float.IsInfinity(dt)) Time += dt; }
        public bool IsActive(string key) => conditions.TryGetValue(key, out var state) && state.active;
        public bool Observe(string key, bool active, float delaySeconds, float cooldownSeconds, bool silent = false)
        {
            if (!conditions.TryGetValue(key, out var state)) conditions.Add(key, state = new Condition());
            if (!active) { state.active = false; state.reported = false; return false; }
            if (!state.active) { state.active = true; state.since = Time; }
            if (silent) { state.reported = true; state.lastReport = Time; return false; }
            if (state.reported || Time - state.since < Nonnegative(delaySeconds)
                || Time - state.lastReport < Nonnegative(cooldownSeconds)) return false;
            state.reported = true; state.lastReport = Time; return true;
        }
        public void Retain(HashSet<string> keys)
        {
            var removed = new List<string>();
            foreach (var pair in conditions) if (!keys.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (string key in removed) conditions.Remove(key);
        }
        public void Clear() => conditions.Clear();
        private static float Nonnegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
    }

    /// <summary>Observes the simulation without assigning creatures to enclosures or changing their decisions.</summary>
    public sealed class ParkAlertMonitor
    {
        private sealed class CreatureState
        {
            public readonly CreatureContainmentObservation containment = new CreatureContainmentObservation();
            public double firstSeen;
        }
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly ParkBuildingNetwork buildings;
        private readonly CreatureWorld creatures;
        private readonly VisitorWorld visitors;
        private readonly StaffWorld staff;
        private readonly EnclosureMap enclosures;
        private readonly NotificationInbox inbox;
        private readonly CreatureNavigation ordinaryMovement;
        private readonly Dictionary<int, CreatureState> creatureStates = new Dictionary<int, CreatureState>();
        private readonly Dictionary<Vector2Int, bool> pathExposure = new Dictionary<Vector2Int, bool>();
        private readonly HashSet<int> deadVisitors = new HashSet<int>();
        private readonly HashSet<string> observedKeys = new HashSet<string>();
        private readonly ParkAlertLatch latch = new ParkAlertLatch();
        private int gridRevision = -1, fenceRevision = -1, lastVisitorDeaths;
        private float accumulated;
        private bool initialized;
        public ParkAlertRules Rules { get; }

        public ParkAlertMonitor(SquareBuildGrid grid, FenceLayout fences, ParkBuildingNetwork buildings,
            CreatureWorld creatures, VisitorWorld visitors, StaffWorld staff, EnclosureMap enclosures,
            NotificationInbox inbox, ParkAlertRules rules = null)
        {
            this.grid = grid; this.fences = fences; this.buildings = buildings; this.creatures = creatures;
            this.visitors = visitors; this.staff = staff; this.enclosures = enclosures; this.inbox = inbox;
            Rules = rules ?? ParkAlertDefinition.Current.rules;
            ordinaryMovement = new CreatureNavigation(grid, fences, creatures.IsClear);
        }

        /// <summary>Call after reconstructing a save. Existing problems remain in history without sending duplicate letters.</summary>
        public void ResetBaseline()
        {
            creatureStates.Clear(); pathExposure.Clear(); deadVisitors.Clear(); latch.Clear();
            gridRevision = fenceRevision = -1; lastVisitorDeaths = visitors.LostCount;
            accumulated = 0; initialized = true; Scan(true);
        }

        public void Tick(float dt)
        {
            if (!Rules.enabled) { initialized = false; return; }
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (creatures.RestoringSave) { initialized = false; return; }
            latch.Advance(dt);
            if (!initialized) { ResetBaseline(); return; }
            accumulated += dt;
            if (accumulated < Math.Max(.1f, Rules.checkIntervalSeconds)) return;
            accumulated = 0; Scan(false);
        }

        private void Scan(bool silent)
        {
            buildings.Refresh();
            enclosures.Refresh(buildings.IsPathConnected);
            if (gridRevision != grid.Revision || fenceRevision != fences.Revision)
            { pathExposure.Clear(); gridRevision = grid.Revision; fenceRevision = fences.Revision; }
            observedKeys.Clear();
            var living = new HashSet<int>();
            Vector2Int? deathLocation = null;
            foreach (var actor in creatures.Agents)
            {
                if (actor == null || actor.Definition == null) continue;
                if ((actor.Definition.tags & CreatureTags.Visitor) != 0 && !actor.Alive)
                {
                    if (deadVisitors.Add(actor.GetInstanceID())) deathLocation = actor.Cell;
                    continue;
                }
                if (!actor.Alive || actor.ExternallyControlled) continue;
                int id = actor.GetInstanceID(); living.Add(id);
                if (!creatureStates.TryGetValue(id, out var state))
                    creatureStates.Add(id, state = new CreatureState
                    { firstSeen = latch.Time - (silent ? Math.Max(0, Rules.placementGraceSeconds) : 0) });
                bool transported = actor.CarriedBy != null || actor.IsSedated;
                if (transported) state.firstSeen = latch.Time;
                var region = enclosures.At(actor.Cell);
                bool escaped = state.containment.Observe(actor.Cell, region != null && region.IsSecured ? region.Cells : null, transported);
                if (Observe("escaped:" + id, Rules.creatureEscapes && escaped, 0, silent))
                    inbox.PostLocalized("escaped:" + id, ParkNoticeKind.CreatureEscaped, "park.notification.creature_escaped",
                        "park.notification.creature_escaped_body", actor.Cell,titleArguments:new[]{actor.Definition.displayName});
                bool exposed = Rules.publicPathExposure && !transported && actor.Definition.threat >= Rules.publicPathThreat
                    && latch.Time - state.firstSeen >= Math.Max(0, Rules.placementGraceSeconds) && CanReachVisitorPath(actor.Cell);
                if (Observe("exposed:" + id, exposed, 0, silent))
                    inbox.PostLocalized("exposed:" + id, ParkNoticeKind.CreatureExposed, "park.notification.creature_exposed",
                        "park.notification.creature_exposed_body", actor.Cell,titleArguments:new[]{actor.Definition.displayName});
            }
            var removed = new List<int>();
            foreach (var pair in creatureStates) if (!living.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (int id in removed) creatureStates.Remove(id);

            foreach (var guest in visitors.Visitors)
            {
                var actor = guest != null ? guest.Actor : null;
                if (actor == null || !actor.Alive || actor.Health == null) continue;
                string key = "injured-visitor:" + actor.GetInstanceID();
                float threshold = latch.IsActive(key) ? Rules.injuryRecoveryFraction : Rules.injuryHealthFraction;
                bool injured = Rules.visitorInjuries && actor.Health.Current / actor.Health.Maximum <= threshold;
                if (Observe(key, injured, 0, silent))
                    inbox.PostLocalized(key, ParkNoticeKind.VisitorInjured, "park.notification.visitor_injured",
                        "park.notification.visitor_injured_body", actor.Cell,bodyArguments:new[]{actor.Health.Current.ToString("0"),actor.Health.Maximum.ToString("0")});
            }
            int deaths = Math.Max(0, visitors.LostCount - lastVisitorDeaths);
            if (!silent && Rules.visitorDeaths && deaths > 0)
                inbox.PostLocalized("visitor-fatality:" + visitors.LostCount, ParkNoticeKind.VisitorDied,
                    deaths==1?"park.notification.visitor_died":"park.notification.visitors_died",
                    deaths==1?"park.notification.visitor_died_body":"park.notification.visitors_died_body", deathLocation,
                    titleArguments:new[]{deaths.ToString()},bodyArguments:new[]{deaths.ToString()});
            lastVisitorDeaths = visitors.LostCount;

            foreach (var building in buildings.Records)
            {
                if (building.Footprint.Definition.kind != BuildingKind.GoatDispenser) continue;
                string key = "empty-feeder:" + building.RuntimeId;
                if (Observe(key, Rules.emptyFeeders && building.FeedStock <= 0, Rules.emptyFeederDelaySeconds, silent))
                    inbox.PostLocalized(key, ParkNoticeKind.FeederEmpty, "park.notification.feeder_empty",
                        "park.notification.feeder_empty_body", building.Footprint.Anchor);
            }
            foreach (var worker in staff.Staff)
            {
                if (worker == null || worker.Actor == null || !worker.Actor.Alive) continue;
                string key = "blocked-staff:" + worker.Actor.GetInstanceID();
                if (Observe(key, Rules.blockedStaff && worker.Task == StaffTask.Blocked, Rules.blockedStaffDelaySeconds, silent))
                    inbox.PostLocalized(key, ParkNoticeKind.StaffBlocked, "park.notification.staff_blocked",
                        "park.notification.staff_blocked_body", worker.Actor.Cell,titleArguments:new[]{"enum.StaffRole."+worker.Role});
            }
            latch.Retain(observedKeys);
            // Dead actor IDs are only needed while a corpse remains in the simulation.
            var existingDead = new HashSet<int>();
            foreach (var actor in creatures.Agents)
                if (actor != null && !actor.Alive && (actor.Definition.tags & CreatureTags.Visitor) != 0) existingDead.Add(actor.GetInstanceID());
            deadVisitors.IntersectWith(existingDead);
        }

        private bool Observe(string key, bool active, float delay, bool silent)
        {
            observedKeys.Add(key);
            return latch.Observe(key, active, delay, Rules.recurrenceCooldownSeconds, silent);
        }

        private bool CanReachVisitorPath(Vector2Int cell)
        {
            if (pathExposure.TryGetValue(cell, out bool exposed)) return exposed;
            var region = ordinaryMovement.Reachable(cell, grid.Width * grid.Depth);
            foreach (var ground in region) if (buildings.IsPathConnected(ground)) { exposed = true; break; }
            foreach (var ground in region) pathExposure[ground] = exposed;
            return exposed;
        }
    }
}

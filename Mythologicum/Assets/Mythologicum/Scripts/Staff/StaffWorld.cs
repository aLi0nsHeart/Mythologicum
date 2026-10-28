using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Physical staff jobs. Requests and research belong to this session, not to enclosures.</summary>
    public sealed partial class StaffWorld : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly CreatureWorld creatures;
        private readonly ParkEconomy economy;
        private readonly PeopleDefinition people;
        private readonly StaffNavigation navigation;
        private readonly CreatureNavigation groundNavigation;
        private readonly Dictionary<StaffRole, CreatureDefinition> profiles = new Dictionary<StaffRole, CreatureDefinition>();
        private readonly List<StaffAgent> workers = new List<StaffAgent>();
        private readonly Dictionary<ParkBuildingRecord, CreatureDefinition> researchTargets = new Dictionary<ParkBuildingRecord, CreatureDefinition>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> groundRegions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        private readonly StaffDefinition tuning;
        private readonly bool ownsTuning;
        private int gridRevision = -1, fenceRevision = -1;
        public ParkBuildingNetwork Buildings { get; }
        public SpeciesResearch Research { get; }
        public IReadOnlyList<StaffAgent> Staff => workers;

        public StaffWorld(SquareBuildGrid grid, FenceLayout fences, ParkBuildingNetwork buildings,
            CreatureWorld creatures, ParkEconomy economy, PeopleDefinition people, CreatureDefinition[] species, StaffDefinition tuning = null)
        {
            this.grid = grid; this.fences = fences; Buildings = buildings; this.creatures = creatures;
            this.economy = economy; this.people = people;
            this.tuning = tuning ?? Resources.Load<StaffDefinition>("Staff/DefaultStaff");
            if (this.tuning == null) { this.tuning = ScriptableObject.CreateInstance<StaffDefinition>(); ownsTuning = true; }
            navigation = new StaffNavigation(grid, fences, creatures.IsClear);
            groundNavigation = new CreatureNavigation(grid, fences, creatures.IsClear);
            Research = new SpeciesResearch(species);
        }

        public bool TryHire(ParkBuildingRecord home)
        {
            Buildings.Refresh();
            if (home == null || !Buildings.Contains(home) || !home.IsOpen || !home.Footprint.Definition.IsEmployeeBuilding
                || home.HiredCount >= 5 || !economy.CanAfford(home.Footprint.Definition.hireCost)) return false;
            StaffRole role = RoleFor(home.Footprint.Definition.kind);
            CreatureAgent actor = null;
            foreach (Vector2Int cell in navigation.Reachable(home.Footprint.AccessCell))
            {
                if (ConstructionRouteGraph.CellDistance(home.Footprint.AccessCell, cell) > 2) continue;
                if (!creatures.CanSpawn(cell)) continue;
                GameObject model = people == null ? null : role == StaffRole.Mercenary ? people.police
                    : role == StaffRole.Researcher ? people.doctor : people.worker;
                actor = creatures.SpawnExternal(Profile(role), cell, model);
                if (actor != null) break;
            }
            if (actor == null) return false;
            float cost = home.Footprint.Definition.hireCost;
            economy.TrySpend(cost); home.RecordExpense(cost); home.HiredCount++;
            var worker = actor.gameObject.AddComponent<StaffAgent>();
            worker.Actor = actor; worker.Home = home; worker.Role = role;
            workers.Add(worker); UseLocomotionController(actor);
            return true;
        }

        internal StaffAgent RestoreEmployee(ParkBuildingRecord home, Vector2Int cell)
        {
            if (!Buildings.Contains(home) || !home.Footprint.Definition.IsEmployeeBuilding || home.HiredCount >= 5 || !grid.Contains(cell)) return null;
            StaffRole role = RoleFor(home.Footprint.Definition.kind);
            GameObject model = people == null ? null : role == StaffRole.Mercenary ? people.police : role == StaffRole.Researcher ? people.doctor : people.worker;
            var actor = creatures.SpawnExternal(Profile(role), cell, model);
            if (actor == null) return null;
            var worker = actor.gameObject.AddComponent<StaffAgent>();
            worker.Actor = actor; worker.Home = home; worker.Role = role;
            home.HiredCount++; workers.Add(worker); UseLocomotionController(actor); return worker;
        }

        public bool Fire(ParkBuildingRecord home)
        {
            for (int i = workers.Count - 1; i >= 0; i--)
                if (workers[i].Home == home) { RemoveWorker(i); return true; }
            return false;
        }
        public CreatureDefinition ResearchTarget(ParkBuildingRecord home) => home != null
            && researchTargets.TryGetValue(home, out var species) ? species : null;
        public bool SetResearchTarget(ParkBuildingRecord home, CreatureDefinition species)
        {
            if (home == null || !Buildings.Contains(home) || home.Footprint.Definition.kind != BuildingKind.ResearchCentre || species == null) return false;
            researchTargets[home] = species;
            foreach (StaffAgent worker in workers) if (worker.Home == home) ClearJob(worker);
            return true;
        }

        public bool RequestRetire(CreatureAgent target)
        {
            if (!ValidCreature(target)) return false;
            CancelJobsFor(target);
            target.RetirementRequested = true; target.SedationRequested = false; target.MoveRequestedDestination = null;
            target.StaffStatus = "Retirement requested: waiting for mercenaries";
            return true;
        }
        public bool RequestSedate(CreatureAgent target)
        {
            if (!ValidCreature(target) || target.IsSedated || target.RetirementRequested) return false;
            target.SedationRequested = true; target.StaffStatus = "Sedation requested: waiting for a zookeeper";
            return true;
        }
        public bool RequestMove(CreatureAgent target, Vector2Int destination)
        {
            if (!CanMoveTo(target,destination)) return false;
            CancelJobsFor(target);
            target.MoveRequestedDestination = destination; target.StaffStatus = "Move requested: waiting for a zookeeper";
            return true;
        }
        public bool CanMoveTo(CreatureAgent target, Vector2Int destination) => ValidCreature(target) && target.IsSedated
            && target.CarriedBy == null && !target.RetirementRequested && creatures.CanRelocate(target,destination)
            && navigation.FindPath(target.Cell,destination).Count > 0;

        public void Tick(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Buildings.Refresh(); RefreshRoutes(); TickShots(dt);
            for (int i = workers.Count - 1; i >= 0; i--)
            {
                StaffAgent worker = workers[i]; CreatureAgent actor = worker.Actor;
                if (actor == null || !actor.Alive || !Buildings.Contains(worker.Home)) { RemoveWorker(i); continue; }
                worker.DecisionTimer -= dt; worker.ShotTimer -= dt;
                if (actor.CapturedBy != null && actor.CapturedBy.Alive)
                { ClearJob(worker); SetStatus(worker, StaffTask.Blocked, "Caught by a creature"); continue; }
                if (!worker.Home.IsOpen)
                { ClearJob(worker); SetStatus(worker, StaffTask.Blocked, "Workplace disconnected from the entrance"); continue; }
                CreatureAgent danger = WorkerDanger(worker);
                actor.Vitals.Tick(dt, 0f, 30f, 20f, danger != null);
                actor.Energy.Tick(dt, 0f, 1f, false, 0f);
                if (danger != null && worker.Role != StaffRole.Mercenary)
                {
                    if (worker.DecisionTimer <= 0f || worker.Task != StaffTask.Fleeing)
                    {
                        ClearJob(worker); Flee(worker, danger); worker.DecisionTimer = .5f;
                    }
                }
                else
                {
                    if (worker.Task == StaffTask.Fleeing) ClearJob(worker);
                    if (worker.Target != null && !worker.Target.Alive) ClearJob(worker);
                    if (worker.Task == StaffTask.Idle || worker.Task == StaffTask.Blocked || worker.Task == StaffTask.Patrolling
                        || worker.Task == StaffTask.Returning)
                    {
                        if (worker.DecisionTimer <= 0f)
                        { Assign(worker); worker.DecisionTimer = 1f; }
                    }
                    Work(worker, dt);
                }
                Move(worker, dt);
                actor.ExternalStatus = worker.Status;
                creatures.RefreshExternal(actor);
            }
        }

        private static StaffRole RoleFor(BuildingKind kind) => kind == BuildingKind.ResearchCentre ? StaffRole.Researcher
            : kind == BuildingKind.MercenaryHQ ? StaffRole.Mercenary : StaffRole.Zookeeper;
        private CreatureDefinition Profile(StaffRole role)
        {
            if (profiles.TryGetValue(role, out var result)) return result;
            result = ScriptableObject.CreateInstance<CreatureDefinition>();
            result.displayName = role.ToString(); result.origin = "Human"; result.diet = CreatureDiet.None;
            result.tags = CreatureTags.Staff | CreatureTags.Visitor;
            result.maximumHealth = 100f; result.damage = role == StaffRole.Mercenary ? tuning.mercenaryDamage : 0f;
            result.threat = role == StaffRole.Mercenary ? tuning.mercenaryStrength : 5f;
            result.hungerPerSecond = 0f; result.energyDrainPerSecond = 0f;
            result.minimumSpace = 0f; result.bodyScale = new Vector3(.5f,.85f,.5f);
            result.color = role == StaffRole.Mercenary ? new Color(.2f,.25f,.7f) : new Color(.9f,.8f,.3f);
            result.manuallyPlaceable = false;
            profiles[role] = result; return result;
        }
        private void UseLocomotionController(CreatureAgent actor)
        {
            if (actor.BodyAnimator == null || people == null || people.civilians == null) return;
            bool female = actor.Body.name.IndexOf("Female", StringComparison.OrdinalIgnoreCase) >= 0;
            foreach (GameObject civilian in people.civilians)
            {
                if (civilian == null) continue;
                if (civilian.name.IndexOf(female ? "_Female_" : "_Male_", StringComparison.OrdinalIgnoreCase) < 0) continue;
                Animator source = civilian.GetComponentInChildren<Animator>();
                if (source == null || source.runtimeAnimatorController == null) continue;
                actor.BodyAnimator.runtimeAnimatorController = source.runtimeAnimatorController;
                actor.BodyAnimator.applyRootMotion = false; return;
            }
        }
        private bool ValidCreature(CreatureAgent target) => target != null && target.Alive && !target.ExternallyControlled;
        private void CancelJobsFor(CreatureAgent target)
        { foreach (StaffAgent worker in workers) if (worker.Target == target) ClearJob(worker); }
        private void RemoveWorker(int index)
        {
            StaffAgent worker = workers[index]; ClearJob(worker);
            worker.Home.HiredCount = Mathf.Max(0, worker.Home.HiredCount - 1);
            // Dead bodies remain in the shared world until feeding/death presentation finishes.
            if (worker.Actor != null && worker.Actor.Alive) creatures.RemoveExternal(worker.Actor);
            workers.RemoveAt(index);
        }
        private void ClearJob(StaffAgent worker)
        {
            if (worker.Target != null && worker.Target.CarriedBy == worker.Actor) creatures.CancelCarry(worker.Target);
            worker.Target = null; worker.Fence = null; worker.Feeder = null; worker.Destination = null; worker.MeetingPoint = null;
            worker.ActionTimer = 0f;
            if (worker.Actor != null) { worker.Actor.Route.Clear(); worker.Actor.Goal = null; }
            SetStatus(worker, StaffTask.Idle, "Ready for work");
        }
        private static void SetStatus(StaffAgent worker, StaffTask task, string status)
        {
            worker.Task = task; worker.Status = status;
            if (worker.Actor != null)
            {
                worker.Actor.Activity = task == StaffTask.Fleeing ? CreatureActivity.Fleeing
                    : task == StaffTask.Hunting || task == StaffTask.Retiring ? CreatureActivity.Hunting : CreatureActivity.Wandering;
                worker.Actor.ExternalStatus = status;
            }
        }
        private bool Claimed(CreatureAgent target, StaffAgent except)
        { foreach (StaffAgent other in workers) if (other != except && other.Target == target && other.Role == StaffRole.Zookeeper) return true; return false; }
        private void RefreshRoutes()
        {
            if (gridRevision == grid.Revision && fenceRevision == fences.Revision) return;
            gridRevision = grid.Revision; fenceRevision = fences.Revision; groundRegions.Clear();
            foreach (StaffAgent worker in workers)
            {
                worker.DecisionTimer = 0f;
                if (worker.Actor.Route.Count > 0 && !navigation.CanCross(worker.Actor.Cell, worker.Actor.Route[0]))
                { worker.Actor.Route.Clear(); worker.Actor.Goal = null; worker.Actor.transform.position = Position(worker.Actor, worker.Actor.Cell); }
            }
        }
        private bool SetGoal(StaffAgent worker, Vector2Int target, bool pathsOnly = false)
        {
            CreatureAgent actor = worker.Actor;
            if (actor.Goal == target && actor.Route.Count > 0) return true;
            bool moving = actor.Route.Count > 0 && navigation.CanCross(actor.Cell, actor.Route[0], pathsOnly);
            Vector2Int from = moving ? actor.Route[0] : actor.Cell;
            var route = navigation.FindPath(from, target, pathsOnly);
            if (route.Count == 0) return false;
            actor.Route.Clear(); actor.Goal = target;
            for (int i = moving ? 0 : 1; i < route.Count; i++) actor.Route.Add(route[i]);
            return true;
        }
        private void Move(StaffAgent worker, float dt)
        {
            CreatureAgent actor = worker.Actor;
            if (actor.Route.Count == 0) return;
            Vector2Int next = actor.Route[0];
            if (!navigation.CanCross(actor.Cell, next)) { actor.Route.Clear(); actor.Goal = null; worker.DecisionTimer = 0f; return; }
            Vector3 goal = Position(actor,next), direction = goal - actor.transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
            float speed = worker.Task == StaffTask.Fleeing || worker.Task == StaffTask.Hunting || worker.Task == StaffTask.Retiring
                ? tuning.runSpeed : tuning.walkSpeed;
            actor.transform.position = Vector3.MoveTowards(actor.transform.position, goal, Mathf.Max(.1f,speed)*dt);
            if ((actor.transform.position-goal).sqrMagnitude < .0001f) { actor.Cell=next; actor.Route.RemoveAt(0); }
        }
        private Vector3 Position(CreatureAgent actor, Vector2Int cell) => grid.CellCenter(cell) + Vector3.up * actor.Definition.bodyScale.y;
        private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x-b.x,a.z-b.z).magnitude;
        public void Dispose()
        {
            for (int i=workers.Count-1;i>=0;i--) RemoveWorker(i);
            foreach (var profile in profiles.Values) UnityEngine.Object.Destroy(profile);
            foreach (var shot in shots) if (shot.Object != null) UnityEngine.Object.Destroy(shot.Object);
            if (shotMaterial != null) UnityEngine.Object.Destroy(shotMaterial);
            if (ownsTuning) UnityEngine.Object.Destroy(tuning);
        }
    }
}

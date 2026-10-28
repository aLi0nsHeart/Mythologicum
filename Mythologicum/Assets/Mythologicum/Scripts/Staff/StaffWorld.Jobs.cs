using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class StaffWorld
    {
        private sealed class ShotEffect { public GameObject Object; public float Remaining; }
        private readonly List<ShotEffect> shots = new List<ShotEffect>();
        private Material shotMaterial;

        private void Assign(StaffAgent worker)
        {
            if (worker.Role == StaffRole.Researcher)
            {
                CreatureDefinition species = ResearchTarget(worker.Home);
                if (species == null)
                    foreach (CreatureAgent creature in creatures.Agents)
                        if (ValidCreature(creature)) { species = creature.Definition; researchTargets[worker.Home] = species; break; }
                if (species != null && Research.Progress(species) < 100f)
                    foreach (CreatureAgent creature in creatures.Agents)
                        if (ValidCreature(creature) && creature.Definition == species && FindRangedPosition(worker, creature, tuning.studyRange).HasValue)
                        { worker.Target = creature; SetStatus(worker, StaffTask.Researching, "Travelling to observe " + species.displayName); return; }
                ReturnHome(worker, species == null ? "Choose a species to research" : Research.Progress(species) >= 100f
                    ? "Research complete: choose another species" : "No visible, reachable specimen to study");
                return;
            }
            if (worker.Role == StaffRole.Mercenary)
            {
                CreatureAgent target = null;
                foreach (CreatureAgent creature in creatures.Agents)
                    if (ValidCreature(creature) && creature.RetirementRequested && creature.CarriedBy == null)
                    { target = creature; break; }
                if (target == null)
                    foreach (CreatureAgent creature in creatures.Agents)
                        if (ValidCreature(creature) && !creature.IsSedated && IsExposed(creature)
                            && creatures.CanSee(worker.Actor, creature, tuning.perceptionRange))
                        { target = creature; break; }
                if (target != null) { JoinGroup(worker, target); return; }
                Patrol(worker); return;
            }
            foreach (CreatureAgent creature in creatures.Agents)
                if (ValidCreature(creature) && creature.SedationRequested && !creature.RetirementRequested && !Claimed(creature,worker))
                { worker.Target = creature; SetStatus(worker, StaffTask.Sedating, "Responding to a sedation request"); return; }
            foreach (CreatureAgent creature in creatures.Agents)
                if (ValidCreature(creature) && creature.MoveRequestedDestination.HasValue && creature.IsSedated
                    && !creature.RetirementRequested && !Claimed(creature,worker))
                { worker.Target = creature; SetStatus(worker, StaffTask.FetchingCreature, "Going to collect " + creature.Definition.displayName); return; }
            foreach (CreatureAgent creature in creatures.Agents)
                if (ValidCreature(creature) && creature.Health.Current < creature.Health.Maximum && !creature.RetirementRequested
                    && !Claimed(creature,worker) && FindRangedPosition(worker, creature, tuning.shootingRange).HasValue)
                { worker.Target = creature; SetStatus(worker, StaffTask.Healing, "Going to treat " + creature.Definition.displayName); return; }
            foreach (ParkBuildingRecord feeder in Buildings.Records)
            {
                if (feeder.Footprint.Definition.kind != BuildingKind.GoatDispenser || feeder.FeedStock > 2) continue;
                bool reserved = false; foreach (StaffAgent other in workers) if (other.Feeder == feeder) reserved = true;
                if (reserved || !SetGoal(worker, feeder.Footprint.AccessCell)) continue;
                worker.Feeder = feeder; SetStatus(worker, StaffTask.Restocking, "Taking supplies to a goat feeder"); return;
            }
            foreach (FenceEdge edge in fences.Edges)
            {
                if (fences.HealthAt(edge) >= fences.MaximumHealthAt(edge)) continue;
                bool reserved = false; foreach (StaffAgent other in workers) if (other.Fence.HasValue && other.Fence.Value.Equals(edge)) reserved = true;
                if (reserved) continue;
                Vector2Int? approach = FenceApproach(worker,edge);
                if (!approach.HasValue || !SetGoal(worker,approach.Value)) continue;
                worker.Fence = edge; SetStatus(worker, StaffTask.Repairing, "Going to repair a damaged fence"); return;
            }
            ReturnHome(worker, "No care tasks waiting");
        }

        private void Work(StaffAgent worker, float dt)
        {
            if (worker.Task == StaffTask.Researching)
            {
                if (!ValidCreature(worker.Target) || worker.Target.Definition != ResearchTarget(worker.Home)
                    || Research.Progress(worker.Target.Definition) >= 100f) { ClearJob(worker); return; }
                if (ApproachRanged(worker,worker.Target,tuning.studyRange))
                {
                    Research.Study(worker.Target.Definition, tuning.researchPerSecond * dt);
                    worker.Status = "Studying " + worker.Target.Definition.displayName;
                }
            }
            else if (worker.Task == StaffTask.Regrouping || worker.Task == StaffTask.Hunting || worker.Task == StaffTask.Retiring)
                MercenaryWork(worker);
            else if (worker.Task == StaffTask.Sedating)
            {
                if (!ValidCreature(worker.Target) || worker.Target.IsSedated)
                { if (worker.Target != null) worker.Target.SedationRequested = false; ClearJob(worker); return; }
                if (ApproachRanged(worker,worker.Target,tuning.shootingRange) && ShootSedative(worker, worker.Target))
                { worker.Target.SedationRequested = false; worker.Target.StaffStatus = "Sedated: ready to move"; ClearJob(worker); }
            }
            else if (worker.Task == StaffTask.FetchingCreature || worker.Task == StaffTask.CarryingCreature)
                MoveCreature(worker);
            else if (worker.Task == StaffTask.Healing)
            {
                CreatureAgent target = worker.Target;
                if (!ValidCreature(target) || target.RetirementRequested || target.Health.Current >= target.Health.Maximum)
                { ClearJob(worker); return; }
                if (!target.IsSedated && target.Definition.threat > worker.Actor.Definition.threat)
                {
                    worker.Status = "Sedating a dangerous patient before treatment";
                    if (ApproachRanged(worker,target,tuning.shootingRange)) ShootSedative(worker,target);
                    return;
                }
                if (ApproachHandsOn(worker,target))
                { target.Health.Restore(tuning.healPerSecond * dt); worker.Status = "Treating " + target.Definition.displayName; }
            }
            else if (worker.Task == StaffTask.Repairing)
            {
                if (!worker.Fence.HasValue || !fences.HasFence(worker.Fence.Value)
                    || fences.HealthAt(worker.Fence.Value) >= fences.MaximumHealthAt(worker.Fence.Value)) { ClearJob(worker); return; }
                Vector2Int? approach = FenceApproach(worker,worker.Fence.Value);
                if (!approach.HasValue || !SetGoal(worker,approach.Value)) { Block(worker,"Fence cannot be reached: add staff access"); return; }
                if (AtNode(worker) && worker.Actor.Route.Count == 0)
                { fences.TryRepair(worker.Fence.Value, tuning.repairPerSecond * dt); worker.Status = "Repairing fence"; }
            }
            else if (worker.Task == StaffTask.Restocking)
            {
                var feeder = worker.Feeder;
                if (feeder == null || !Buildings.Contains(feeder) || feeder.FeedStock >= feeder.Footprint.Definition.feederCapacity)
                { ClearJob(worker); return; }
                if (!SetGoal(worker,feeder.Footprint.AccessCell)) { Block(worker,"Feeder cannot be reached: add a staff gate"); return; }
                if (AtNode(worker) && worker.Actor.Route.Count == 0)
                {
                    worker.Status = "Restocking goat feeder"; worker.ActionTimer += dt;
                    if (worker.ActionTimer >= tuning.restockSeconds)
                    {
                        float cost = feeder.Footprint.Definition.restockCost;
                        if (economy.TrySpend(cost)) { feeder.RecordExpense(cost); feeder.RestockFeed(); ClearJob(worker); }
                        else worker.Status = "Waiting for funds to buy feeder supplies";
                    }
                }
            }
        }

        private void MoveCreature(StaffAgent worker)
        {
            CreatureAgent target = worker.Target;
            if (!ValidCreature(target) || !target.IsSedated || !target.MoveRequestedDestination.HasValue)
            { if (target != null) target.MoveRequestedDestination = null; ClearJob(worker); return; }
            if (target.CarriedBy == null)
            {
                if (!ApproachHandsOn(worker,target)) return;
                // Pickup must finish on the creature's actual tile, including travel through any staff gate.
                if (worker.Actor.Cell != target.Cell || !AtNode(worker)) { SetGoal(worker,target.Cell); return; }
                if (!creatures.BeginCarry(target,worker.Actor)) { Block(worker,"Cannot safely collect this creature"); return; }
                SetStatus(worker, StaffTask.CarryingCreature, "Carrying " + target.Definition.displayName);
            }
            if (target.CarriedBy != worker.Actor) { ClearJob(worker); return; }
            Vector2Int destination = target.MoveRequestedDestination.Value;
            if (!SetGoal(worker,destination)) { Block(worker,"Drop-off cannot be reached: restore staff access"); return; }
            target.StaffStatus = "Being carried to the selected location";
            if (worker.Actor.Cell == destination && AtNode(worker) && worker.Actor.Route.Count == 0)
            {
                if (creatures.DropCarried(target,destination))
                { target.MoveRequestedDestination = null; target.StaffStatus = "Moved; resting under sedation"; ClearJob(worker); }
                else worker.Status = "Drop-off occupied: waiting for clear ground";
            }
        }

        private bool IsExposed(CreatureAgent target)
        {
            if (!groundRegions.TryGetValue(target.Cell,out var region))
            {
                region = new HashSet<Vector2Int>(groundNavigation.Reachable(target.Cell,grid.Width * grid.Depth));
                foreach (Vector2Int cell in region) groundRegions[cell] = region;
            }
            foreach (Vector2Int cell in region) if (Buildings.IsPathConnected(cell)) return true;
            return false;
        }
        private void JoinGroup(StaffAgent leader, CreatureAgent target)
        {
            int needed = target.IsSedated ? 1 : Mathf.Max(1,Mathf.CeilToInt(target.Definition.threat / Mathf.Max(1f,tuning.mercenaryStrength)));
            var group = new List<StaffAgent>();
            foreach (StaffAgent other in workers)
                if (other.Role == StaffRole.Mercenary && other.Actor.Alive && other.Home.IsOpen && other.Target == target) group.Add(other);
            if (!group.Contains(leader)) group.Add(leader);
            foreach (StaffAgent other in workers)
            {
                if (group.Count >= needed) break;
                if (other.Role == StaffRole.Mercenary && other.Actor.Alive && other.Home.IsOpen && other.Target == null && !group.Contains(other)) group.Add(other);
            }
            Vector2Int meeting = group[0].MeetingPoint ?? MeetingPoint(leader,target);
            foreach (StaffAgent member in group)
            {
                member.Target = target; member.MeetingPoint = meeting;
                SetStatus(member,StaffTask.Regrouping,$"Regrouping: {group.Count}/{needed} mercenaries available");
            }
        }
        private Vector2Int MeetingPoint(StaffAgent worker, CreatureAgent target)
        {
            Vector2Int best = worker.Home.Footprint.AccessCell; int score = int.MaxValue;
            foreach (Vector2Int cell in navigation.Reachable(worker.Actor.Cell))
            {
                if (!Buildings.IsPathConnected(cell) || Distance(grid.CellCenter(cell),target.transform.position) < tuning.shootingRange + 1f) continue;
                int distance = ConstructionRouteGraph.CellDistance(worker.Actor.Cell,cell);
                if (distance < score) { score = distance; best = cell; }
            }
            return best;
        }
        private void MercenaryWork(StaffAgent worker)
        {
            CreatureAgent target = worker.Target;
            if (!ValidCreature(target) || target.CarriedBy != null
                || (!target.RetirementRequested && (target.IsSedated || !IsExposed(target))))
            { ClearJob(worker); return; }
            int needed = target.IsSedated ? 1 : Mathf.Max(1,Mathf.CeilToInt(target.Definition.threat / Mathf.Max(1f,tuning.mercenaryStrength)));
            var group = new List<StaffAgent>();
            foreach (StaffAgent other in workers)
                if (other.Role == StaffRole.Mercenary && other.Actor.Alive && other.Home.IsOpen && other.Target == target) group.Add(other);
            if (group.Count < needed)
            { JoinGroup(worker,target); SetGoal(worker,worker.MeetingPoint.Value); return; }
            bool assembled = true;
            foreach (StaffAgent other in group)
                if (!other.MeetingPoint.HasValue || Distance(other.Actor.transform.position,grid.CellCenter(worker.MeetingPoint.Value)) > tuning.regroupDistance) assembled = false;
            bool hunting = worker.Task == StaffTask.Hunting || worker.Task == StaffTask.Retiring;
            if (!assembled && !hunting) { SetGoal(worker,worker.MeetingPoint.Value); return; }
            foreach (StaffAgent other in group)
                SetStatus(other,target.RetirementRequested ? StaffTask.Retiring : StaffTask.Hunting,
                    target.RetirementRequested ? "Responding to retirement order" : "Hunting an escaped creature");
            if (ApproachRanged(worker,target,tuning.shootingRange,true) && worker.ShotTimer <= 0f)
            {
                Shoot(worker,target,false); creatures.ApplyDamage(target,tuning.mercenaryDamage);
                if (!target.Alive) target.RetirementRequested = false;
            }
        }

        private Vector2Int? FindRangedPosition(StaffAgent worker, CreatureAgent target, float range, bool clearShot = false)
        {
            Vector2Int? best = null; float score = float.MaxValue;
            foreach (Vector2Int cell in navigation.Reachable(worker.Actor.Cell))
            {
                if (Distance(grid.CellCenter(cell),target.transform.position) > range || !navigation.HasLineOfSight(cell,target.Cell)) continue;
                if (clearShot && !navigation.HasClearShot(cell,target.Cell)) continue;
                float distance = ConstructionRouteGraph.CellDistance(worker.Actor.Cell,cell) + (grid.ContentAt(cell) == BuildCellContent.Path ? 0f : .5f);
                if (distance < score) { score = distance; best = cell; }
            }
            return best;
        }
        private bool ApproachRanged(StaffAgent worker, CreatureAgent target, float range, bool clearShot = false)
        {
            if (Distance(worker.Actor.transform.position,target.transform.position) <= range && creatures.CanSee(worker.Actor,target,range)
                && (!clearShot || navigation.HasClearShot(worker.Actor.Cell,target.Cell)))
            {
                if (!AtNode(worker)) { FinishSegment(worker); return false; }
                worker.Actor.Route.Clear(); worker.Actor.Goal = null; return true;
            }
            Vector2Int? goal = FindRangedPosition(worker,target,range,clearShot);
            if (!goal.HasValue || !SetGoal(worker,goal.Value))
            { Block(worker,clearShot ? "Shot blocked: add a staff gate or clear the approach" : "No clear approach or line of sight"); return false; }
            return false;
        }
        private bool ApproachHandsOn(StaffAgent worker, CreatureAgent target)
        {
            if (!SetGoal(worker,target.Cell)) { Block(worker,"Hands-on access blocked: add a staff gate"); return false; }
            return worker.Actor.Route.Count == 0 && AtNode(worker) && Distance(worker.Actor.transform.position,target.transform.position) <= 2.5f;
        }
        private Vector2Int? FenceApproach(StaffAgent worker, FenceEdge edge)
        {
            var a = navigation.FindPath(worker.Actor.Cell,edge.CellA);
            var b = navigation.FindPath(worker.Actor.Cell,edge.CellB);
            if (a.Count == 0 && b.Count == 0) return null;
            return a.Count > 0 && (b.Count == 0 || a.Count <= b.Count) ? edge.CellA : edge.CellB;
        }
        private bool ShootSedative(StaffAgent worker, CreatureAgent target)
        {
            if (worker.ShotTimer > 0f) return false;
            if (!creatures.Sedate(target,tuning.sedationSeconds)) { worker.Status = "Waiting for a safe sedation shot"; return false; }
            Shoot(worker,target,true); return true;
        }
        private void Shoot(StaffAgent worker, CreatureAgent target, bool sedative)
        {
            worker.ShotTimer = Mathf.Max(.1f,tuning.shotInterval);
            Vector3 direction = target.transform.position - worker.Actor.transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > .001f) worker.Actor.transform.rotation = Quaternion.LookRotation(direction);
            if (shotMaterial == null) shotMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            var effect = new GameObject(sedative ? "Sedation dart" : "Mercenary shot");
            var line = effect.AddComponent<LineRenderer>(); line.sharedMaterial = shotMaterial;
            line.positionCount = 2; line.startWidth = line.endWidth = .045f;
            line.startColor = line.endColor = sedative ? Color.cyan : Color.yellow;
            line.SetPosition(0,worker.Actor.transform.position + Vector3.up * .3f); line.SetPosition(1,target.transform.position);
            shots.Add(new ShotEffect { Object = effect, Remaining = .15f });
        }
        private void TickShots(float dt)
        { for (int i=shots.Count-1;i>=0;i--) if ((shots[i].Remaining -= dt) <= 0f) { UnityEngine.Object.Destroy(shots[i].Object); shots.RemoveAt(i); } }
        private bool AtNode(StaffAgent worker) => Distance(worker.Actor.transform.position,Position(worker.Actor,worker.Actor.Cell)) < .03f;
        private static void FinishSegment(StaffAgent worker)
        { if (worker.Actor.Route.Count > 1) worker.Actor.Route.RemoveRange(1,worker.Actor.Route.Count-1); worker.Actor.Goal = null; }
        private void Block(StaffAgent worker,string reason)
        {
            worker.Status = reason; worker.Actor.Route.Clear(); worker.Actor.Goal = null;
            if (worker.Target != null) worker.Target.StaffStatus = reason;
        }
        private void ReturnHome(StaffAgent worker,string reason)
        { SetGoal(worker,worker.Home.Footprint.AccessCell); SetStatus(worker,StaffTask.Returning,reason); }
        private void Patrol(StaffAgent worker)
        {
            if (worker.Actor.Route.Count > 0) { SetStatus(worker,StaffTask.Patrolling,"Patrolling visitor paths"); return; }
            var paths = navigation.Reachable(worker.Actor.Cell,true);
            if (paths.Count == 0) { SetGoal(worker,worker.Home.Footprint.AccessCell); SetStatus(worker,StaffTask.Returning,"Returning to visitor paths"); return; }
            SetGoal(worker,paths[UnityEngine.Random.Range(0,paths.Count)],true);
            SetStatus(worker,StaffTask.Patrolling,"Patrolling visitor paths");
        }
        private CreatureAgent WorkerDanger(StaffAgent worker)
        {
            CreatureAgent best = null; float distance = float.MaxValue;
            foreach (CreatureAgent target in creatures.Agents)
            {
                if (!ValidCreature(target) || target.IsSedated || target.Definition.threat <= worker.Actor.Definition.threat
                    || !creatures.CanSee(worker.Actor,target,tuning.threatFleeRange)) continue;
                // Staff gates provide access but an intact fence still separates a patient from its observer.
                var region = groundNavigation.Reachable(worker.Actor.Cell,grid.Width * grid.Depth);
                if (!region.Contains(target.Cell)) continue;
                float current = Distance(worker.Actor.transform.position,target.transform.position);
                if (current < distance) { distance = current; best = target; }
            }
            return best;
        }
        private void Flee(StaffAgent worker, CreatureAgent danger)
        {
            Vector2Int best = worker.Actor.Cell; float distance = -1f;
            foreach (Vector2Int cell in navigation.Reachable(worker.Actor.Cell))
            {
                float score = Distance(grid.CellCenter(cell),danger.transform.position)
                    - ConstructionRouteGraph.CellDistance(worker.Actor.Cell,cell) * .05f;
                if (score > distance) { distance = score; best = cell; }
            }
            SetGoal(worker,best); SetStatus(worker,StaffTask.Fleeing,"Retreating from a dangerous creature");
        }
    }
}

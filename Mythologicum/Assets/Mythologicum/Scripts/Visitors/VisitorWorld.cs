using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Guest visits, path travel, viewing and services. Uses the creature world's shared prey actors.</summary>
    public sealed class VisitorWorld : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly ParkBuildingNetwork buildings;
        private readonly CreatureWorld creatures;
        private readonly ParkEconomy economy;
        private readonly VisitorNavigation navigation;
        private readonly CreatureNavigation sight;
        private readonly CreatureDefinition actorProfile;
        private readonly List<VisitorAgent> visitors = new List<VisitorAgent>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> groundRegions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        private readonly System.Random appearanceRandom = new System.Random();
        private float arrivalTimer, time;
        private int gridRevision = -1, fenceRevision = -1;
        public VisitorDefinition Definition { get; }
        public IReadOnlyList<VisitorAgent> Visitors => visitors;
        public int Population => visitors.Count;
        public int DepartedCount { get; private set; }
        public int LostCount { get; private set; }
        internal void RestoreCounts(int departed, int lost) { DepartedCount = Math.Max(0, departed); LostCount = Math.Max(0, lost); }
        public int StrandedCount { get { int count = 0; foreach (var guest in visitors) if (guest.Activity == VisitorActivity.Stranded) count++; return count; } }
        public float AverageSatisfaction
        {
            get { float total = 0f; foreach (var guest in visitors) total += guest.Mood.Satisfaction; return visitors.Count > 0 ? total / visitors.Count : 0f; }
        }

        public VisitorWorld(SquareBuildGrid grid, FenceLayout fences, ParkBuildingNetwork buildings,
            CreatureWorld creatures, VisitorDefinition definition, ParkEconomy economy)
        {
            this.grid = grid; this.fences = fences; this.buildings = buildings;
            this.creatures = creatures; this.economy = economy;
            Definition = definition;
            navigation = new VisitorNavigation(grid, fences, creatures.IsClear);
            sight = new CreatureNavigation(grid, fences, creatures.IsClear);
            actorProfile = ScriptableObject.CreateInstance<CreatureDefinition>();
            actorProfile.name = "Visitor actor"; actorProfile.displayName = "Visitor";
            actorProfile.diet = CreatureDiet.None; actorProfile.tags = CreatureTags.Visitor;
            actorProfile.maximumHealth = 60f; actorProfile.damage = 0f; actorProfile.threat = 10f; actorProfile.origin = "Human";
            actorProfile.fearedTags = CreatureTags.Predator; actorProfile.manuallyPlaceable = false;
            actorProfile.initialHunger = definition.initialHunger; actorProfile.initialEnergy = definition.initialEnergy;
            actorProfile.hungerPerSecond = definition.hungerPerSecond; actorProfile.energyDrainPerSecond = definition.energyDrainPerSecond;
            actorProfile.fearGainPerSecond = definition.fearGainPerSecond; actorProfile.fearRecoveryPerSecond = definition.fearRecoveryPerSecond;
            actorProfile.walkSpeed = definition.walkSpeed; actorProfile.runSpeed = definition.runSpeed;
            actorProfile.minimumSpace = 0f; actorProfile.color = new Color(0.35f, 0.65f, 0.95f);
            actorProfile.bodyScale = new Vector3(0.5f, 0.85f, 0.5f);
            arrivalTimer = definition.spawnInterval;
        }

        public VisitorAgent TrySpawn()
        {
            buildings.Refresh();
            if (Population >= Definition.maximumVisitors || buildings.Entrance == null || !buildings.Entrance.IsOpen) return null;
            GameObject model = null;
            if (Definition.people != null && Definition.people.civilians != null && Definition.people.civilians.Length > 0)
                model = Definition.people.civilians[appearanceRandom.Next(Definition.people.civilians.Length)];
            CreatureAgent actor = creatures.SpawnExternal(actorProfile, buildings.Entrance.Footprint.AccessCell, model);
            if (actor == null) return null;
            var visitor = actor.gameObject.AddComponent<VisitorAgent>();
            visitor.Actor = actor; visitor.Activity = VisitorActivity.Arriving;
            visitors.Add(visitor);
            float ticket = buildings.Entrance.Price;
            buildings.Entrance.RecordRevenue(ticket); economy.Earn(ticket);
            return visitor;
        }

        internal VisitorAgent RestoreGuest(Vector2Int cell)
        {
            if (!grid.Contains(cell)) return null;
            GameObject model = null;
            if (Definition.people != null && Definition.people.civilians != null && Definition.people.civilians.Length > 0)
                model = Definition.people.civilians[appearanceRandom.Next(Definition.people.civilians.Length)];
            var actor = creatures.SpawnExternal(actorProfile, cell, model);
            if (actor == null) return null;
            var guest = actor.gameObject.AddComponent<VisitorAgent>();
            guest.Actor = actor; guest.Activity = VisitorActivity.Arriving; visitors.Add(guest); return guest;
        }

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            float dt = deltaTime; time += dt;
            buildings.Refresh();
            RefreshNavigation();
            if (buildings.Entrance != null && buildings.Entrance.IsOpen && Population < Definition.maximumVisitors)
            {
                arrivalTimer -= dt;
                if (arrivalTimer <= 0f)
                {
                    TrySpawn(); arrivalTimer = Mathf.Max(0.1f, Definition.spawnInterval);
                }
            }
            for (int i = visitors.Count - 1; i >= 0; i--)
            {
                VisitorAgent guest = visitors[i];
                CreatureAgent actor = guest == null ? null : guest.Actor;
                if (actor == null || !actor.Alive)
                { visitors.RemoveAt(i); LostCount++; continue; }
                if (actor.CapturedBy != null && actor.CapturedBy.Alive)
                {
                    guest.LeavingPark = true;
                    ReleaseService(guest); actor.Route.Clear(); actor.Goal = null;
                    SetActivity(guest, VisitorActivity.Captured, "Caught by a predator");
                    guest.Mood.Tick(dt, actor.Vitals.Hunger, actor.Energy.Energy, 100f, false, false,
                        Definition.minimumStaySeconds, Definition.maximumStaySeconds);
                    creatures.RefreshExternal(actor); continue;
                }
                guest.StateTimer -= dt; guest.DecisionTimer -= dt;
                CreatureAgent threat = FindThreat(guest);
                actor.Vitals.Tick(dt, Definition.hungerPerSecond, Definition.fearGainPerSecond,
                    Definition.fearRecoveryPerSecond, threat != null);
                bool resting = guest.Activity == VisitorActivity.Resting && ServiceAvailable(guest);
                float recovery = resting ? guest.Service.Footprint.Definition.restRecoveryPerSecond : 0f;
                actor.Energy.Tick(dt, Definition.energyDrainPerSecond,
                    guest.Activity == VisitorActivity.Fleeing ? 3f : 1f, resting, recovery);
                bool watching = guest.Activity == VisitorActivity.Watching && guest.Watching != null
                    && guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange);
                guest.Mood.Tick(dt, actor.Vitals.Hunger, actor.Energy.Energy, actor.Vitals.Fear,
                    watching, resting || (guest.Activity == VisitorActivity.Eating && ServiceAvailable(guest)),
                    Definition.minimumStaySeconds, Definition.maximumStaySeconds);
                if (guest.Mood.ShouldLeave) guest.LeavingPark = true;
                if (threat != null)
                {
                    guest.LastDanger = threat.transform.position;
                    if (!guest.Panicked) guest.Mood.Punish(25f);
                    guest.Panicked = true; guest.LeavingPark = true;
                }
                bool danger = threat != null || (guest.Panicked && actor.Vitals.Fear >= 20f
                    && Distance(actor.transform.position, guest.LastDanger) < Definition.fleeSafeDistance);
                if (danger)
                {
                    ReleaseService(guest);
                    if (guest.Activity != VisitorActivity.Fleeing || guest.DecisionTimer <= 0f || actor.Route.Count == 0)
                    {
                        guest.DecisionTimer = Mathf.Max(.05f, Definition.decisionInterval);
                        Flee(guest);
                    }
                }
                else if (guest.DecisionTimer <= 0f)
                {
                    guest.DecisionTimer = Mathf.Max(.05f, Definition.decisionInterval);
                    Think(guest);
                }
                Move(guest, dt);
                CompleteActivity(guest);
                if (guest.Activity == VisitorActivity.Leaving && actor.Route.Count == 0
                    && buildings.Entrance != null && buildings.Entrance.IsOpen
                    && actor.Cell == buildings.Entrance.Footprint.AccessCell)
                {
                    ReleaseService(guest); visitors.RemoveAt(i); DepartedCount++;
                    creatures.RemoveExternal(actor); continue;
                }
                actor.ExternalStatus = guest.Status + $" | Satisfaction {guest.Mood.Satisfaction:0}";
                creatures.RefreshExternal(actor);
            }
        }

        private void RefreshNavigation()
        {
            if (gridRevision == grid.Revision && fenceRevision == fences.Revision) return;
            gridRevision = grid.Revision; fenceRevision = fences.Revision; groundRegions.Clear();
            foreach (VisitorAgent guest in visitors)
            {
                guest.DecisionTimer = 0f;
                CreatureAgent actor = guest.Actor;
                if (actor == null) continue;
                if (actor.Route.Count > 0 && !navigation.CanCross(actor.Cell, actor.Route[0], guest.GroundRoute))
                {
                    actor.Route.Clear(); actor.Goal = null;
                    actor.transform.position = Position(actor.Cell);
                }
            }
        }

        private HashSet<Vector2Int> GroundRegion(Vector2Int cell)
        {
            if (groundRegions.TryGetValue(cell, out var result)) return result;
            result = new HashSet<Vector2Int>(navigation.Reachable(cell, true));
            foreach (Vector2Int member in result) groundRegions[member] = result;
            return result;
        }

        private CreatureAgent FindThreat(VisitorAgent guest)
        {
            CreatureAgent best = null; float nearest = float.MaxValue;
            foreach (CreatureAgent other in creatures.Agents)
            {
                if (!other.Alive || other.IsSedated || (other.Definition.tags & CreatureTags.Staff) != 0 || other.Definition.threat <= guest.Actor.Definition.threat
                    || !creatures.CanSee(guest.Actor, other, Definition.fearRange)) continue;
                if (!GroundRegion(guest.Actor.Cell).Contains(other.Cell)) continue;
                float distance = Distance(guest.Actor.transform.position, other.transform.position);
                if (distance < nearest) { nearest = distance; best = other; }
            }
            return best;
        }

        private void Think(VisitorAgent guest)
        {
            CreatureAgent actor = guest.Actor;
            CompleteActivity(guest);
            if (guest.LeavingPark && (guest.Activity == VisitorActivity.Eating || guest.Activity == VisitorActivity.Resting))
            { ReleaseService(guest); SetActivity(guest, VisitorActivity.Arriving, "Ready to leave"); }
            if (guest.Activity == VisitorActivity.Eating || guest.Activity == VisitorActivity.Resting)
            {
                if (!ServiceAvailable(guest))
                { ReleaseService(guest); guest.StateTimer = 0f; }
                else if ((guest.Activity == VisitorActivity.Eating && guest.StateTimer > 0f)
                    || (guest.Activity == VisitorActivity.Resting && actor.Energy.Energy < Definition.comfortableEnergy)) return;
                else CompleteActivity(guest);
            }
            // Fleeing may put a guest off the public paths. Rejoin the connected network before normal travel.
            if (guest.GroundRoute || grid.ContentAt(actor.Cell) != BuildCellContent.Path)
            {
                ReleaseService(guest);
                if (ReturnToPath(guest)) return;
                SetActivity(guest, VisitorActivity.Stranded, "Cannot reach a connected path"); return;
            }
            if (guest.LeavingPark)
            {
                ReleaseService(guest);
                if (buildings.Entrance != null && buildings.Entrance.IsOpen
                    && SetRoute(guest, buildings.Entrance.Footprint.AccessCell))
                    SetActivity(guest, VisitorActivity.Leaving, "Returning to the park entrance");
                else { FinishSegment(actor); SetActivity(guest, VisitorActivity.Stranded, "Exit disconnected: reconnect the path"); }
                return;
            }
            if ((guest.Activity == VisitorActivity.SeekingFood || guest.Activity == VisitorActivity.SeekingRest)
                && ServiceAvailable(guest)) return;
            if (guest.Service != null) ReleaseService(guest);
            if (actor.Vitals.Hunger >= Definition.hungerThreshold && TryService(guest, BuildingKind.Kiosk)) return;
            if (actor.Energy.Energy <= Definition.restThreshold && TryService(guest, BuildingKind.RestArea)) return;
            if (guest.Activity == VisitorActivity.Watching && guest.StateTimer > 0f && guest.Watching != null
                && guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange)) return;
            if (guest.Activity == VisitorActivity.Walking && actor.Route.Count > 0 && guest.Watching != null
                && guest.Watching.Alive) return;
            if (TryViewingSpot(guest)) return;
            if (actor.Route.Count > 0) return;
            List<Vector2Int> paths = navigation.Reachable(actor.Cell);
            if (paths.Count > 1 && SetRoute(guest, paths[UnityEngine.Random.Range(0, paths.Count)]))
                SetActivity(guest, VisitorActivity.Walking, "Exploring the paths");
            else SetActivity(guest, VisitorActivity.Arriving, "Looking for creatures to watch");
        }

        private bool TryService(VisitorAgent guest, BuildingKind kind)
        {
            ParkBuildingRecord best = null; int shortest = int.MaxValue;
            Vector2Int from = guest.Actor.Route.Count > 0 ? guest.Actor.Route[0] : guest.Actor.Cell;
            foreach (ParkBuildingRecord building in buildings.Records)
            {
                if (!building.IsOpen || building.Footprint.Definition.kind != kind) continue;
                int reservations = 0;
                foreach (VisitorAgent other in visitors)
                    if (other != guest && other.Actor != null && other.Actor.Alive && other.Service == building) reservations++;
                if (reservations >= Mathf.Max(1, building.Footprint.Definition.serviceCapacity)) continue;
                List<Vector2Int> route = navigation.FindPath(from, building.Footprint.AccessCell);
                if (route.Count == 0 || route.Count >= shortest) continue;
                shortest = route.Count; best = building;
            }
            if (best == null || !SetRoute(guest, best.Footprint.AccessCell)) return false;
            guest.Service = best; guest.Watching = null;
            SetActivity(guest, kind == BuildingKind.Kiosk ? VisitorActivity.SeekingFood : VisitorActivity.SeekingRest,
                kind == BuildingKind.Kiosk ? "Walking to a kiosk" : "Walking to a rest area");
            return true;
        }

        private bool ServiceAvailable(VisitorAgent guest)
        {
            if (guest.Service == null || !guest.Service.IsOpen) return false;
            foreach (ParkBuildingRecord building in buildings.Records) if (building == guest.Service) return true;
            return false;
        }

        private static void ReleaseService(VisitorAgent guest) { guest.Service = null; }

        private bool TryViewingSpot(VisitorAgent guest)
        {
            CreatureAgent animal = null; Vector2Int point = guest.Actor.Cell;
            float best = float.MinValue;
            List<Vector2Int> paths = navigation.Reachable(guest.Actor.Cell);
            foreach (CreatureAgent candidate in creatures.Agents)
            {
                if (!candidate.Alive || candidate.ExternallyControlled
                    || candidate.Definition.requiresGravestoneForSleep && candidate.Activity == CreatureActivity.Sleeping) continue;
                if (guest.ViewedAt.TryGetValue(candidate, out float last) && time - last < 30f) continue;
                foreach (Vector2Int path in paths)
                {
                    float distance = Distance(grid.CellCenter(path), candidate.transform.position);
                    if (distance > Definition.viewRange || !sight.HasLineOfSight(path, candidate.Cell)) continue;
                    float score = 100f - ConstructionRouteGraph.CellDistance(guest.Actor.Cell, path)
                        - Mathf.Abs(distance - 5f) * 2f;
                    if (score > best) { best = score; animal = candidate; point = path; }
                }
            }
            if (animal == null || !SetRoute(guest, point)) return false;
            guest.Watching = animal;
            SetActivity(guest, VisitorActivity.Walking, "Walking to a viewing spot");
            return true;
        }

        private void Flee(VisitorAgent guest)
        {
            CreatureAgent actor = guest.Actor;
            Vector2Int best = actor.Cell; float bestScore = float.MinValue;
            foreach (Vector2Int candidate in GroundRegion(actor.Cell))
            {
                float distance = Distance(grid.CellCenter(candidate), guest.LastDanger);
                int steps = ConstructionRouteGraph.CellDistance(actor.Cell, candidate);
                float score = distance >= Definition.fleeSafeDistance ? 1000f - steps : distance;
                if (grid.ContentAt(candidate) == BuildCellContent.Path) score += .1f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            SetRoute(guest, best, true);
            SetActivity(guest, VisitorActivity.Fleeing, "Fleeing a predator");
        }

        private bool ReturnToPath(VisitorAgent guest)
        {
            CreatureAgent actor = guest.Actor;
            if (actor.Route.Count > 0 && guest.Activity == VisitorActivity.ReturningToPath) return true;
            Vector2Int? best = null; int distance = int.MaxValue;
            foreach (Vector2Int cell in GroundRegion(actor.Cell))
            {
                if (!buildings.IsPathConnected(cell)) continue;
                int candidate = ConstructionRouteGraph.CellDistance(actor.Cell, cell);
                if (candidate < distance) { distance = candidate; best = cell; }
            }
            if (!best.HasValue || !SetRoute(guest, best.Value, true)) return false;
            SetActivity(guest, VisitorActivity.ReturningToPath, "Rejoining a path after fleeing");
            if (actor.Route.Count == 0) guest.GroundRoute = false;
            return true;
        }

        private bool SetRoute(VisitorAgent guest, Vector2Int target, bool allowGround = false)
        {
            CreatureAgent actor = guest.Actor;
            bool moving = actor.Route.Count > 0;
            Vector2Int from = moving ? actor.Route[0] : actor.Cell;
            List<Vector2Int> route = navigation.FindPath(from, target, allowGround);
            if (route.Count == 0) return false;
            actor.Route.Clear(); actor.Goal = target; guest.GroundRoute = allowGround;
            for (int i = moving ? 0 : 1; i < route.Count; i++) actor.Route.Add(route[i]);
            return true;
        }

        private void Move(VisitorAgent guest, float dt)
        {
            CreatureAgent actor = guest.Actor;
            if (actor.Route.Count == 0) return;
            Vector2Int next = actor.Route[0];
            if (!navigation.CanCross(actor.Cell, next, guest.GroundRoute))
            {
                actor.Route.Clear(); actor.Goal = null; guest.DecisionTimer = 0f;
                actor.transform.position = Position(actor.Cell); return;
            }
            Vector3 goal = Position(next), direction = goal - actor.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
            float speed = guest.Activity == VisitorActivity.Fleeing ? Definition.runSpeed : Definition.walkSpeed;
            actor.transform.position = Vector3.MoveTowards(actor.transform.position, goal, Mathf.Max(.1f, speed) * dt);
            if(grid.Terrain!=null)actor.transform.position=grid.ProjectToGround(actor.transform.position,actor.Definition.bodyScale.y);
            if ((actor.transform.position - goal).sqrMagnitude < .0001f)
            { actor.Cell = next; actor.Route.RemoveAt(0); }
        }

        private void CompleteActivity(VisitorAgent guest)
        {
            CreatureAgent actor = guest.Actor;
            if (actor.Route.Count > 0) return;
            if (guest.Activity == VisitorActivity.SeekingFood || guest.Activity == VisitorActivity.SeekingRest)
            {
                if (!ServiceAvailable(guest) || actor.Cell != guest.Service.Footprint.AccessCell)
                { ReleaseService(guest); guest.DecisionTimer = 0f; return; }
                bool food = guest.Activity == VisitorActivity.SeekingFood;
                guest.QuotedPrice = guest.Service.Price;
                guest.StateTimer = Mathf.Max(.1f, guest.Service.Footprint.Definition.serviceSeconds);
                SetActivity(guest, food ? VisitorActivity.Eating : VisitorActivity.Resting,
                    food ? "Eating at a kiosk" : "Resting at a bench area");
            }
            else if (guest.Activity == VisitorActivity.Eating && guest.StateTimer <= 0f && ServiceAvailable(guest))
            {
                BuildingDefinition service = guest.Service.Footprint.Definition;
                actor.Vitals.Eat(service.foodRelief); ChargeService(guest); guest.Mood.Reward(8f);
                ReleaseService(guest); SetActivity(guest, VisitorActivity.Arriving, "Finished eating"); guest.DecisionTimer = 0f;
            }
            else if (guest.Activity == VisitorActivity.Resting && actor.Energy.Energy >= Definition.comfortableEnergy && ServiceAvailable(guest))
            {
                ChargeService(guest); guest.Mood.Reward(6f); ReleaseService(guest);
                SetActivity(guest, VisitorActivity.Arriving, "Feeling rested"); guest.DecisionTimer = 0f;
            }
            else if (guest.Activity == VisitorActivity.Walking && guest.Watching != null)
            {
                if (guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange))
                {
                    guest.StateTimer = Mathf.Max(.1f, Definition.watchingSeconds);
                    SetActivity(guest, VisitorActivity.Watching, "Watching " + guest.Watching.Definition.displayName);
                }
                else { guest.Watching = null; guest.DecisionTimer = 0f; }
            }
            else if (guest.Activity == VisitorActivity.Watching && guest.StateTimer <= 0f)
            {
                if (guest.Watching != null)
                {
                    guest.ViewedAt[guest.Watching] = time;
                    if (guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange)) guest.Mood.Reward(4f);
                }
                guest.Watching = null; SetActivity(guest, VisitorActivity.Arriving, "Looking for another exhibit");
                guest.DecisionTimer = 0f;
            }
            else if (guest.Activity == VisitorActivity.ReturningToPath)
            { guest.GroundRoute = false; guest.DecisionTimer = 0f; }
        }

        private static void FinishSegment(CreatureAgent actor)
        {
            if (actor.Route.Count > 1) actor.Route.RemoveRange(1, actor.Route.Count - 1);
            actor.Goal = null;
        }

        private void ChargeService(VisitorAgent guest)
        {
            guest.Service.RecordRevenue(guest.QuotedPrice);
            economy.Earn(guest.QuotedPrice);
            guest.QuotedPrice = 0f;
        }

        private static void SetActivity(VisitorAgent guest, VisitorActivity activity, string status)
        {
            guest.Activity = activity; guest.Status = status;
            guest.Actor.Activity = activity == VisitorActivity.Fleeing ? CreatureActivity.Fleeing
                : activity == VisitorActivity.Eating ? CreatureActivity.Feeding
                : activity == VisitorActivity.Resting || activity == VisitorActivity.Watching || activity == VisitorActivity.Stranded
                    ? CreatureActivity.Resting : CreatureActivity.Wandering;
        }

        private Vector3 Position(Vector2Int cell) => grid.ActorPosition(cell,actorProfile.bodyScale.y);
        private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        public void Dispose()
        {
            foreach (VisitorAgent guest in visitors) if (guest != null && guest.Actor != null) creatures.RemoveExternal(guest.Actor);
            visitors.Clear(); UnityEngine.Object.Destroy(actorProfile);
        }
    }
}

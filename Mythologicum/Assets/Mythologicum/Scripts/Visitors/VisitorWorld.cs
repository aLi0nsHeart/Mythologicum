using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Guest visits, path travel, viewing and services. Uses the creature world's shared prey actors.</summary>
    public sealed partial class VisitorWorld : IDisposable
    {
        private static readonly ProfilerMarker ArrivalProfile = new ProfilerMarker("Mythologicum.VisitorArrival");
        private static readonly ProfilerMarker ModelPreparationProfile = new ProfilerMarker("Mythologicum.VisitorModelPreparation");
        private static readonly ProfilerMarker PathPreparationProfile = new ProfilerMarker("Mythologicum.ArrivalPathPreparation");
        private static readonly ProfilerMarker DecisionProfile = new ProfilerMarker("Mythologicum.VisitorDecision");
        private static readonly ProfilerMarker ReputationProfile = new ProfilerMarker("Mythologicum.ExhibitRecount");
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly ParkBuildingNetwork buildings;
        private readonly CreatureWorld creatures;
        private readonly ParkEconomy economy;
        private readonly VisitorNavigation navigation;
        private readonly VisitorArrivalPaths arrivalPaths;
        private readonly CreatureNavigation sight;
        private readonly VisitorViewingCache viewing;
        private readonly Dictionary<Vector2Int, int> viewingOrder = new Dictionary<Vector2Int, int>();
        private readonly CreatureDefinition actorProfile;
        private readonly Dictionary<VisitorType, CreatureDefinition> actorProfiles = new Dictionary<VisitorType, CreatureDefinition>();
        private readonly Dictionary<CreatureDefinition, string> speciesIds = new Dictionary<CreatureDefinition, string>();
        private readonly List<VisitorAgent> visitors = new List<VisitorAgent>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> groundRegions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        private readonly System.Random appearanceRandom = new System.Random();
        private float arrivalTimer, time, reputationTimer, admissionCredit;
        private int reputationGridRevision = -1, reputationFenceRevision = -1;
        private int gridRevision = -1, fenceRevision = -1;
        private bool modelsPrepared;
        public VisitorDefinition Definition { get; }
        public ParkReputation Reputation { get; }
        public int RejectedAdmissions { get; private set; }
        public int ExpensiveServiceRefusals { get; private set; }
        internal void RestoreFeedbackCounts(int rejected, int serviceRefusals)
        { RejectedAdmissions = Math.Max(0, rejected); ExpensiveServiceRefusals = Math.Max(0, serviceRefusals); }
        public float AdmissionAcceptance => buildings.Entrance != null ? Reputation.PriceAcceptance(buildings.Entrance.Price) : 0f;
        public float ExpectedArrivalsPerMinute => buildings.Entrance != null && buildings.Entrance.IsOpen
            ? 60f / Mathf.Max(.1f, Definition.spawnInterval) * Reputation.DemandMultiplier * AdmissionAcceptance : 0f;
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
            CreatureWorld creatures, VisitorDefinition definition, ParkEconomy economy, ParkReputation reputation = null)
        {
            this.grid = grid; this.fences = fences; this.buildings = buildings;
            this.creatures = creatures; this.economy = economy;
            Definition = definition;
            Reputation = reputation ?? new ParkReputation();
            navigation = new VisitorNavigation(grid, fences, creatures.IsClear);
            arrivalPaths = new VisitorArrivalPaths(grid, fences, navigation);
            sight = new CreatureNavigation(grid, fences, creatures.IsClear);
            viewing = new VisitorViewingCache(grid, sight.HasLineOfSight);
            actorProfile = Profile(VisitorType.Legacy);
            arrivalTimer = definition.spawnInterval;
        }

        /// <summary>Warm native model/avatar resources behind the loading screen, one model per rendered frame.</summary>
        public IEnumerator PrepareModels(Camera warmupCamera = null)
        {
            if (modelsPrepared || !Definition.prewarmVisitorModels) yield break;
            yield return null; // Present the loader before the first imported-model initialization.
            foreach (var type in new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children }) Profile(type);
            var models = new HashSet<GameObject>();
            var people = Definition.people;
            if (people != null)
                foreach (var pool in new[] { people.civilians, people.adultVisitors, people.childVisitors })
                    if (pool != null) foreach (var model in pool) if (model != null) models.Add(model);
            foreach (var model in models)
            {
                GameObject preview = null;
                try
                {
                    using (ModelPreparationProfile.Auto())
                    {
                        preview = UnityEngine.Object.Instantiate(model);
                        preview.name = "Visitor model preparation";
                        foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                        foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                            // A supplied gameplay camera renders the model behind the opaque loading screen.
                            renderer.forceRenderingOff = warmupCamera == null;
                        }
                        if (warmupCamera != null)
                            preview.transform.position = warmupCamera.transform.position + warmupCamera.transform.forward
                                * Mathf.Max(warmupCamera.nearClipPlane * 2f, 5f);
                        foreach (var animator in preview.GetComponentsInChildren<Animator>(true))
                        {
                            animator.applyRootMotion = false;
                            if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) continue;
                            animator.Rebind(); animator.Update(0f);
                        }
                    }
                    yield return null;
                }
                finally
                {
                    if (preview != null)
                    {
                        preview.SetActive(false);
                        if (Application.isPlaying) UnityEngine.Object.Destroy(preview);
                        else UnityEngine.Object.DestroyImmediate(preview);
                    }
                }
            }
            modelsPrepared = true;
        }

        private CreatureDefinition Profile(VisitorType type)
        {
            if (actorProfiles.TryGetValue(type, out var profile)) return profile;
            var settings = Definition.SettingsFor(type);
            profile = ScriptableObject.CreateInstance<CreatureDefinition>();
            profile.name = type == VisitorType.Legacy ? "Visitor actor" : "Visitor actor " + type;
            profile.displayName = "visitor.name"; profile.diet = CreatureDiet.None; profile.tags = CreatureTags.Visitor;
            profile.maximumHealth = Definition.maximumHealth; profile.damage = 0; profile.threat = Definition.threat; profile.origin = "creature.origin.human";
            profile.fearedTags = CreatureTags.Predator; profile.manuallyPlaceable = false;
            profile.initialHunger = Definition.initialHunger; profile.initialEnergy = Definition.initialEnergy;
            profile.hungerPerSecond = Definition.hungerPerSecond * VisitorInterests.Factor(settings.hungerMultiplier);
            profile.energyDrainPerSecond = Definition.energyDrainPerSecond * VisitorInterests.Factor(settings.energyMultiplier);
            profile.fearGainPerSecond = Definition.fearGainPerSecond * VisitorInterests.Factor(settings.fearGainMultiplier);
            profile.fearRecoveryPerSecond = Definition.fearRecoveryPerSecond;
            profile.walkSpeed = Definition.walkSpeed * VisitorInterests.Factor(settings.walkSpeedMultiplier);
            profile.runSpeed = Definition.runSpeed * VisitorInterests.Factor(settings.runSpeedMultiplier);
            profile.minimumSpace = 0; profile.color = new Color(.35f, .65f, .95f); profile.bodyScale = settings.bodyScale;
            actorProfiles.Add(type, profile);
            return profile;
        }

        private VisitorAgent CreateGuest(Vector2Int cell, VisitorType type)
        {
            if (!grid.Contains(cell)) return null;
            if (!VisitorInterests.IsRequestedType(type)) type = VisitorType.Legacy;
            GameObject model = null;
            if (Definition.people != null)
            {
                var models = type == VisitorType.Legacy ? Definition.people.civilians : Definition.people.VisitorModels(type == VisitorType.Children);
                if (models != null && models.Length > 0) model = models[appearanceRandom.Next(models.Length)];
            }
            var actor = creatures.SpawnExternal(Profile(type), cell, model);
            if (actor == null) return null;
            var guest = actor.gameObject.AddComponent<VisitorAgent>();
            guest.Type = type; guest.TypeSettings = Definition.SettingsFor(type);
            guest.Mood = new VisitorMood(rules: Definition.mood, patienceMultiplier: guest.TypeSettings.patienceMultiplier);
            guest.Actor = actor; guest.Activity = VisitorActivity.Arriving; visitors.Add(guest);
            return guest;
        }

        public VisitorAgent TrySpawn()
        {
            using var sample = ArrivalProfile.Auto();
            buildings.Refresh();
            if (Population >= Definition.maximumVisitors || buildings.Entrance == null || !buildings.Entrance.IsOpen) return null;
            RefreshReputationIfNeeded();
            float acceptance = AdmissionAcceptance;
            admissionCredit += acceptance;
            if (acceptance <= 0f || admissionCredit < 1f)
            { if (RejectedAdmissions < int.MaxValue) RejectedAdmissions++; return null; }
            admissionCredit = Mathf.Max(0f, admissionCredit - 1f);
            var visitor = CreateGuest(buildings.Entrance.Footprint.AccessCell, VisitorInterests.Pick(Definition.typeSettings, appearanceRandom.NextDouble()));
            if (visitor == null) return null;
            float ticket = buildings.Entrance.Price;
            buildings.Entrance.RecordRevenue(ticket); economy.Earn(ticket, ParkTransactionCategory.Admission);
            return visitor;
        }

        internal VisitorAgent RestoreGuest(Vector2Int cell) => CreateGuest(cell, VisitorType.Legacy);
        internal VisitorAgent RestoreGuestWithType(Vector2Int cell, VisitorType type) => CreateGuest(cell, type);

        public void Tick(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            float dt = deltaTime; time += dt;
            buildings.Refresh();
            TickRideCycles(dt);
            RefreshNavigation();
            viewing.BeginTick(Definition.viewRange);
            Reputation.Tick(dt);
            reputationTimer -= dt;
            RefreshReputationIfNeeded();
            if (buildings.Entrance != null && buildings.Entrance.IsOpen && (Population > 0 || Definition.maximumVisitors > 0))
                using (PathPreparationProfile.Auto())
                    arrivalPaths.Prepare(buildings.Entrance.Footprint.AccessCell, Definition.arrivalPathCellsPerFrame);
            if (buildings.Entrance != null && buildings.Entrance.IsOpen && Population < Definition.maximumVisitors)
            {
                arrivalTimer -= dt * Reputation.DemandMultiplier;
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
                { visitors.RemoveAt(i); LostCount++; Reputation.ReportDeath(); continue; }
                var settings = guest.TypeSettings;
                if (actor.CapturedBy != null && actor.CapturedBy.Alive)
                {
                    CancelAttraction(guest, false);
                    guest.LeavingPark = true;
                    ReleaseService(guest); actor.Route.Clear(); actor.Goal = null;
                    SetActivity(guest, VisitorActivity.Captured, "visitor.status.caught_by_a_predator");
                    guest.Mood.Tick(dt, actor.Vitals.Hunger, actor.Energy.Energy, 100f, false, false,
                        Definition.minimumStaySeconds, Definition.maximumStaySeconds, fearLossMultiplier: settings.fearLossMultiplier);
                    creatures.RefreshExternal(actor); continue;
                }
                guest.StateTimer -= dt; guest.DecisionTimer -= dt;
                CreatureAgent threat = FindThreat(guest);
                actor.Vitals.Tick(dt, Definition.hungerPerSecond * VisitorInterests.Factor(settings.hungerMultiplier),
                    Definition.fearGainPerSecond * VisitorInterests.Factor(settings.fearGainMultiplier),
                    Definition.fearRecoveryPerSecond, threat != null);
                bool resting = guest.Activity == VisitorActivity.Resting && ServiceAvailable(guest);
                float recovery = resting ? guest.Service.Footprint.Definition.restRecoveryPerSecond : 0f;
                actor.Energy.Tick(dt, Definition.energyDrainPerSecond * VisitorInterests.Factor(settings.energyMultiplier),
                    guest.Activity == VisitorActivity.Fleeing ? Definition.fleeingEnergyMultiplier : 1f, resting, recovery);
                bool watching = guest.Activity == VisitorActivity.Watching && guest.Watching != null
                    && guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange);
                guest.Mood.Tick(dt, actor.Vitals.Hunger, actor.Energy.Energy, actor.Vitals.Fear,
                    watching, resting || (guest.Activity == VisitorActivity.Eating && ServiceAvailable(guest))
                        || guest.Activity == VisitorActivity.EnjoyingAttraction,
                    Definition.minimumStaySeconds, Definition.maximumStaySeconds,
                    settings.RewardMultiplier(guest.LastExhibitInterest), settings.fearLossMultiplier);
                if (guest.Mood.ShouldLeave) guest.LeavingPark = true;
                if (threat != null)
                {
                    guest.LastDanger = threat.transform.position;
                    if (!guest.Panicked) guest.Mood.Punish(Definition.predatorEncounterPenalty);
                    guest.Panicked = true; guest.LeavingPark = true;
                }
                bool danger = threat != null || (guest.Panicked && actor.Vitals.Fear >= Definition.panicFearThreshold
                    && Distance(actor.transform.position, guest.LastDanger) < Definition.fleeSafeDistance);
                if (danger)
                {
                    InterruptAttraction(guest);
                    ReleaseService(guest);
                    if (guest.Activity != VisitorActivity.Fleeing || guest.DecisionTimer <= 0f || actor.Route.Count == 0)
                    {
                        guest.DecisionTimer = Mathf.Max(.05f, Definition.decisionInterval);
                        Flee(guest);
                    }
                }
                else if (guest.Attraction != null) TickAttraction(guest);
                else if (guest.DecisionTimer <= 0f)
                {
                    // Build the shared entrance search over several frames instead of charging
                    // its entire cost to a freshly spawned visitor. Safety and exits never wait.
                    bool preparingArrival = !guest.LeavingPark && guest.Activity == VisitorActivity.Arriving
                        && actor.Route.Count == 0 && buildings.Entrance != null && buildings.Entrance.IsOpen
                        && actor.Cell == buildings.Entrance.Footprint.AccessCell
                        && !arrivalPaths.TryGet(actor.Cell, out _);
                    if (!preparingArrival)
                    {
                        guest.DecisionTimer = Mathf.Max(.05f, Definition.decisionInterval);
                        Think(guest);
                    }
                }
                Move(guest, dt);
                CompleteActivity(guest);
                if (guest.Activity == VisitorActivity.Leaving && actor.Route.Count == 0
                    && buildings.Entrance != null && buildings.Entrance.IsOpen
                    && actor.Cell == buildings.Entrance.Footprint.AccessCell)
                {
                    ReleaseService(guest); visitors.RemoveAt(i); DepartedCount++;
                    Reputation.ReportDeparture(guest.Mood.Satisfaction);
                    creatures.RemoveExternal(actor); continue;
                }
                actor.ExternalResting = guest.Activity == VisitorActivity.Resting;
                creatures.RefreshExternal(actor);
            }
        }

        /// <summary>Recounts creatures visible from connected public paths; fence ownership is irrelevant.</summary>
        private void RefreshReputationIfNeeded()
        {
            if (reputationTimer <= 0f || reputationGridRevision != grid.Revision || reputationFenceRevision != fences.Revision)
                RefreshReputation();
        }

        /// <summary>Explicitly refreshes exhibition data; ordinary arrivals share the scheduled snapshot.</summary>
        public void RefreshReputation()
        {
            using var sample = ReputationProfile.Auto();
            buildings.Refresh();
            var displayed = new Dictionary<string, ParkExhibit>(StringComparer.Ordinal);
            float range = Definition.viewRange;
            bool validRange = !float.IsNaN(range) && !float.IsInfinity(range) && range >= 0f;
            if (buildings.Entrance != null && buildings.Entrance.IsOpen && validRange)
            {
                int radius = Math.Min(Math.Max(grid.Width, grid.Depth), (int)Math.Ceiling(range / grid.CellSize));
                foreach (CreatureAgent animal in creatures.Agents)
                {
                    if (!animal.Alive || animal.ExternallyControlled || animal.CarriedBy != null || animal.IsUnderground
                        || animal.Definition.requiresGravestoneForSleep && animal.Activity == CreatureActivity.Sleeping) continue;
                    bool visible = false;
                    for (int z = Math.Max(0, animal.Cell.y - radius); !visible && z <= Math.Min(grid.Depth - 1, animal.Cell.y + radius); z++)
                    for (int x = Math.Max(0, animal.Cell.x - radius); !visible && x <= Math.Min(grid.Width - 1, animal.Cell.x + radius); x++)
                    {
                        var cell = new Vector2Int(x, z);
                        if (buildings.IsPathConnected(cell) && Distance(grid.CellCenter(cell), animal.transform.position) <= range
                            && sight.HasLineOfSight(cell, animal.Cell)) visible = true;
                    }
                    if (!visible) continue;
                    CreatureDefinition definition = animal.Definition;
                    string species = string.IsNullOrEmpty(definition.name) ? definition.displayName : definition.name;
                    if (string.IsNullOrEmpty(species)) species = "creature.name.unidentified";
                    if (displayed.TryGetValue(species, out ParkExhibit existing)) existing.count++;
                    else displayed.Add(species, new ParkExhibit(species, definition.rating, 1,
                        definition.IsDomestic));
                }
            }
            Reputation.UpdateExhibition(displayed.Values);
            reputationTimer = Definition.reputationRefreshSeconds;
            reputationGridRevision = grid.Revision; reputationFenceRevision = fences.Revision;
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
                if (actor.Route.Count > 0 && !PettingPortalStep(guest, actor.Cell, actor.Route[0])
                    && !navigation.CanCross(actor.Cell, actor.Route[0], guest.GroundRoute))
                {
                    actor.Route.Clear(); actor.Goal = null;
                    actor.transform.position = Position(actor, actor.Cell);
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
            using var sample = DecisionProfile.Auto();
            CreatureAgent actor = guest.Actor;
            if (guest.Attraction != null) { TickAttraction(guest); return; }
            CompleteActivity(guest);
            if (guest.LeavingPark && (guest.Activity == VisitorActivity.Eating || guest.Activity == VisitorActivity.Resting))
            { ReleaseService(guest); SetActivity(guest, VisitorActivity.Arriving, "visitor.status.ready_to_leave"); }
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
                SetActivity(guest, VisitorActivity.Stranded, "visitor.status.cannot_reach_a_connected_path"); return;
            }
            if (guest.LeavingPark)
            {
                ReleaseService(guest);
                if (buildings.Entrance != null && buildings.Entrance.IsOpen
                    && SetRoute(guest, buildings.Entrance.Footprint.AccessCell))
                    SetActivity(guest, VisitorActivity.Leaving, "visitor.status.returning_to_the_park_entrance");
                else { FinishSegment(actor); SetActivity(guest, VisitorActivity.Stranded, "visitor.status.exit_disconnected_reconnect_the_path"); }
                return;
            }
            if ((guest.Activity == VisitorActivity.SeekingFood || guest.Activity == VisitorActivity.SeekingRest)
                && ServiceAvailable(guest)) return;
            if (guest.Service != null) ReleaseService(guest);
            if (actor.Vitals.Hunger >= Mathf.Clamp(Definition.hungerThreshold * VisitorInterests.Factor(guest.TypeSettings.hungerThresholdMultiplier), 0, 100)
                && TryService(guest, BuildingKind.Kiosk)) return;
            if (actor.Energy.Energy <= Mathf.Clamp(Definition.restThreshold * VisitorInterests.Factor(guest.TypeSettings.restThresholdMultiplier), 0, 100)
                && TryService(guest, BuildingKind.RestArea)) return;
            if (guest.Activity == VisitorActivity.Watching && guest.StateTimer > 0f && guest.Watching != null
                && guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange)) return;
            if (guest.Activity == VisitorActivity.Walking && actor.Route.Count > 0 && guest.Watching != null
                && guest.Watching.Alive) return;
            List<Vector2Int> paths;
            if (buildings.Entrance != null && actor.Cell == buildings.Entrance.Footprint.AccessCell)
            {
                arrivalPaths.Prepare(actor.Cell, 1);
                paths = arrivalPaths.GetOrBuild(actor.Cell);
            }
            else paths = navigation.Reachable(actor.Cell);
            if (TryAttraction(guest, paths)) return;
            if (TryViewingSpot(guest, paths)) return;
            if (actor.Route.Count > 0) return;
            if (paths.Count > 1 && SetRoute(guest, paths[UnityEngine.Random.Range(0, paths.Count)]))
                SetActivity(guest, VisitorActivity.Walking, "visitor.status.exploring_the_paths");
            else SetActivity(guest, VisitorActivity.Arriving, "visitor.status.looking_for_creatures_to_watch");
        }

        private bool TryService(VisitorAgent guest, BuildingKind kind)
        {
            ParkBuildingRecord best = null; int shortest = int.MaxValue;
            bool expensive = false;
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
                if (!Reputation.ServiceAffordable(building.Price, kind == BuildingKind.Kiosk))
                { expensive = true; continue; }
                shortest = route.Count; best = building;
            }
            if (best == null || !SetRoute(guest, best.Footprint.AccessCell))
            { if (expensive) RefuseServicePrice(guest, kind == BuildingKind.Kiosk); return false; }
            guest.Service = best; guest.Watching = null;
            guest.PriceComplaint = null;
            SetActivity(guest, kind == BuildingKind.Kiosk ? VisitorActivity.SeekingFood : VisitorActivity.SeekingRest,
                kind == BuildingKind.Kiosk ? "visitor.status.walking_to_a_kiosk" : "visitor.status.walking_to_a_rest_area");
            return true;
        }

        private void RefuseServicePrice(VisitorAgent guest, bool food)
        {
            float next = food ? guest.NextFoodPriceComplaint : guest.NextRestPriceComplaint;
            if (time < next) return;
            if (food) guest.NextFoodPriceComplaint = time + Definition.priceComplaintCooldownSeconds; else guest.NextRestPriceComplaint = time + Definition.priceComplaintCooldownSeconds;
            if (ExpensiveServiceRefusals < int.MaxValue) ExpensiveServiceRefusals++;
            guest.PriceComplaint = food ? "visitor.status.food_prices_are_too_high" : "visitor.status.rest_prices_are_too_high";
            guest.Mood.Punish(Definition.expensiveServicePenalty);
        }

        private bool ServiceAvailable(VisitorAgent guest)
        {
            if (guest.Service == null || !guest.Service.IsOpen) return false;
            foreach (ParkBuildingRecord building in buildings.Records) if (building == guest.Service) return true;
            return false;
        }

        private static void ReleaseService(VisitorAgent guest) { guest.Service = null; }

        private bool TryViewingSpot(VisitorAgent guest, List<Vector2Int> paths)
        {
            if (paths.Count == 0) return false;
            CreatureAgent animal = null; Vector2Int point = guest.Actor.Cell;
            float best = float.MinValue;
            int bestOrder = int.MaxValue;
            viewingOrder.Clear();
            for (int i = 0; i < paths.Count; i++) viewingOrder.Add(paths[i], i);
            foreach (CreatureAgent candidate in creatures.Agents)
            {
                if (!candidate.Alive || candidate.ExternallyControlled || candidate.IsUnderground
                    || candidate.Definition.requiresGravestoneForSleep && candidate.Activity == CreatureActivity.Sleeping) continue;
                if (guest.ViewedAt.TryGetValue(candidate, out float last)
                    && time - last < Definition.exhibitRevisitSeconds * VisitorInterests.Factor(guest.TypeSettings.revisitMultiplier)) continue;
                float interest = Interest(guest, candidate.Definition);
                float preference = interest * VisitorInterests.NonNegative(guest.TypeSettings.interestChoiceWeight);
                var points = viewing.ForAnimal(candidate.Cell, candidate.transform.position);
                for (int i = 0; i < points.Count; i++)
                {
                    var spot = points[i];
                    if (!viewingOrder.TryGetValue(spot.Cell, out int order)) continue;
                    float score = preference - ConstructionRouteGraph.CellDistance(guest.Actor.Cell, spot.Cell) * Definition.viewingTravelPenalty
                        - Mathf.Abs(spot.Distance - Definition.preferredViewingDistance) * Definition.viewingDistancePenalty;
                    // Preserve the original creature order, then this guest's BFS path order on ties.
                    if (score > best || score == best && animal == candidate && order < bestOrder)
                    { best = score; animal = candidate; point = spot.Cell; bestOrder = order; }
                }
            }
            if (animal == null || !SetRoute(guest, point)) return false;
            guest.Watching = animal;
            guest.LastExhibitInterest = Interest(guest, animal.Definition);
            SetActivity(guest, VisitorActivity.Walking, "visitor.status.walking_to_a_viewing_spot");
            return true;
        }

        private string SpeciesId(CreatureDefinition definition)
        {
            if (!speciesIds.TryGetValue(definition, out var id))
            {
                id = string.IsNullOrEmpty(definition.name) ? definition.displayName ?? "" : definition.name;
                speciesIds.Add(definition, id);
            }
            return id;
        }

        private float Interest(VisitorAgent guest, CreatureDefinition species)
        {
            if (guest.Type == VisitorType.Legacy) return 1f;
            var appeal = species.VisitorAppeal;
            float value = guest.Type == VisitorType.ThrillSeekers ? appeal.thrill
                : guest.Type == VisitorType.Wonderlust ? appeal.wonder
                : guest.Type == VisitorType.Adventurers ? appeal.adventure : appeal.children;
            string id = SpeciesId(species), origin = species.origin;
            return VisitorInterests.Interest(guest.TypeSettings, CreatureVisitorAppeal.Normalize(value), species.rating,
                string.IsNullOrEmpty(id) || guest.ObservedSpecies.Contains(id),
                string.IsNullOrEmpty(origin) || guest.ObservedOrigins.Contains(origin));
        }

        private void Flee(VisitorAgent guest)
        {
            if (TryPettingEmergencyExit(guest)) return;
            CreatureAgent actor = guest.Actor;
            Vector2Int best = actor.Cell; float bestScore = float.MinValue;
            foreach (Vector2Int candidate in GroundRegion(actor.Cell))
            {
                float distance = Distance(grid.CellCenter(candidate), guest.LastDanger);
                int steps = ConstructionRouteGraph.CellDistance(actor.Cell, candidate);
                float score = distance >= Definition.fleeSafeDistance ? Definition.safeEscapeScore - steps : distance;
                if (grid.ContentAt(candidate) == BuildCellContent.Path) score += Definition.escapePathPreference;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            SetRoute(guest, best, true);
            SetActivity(guest, VisitorActivity.Fleeing, "visitor.status.fleeing_a_predator");
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
            SetActivity(guest, VisitorActivity.ReturningToPath, "visitor.status.rejoining_a_path_after_fleeing");
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
            if (guest.Attraction?.InsideInterior == true) return;
            CreatureAgent actor = guest.Actor;
            if (actor.Route.Count == 0) return;
            Vector2Int next = actor.Route[0];
            if (!PettingPortalStep(guest, actor.Cell, next) && !navigation.CanCross(actor.Cell, next, guest.GroundRoute))
            {
                actor.Route.Clear(); actor.Goal = null; guest.DecisionTimer = 0f;
                actor.transform.position = Position(actor, actor.Cell); return;
            }
            Vector3 goal = Position(actor, next), direction = goal - actor.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .001f) actor.transform.rotation = Quaternion.LookRotation(direction);
            float speed = guest.Activity == VisitorActivity.Fleeing
                ? Definition.runSpeed * VisitorInterests.Factor(guest.TypeSettings.runSpeedMultiplier)
                : Definition.walkSpeed * VisitorInterests.Factor(guest.TypeSettings.walkSpeedMultiplier);
            actor.transform.position = Vector3.MoveTowards(actor.transform.position, goal, Mathf.Max(.1f, speed) * dt);
            if(grid.Terrain!=null)actor.transform.position=grid.ProjectToGround(actor.transform.position,actor.Definition.bodyScale.y);
            if ((actor.transform.position - goal).sqrMagnitude < .0001f)
            { actor.Cell = next; actor.Route.RemoveAt(0); }
        }

        private void CompleteActivity(VisitorAgent guest)
        {
            CreatureAgent actor = guest.Actor;
            if (actor.Route.Count > 0) return;
            if (CompleteAttractionMovement(guest)) return;
            if (guest.Activity == VisitorActivity.SeekingFood || guest.Activity == VisitorActivity.SeekingRest)
            {
                if (!ServiceAvailable(guest) || actor.Cell != guest.Service.Footprint.AccessCell)
                { ReleaseService(guest); guest.DecisionTimer = 0f; return; }
                bool food = guest.Activity == VisitorActivity.SeekingFood;
                if (!Reputation.ServiceAffordable(guest.Service.Price, food))
                {
                    RefuseServicePrice(guest, food); ReleaseService(guest); guest.DecisionTimer = 0f;
                    SetActivity(guest, VisitorActivity.Arriving, "visitor.status.looking_for_an_affordable_service"); return;
                }
                guest.QuotedPrice = guest.Service.Price;
                guest.StateTimer = Mathf.Max(.1f, guest.Service.Footprint.Definition.serviceSeconds);
                SetActivity(guest, food ? VisitorActivity.Eating : VisitorActivity.Resting,
                    food ? "visitor.status.eating_at_a_kiosk" : "visitor.status.resting_at_a_bench_area");
            }
            else if (guest.Activity == VisitorActivity.Eating && guest.StateTimer <= 0f && ServiceAvailable(guest))
            {
                BuildingDefinition service = guest.Service.Footprint.Definition;
                actor.Vitals.Eat(service.foodRelief); ChargeService(guest);
                guest.Mood.Reward(Definition.mealSatisfactionReward * VisitorInterests.Factor(guest.TypeSettings.serviceRewardMultiplier));
                ReleaseService(guest); SetActivity(guest, VisitorActivity.Arriving, "visitor.status.finished_eating"); guest.DecisionTimer = 0f;
            }
            else if (guest.Activity == VisitorActivity.Resting && actor.Energy.Energy >= Definition.comfortableEnergy && ServiceAvailable(guest))
            {
                ChargeService(guest); guest.Mood.Reward(Definition.restSatisfactionReward * VisitorInterests.Factor(guest.TypeSettings.serviceRewardMultiplier)); ReleaseService(guest);
                SetActivity(guest, VisitorActivity.Arriving, "visitor.status.feeling_rested"); guest.DecisionTimer = 0f;
            }
            else if (guest.Activity == VisitorActivity.Walking && guest.Watching != null)
            {
                if (guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange))
                {
                    guest.StateTimer = Mathf.Max(.1f, Definition.watchingSeconds * VisitorInterests.Factor(guest.TypeSettings.watchingSecondsMultiplier));
                    SetActivity(guest, VisitorActivity.Watching, "visitor.status.watching", guest.Watching.Definition.displayName);
                }
                else { guest.Watching = null; guest.DecisionTimer = 0f; }
            }
            else if (guest.Activity == VisitorActivity.Watching && guest.StateTimer <= 0f)
            {
                if (guest.Watching != null)
                {
                    guest.ViewedAt[guest.Watching] = time;
                    if (guest.Watching.Alive && creatures.CanSee(actor, guest.Watching, Definition.viewRange))
                    {
                        guest.Mood.Reward(Definition.exhibitSatisfactionReward * guest.TypeSettings.RewardMultiplier(guest.LastExhibitInterest));
                        string id = SpeciesId(guest.Watching.Definition), origin = guest.Watching.Definition.origin;
                        if (!string.IsNullOrEmpty(id)) guest.ObservedSpecies.Add(id);
                        if (!string.IsNullOrEmpty(origin)) guest.ObservedOrigins.Add(origin);
                    }
                }
                guest.Watching = null; SetActivity(guest, VisitorActivity.Arriving, "visitor.status.looking_for_another_exhibit");
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
            economy.Earn(guest.QuotedPrice, guest.Service.Footprint.Definition.kind == BuildingKind.Kiosk
                ? ParkTransactionCategory.Food : ParkTransactionCategory.Rest);
            guest.QuotedPrice = 0f;
        }

        private static void SetActivity(VisitorAgent guest, VisitorActivity activity, string status, params object[] arguments)
        {
            guest.Activity = activity; guest.SetLocalizedStatus(status, arguments);
            guest.Actor.Activity = activity == VisitorActivity.Fleeing ? CreatureActivity.Fleeing
                : activity == VisitorActivity.Eating ? CreatureActivity.Feeding
                : activity == VisitorActivity.Resting || activity == VisitorActivity.Watching || activity == VisitorActivity.Stranded
                    ? CreatureActivity.Resting : CreatureActivity.Wandering;
        }

        private Vector3 Position(CreatureAgent actor, Vector2Int cell) => grid.ActorPosition(cell,actor.Definition.bodyScale.y);
        private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        public void Dispose()
        {
            foreach (VisitorAgent guest in visitors) if (guest != null && guest.Actor != null) creatures.RemoveExternal(guest.Actor);
            visitors.Clear();
            foreach (var profile in actorProfiles.Values) UnityEngine.Object.Destroy(profile);
            actorProfiles.Clear(); speciesIds.Clear();
        }
    }
}

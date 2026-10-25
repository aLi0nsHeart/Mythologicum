using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Shared perception, feeding and movement; no enclosure ownership or species-specific AI branches.</summary>
    public sealed class CreatureWorld
    {
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly ParkBuildingNetwork buildings;
        private readonly Camera camera;
        private readonly GameObject root;
        private readonly Material material, grassMaterial;
        private readonly CreatureNavigation navigation;
        private readonly CreatureNavigation climbingNavigation;
        private readonly GrazingField grass;
        private readonly EnvironmentGrid environment;
        private readonly List<CreatureAgent> agents = new List<CreatureAgent>();
        private readonly Dictionary<Vector2Int, bool> clearance = new Dictionary<Vector2Int, bool>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> reachableRegions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        private readonly Dictionary<Vector2Int, HashSet<Vector2Int>> climbingRegions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        private readonly Dictionary<ParkBuildingRecord, float> dispenserTimers = new Dictionary<ParkBuildingRecord, float>();
        private readonly CreatureDefinition defaultGoat;
        private int gridRevision = -1, fenceRevision = -1;
        private float time;
        public int PopulationLimit { get; set; } = 100;
        public int Population { get { int count = 0; foreach (var agent in agents) if (agent.Alive) count++; return count; } }
        public IReadOnlyList<CreatureAgent> Agents => agents;

        public CreatureWorld(SquareBuildGrid grid, FenceLayout fences, ParkBuildingNetwork buildings,
            Camera camera, Material material, CreatureDefinition[] profiles, EnvironmentGrid environment = null)
        {
            this.grid = grid; this.fences = fences; this.buildings = buildings;
            this.camera = camera; this.material = material;
            this.environment = environment ?? new EnvironmentGrid(grid);
            root = new GameObject("Creatures");
            grassMaterial = new Material(material) { color = new Color(0.2f, 0.65f, 0.16f) };
            navigation = new CreatureNavigation(grid, fences, IsPhysicallyClear);
            climbingNavigation = new CreatureNavigation(grid, fences, IsPhysicallyClear, true);
            grass = new GrazingField(grid);
            foreach (CreatureDefinition profile in profiles)
                if ((profile.tags & CreatureTags.Goat) != 0) { defaultGoat = profile; break; }
        }

        private bool IsPhysicallyClear(Vector2Int cell)
        {
            if (clearance.TryGetValue(cell, out bool clear)) return clear;
            clear = !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * 0.65f,
                new Vector3(grid.CellSize * 0.3f, 0.6f, grid.CellSize * 0.3f), Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            clearance[cell] = clear;
            return clear;
        }

        private void RefreshNavigation()
        {
            if (gridRevision == grid.Revision && fenceRevision == fences.Revision) return;
            Physics.SyncTransforms();
            clearance.Clear();
            reachableRegions.Clear();
            climbingRegions.Clear();
            gridRevision = grid.Revision; fenceRevision = fences.Revision;
            foreach (CreatureAgent agent in agents)
            {
                // External controllers own their routes, including authorized staff/visitor gate crossings.
                if (agent.ExternallyControlled) continue;
                agent.DecisionTimer = 0f;
                // Preserve valid movement when unrelated construction changes elsewhere.
                if (agent.Activity != CreatureActivity.ClimbingFence && agent.Route.Count > 0
                    && !Navigation(agent).CanCross(agent.Cell, agent.Route[0]))
                {
                    agent.Route.Clear(); agent.Goal = null;
                    agent.transform.position = GroundPosition(agent, agent.Cell);
                }
            }
        }

        public bool CanSpawn(Vector2Int cell)
        {
            RefreshNavigation();
            if (Population >= PopulationLimit || !navigation.IsWalkable(cell)) return false;
            Vector3 point = grid.CellCenter(cell);
            foreach (CreatureAgent other in agents)
                if (other.Alive && HorizontalDistance(other.transform.position, point) < grid.CellSize * 0.7f) return false;
            return true;
        }

        public bool IsClear(Vector2Int cell)
        {
            RefreshNavigation();
            return navigation.IsWalkable(cell);
        }

        public bool CanSee(CreatureAgent observer, CreatureAgent target, float range)
        {
            if (observer == null || target == null || !observer.Alive || !target.Alive || range < 0f) return false;
            RefreshNavigation();
            return HorizontalDistance(observer.transform.position, target.transform.position) <= range
                && navigation.HasLineOfSight(observer.Cell, target.Cell);
        }

        /// <summary>Registers a visible, huntable actor whose decisions and movement belong to another controller.</summary>
        public CreatureAgent SpawnExternal(CreatureDefinition definition, Vector2Int cell)
        {
            if (!TrySpawn(definition, cell)) return null;
            CreatureAgent actor = agents[agents.Count - 1];
            actor.ExternallyControlled = true;
            actor.ExternalStatus = "Arriving";
            return actor;
        }

        public void RefreshExternal(CreatureAgent actor)
        {
            if (actor != null && actor.ExternallyControlled && agents.Contains(actor)) RefreshVisual(actor);
        }

        /// <summary>Removes an external actor at departure and safely cancels any outstanding predation references.</summary>
        public void RemoveExternal(CreatureAgent actor)
        {
            if (actor == null || !actor.ExternallyControlled || !agents.Contains(actor)) return;
            ReleaseClaim(actor);
            actor.Alive = false;
            actor.CapturedBy = null;
            foreach (CreatureAgent other in agents)
            {
                if (other.Prey == actor)
                {
                    ReleaseClaim(other);
                    if (other.Activity == CreatureActivity.Attacking || other.Activity == CreatureActivity.Feeding)
                    {
                        other.Activity = CreatureActivity.Resting;
                        other.StateTimer = PauseDuration(other);
                    }
                    other.DecisionTimer = 0f;
                }
                if (other.Threat == actor) { other.Threat = null; other.DecisionTimer = 0f; }
                if (other.SocialPartner == actor) other.SocialPartner = null;
                if (other.CapturedBy == actor) other.CapturedBy = null;
            }
            agents.Remove(actor);
            actor.GetComponent<Collider>().enabled = false;
            actor.gameObject.SetActive(false);
            if (actor.Label != null) UnityEngine.Object.Destroy(actor.Label.gameObject);
            if (actor.Grass != null) UnityEngine.Object.Destroy(actor.Grass);
            UnityEngine.Object.Destroy(actor.gameObject);
        }

        public bool Occupies(BuildingFootprint footprint)
        {
            foreach (CreatureAgent agent in agents)
                if (agent.Alive && (footprint.Contains(agent.Cell)
                    || footprint.Contains(grid.WorldToCell(agent.transform.position)))) return true;
            return false;
        }

        public bool TrySpawn(CreatureDefinition definition, Vector2Int cell, ParkBuildingRecord dispenser = null)
        {
            if (definition == null || !CanSpawn(cell)) return false;
            var go = new GameObject(definition.displayName);
            go.name = definition.displayName;
            go.transform.SetParent(root.transform);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.isTrigger = true; collider.radius = Mathf.Max(definition.bodyScale.x, definition.bodyScale.z) * 0.5f;
            collider.height = Mathf.Max(definition.bodyScale.y * 2f, collider.radius * 2f);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Animated placeholder"; body.transform.SetParent(go.transform, false);
            body.transform.localScale = definition.bodyScale;
            body.GetComponent<Collider>().enabled = false;
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", definition.color); properties.SetColor("_Color", definition.color);
            renderer.SetPropertyBlock(properties);
            CreatureAgent agent = go.AddComponent<CreatureAgent>();
            agent.Body = body.transform; agent.BodyRenderer = renderer;
            agent.Definition = definition; agent.Vitals = new CreatureVitals(definition.initialHunger);
            agent.Energy = new CreatureEnergy(definition.initialEnergy);
            agent.Cell = cell; agent.SourceDispenser = dispenser;
            agent.Activity = CreatureActivity.Resting;
            agent.StateTimer = PauseDuration(agent);
            agent.DecisionTimer = UnityEngine.Random.Range(0f, definition.decisionInterval);
            go.transform.position = GroundPosition(agent, cell);
            var labelObject = new GameObject("Creature stats", typeof(TextMesh));
            labelObject.transform.SetParent(root.transform);
            agent.Label = labelObject.GetComponent<TextMesh>();
            agent.Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            agent.Label.GetComponent<MeshRenderer>().sharedMaterial = agent.Label.font.material;
            agent.Label.fontSize = 48; agent.Label.characterSize = 0.085f;
            agent.Label.anchor = TextAnchor.LowerCenter; agent.Label.alignment = TextAlignment.Center;
            var patch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            patch.name = "Grazing grass"; patch.transform.SetParent(root.transform);
            patch.GetComponent<Collider>().enabled = false;
            patch.GetComponent<MeshRenderer>().sharedMaterial = grassMaterial;
            patch.transform.localScale = new Vector3(grid.CellSize * 0.65f, 0.1f, grid.CellSize * 0.65f);
            patch.SetActive(false); agent.Grass = patch;
            agents.Add(agent);
            return true;
        }

        public void Tick(float deltaTime)
        {
            float dt = Mathf.Max(0f, deltaTime);
            time += dt;
            RefreshNavigation();
            TickDispensers(dt);
            // Consumption marks prey dead immediately; cleanup happens after this stable iteration.
            foreach (CreatureAgent agent in agents)
            {
                if (!agent.Alive)
                { agent.CorpseTimer -= dt; RefreshVisual(agent); continue; }
                if (agent.CapturedBy != null && agent.CapturedBy.Alive)
                {
                    agent.Vitals.Tick(dt, agent.Definition.hungerPerSecond, agent.Definition.fearGainPerSecond,
                        agent.Definition.fearRecoveryPerSecond, true);
                    agent.Energy.Tick(dt, agent.Definition.energyDrainPerSecond, agent.Definition.strenuousEnergyMultiplier,
                        false, agent.Definition.sleepRecoveryPerSecond);
                    RefreshVisual(agent); continue;
                }
                if (agent.ExternallyControlled) { RefreshVisual(agent); continue; }
                agent.StateTimer -= dt;
                if (agent.Activity != CreatureActivity.Sleeping) agent.Exploration.Tick(dt);
                agent.DecisionTimer -= dt;
                agent.WanderTimer -= dt;
                bool traversing = TickTraversal(agent, dt);
                if (!traversing && agent.DecisionTimer <= 0f)
                {
                    agent.DecisionTimer = agent.Definition.decisionInterval;
                    Think(agent);
                }
                CreatureDefinition definition = agent.Definition;
                agent.Vitals.Tick(dt, definition.hungerPerSecond, definition.fearGainPerSecond,
                    definition.fearRecoveryPerSecond, agent.Threat != null && agent.Threat.Alive);
                bool sleeping = agent.Activity == CreatureActivity.Sleeping
                    && (!definition.requiresTreeForSleep || (agent.SleepTree.HasValue && environment.TreesAt(agent.SleepTree.Value) > 0));
                float energyMultiplier = agent.Activity == CreatureActivity.Socializing ? definition.socializingEnergyMultiplier
                    : agent.Activity == CreatureActivity.Hunting || agent.Activity == CreatureActivity.Fleeing
                        || agent.Activity == CreatureActivity.Attacking || agent.Activity == CreatureActivity.ClimbingFence
                        || agent.Activity == CreatureActivity.ClimbingTree || agent.Activity == CreatureActivity.FailedClimb
                        ? definition.strenuousEnergyMultiplier : 1f;
                agent.Energy.Tick(dt, definition.energyDrainPerSecond, energyMultiplier, sleeping, definition.sleepRecoveryPerSecond);
                if (!traversing) { Move(agent, dt); Feed(agent, dt); }
                UpdateWellbeing(agent, dt);
                TickEscape(agent, dt);
                RefreshVisual(agent);
            }
            for (int i = agents.Count - 1; i >= 0; i--)
                if (!agents[i].Alive && agents[i].CorpseTimer <= 0f)
                {
                    CreatureAgent dead = agents[i]; agents.RemoveAt(i);
                    UnityEngine.Object.Destroy(dead.Label.gameObject);
                    UnityEngine.Object.Destroy(dead.Grass);
                    UnityEngine.Object.Destroy(dead.gameObject);
                }
        }

        private void Think(CreatureAgent agent)
        {
            CreatureDefinition definition = agent.Definition;
            agent.Threat = ClosestVisible(agent, definition.fearedTags);
            if (agent.Threat != null) { agent.LastDanger = agent.Threat.transform.position; agent.HasDanger = true; }
            bool stillUnsafe = agent.HasDanger && HorizontalDistance(agent.transform.position, agent.LastDanger) < definition.fleeSafeDistance;
            if (agent.Threat != null || (stillUnsafe && agent.Vitals.Fear > 0f && agent.Vitals.Fear >= definition.fleeThreshold))
            {
                ReleaseClaim(agent);
                if (agent.Elevation > 0.01f) { BeginTreeTraversal(agent, false); return; }
                agent.SleepTree = null;
                agent.Activity = CreatureActivity.Fleeing; agent.GrazeTimer = 0f;
                Flee(agent);
                return;
            }
            if (agent.Activity == CreatureActivity.Attacking || agent.Activity == CreatureActivity.Feeding) return;
            if (agent.Energy.Energy <= definition.sleepThreshold) agent.WantsSleep = true;
            if (agent.Activity == CreatureActivity.Sleeping || agent.WantsSleep)
            { SeekSleep(agent); return; }
            if (agent.EscapeIntent && Escape(agent)) return;
            if (agent.Activity == CreatureActivity.Fleeing)
            { FinishSegment(agent); agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent); }
            if (!stillUnsafe || agent.Vitals.Fear < definition.fleeThreshold) agent.HasDanger = false;
            if (agent.Activity == CreatureActivity.Socializing && agent.StateTimer > 0f && agent.SocialPartner != null
                && agent.SocialPartner.Alive && HorizontalDistance(agent.transform.position, agent.SocialPartner.transform.position) <= 3f
                && agent.Vitals.Hunger < definition.huntThreshold) return;
            if (agent.Vitals.Hunger >= definition.huntThreshold)
            {
                if (definition.diet == CreatureDiet.Carnivore && Hunt(agent)) return;
                if (definition.diet == CreatureDiet.Herbivore)
                {
                    if (grass.HasFood(agent.Cell, time) && agent.Route.Count == 0)
                    { agent.Activity = CreatureActivity.Grazing; agent.Goal = null; return; }
                    if (agent.Activity == CreatureActivity.Searching && agent.Route.Count > 0 && agent.Goal.HasValue
                        && grass.HasFood(agent.Goal.Value, time)) return;
                    foreach (Vector2Int candidate in navigation.Reachable(agent.Cell, 5))
                        if (grass.HasFood(candidate, time) && SetGoal(agent, candidate))
                        { agent.Activity = CreatureActivity.Searching; return; }
                }
            }
            else ReleaseClaim(agent);
            agent.GrazeTimer = 0f;
            if (agent.Route.Count > 0) { agent.Activity = CreatureActivity.Wandering; return; }
            if (agent.Activity != CreatureActivity.Resting)
            { agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent); }
            if (agent.StateTimer > 0f) return;
            if (definition.minimumCompanions > 0)
                foreach (CreatureAgent other in agents)
                    if (other != agent && other.Alive && other.Definition == definition
                        && HorizontalDistance(other.transform.position, agent.transform.position) <= 3f
                        && ReachableRegion(agent.Cell, definition.canClimbWoodenFences).Contains(other.Cell))
                    {
                        agent.SocialPartner = other; agent.Activity = CreatureActivity.Socializing;
                        agent.StateTimer = definition.socializingSeconds; return;
                    }
            List<Vector2Int> choices = navigation.Reachable(agent.Cell, 4);
            if (choices.Count > 1 && SetGoal(agent, choices[UnityEngine.Random.Range(1, choices.Count)], true))
                agent.Activity = CreatureActivity.Wandering;
            else agent.StateTimer = PauseDuration(agent);
        }

        private float PauseDuration(CreatureAgent agent) => UnityEngine.Random.Range(agent.Definition.pauseMinSeconds,
            Mathf.Max(agent.Definition.pauseMinSeconds, agent.Definition.pauseMaxSeconds))
            * Mathf.Lerp(1f, 0.4f, agent.Wellbeing.Agitation);

        private void TickEscape(CreatureAgent agent, float dt)
        {
            agent.EscapeCheckTimer -= dt;
            if (agent.EscapeCheckTimer <= 0f)
            {
                agent.EscapeCheckTimer = .5f;
                var ground = ReachableRegion(agent.Cell);
                float required = 0f;
                foreach (CreatureAgent other in agents)
                    if (other.Alive && !other.ExternallyControlled && ground.Contains(other.Cell))
                        required += Mathf.Max(0f, other.Definition.minimumSpace);
                agent.Crowding = CreatureEscape.Overcrowding(ground.Count * grid.CellSize * grid.CellSize, required);
            }
            CreatureDefinition data = agent.Definition;
            agent.EscapeChancePerMinute = CreatureEscape.Chance(agent.Vitals.Hunger, agent.Wellbeing.Agitation,
                agent.Crowding, data.randomEscapeChance, data.hungryEscapeChance, data.agitatedEscapeChance, data.crowdedEscapeChance);
            if (agent.EscapeIntent || time < agent.EscapeAfter || agent.Elevation > .01f
                || agent.Activity == CreatureActivity.Sleeping || agent.Activity == CreatureActivity.SeekingSleep
                || agent.Activity == CreatureActivity.ClimbingTree || agent.Activity == CreatureActivity.DescendingTree
                || agent.Activity == CreatureActivity.ClimbingFence || agent.Activity == CreatureActivity.FailedClimb
                || agent.Activity == CreatureActivity.Attacking || agent.Activity == CreatureActivity.Feeding
                || agent.Activity == CreatureActivity.Fleeing) return;
            if (UnityEngine.Random.value >= CreatureEscape.Probability(agent.EscapeChancePerMinute, dt)) return;
            agent.EscapeIntent = true; agent.EscapeUntil = time + 30f;
            agent.EscapeGoal = null; agent.EscapeBarrier = null;
            agent.EscapeStatus = "Seeking a way out"; agent.DecisionTimer = 0f;
        }

        private void EndEscape(CreatureAgent agent, string reason)
        {
            agent.EscapeIntent = false; agent.EscapeGoal = null; agent.EscapeBarrier = null;
            agent.EscapeAfter = time + Mathf.Max(1f, agent.Definition.escapeRetrySeconds);
            agent.EscapeStatus = reason; FinishSegment(agent);
        }

        private bool Escape(CreatureAgent agent)
        {
            ReleaseClaim(agent);
            if (time >= agent.EscapeUntil)
            { EndEscape(agent, "Attempt ended; resting before retry"); return false; }
            if (agent.EscapeGoal.HasValue)
            {
                if (agent.Cell == agent.EscapeGoal.Value && agent.Route.Count == 0)
                {
                    FenceEdge? barrier = agent.EscapeBarrier;
                    EndEscape(agent, barrier.HasValue ? "Blocked by a fence" : "Reached new ground");
                    if (barrier.HasValue && agent.Definition.canClimbWoodenFences && fences.HasFence(barrier.Value))
                    { agent.AttemptFence = barrier; BeginFailedClimb(agent); return true; }
                    return false;
                }
                if (SetGoal(agent, agent.EscapeGoal.Value))
                { agent.Activity = CreatureActivity.Escaping; return true; }
                EndEscape(agent, "Escape route blocked"); return false;
            }
            var ground = ReachableRegion(agent.Cell);
            Vector2Int? goal = null; FenceEdge? obstruction = null;
            float best = float.MaxValue;
            // Explore physical barriers, independent of enclosure identity. Goats cannot acquire climbing abilities.
            foreach (FenceEdge edge in fences.Edges)
            {
                bool a = ground.Contains(edge.CellA), b = ground.Contains(edge.CellB);
                if (a == b) continue;
                Vector2Int near = a ? edge.CellA : edge.CellB, far = a ? edge.CellB : edge.CellA;
                if (!navigation.IsWalkable(far)) continue;
                bool climb = agent.Definition.canClimbWoodenFences && fences.CanClimb(edge);
                float distance = ConstructionRouteGraph.CellDistance(agent.Cell, near) + (climb ? 0f : grid.Width * grid.Depth);
                if (distance >= best) continue;
                best = distance; goal = climb ? far : near; obstruction = climb ? (FenceEdge?)null : edge;
            }
            if (!goal.HasValue)
            {
                // With a gap or no fence, seek room away from the current group using ordinary ground travel.
                int distance = 0;
                foreach (Vector2Int cell in navigation.Reachable(agent.Cell, 6))
                {
                    int candidate = ConstructionRouteGraph.CellDistance(agent.Cell, cell);
                    if (candidate > distance) { distance = candidate; goal = cell; }
                }
            }
            if (!goal.HasValue || !SetGoal(agent, goal.Value))
            { EndEscape(agent, "No usable escape route"); return false; }
            agent.EscapeGoal = goal; agent.EscapeBarrier = obstruction;
            agent.Activity = CreatureActivity.Escaping; return true;
        }

        private void FinishSegment(CreatureAgent agent)
        {
            // Keep only the active segment; never teleport to a grid centre to change activity.
            if (agent.Route.Count > 1) agent.Route.RemoveRange(1, agent.Route.Count - 1);
            agent.Goal = null;
        }

        private bool CanClimbForNeeds(CreatureAgent agent) => agent.Definition.canClimbWoodenFences
            && (agent.EscapeIntent || agent.Exploration.WillClimb(agent.Definition.settlingSeconds, agent.Vitals.Hunger,
                agent.Energy.Energy, agent.Activity == CreatureActivity.Fleeing));
        private CreatureNavigation Navigation(CreatureAgent agent) => CanClimbForNeeds(agent) ? climbingNavigation : navigation;

        private static FenceEdge CrossingEdge(Vector2Int from, Vector2Int to) => from.x != to.x
            ? new FenceEdge(new Vector2Int(Math.Max(from.x, to.x), from.y), false)
            : new FenceEdge(new Vector2Int(from.x, Math.Max(from.y, to.y)), true);

        private void SeekSleep(CreatureAgent agent)
        {
            ReleaseClaim(agent);
            if (agent.Activity == CreatureActivity.Sleeping)
            {
                if (agent.Energy.Energy >= agent.Definition.wakeThreshold)
                {
                    agent.WantsSleep = false;
                    if (agent.Elevation > 0.01f) BeginTreeTraversal(agent, false);
                    else { agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent); }
                }
                else if (agent.Definition.requiresTreeForSleep
                    && (!agent.SleepTree.HasValue || environment.TreesAt(agent.SleepTree.Value) == 0))
                    BeginTreeTraversal(agent, false);
                return;
            }
            if (!agent.Definition.requiresTreeForSleep)
            {
                FinishSegment(agent);
                agent.Activity = agent.Route.Count > 0 ? CreatureActivity.SeekingSleep : CreatureActivity.Sleeping;
                return;
            }

            HashSet<Vector2Int> accessible = ReachableRegion(agent.Cell, CanClimbForNeeds(agent));
            HashSet<Vector2Int> local = ReachableRegion(agent.Cell);
            Vector2Int? tree = null;
            float closest = float.MaxValue;
            foreach (Vector2Int cell in accessible)
            {
                if (environment.TreesAt(cell) == 0) continue;
                bool reserved = false;
                foreach (CreatureAgent other in agents)
                    if (other != agent && other.Alive && other.SleepTree == cell) { reserved = true; break; }
                if (reserved) continue;
                float distance = ConstructionRouteGraph.CellDistance(agent.Cell, cell) + (local.Contains(cell) ? 0f : grid.Width * grid.Depth);
                if (distance < closest) { closest = distance; tree = cell; }
            }
            if (!tree.HasValue)
            {
                agent.SleepTree = null; FinishSegment(agent);
                agent.Activity = CreatureActivity.SeekingSleep;
                return; // Missing a suitable sleep site never restores energy.
            }
            agent.SleepTree = tree;
            agent.Activity = CreatureActivity.SeekingSleep;
            if (agent.Cell == tree.Value && agent.Route.Count == 0) BeginTreeTraversal(agent, true);
            else SetGoal(agent, tree.Value);
        }

        private void BeginTreeTraversal(CreatureAgent agent, bool upward)
        {
            agent.Route.Clear(); agent.Goal = null;
            agent.Activity = upward ? CreatureActivity.ClimbingTree : CreatureActivity.DescendingTree;
            agent.TraversalStartElevation = agent.Elevation;
            agent.TraversalDuration = Mathf.Max(0.1f, agent.Definition.treeClimbSeconds);
            agent.StateTimer = agent.TraversalDuration;
            if (upward) agent.Energy.Spend(agent.Definition.climbEnergyCost);
        }

        private void BeginFenceClimb(CreatureAgent agent, Vector2Int next)
        {
            agent.TraversalFrom = agent.Cell; agent.TraversalTo = next;
            agent.Activity = CreatureActivity.ClimbingFence;
            agent.TraversalDuration = Mathf.Max(0.1f, agent.Definition.fenceClimbSeconds);
            agent.StateTimer = agent.TraversalDuration;
            agent.Energy.Spend(agent.Definition.climbEnergyCost);
            Vector3 direction = GroundPosition(agent, next) - GroundPosition(agent, agent.Cell);
            agent.transform.rotation = Quaternion.LookRotation(direction);
        }

        private void BeginFailedClimb(CreatureAgent agent)
        {
            agent.Route.Clear(); agent.Goal = null;
            agent.Activity = CreatureActivity.FailedClimb;
            agent.TraversalDuration = Mathf.Max(0.1f, agent.Definition.failedClimbSeconds);
            agent.StateTimer = agent.TraversalDuration;
            agent.AttemptAfter = time + agent.TraversalDuration + agent.Definition.climbRetrySeconds;
            agent.Energy.Spend(agent.Definition.climbEnergyCost);
            if (agent.AttemptFence.HasValue)
            {
                Vector3 direction = fences.Center(agent.AttemptFence.Value) - grid.CellCenter(agent.Cell);
                direction.y = 0f;
                if (direction.sqrMagnitude > 0f) agent.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private bool TickTraversal(CreatureAgent agent, float dt)
        {
            CreatureActivity activity = agent.Activity;
            if (activity != CreatureActivity.ClimbingTree && activity != CreatureActivity.DescendingTree
                && activity != CreatureActivity.ClimbingFence && activity != CreatureActivity.FailedClimb) return false;
            float progress = 1f - Mathf.Clamp01(agent.StateTimer / Mathf.Max(0.1f, agent.TraversalDuration));
            if (activity == CreatureActivity.ClimbingTree || activity == CreatureActivity.DescendingTree)
            {
                if (activity == CreatureActivity.ClimbingTree
                    && (!agent.SleepTree.HasValue || environment.TreesAt(agent.SleepTree.Value) == 0))
                { BeginTreeTraversal(agent, false); return true; }
                agent.Elevation = Mathf.Lerp(agent.TraversalStartElevation,
                    activity == CreatureActivity.ClimbingTree ? 1.8f : 0f, Mathf.SmoothStep(0f, 1f, progress));
                agent.transform.position = GroundPosition(agent, agent.Cell) + Vector3.up * agent.Elevation;
                if (agent.StateTimer <= 0f)
                {
                    agent.Activity = activity == CreatureActivity.ClimbingTree ? CreatureActivity.Sleeping : CreatureActivity.Resting;
                    if (activity == CreatureActivity.DescendingTree) agent.SleepTree = null;
                    agent.StateTimer = PauseDuration(agent); agent.DecisionTimer = 0f;
                }
                return true;
            }
            if (activity == CreatureActivity.ClimbingFence)
            {
                FenceEdge edge = CrossingEdge(agent.TraversalFrom, agent.TraversalTo);
                // Upgrading a fence during a crossing must not let the animal slip through the spikes.
                if ((fences.HasFence(edge) && !fences.CanClimb(edge)) || !Navigation(agent).IsWalkable(agent.TraversalTo))
                {
                    agent.transform.position = GroundPosition(agent, agent.TraversalFrom);
                    agent.AttemptFence = edge; BeginFailedClimb(agent); return true;
                }
                agent.transform.position = Vector3.Lerp(GroundPosition(agent, agent.TraversalFrom),
                    GroundPosition(agent, agent.TraversalTo), progress)
                    + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * (FenceConstruction.Height + 0.65f));
                if (agent.StateTimer <= 0f)
                {
                    agent.Cell = agent.TraversalTo;
                    agent.transform.position = GroundPosition(agent, agent.Cell);
                    if (agent.Route.Count > 0 && agent.Route[0] == agent.Cell) agent.Route.RemoveAt(0);
                    agent.Activity = agent.WantsSleep ? CreatureActivity.SeekingSleep
                        : agent.Prey != null && agent.Prey.Alive ? CreatureActivity.Hunting : CreatureActivity.Wandering;
                    agent.DecisionTimer = 0f;
                }
                return true;
            }
            // A rejected attempt is visual only: logical position stays on the safe side of the fence.
            if (agent.StateTimer <= 0f)
            {
                agent.AttemptFence = null; agent.Activity = CreatureActivity.Resting;
                agent.StateTimer = PauseDuration(agent); agent.DecisionTimer = 0f;
            }
            return true;
        }

        private bool TryBlockedFence(CreatureAgent agent, List<CreatureAgent> candidates)
        {
            if (!CanClimbForNeeds(agent) || candidates.Count == 0 || time < agent.AttemptAfter) return false;
            HashSet<Vector2Int> region = ReachableRegion(agent.Cell, true);
            FenceEdge? best = null;
            Vector2Int approach = agent.Cell;
            float score = float.MaxValue;
            foreach (FenceEdge edge in fences.Edges)
            {
                if (fences.KindAt(edge) != FenceKind.Spiked || fences.IsGate(edge)) continue;
                bool a = region.Contains(edge.CellA), b = region.Contains(edge.CellB);
                if (a == b) continue;
                Vector2Int near = a ? edge.CellA : edge.CellB, far = a ? edge.CellB : edge.CellA;
                if (!climbingNavigation.IsWalkable(far)) continue;
                HashSet<Vector2Int> beyond = ReachableRegion(far, true);
                foreach (CreatureAgent candidate in candidates)
                {
                    if (candidate.CapturedBy != null || !beyond.Contains(candidate.Cell)) continue;
                    float distance = ConstructionRouteGraph.CellDistance(agent.Cell, near)
                        + ConstructionRouteGraph.CellDistance(far, candidate.Cell);
                    if (distance < score) { score = distance; best = edge; approach = near; }
                }
            }
            if (!best.HasValue || !SetGoal(agent, approach)) return false;
            agent.AttemptFence = best;
            agent.Activity = CreatureActivity.Searching;
            if (agent.Route.Count == 0) BeginFailedClimb(agent);
            return true;
        }

        private void Flee(CreatureAgent agent)
        {
            float safe = agent.Definition.fleeSafeDistance;
            if (agent.Route.Count > 0 && agent.Goal.HasValue
                && HorizontalDistance(grid.CellCenter(agent.Goal.Value), agent.LastDanger) >= safe) return;
            Vector2Int best = agent.Cell;
            float bestScore = float.MinValue;
            foreach (Vector2Int candidate in Navigation(agent).Reachable(agent.Cell, Mathf.Max(3, Mathf.CeilToInt(safe / grid.CellSize) + 2)))
            {
                float distance = HorizontalDistance(grid.CellCenter(candidate), agent.LastDanger);
                float score = distance >= safe ? 1000f - ConstructionRouteGraph.CellDistance(agent.Cell, candidate) : distance;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            SetGoal(agent, best);
        }

        private bool Hunt(CreatureAgent agent)
        {
            CreatureAgent prey = agent.Prey;
            if (prey != null && prey.Alive && (prey.CapturedBy == null || prey.CapturedBy == agent))
            {
                bool visible = HorizontalDistance(agent.transform.position, prey.transform.position) <= agent.Definition.visionRange
                    && navigation.HasLineOfSight(agent.Cell, prey.Cell);
                if (visible) { agent.PreyLastSeen = time; agent.LastSeenPreyCell = prey.Cell; }
                if (time - agent.PreyLastSeen <= agent.Definition.preyMemorySeconds && agent.LastSeenPreyCell.HasValue
                    && ReachableRegion(agent.Cell, CanClimbForNeeds(agent)).Contains(agent.LastSeenPreyCell.Value)
                    && SetGoal(agent, agent.LastSeenPreyCell.Value))
                { agent.Activity = visible ? CreatureActivity.Hunting : CreatureActivity.Searching; return true; }
            }
            if (agent.Prey != null) FinishSegment(agent);
            ReleaseClaim(agent);
            var candidates = Visible(agent, agent.Definition.preyTags, agent.Definition.visionRange);
            HashSet<Vector2Int> local = ReachableRegion(agent.Cell);
            candidates.Sort((a, b) =>
            {
                int preference = local.Contains(b.Cell).CompareTo(local.Contains(a.Cell));
                return preference != 0 ? preference : (a.transform.position - agent.transform.position).sqrMagnitude
                    .CompareTo((b.transform.position - agent.transform.position).sqrMagnitude);
            });
            foreach (CreatureAgent candidate in candidates)
                if (candidate.CapturedBy == null && ReachableRegion(agent.Cell, CanClimbForNeeds(agent)).Contains(candidate.Cell) && SetGoal(agent, candidate.Cell))
                {
                    agent.AttemptFence = null;
                    agent.Prey = candidate; agent.PreyLastSeen = time; agent.LastSeenPreyCell = candidate.Cell;
                    agent.Activity = CreatureActivity.Hunting; return true;
                }
            return TryBlockedFence(agent, candidates);
        }

        private void ReleaseClaim(CreatureAgent agent)
        {
            if (agent.Prey != null && agent.Prey.CapturedBy == agent) agent.Prey.CapturedBy = null;
            agent.Prey = null; agent.LastSeenPreyCell = null;
        }

        private void UpdateWellbeing(CreatureAgent agent, float dt)
        {
            CreatureDefinition definition = agent.Definition;
            agent.NeedsTimer -= dt;
            if (agent.NeedsTimer <= 0f)
            {
                agent.NeedsTimer = 0.5f;
                HashSet<Vector2Int> accessible = ReachableRegion(agent.Cell, definition.canClimbWoodenFences);
                agent.AccessibleArea = accessible.Count * grid.CellSize * grid.CellSize;
                int trees = 0;
                foreach (Vector2Int cell in accessible)
                    if (HorizontalDistance(grid.CellCenter(cell), agent.transform.position) <= definition.environmentRadius)
                        trees += environment.TreesAt(cell);
                agent.NearbyTrees = trees;
                int companions = 0;
                foreach (CreatureAgent other in agents)
                    if (other != agent && other.Alive && other.Definition == definition && accessible.Contains(other.Cell)
                        && HorizontalDistance(other.transform.position, agent.transform.position) <= definition.herdRange) companions++;
                agent.Companions = companions;
            }
            agent.Wellbeing.Update(CreatureWellbeing.Evaluate(agent.Vitals.Hunger, agent.Vitals.Fear,
                agent.AccessibleArea, agent.NearbyTrees, agent.Companions, definition.minimumSpace, definition.requiredTrees,
                definition.minimumCompanions, definition.maximumCompanions), dt,
                definition.happinessChangePerSecond, definition.happinessThreshold);
        }

        private List<CreatureAgent> Visible(CreatureAgent observer, CreatureTags tags, float range)
        {
            var result = new List<CreatureAgent>();
            if (tags == CreatureTags.None) return result;
            foreach (CreatureAgent other in agents)
                if (other != observer && other.Alive && (other.Definition.tags & tags) != 0
                    && HorizontalDistance(observer.transform.position, other.transform.position) <= range
                    && navigation.HasLineOfSight(observer.Cell, other.Cell)) result.Add(other);
            return result;
        }

        private HashSet<Vector2Int> ReachableRegion(Vector2Int start, bool climbing = false)
        {
            var cache = climbing ? climbingRegions : reachableRegions;
            if (cache.TryGetValue(start, out var known)) return known;
            var region = new HashSet<Vector2Int>((climbing ? climbingNavigation : navigation).Reachable(start, grid.Width * grid.Depth));
            foreach (Vector2Int cell in region) cache[cell] = region;
            return region;
        }

        private CreatureAgent ClosestVisible(CreatureAgent observer, CreatureTags tags)
        {
            CreatureAgent closest = null; float best = float.MaxValue;
            foreach (CreatureAgent candidate in Visible(observer, tags,
                Mathf.Min(observer.Definition.visionRange, observer.Definition.fearRange)))
            {
                float distance = (observer.transform.position - candidate.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; closest = candidate; }
            }
            return closest;
        }

        private bool SetGoal(CreatureAgent agent, Vector2Int goal, bool walkingOnly = false)
        {
            if (agent.Goal == goal && agent.Route.Count > 0) return true;
            // Preserve the current movement segment when replanning toward moving prey.
            bool moving = agent.Route.Count > 0;
            Vector2Int start = moving ? agent.Route[0] : agent.Cell;
            List<Vector2Int> route = navigation.FindPath(start, goal);
            if (route.Count == 0 && !walkingOnly) route = Navigation(agent).FindPath(start, goal);
            if (route.Count == 0) return false;
            agent.Route.Clear(); agent.Goal = goal;
            for (int i = moving ? 0 : 1; i < route.Count; i++) agent.Route.Add(route[i]);
            return true;
        }

        private void Move(CreatureAgent agent, float dt)
        {
            if (agent.Activity == CreatureActivity.Attacking || agent.Activity == CreatureActivity.Feeding
                || agent.Activity == CreatureActivity.Grazing || agent.Activity == CreatureActivity.Sleeping
                || agent.Activity == CreatureActivity.ClimbingTree || agent.Activity == CreatureActivity.DescendingTree
                || agent.Activity == CreatureActivity.ClimbingFence || agent.Activity == CreatureActivity.FailedClimb
                || agent.Activity == CreatureActivity.Socializing) return;
            if (agent.Route.Count == 0) return;
            Vector2Int next = agent.Route[0];
            if (!navigation.CanCross(agent.Cell, next))
            {
                if (Navigation(agent).CanCross(agent.Cell, next))
                {
                    Vector3 origin = GroundPosition(agent, agent.Cell);
                    agent.transform.position = Vector3.MoveTowards(agent.transform.position, origin, agent.Definition.walkSpeed * dt);
                    if ((agent.transform.position - origin).sqrMagnitude < 0.0001f) BeginFenceClimb(agent, next);
                }
                else { agent.Route.Clear(); agent.Goal = null; agent.transform.position = GroundPosition(agent, agent.Cell); }
                return;
            }
            bool running = agent.Activity == CreatureActivity.Fleeing || agent.Activity == CreatureActivity.Hunting;
            Vector3 goal = GroundPosition(agent, next);
            Vector3 direction = goal - agent.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) agent.transform.rotation = Quaternion.LookRotation(direction);
            float speed = running ? agent.Definition.runSpeed : agent.Definition.walkSpeed
                * Mathf.Lerp(1f, agent.Definition.agitatedSpeedMultiplier, agent.Wellbeing.Agitation);
            agent.transform.position = Vector3.MoveTowards(agent.transform.position, goal, speed * dt);
            if ((agent.transform.position - goal).sqrMagnitude < 0.0001f)
            { agent.Cell = next; agent.Route.RemoveAt(0); }
        }

        private void Feed(CreatureAgent agent, float dt)
        {
            if (agent.Threat != null || agent.Activity == CreatureActivity.Fleeing) return;
            CreatureAgent prey = agent.Prey;
            if (agent.Activity == CreatureActivity.Attacking)
            {
                if (prey == null || !prey.Alive || prey.CapturedBy != agent || !CanInteract(agent, prey))
                { ReleaseClaim(agent); agent.Activity = CreatureActivity.Resting; agent.StateTimer = PauseDuration(agent); return; }
                if (agent.StateTimer > 0f) return;
                ReleaseClaim(prey);
                prey.Alive = false; prey.Activity = CreatureActivity.Dying;
                prey.Route.Clear(); prey.Goal = null;
                prey.CorpseTimer = agent.Definition.feedingSeconds + 1f;
                prey.GetComponent<Collider>().enabled = false;
                agent.Activity = CreatureActivity.Feeding; agent.StateTimer = agent.Definition.feedingSeconds;
                return;
            }
            if (agent.Activity == CreatureActivity.Feeding)
            {
                if (agent.StateTimer > 0f) return;
                agent.Vitals.Eat(agent.Definition.foodRelief);
                ReleaseClaim(agent); agent.Activity = CreatureActivity.Resting;
                agent.StateTimer = PauseDuration(agent); agent.DecisionTimer = 0f;
                return;
            }
            if (agent.Activity == CreatureActivity.Hunting && prey != null && prey.Alive
                && prey.CapturedBy == null && CanInteract(agent, prey)
                && (agent.transform.position - GroundPosition(agent, agent.Cell)).sqrMagnitude < 0.0025f)
            {
                prey.CapturedBy = agent; prey.GrazeTimer = 0f;
                agent.Route.Clear(); agent.Goal = null;
                agent.Activity = CreatureActivity.Attacking; agent.StateTimer = agent.Definition.attackSeconds;
                return;
            }
            if (agent.Activity != CreatureActivity.Grazing || agent.Vitals.Hunger < agent.Definition.huntThreshold) return;
            agent.GrazeTimer += dt;
            if (agent.GrazeTimer >= agent.Definition.grazeSeconds)
            {
                if (grass.TryConsume(agent.Cell, time)) agent.Vitals.Eat(agent.Definition.foodRelief);
                agent.GrazeTimer = 0f; agent.Activity = CreatureActivity.Resting;
                agent.StateTimer = PauseDuration(agent); agent.DecisionTimer = 0f;
            }
        }

        private bool CanInteract(CreatureAgent agent, CreatureAgent target) =>
            HorizontalDistance(agent.transform.position, target.transform.position) <= agent.Definition.interactionRange
            && target.Elevation <= 0.01f && agent.Elevation <= 0.01f
            && target.Activity != CreatureActivity.ClimbingFence && agent.Activity != CreatureActivity.ClimbingFence
            && (agent.Cell == target.Cell || navigation.CanCross(agent.Cell, target.Cell));

        private void RefreshVisual(CreatureAgent agent)
        {
            agent.Label.transform.position = agent.transform.position + Vector3.up * (agent.Definition.bodyScale.y + 0.4f);
            agent.Label.transform.rotation = camera.transform.rotation;
            bool agitated = !agent.ExternallyControlled && agent.Wellbeing.Agitation > 0f;
            if (agent.ExternallyControlled)
            {
                string state = !agent.Alive ? "Dying" : agent.CapturedBy != null ? "Captured"
                    : string.IsNullOrEmpty(agent.ExternalStatus) ? agent.Activity.ToString() : agent.ExternalStatus;
                agent.Label.text = $"{agent.Definition.displayName} - {state}"
                    + $"\nHunger {agent.Vitals.Hunger:0} | Fear {agent.Vitals.Fear:0} | Energy {agent.Energy.Energy:0}";
            }
            else
                agent.Label.text = $"{agent.Definition.displayName} - {agent.Activity}" + (agitated ? " (Agitated)" : "")
                    + $"\nHappiness {agent.Wellbeing.Happiness:0} | Hunger {agent.Vitals.Hunger:0} | Fear {agent.Vitals.Fear:0}"
                    + $"\nEnergy {agent.Energy.Energy:0}";
            agent.Label.color = agitated ? new Color(1f, 0.4f, 0.2f)
                : agent.Activity == CreatureActivity.Fleeing ? new Color(1f, 0.65f, 0.2f) : Color.white;
            Animate(agent);
            agent.Grass.SetActive(!agent.ExternallyControlled && agent.Alive && agent.CapturedBy == null && agent.Activity == CreatureActivity.Grazing);
            agent.Grass.transform.position = grid.CellCenter(agent.Cell) + Vector3.up * 0.15f;
        }

        private void Animate(CreatureAgent agent)
        {
            float wave = Mathf.Sin(time * 9f);
            Vector3 offset = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            if (!agent.Alive) rotation = Quaternion.Euler(0f, 0f, 85f);
            else if (agent.CapturedBy != null) rotation = Quaternion.Euler(wave * 12f, 0f, wave * 8f);
            else if (agent.Activity == CreatureActivity.Attacking)
            {
                rotation = Quaternion.Euler(25f + wave * 15f, 0f, 0f);
                offset = Vector3.forward * (0.15f + 0.1f * wave);
            }
            else if (agent.Activity == CreatureActivity.Feeding || agent.Activity == CreatureActivity.Grazing)
                rotation = Quaternion.Euler(18f + wave * 12f, 0f, 0f);
            else if (agent.ExternallyControlled && agent.Activity == CreatureActivity.Resting
                && agent.ExternalStatus != null && agent.ExternalStatus.StartsWith("Resting", StringComparison.Ordinal))
            {
                offset = Vector3.down * 0.3f;
                rotation = Quaternion.Euler(-12f + Mathf.Sin(time * 2f) * 2f, 0f, 0f);
            }
            else if (agent.Activity == CreatureActivity.Sleeping)
            {
                rotation = Quaternion.Euler(0f, 0f, 75f);
                offset = new Vector3(agent.Elevation > 0f ? 0.55f : 0f,
                    -agent.Definition.bodyScale.y * 0.45f + Mathf.Sin(time * 2f) * 0.025f, 0f);
            }
            else if (agent.Activity == CreatureActivity.ClimbingTree || agent.Activity == CreatureActivity.DescendingTree)
            {
                rotation = Quaternion.Euler(-25f + wave * 10f, 0f, wave * 6f);
                offset = new Vector3(0.5f, 0f, 0f);
            }
            else if (agent.Activity == CreatureActivity.ClimbingFence)
                rotation = Quaternion.Euler(-25f + wave * 14f, 0f, 0f);
            else if (agent.Activity == CreatureActivity.FailedClimb)
            {
                float progress = 1f - Mathf.Clamp01(agent.StateTimer / Mathf.Max(0.1f, agent.TraversalDuration));
                float reach = Mathf.Sin(progress * Mathf.PI);
                offset = Vector3.up * (reach * 0.8f) + Vector3.forward * (reach * 0.25f);
                rotation = Quaternion.Euler(-reach * 40f, 0f, wave * 8f);
            }
            else if (agent.Activity == CreatureActivity.Socializing)
            {
                if (agent.SocialPartner != null)
                {
                    Vector3 direction = agent.SocialPartner.transform.position - agent.transform.position;
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 0.001f) agent.transform.rotation = Quaternion.LookRotation(direction);
                }
                rotation = Quaternion.Euler(0f, wave * 12f, 0f);
                offset = Vector3.up * Mathf.Abs(wave) * 0.06f;
            }
            else if (agent.Route.Count > 0) offset = Vector3.up * Mathf.Abs(wave) * 0.06f;
            if (!agent.Alive) offset = Vector3.down * agent.Definition.bodyScale.y * 0.5f;
            agent.Body.localPosition = offset; agent.Body.localRotation = rotation;
            Color color = Color.Lerp(agent.Definition.color, new Color(0.9f, 0.15f, 0.1f), agent.Wellbeing.Agitation * 0.7f);
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color);
            agent.BodyRenderer.SetPropertyBlock(properties);
        }

        private void TickDispensers(float dt)
        {
            var active = new HashSet<ParkBuildingRecord>();
            foreach (ParkBuildingRecord building in buildings.Records)
            {
                BuildingDefinition definition = building.Footprint.Definition;
                if (definition.kind != BuildingKind.GoatDispenser) continue;
                active.Add(building);
                if (!dispenserTimers.TryGetValue(building, out float remaining)) remaining = definition.dispenseInterval;
                // Respect each building's operating state; dispensers do not require park paths.
                if (building.IsOpen) remaining -= dt;
                if (remaining <= 0f)
                {
                    int living = 0;
                    foreach (CreatureAgent agent in agents) if (agent.Alive && agent.SourceDispenser == building) living++;
                    CreatureDefinition goat = definition.dispensedCreature != null ? definition.dispensedCreature : defaultGoat;
                    if (living < definition.maximumDispensed && goat != null)
                        foreach (Vector2Int cell in navigation.Reachable(building.Footprint.AccessCell, 2))
                            if (TrySpawn(goat, cell, building)) break;
                    remaining = definition.dispenseInterval;
                }
                dispenserTimers[building] = remaining;
            }
            var obsolete = new List<ParkBuildingRecord>();
            foreach (ParkBuildingRecord record in dispenserTimers.Keys) if (!active.Contains(record)) obsolete.Add(record);
            foreach (ParkBuildingRecord record in obsolete) dispenserTimers.Remove(record);
        }

        private Vector3 GroundPosition(CreatureAgent agent, Vector2Int cell) =>
            grid.CellCenter(cell) + Vector3.up * agent.Definition.bodyScale.y;
        private static float HorizontalDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (grassMaterial != null) UnityEngine.Object.Destroy(grassMaterial);
        }
    }
}

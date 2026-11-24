using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    internal sealed class CreatureFlightRuntime
    {
        internal float Altitude;
        internal bool LandingForSleep;
        internal Transform Body, LeftWing, RightWing, LeftEye, RightEye;
        internal Quaternion LeftRest, RightRest;
        internal LineRenderer LeftBeam, RightBeam;
        internal Vector3 BeamTarget;
        internal float BeamUntil;
    }

    public sealed partial class CreatureWorld
    {
        private sealed class FlightRouteCache
        {
            internal int Settings;
            internal CreatureNavigation Navigation;
            internal readonly Dictionary<Vector2Int, bool> Clearance = new Dictionary<Vector2Int, bool>();
            internal readonly Dictionary<Vector2Int, HashSet<Vector2Int>> Regions = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        }
        private readonly Dictionary<CreatureDefinition, FlightRouteCache> flightRoutes = new Dictionary<CreatureDefinition, FlightRouteCache>();
        private Func<Vector2Int, Vector2Int, bool> flightCrossing;
        private Func<Vector2Int, float> flightCeiling;
        private Func<int> flightContainmentRevision;
        private int flightGridRevision = -1, flightFenceRevision = -1, flightRoofRevision = -1;
        private Material flightLightningMaterial;

        /// <summary>Containment comes from physical completed roofs, never a creature/enclosure assignment.</summary>
        public void ConfigureFlightContainment(Func<Vector2Int, Vector2Int, bool> canCross,
            Func<Vector2Int, float> ceilingAt, Func<int> revision = null)
        {
            flightCrossing = canCross; flightCeiling = ceilingAt; flightContainmentRevision = revision;
            flightRoutes.Clear(); flightRoofRevision = -1;
        }

        private void RefreshFlightRoutes()
        {
            int roofRevision = flightContainmentRevision?.Invoke() ?? 0;
            if (flightGridRevision == grid.Revision && flightFenceRevision == fences.Revision && flightRoofRevision == roofRevision) return;
            foreach (var cache in flightRoutes.Values) { cache.Clearance.Clear(); cache.Regions.Clear(); }
            flightGridRevision = grid.Revision; flightFenceRevision = fences.Revision; flightRoofRevision = roofRevision;
        }

        private static int FlightSettingsKey(CreatureDefinition definition)
        {
            var settings = definition.Flight;
            unchecked
            {
                int value = settings.cruiseHeight.GetHashCode();
                value = value * 397 ^ settings.bodyHeadroom.GetHashCode();
                value = value * 397 ^ settings.roofClearance.GetHashCode();
                value = value * 397 ^ settings.maximumHeight.GetHashCode();
                value = value * 397 ^ settings.clearanceRadius.GetHashCode();
                return value * 397 ^ definition.bodyScale.y.GetHashCode();
            }
        }

        private FlightRouteCache FlightCache(CreatureDefinition definition)
        {
            RefreshFlightRoutes(); int key = FlightSettingsKey(definition);
            if (!flightRoutes.TryGetValue(definition, out var cache) || cache.Settings != key)
            {
                cache = new FlightRouteCache { Settings = key };
                var captured = cache;
                cache.Navigation = new CreatureNavigation(grid, fences,
                    cell => FlightCellClear(definition, captured, cell), canFly: true,
                    flightCrossingAllowed: (from, to) => FlightCrossingAllowed(definition, from, to));
                flightRoutes[definition] = cache;
            }
            return cache;
        }

        private bool FlightCellClear(CreatureDefinition definition, FlightRouteCache cache, Vector2Int cell)
        {
            if (cache.Clearance.TryGetValue(cell, out bool clear)) return clear;
            Vector3 point = FlightBase(definition, grid.CellCenter(cell));
            point.y += Mathf.Min(definition.Flight.cruiseHeight, MaximumFlightAltitude(definition, cell));
            float radius = definition.Flight.clearanceRadius;
            clear = !Physics.CheckBox(point, new Vector3(radius, radius, radius), Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            cache.Clearance[cell] = clear; return clear;
        }

        private CreatureNavigation FlyingNavigation(CreatureAgent actor) => FlightCache(actor.Definition).Navigation;
        private HashSet<Vector2Int> FlyingRegion(CreatureAgent actor, Vector2Int start)
        {
            var cache = FlightCache(actor.Definition);
            if (cache.Regions.TryGetValue(start, out var region)) return region;
            using var profile = Profiles.RegionSearch.Auto();
            region = new HashSet<Vector2Int>(cache.Navigation.Reachable(start, grid.Width * grid.Depth));
            foreach (var cell in region) cache.Regions[cell] = region;
            return region;
        }

        private Vector3 FlightBase(CreatureDefinition definition, Vector3 point)
        {
            point = grid.ProjectToGround(point, definition.bodyScale.y);
            var cell = grid.WorldToCell(point);
            // A grounded or tranquilized bird stays on the surface rather than being
            // projected into an excavated pond floor during staff-controlled movement.
            if (grid.Contains(cell) && grid.Habitat.IsWater(cell))
                point.y = Mathf.Max(point.y, grid.Habitat.WaterLevel(cell) + definition.bodyScale.y);
            return point;
        }

        private float MaximumFlightAltitude(CreatureDefinition definition, Vector2Int cell)
        {
            float desired = Mathf.Max(definition.Flight.cruiseHeight, definition.Flight.maximumHeight);
            float roof = flightCeiling?.Invoke(cell) ?? float.PositiveInfinity;
            if (float.IsInfinity(roof)) return desired;
            if (float.IsNaN(roof)) return 0;
            float origin = FlightBase(definition, grid.CellCenter(cell)).y;
            return Mathf.Min(desired, Mathf.Max(0, roof - origin - definition.Flight.bodyHeadroom - definition.Flight.roofClearance));
        }

        private static bool FlightEnabled(CreatureAgent actor) => actor != null && actor.Definition.Flight.canFly;
        private static bool FlightControlled(CreatureAgent actor) => !actor.Alive || actor.IsSedated || actor.CarriedBy != null
            || actor.CapturedBy != null || actor.ExternallyControlled || actor.AttractionPoseControlled;
        private static bool FlightLandedActivity(CreatureActivity activity) => activity == CreatureActivity.Sleeping
            || activity == CreatureActivity.ClimbingTree || activity == CreatureActivity.DescendingTree
            || activity == CreatureActivity.Feeding
            || activity == CreatureActivity.SeekingWater || activity == CreatureActivity.Drinking || activity == CreatureActivity.Grazing;

        private CreatureFlightRuntime FlightState(CreatureAgent actor)
        {
            if (actor.FlightState == null) actor.FlightState = new CreatureFlightRuntime();
            return actor.FlightState;
        }

        private void TickFlightMotion(CreatureAgent actor, float dt)
        {
            if (!FlightEnabled(actor)) return;
            var state = FlightState(actor); float desired = 0;
            bool landed = FlightLandedActivity(actor.Activity) && !(actor.Activity == CreatureActivity.SeekingWater && actor.Route.Count > 0);
            if (!FlightControlled(actor) && !(state.LandingForSleep && actor.Activity == CreatureActivity.SeekingSleep) && !landed)
            {
                desired = actor.Definition.Flight.cruiseHeight;
                if (actor.Activity == CreatureActivity.FailedFlight) desired = MaximumFlightAltitude(actor.Definition, actor.Cell);
                if (actor.Route.Count > 0)
                {
                    var edge = CrossingEdge(actor.Cell, actor.Route[0]);
                    if (fences.HasFence(edge) && FlightCrossingAllowed(actor.Definition, actor.Cell, actor.Route[0]))
                        desired = Mathf.Max(desired, FenceConstruction.HeightFor(fences.KindAt(edge)) + actor.Definition.Flight.roofClearance);
                }
                desired = Mathf.Min(desired, MaximumFlightAltitude(actor.Definition, actor.Cell));
            }
            float speed = desired > state.Altitude ? actor.Definition.Flight.riseSpeed : actor.Definition.Flight.descentSpeed;
            state.Altitude = FlightControlled(actor) ? 0 : Mathf.MoveTowards(state.Altitude, desired, speed * dt);
            if (actor.CarriedBy != null || actor.CapturedBy != null || actor.AttractionPoseControlled) return;
            if (Mathf.Abs(actor.Elevation) > .001f || actor.Activity == CreatureActivity.ClimbingTree || actor.Activity == CreatureActivity.DescendingTree) return;
            actor.transform.position = ProjectFlightPosition(actor, actor.transform.position);
        }

        public float FlightAltitude(CreatureAgent actor) => !FlightEnabled(actor) || FlightControlled(actor) ? 0
            : Mathf.Min(actor.FlightState?.Altitude ?? 0, MaximumFlightAltitude(actor.Definition, grid.WorldToCell(actor.transform.position)));

        private bool TryLandBeforeSleep(CreatureAgent actor)
        {
            if (!FlightEnabled(actor) || FlightControlled(actor)) return false;
            var state = FlightState(actor);
            if (actor.Activity == CreatureActivity.Sleeping || actor.Activity == CreatureActivity.ClimbingTree || actor.Activity == CreatureActivity.DescendingTree)
            { state.LandingForSleep = false; return false; }
            var mode = actor.Definition.Shelter.mode;
            if (mode != CreatureShelterMode.Ground && mode != CreatureShelterMode.Legacy
                && FindShelterSite(actor, actor.ShelterRetreat, out var source, out var stand))
            {
                actor.SleepTree = actor.ShelterCell = source; actor.HasShelter = true;
                if (actor.Cell != stand || actor.Route.Count > 0)
                {
                    state.LandingForSleep = false; actor.Activity = CreatureActivity.SeekingSleep;
                    SetGoal(actor, stand); return true;
                }
            }
            if (state.Altitude <= .01f) { state.LandingForSleep = false; return false; }
            state.LandingForSleep = true; actor.WantsSleep = true;
            actor.Activity = CreatureActivity.SeekingSleep;
            actor.Route.Clear(); actor.Goal = null;
            return true;
        }

        private bool TryLandForTreeTraversal(CreatureAgent actor)
        {
            if (!FlightEnabled(actor) || FlightControlled(actor) || (actor.FlightState?.Altitude ?? 0) <= .01f) return false;
            FlightState(actor).LandingForSleep = true;
            actor.Activity = CreatureActivity.SeekingSleep; actor.Route.Clear(); actor.Goal = null;
            return true;
        }

        private bool FlightCrossingAllowed(CreatureDefinition definition, Vector2Int from, Vector2Int to)
        {
            if (flightCrossing != null && !flightCrossing(from, to)) return false;
            var edge = CrossingEdge(from, to);
            if (!fences.HasFence(edge)) return true;
            float required = FenceConstruction.HeightFor(fences.KindAt(edge)) + definition.Flight.roofClearance;
            return MaximumFlightAltitude(definition, from) >= required && MaximumFlightAltitude(definition, to) >= required;
        }

        private bool PauseForFlightTakeoff(CreatureAgent actor)
        {
            if (!FlightEnabled(actor) || FlightControlled(actor) || actor.Route.Count == 0) return false;
            var next = actor.Route[0]; var edge = CrossingEdge(actor.Cell, next);
            if (!fences.HasFence(edge) || !FlightCrossingAllowed(actor.Definition, actor.Cell, next)) return false;
            return FlightAltitude(actor) + .02f < FenceConstruction.HeightFor(fences.KindAt(edge)) + actor.Definition.Flight.roofClearance;
        }

        private bool CanAttemptFlightEscape(CreatureAgent actor) => FlightEnabled(actor)
            && actor.EscapeIntent && time < actor.EscapeUntil;

        private bool TryBlockedFlight(CreatureAgent actor, FenceEdge edge, Vector2Int next)
        {
            if (!FlightEnabled(actor) || flightCrossing == null || flightCrossing(actor.Cell, next)) return false;
            actor.AttemptFence = edge; actor.Activity = CreatureActivity.FailedFlight;
            actor.TraversalDuration = Mathf.Max(.1f, actor.Definition.Flight.failedEscapeSeconds);
            actor.StateTimer = actor.TraversalDuration;
            actor.AttemptAfter = time + actor.Definition.climbRetrySeconds;
            return true;
        }

        public void RestoreFlightAltitude(CreatureAgent actor, float altitude)
        {
            if (!FlightEnabled(actor)) return;
            FlightState(actor).Altitude = float.IsNaN(altitude) || float.IsInfinity(altitude) || FlightControlled(actor)
                || FlightLandedActivity(actor.Activity) ? 0 : Mathf.Clamp(altitude, 0, MaximumFlightAltitude(actor.Definition, actor.Cell));
            if (Mathf.Abs(actor.Elevation) <= .001f) actor.transform.position = ProjectFlightPosition(actor, actor.transform.position);
        }

        private Vector3 ProjectFlightPosition(CreatureAgent actor, Vector3 point)
        {
            point = FlightBase(actor.Definition, point);
            point.y += Mathf.Min(FlightAltitude(actor), MaximumFlightAltitude(actor.Definition, grid.WorldToCell(point)));
            return point;
        }

        private float FlightSpeed(CreatureAgent actor, bool running, float fallback) => FlightEnabled(actor)
            && FlightAltitude(actor) > .05f ? running ? actor.Definition.Flight.pursuitSpeed : actor.Definition.Flight.flightSpeed : fallback;

        private bool FlightCanInteract(CreatureAgent attacker, CreatureAgent target)
            => FlightAltitude(target) <= attacker.Definition.interactionRange || FlightEnabled(attacker);

        private void TriggerFlightAttack(CreatureAgent actor, CreatureAgent target)
        {
            if (!FlightEnabled(actor) || !actor.Definition.Flight.lightningAttack || target == null) return;
            var state = FlightState(actor); BindFlightVisuals(actor, state);
            if (flightLightningMaterial == null)
                flightLightningMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { name = "Creature eye lightning" };
            state.LeftBeam = state.LeftBeam != null ? state.LeftBeam : CreateFlightBeam(actor, "Left eye lightning");
            state.RightBeam = state.RightBeam != null ? state.RightBeam : CreateFlightBeam(actor, "Right eye lightning");
            state.BeamTarget = target.transform.position; state.BeamUntil = time + actor.Definition.Flight.lightningSeconds;
        }

        private LineRenderer CreateFlightBeam(CreatureAgent actor, string name)
        {
            var item = new GameObject(name); item.transform.SetParent(actor.transform, false);
            var line = item.AddComponent<LineRenderer>(); line.sharedMaterial = flightLightningMaterial;
            line.positionCount = 2; line.useWorldSpace = true; line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            line.enabled = false; return line;
        }

        private void BindFlightVisuals(CreatureAgent actor, CreatureFlightRuntime state)
        {
            if (state.Body == actor.Body) return;
            state.Body = actor.Body;
            state.LeftWing = actor.Body.Find("Left wing"); state.RightWing = actor.Body.Find("Right wing");
            state.LeftEye = actor.Body.Find("Left lightning eye"); state.RightEye = actor.Body.Find("Right lightning eye");
            state.LeftRest = state.LeftWing != null ? state.LeftWing.localRotation : Quaternion.identity;
            state.RightRest = state.RightWing != null ? state.RightWing.localRotation : Quaternion.identity;
        }

        private void AnimateFlight(CreatureAgent actor)
        {
            if (!FlightEnabled(actor) || actor.Body == null) return;
            var state = FlightState(actor); BindFlightVisuals(actor, state);
            var settings = actor.Definition.Flight;
            bool flying = !FlightControlled(actor) && (FlightAltitude(actor) > .05f && !FlightLandedActivity(actor.Activity)
                || actor.Activity == CreatureActivity.ClimbingTree || actor.Activity == CreatureActivity.DescendingTree);
            float beat = flying ? Mathf.Sin(time * settings.wingBeatFrequency * Mathf.PI * 2f) : 0;
            if (state.LeftWing != null) state.LeftWing.localRotation = state.LeftRest * Quaternion.Euler(0, beat * settings.wingSweepDegrees, beat * settings.wingBeatDegrees);
            if (state.RightWing != null) state.RightWing.localRotation = state.RightRest * Quaternion.Euler(0, -beat * settings.wingSweepDegrees, -beat * settings.wingBeatDegrees);
            bool beamVisible = actor.Alive && !actor.IsSedated && time < state.BeamUntil;
            AnimateBeam(state.LeftBeam, state.LeftEye, actor, state.BeamTarget, beamVisible);
            AnimateBeam(state.RightBeam, state.RightEye, actor, state.BeamTarget, beamVisible);
        }

        private void AnimateBeam(LineRenderer beam, Transform eye, CreatureAgent actor, Vector3 target, bool visible)
        {
            if (beam == null) return;
            beam.enabled = visible;
            if (!visible) return;
            var settings = actor.Definition.Flight;
            beam.startWidth = beam.endWidth = settings.lightningWidth;
            beam.startColor = beam.endColor = settings.lightningColor;
            bodyProperties.SetColor("_BaseColor", settings.lightningColor); bodyProperties.SetColor("_Color", settings.lightningColor);
            beam.SetPropertyBlock(bodyProperties);
            beam.SetPosition(0, eye != null ? eye.position : actor.Body.position + actor.transform.forward * .35f);
            beam.SetPosition(1, target);
        }

        private void DisposeFlight()
        {
            flightRoutes.Clear();
            if (flightLightningMaterial != null) UnityEngine.Object.Destroy(flightLightningMaterial);
        }
    }
}

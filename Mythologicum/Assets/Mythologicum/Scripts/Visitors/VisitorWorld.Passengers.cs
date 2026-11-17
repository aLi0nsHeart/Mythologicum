using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class VisitorWorld
    {
        private Func<ParkBuildingRecord, AttractionVisualAnimator> attractionVisuals;
        private readonly Dictionary<ParkBuildingRecord, AttractionRideCycle> rideCycles = new Dictionary<ParkBuildingRecord, AttractionRideCycle>();
        private readonly List<ParkBuildingRecord> obsoleteRides = new List<ParkBuildingRecord>();

        public void ConfigureAttractionVisuals(Func<ParkBuildingRecord, AttractionVisualAnimator> visuals) => attractionVisuals = visuals;
        public static bool IsBatchRide(BuildingDefinition definition) => definition?.attraction?.kind == AttractionKind.BalloonRide
            || definition?.attraction?.kind == AttractionKind.ClockworkCarousel;
        private static bool UsesInterior(BuildingDefinition definition) => definition != null && definition.IsAttraction
            && definition.kind != BuildingKind.PettingZoo && definition.kind != BuildingKind.ViewingWindow;

        public bool CanDemolishAttraction(ParkBuildingRecord building)
        {
            foreach (var guest in visitors)
                if (guest.Actor != null && guest.Actor.Alive && guest.Attraction?.Building == building && guest.Attraction.InsideInterior) return false;
            return true;
        }

        private AttractionRideCycle RideFor(ParkBuildingRecord building)
        {
            if (!rideCycles.TryGetValue(building, out var ride))
            { ride = new AttractionRideCycle(building.Footprint.Definition); rideCycles.Add(building, ride); }
            return ride;
        }
        public AttractionRideSave CaptureRide(ParkBuildingRecord building) => building != null && IsBatchRide(building.Footprint.Definition)
            ? RideFor(building).Capture() : null;
        public void RestoreRide(ParkBuildingRecord building, AttractionRideSave data)
        {
            if (data == null) return;
            RideFor(building).Restore(data); SynchronizeRideVisual(building, RideFor(building));
        }

        private AttractionVisualAnimator VisualFor(ParkBuildingRecord building)
        {
            if (building == null) return null;
            var visual = attractionVisuals?.Invoke(building);
            if (visual != null) visual.BindSimulation();
            return visual;
        }
        private void SynchronizeRideVisual(ParkBuildingRecord building, AttractionRideCycle ride)
        {
            var visual = VisualFor(building);
            if (visual != null) visual.SetRideProgress(ride.Progress, ride.Phase == AttractionRidePhase.Riding);
        }

        private void TickRideCycles(float dt)
        {
            obsoleteRides.Clear();
            foreach (var pair in rideCycles)
            {
                int boarded = 0, reserved = 0;
                foreach (var guest in visitors)
                {
                    var visit = guest.Attraction;
                    if (guest.Actor == null || !guest.Actor.Alive || visit?.Building != pair.Key || !visit.InsideInterior) continue;
                    reserved++;
                    if (visit.Phase == VisitorAttractionPhase.WaitingForRide || visit.Phase == VisitorAttractionPhase.Visiting) boarded++;
                }
                pair.Value.Tick(dt, boarded, reserved, boarded == reserved);
                if (pair.Value.Phase == AttractionRidePhase.Riding)
                    foreach (var guest in visitors)
                        if (guest.Attraction?.Building == pair.Key && guest.Attraction.Phase == VisitorAttractionPhase.WaitingForRide)
                        {
                            guest.Attraction.Phase = VisitorAttractionPhase.Visiting;
                            SetActivity(guest, VisitorActivity.EnjoyingAttraction, "visitor.attraction.enjoying", AttractionName(guest.Attraction));
                        }
                SynchronizeRideVisual(pair.Key, pair.Value);
                if (reserved == 0 && !buildings.Contains(pair.Key)) obsoleteRides.Add(pair.Key);
            }
            foreach (var building in obsoleteRides) rideCycles.Remove(building);
        }

        private void ConfigureInterior(VisitorAttractionVisit visit)
        {
            if (!UsesInterior(visit.Building?.Footprint.Definition)) return;
            visit.HasInterior = true; visit.Interior = visit.Building.Footprint.DoorCell;
            visit.SeatCapacity = Math.Max(1, AttractionCapacity(visit));
        }
        private void ReserveSeat(VisitorAgent guest, VisitorAttractionVisit visit)
        {
            if (!visit.HasInterior || visit.Seat >= 0) return;
            for (int slot = 0; slot < visit.SeatCapacity; slot++)
            {
                bool occupied = false;
                foreach (var other in visitors)
                    if (other != guest && other.Actor != null && other.Actor.Alive && other.Attraction?.Building == visit.Building && other.Attraction.Seat == slot)
                    { occupied = true; break; }
                if (!occupied) { visit.Seat = slot; return; }
            }
        }

        private void BeginInteriorEntry(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (IsBatchRide(visit.Building.Footprint.Definition) && !RideFor(visit.Building).CanBoard)
            { SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.waiting_to_board"); return; }
            if (IsBatchRide(visit.Building.Footprint.Definition) && visit.SeatCapacity != RideFor(visit.Building).Capacity)
            { visit.Seat = -1; visit.SeatCapacity = RideFor(visit.Building).Capacity; }
            ReserveSeat(guest, visit);
            if (visit.Seat < 0) { CancelAttraction(guest, false); return; }
            if (!visit.HasQuote) { visit.QuotedPrice = AttractionPrice(visit); visit.HasQuote = true; }
            visit.InsideInterior = true; visit.Phase = VisitorAttractionPhase.EnteringAttraction;
            guest.StateTimer = Math.Max(.1f, AttractionSettings(visit).entrySeconds);
            guest.Actor.Route.Clear(); guest.Actor.Goal = null; guest.Actor.Cell = visit.Interior; guest.Actor.AttractionHost = visit.Building;
            guest.Actor.AttractionPoseControlled = true;
            SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.entering", AttractionName(visit));
            ApplyPassengerPose(guest);
        }

        private void BeginInteriorExit(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (visit.Phase == VisitorAttractionPhase.LeavingAttraction) return;
            visit.TransitionOrigin = guest.Actor.transform.position;
            visit.OrphanExit = visit.Building == null || !buildings.Contains(visit.Building);
            visit.Phase = VisitorAttractionPhase.LeavingAttraction;
            guest.StateTimer = Math.Max(.1f, AttractionSettings(visit).exitSeconds);
            guest.Actor.Route.Clear(); guest.Actor.Goal = null;
            SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.leaving", AttractionName(visit));
        }

        private bool TickInteriorTransition(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (visit == null || !visit.HasInterior || !visit.InsideInterior) return false;
            bool riding = visit.Building != null && IsBatchRide(visit.Building.Footprint.Definition)
                && RideFor(visit.Building).Phase == AttractionRidePhase.Riding;
            if (visit.Phase != VisitorAttractionPhase.LeavingAttraction
                && (visit.Interrupted || NeedsService(guest) || !VenueExists(visit)
                    || guest.DecisionTimer <= 0 && !EvaluateAttraction(guest, visit, out _)))
            {
                visit.Interrupted = true;
                if (!riding || !buildings.Contains(visit.Building)) BeginInteriorExit(guest);
            }
            if (visit.Phase == VisitorAttractionPhase.LeavingAttraction)
            {
                if (guest.StateTimer <= 0) FinishAttraction(guest); else ApplyPassengerPose(guest);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.EnteringAttraction && guest.StateTimer <= 0)
            {
                if (IsBatchRide(visit.Building.Footprint.Definition))
                {
                    visit.Phase = VisitorAttractionPhase.WaitingForRide;
                    SetActivity(guest, VisitorActivity.EnjoyingAttraction, "visitor.attraction.waiting_to_depart");
                }
                else BeginAttractionVisit(guest);
            }
            ApplyPassengerPose(guest);
            if (visit.Phase == VisitorAttractionPhase.Visiting && visit.Building != null && IsBatchRide(visit.Building.Footprint.Definition))
            {
                var cycle = RideFor(visit.Building);
                guest.StateTimer = cycle.Phase == AttractionRidePhase.Unloading ? 0 : Math.Max(.001f, cycle.RideSeconds - cycle.Elapsed);
            }
            return visit.Phase != VisitorAttractionPhase.Visiting || visit.Interrupted;
        }

        private Vector3 PassengerSeat(VisitorAgent guest)
        {
            var visit = guest.Attraction; var visual = VisualFor(visit.Building);
            if (visual != null) return visual.PassengerPosition(visit.Seat, visit.SeatCapacity, guest.Actor.Definition.bodyScale.y);
            var center = visit.Building != null ? visit.Building.Footprint.Center(grid) : grid.CellCenter(visit.Interior);
            float height = visit.Building?.Footprint.Definition.attraction.kind == AttractionKind.ViewingTower
                ? visit.Building.Footprint.Definition.attraction.rideHeight : 0;
            return center + Vector3.up * (height + guest.Actor.Definition.bodyScale.y);
        }
        private Vector3 EntryPose(VisitorAgent guest, float progress)
        {
            var visit = guest.Attraction; var visual = VisualFor(visit.Building);
            Vector3 outside = Position(guest.Actor, visit.Access);
            if (visual == null) return Vector3.Lerp(outside, PassengerSeat(guest), Mathf.Clamp01(progress));
            // First walk off the public path to the door, then follow the authored/generated interior route.
            if (progress < .2f) return Vector3.Lerp(outside, visual.EntrancePosition + Vector3.up * guest.Actor.Definition.bodyScale.y, progress * 5);
            return visual.EntryPosition((progress - .2f) / .8f, visit.Seat, visit.SeatCapacity, guest.Actor.Definition.bodyScale.y);
        }
        private void ApplyPassengerPose(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (visit == null || !visit.InsideInterior) return;
            guest.Actor.AttractionWalking = visit.Phase == VisitorAttractionPhase.EnteringAttraction || visit.Phase == VisitorAttractionPhase.LeavingAttraction;
            Vector3 previous = guest.Actor.transform.position, position;
            if (visit.Phase == VisitorAttractionPhase.EnteringAttraction)
                position = EntryPose(guest, 1 - Mathf.Clamp01(guest.StateTimer / Math.Max(.1f, AttractionSettings(visit).entrySeconds)));
            else if (visit.Phase == VisitorAttractionPhase.LeavingAttraction)
            {
                float remaining = Mathf.Clamp01(guest.StateTimer / Math.Max(.1f, AttractionSettings(visit).exitSeconds));
                position = visit.OrphanExit ? Vector3.Lerp(Position(guest.Actor, visit.Access), visit.TransitionOrigin, remaining) : EntryPose(guest, remaining);
            }
            else position = PassengerSeat(guest);
            guest.Actor.transform.position = position;
            guest.Actor.Elevation = Math.Max(0, position.y - Position(guest.Actor, guest.Actor.Cell).y);
            var visual = VisualFor(visit.Building);
            if (visit.Phase == VisitorAttractionPhase.Visiting || visit.Phase == VisitorAttractionPhase.WaitingForRide)
            { if (visual != null) guest.Actor.transform.rotation = visual.PassengerRotation(visit.Seat, visit.SeatCapacity); }
            else
            {
                var direction = position - previous; direction.y = 0;
                if (direction.sqrMagnitude > .0001f) guest.Actor.transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}

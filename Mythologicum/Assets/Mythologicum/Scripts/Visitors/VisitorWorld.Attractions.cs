using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mythologicum
{
    public enum VisitorAttractionPhase { None, Approaching, EnteringPetting, Visiting, ReturningPetting, ExitingPetting, ApproachingPettable,
        EnteringViewingRoom, ExitingViewingRoom, EnteringAttraction, WaitingForRide, LeavingAttraction }

    [Serializable]
    public sealed class VisitorAttractionMemory
    {
        public string id;
        public float remaining;
    }

    /// <summary>Optional visitor-only state. Prices are charged on completed visits, never on restore.</summary>
    [Serializable]
    public sealed class VisitorAttractionSave
    {
        public int building = -1;
        public bool hasWindow, hasPettingPortal, insidePetting, completed, interrupted, performanceSeen;
        public bool hasViewingRoom, insideViewingRoom;
        public bool hasInterior, insideInterior, hasQuote, orphanExit;
        public int seat = -1, seatCapacity;
        public Vector3 transitionOrigin;
        public Vector2Int windowCell, access, interior;
        public bool windowAlongX;
        public VisitorAttractionPhase phase;
        public float remaining, quotedPrice, interest, choiceCooldown;
        public List<VisitorAttractionMemory> history = new List<VisitorAttractionMemory>();
    }

    public static class VisitorAttractionSaveRules
    {
        public static void Validate(VisitorAttractionSave data, IReadOnlyList<ParkBuildingRecord> records,
            SquareBuildGrid grid, FenceLayout fences)
        {
            if (data == null) return;
            bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
            if (!Enum.IsDefined(typeof(VisitorAttractionPhase), data.phase) || !Finite(data.remaining) || !Finite(data.quotedPrice)
                || !Finite(data.interest) || data.interest > 1 || !Finite(data.choiceCooldown) || data.building < -1 || data.building >= records.Count)
                throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (data.history != null)
            {
                if (data.history.Count > 10000) throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                foreach (var item in data.history)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || item.id.Length > 128 || !Finite(item.remaining) || !unique.Add(item.id))
                        throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                    foreach (char c in item.id) if (char.IsControl(c)) throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                }
            }
            if (data.phase == VisitorAttractionPhase.None)
            {
                if (data.building >= 0 || data.hasWindow || data.hasPettingPortal || data.insidePetting || data.hasViewingRoom || data.insideViewingRoom
                    || data.hasInterior || data.insideInterior || data.completed)
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                return;
            }
            bool pettingMovement = data.phase == VisitorAttractionPhase.EnteringPetting || data.phase == VisitorAttractionPhase.ReturningPetting
                || data.phase == VisitorAttractionPhase.ExitingPetting || data.phase == VisitorAttractionPhase.ApproachingPettable;
            if (data.insidePetting && !data.hasPettingPortal || pettingMovement && !data.hasPettingPortal
                || data.completed && data.phase != VisitorAttractionPhase.ReturningPetting && data.phase != VisitorAttractionPhase.ExitingPetting
                    && data.phase != VisitorAttractionPhase.ExitingViewingRoom && data.phase != VisitorAttractionPhase.LeavingAttraction
                || data.insidePetting && (data.phase == VisitorAttractionPhase.Approaching || data.phase == VisitorAttractionPhase.EnteringPetting)
                || data.hasPettingPortal && !data.insidePetting && (data.phase == VisitorAttractionPhase.Visiting
                    || data.phase == VisitorAttractionPhase.ReturningPetting || data.phase == VisitorAttractionPhase.ExitingPetting
                    || data.phase == VisitorAttractionPhase.ApproachingPettable))
                throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (!grid.Contains(data.access) || data.hasPettingPortal && (!grid.Contains(data.interior)
                || (ConstructionRouteGraph.CellDistance(data.access, data.interior) != 1
                    && ConstructionRouteGraph.CellDistance(data.access, data.interior) != 2))) throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (data.building >= 0 && !records[data.building].Footprint.Definition.IsAttraction) throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (data.hasWindow)
            {
                var edge = new FenceEdge(data.windowCell, data.windowAlongX);
                if (data.building >= 0 || data.hasPettingPortal || !fences.HasWindow(edge) || data.access != edge.CellA && data.access != edge.CellB)
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            }
            if (data.building < 0 && !data.hasWindow && !(data.hasPettingPortal && data.insidePetting)
                && !(data.hasViewingRoom && data.insideViewingRoom) && !(data.hasInterior && data.insideInterior)) throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (data.hasPettingPortal && data.building >= 0)
            {
                var footprint = records[data.building].Footprint;
                if (footprint.Definition.attraction.kind != AttractionKind.PettingZoo || data.access != footprint.AccessCell || data.interior != footprint.RearAccessCell)
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            }
            bool roomMovement = data.phase == VisitorAttractionPhase.EnteringViewingRoom || data.phase == VisitorAttractionPhase.ExitingViewingRoom;
            if (roomMovement && !data.hasViewingRoom || data.insideViewingRoom && !data.hasViewingRoom
                || data.hasViewingRoom && (data.hasWindow || data.hasPettingPortal || data.insidePetting || pettingMovement
                    || !grid.Contains(data.interior) || ConstructionRouteGraph.CellDistance(data.access, data.interior) != 1
                    || data.insideViewingRoom && data.phase != VisitorAttractionPhase.Visiting && data.phase != VisitorAttractionPhase.ExitingViewingRoom
                    || !data.insideViewingRoom && data.phase != VisitorAttractionPhase.Approaching && data.phase != VisitorAttractionPhase.EnteringViewingRoom))
                throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (data.building >= 0)
            {
                var footprint = records[data.building].Footprint;
                if (data.hasViewingRoom != (footprint.Definition.kind == BuildingKind.ViewingWindow)
                    || data.hasViewingRoom && (!footprint.IsFenceMounted || data.access != footprint.AccessCell || data.interior != footprint.RearAccessCell))
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            }
            bool interiorMovement = data.phase == VisitorAttractionPhase.EnteringAttraction || data.phase == VisitorAttractionPhase.WaitingForRide
                || data.phase == VisitorAttractionPhase.LeavingAttraction;
            if (interiorMovement && !data.hasInterior || data.insideInterior && !data.hasInterior
                || data.hasInterior && (data.hasWindow || data.hasPettingPortal || data.hasViewingRoom || pettingMovement || roomMovement
                    || !grid.Contains(data.interior) || data.seat < 0 || data.seatCapacity < 1 || data.seatCapacity > 10000 || data.seat >= data.seatCapacity
                    || data.insideInterior && data.phase != VisitorAttractionPhase.EnteringAttraction && data.phase != VisitorAttractionPhase.WaitingForRide
                        && data.phase != VisitorAttractionPhase.Visiting && data.phase != VisitorAttractionPhase.LeavingAttraction
                    || !data.insideInterior && data.phase != VisitorAttractionPhase.Approaching
                    || !PositionFinite(data.transitionOrigin.x) || !PositionFinite(data.transitionOrigin.y) || !PositionFinite(data.transitionOrigin.z)))
                throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            if (data.hasInterior && data.building >= 0)
            {
                var footprint = records[data.building].Footprint;
                if (footprint.Definition.kind == BuildingKind.PettingZoo || footprint.Definition.kind == BuildingKind.ViewingWindow
                    || data.access != footprint.AccessCell || data.interior != footprint.DoorCell)
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            }
            static bool PositionFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }

    }

    internal sealed class VisitorAttractionVisit
    {
        public ParkBuildingRecord Building;
        public FenceWindowRecord Window;
        public Vector2Int Access, Interior;
        public bool HasPettingPortal, InsidePetting, Completed, Interrupted, PerformanceSeen;
        public bool HasViewingRoom, InsideViewingRoom;
        public bool HasInterior, InsideInterior, HasQuote, OrphanExit;
        public int Seat = -1, SeatCapacity;
        public Vector3 TransitionOrigin;
        public VisitorAttractionPhase Phase;
        public float QuotedPrice, Interest;
        public CreatureAgent Exhibit;
        public string Key;
    }

    public sealed partial class VisitorWorld
    {
        private SpeciesResearch attractionResearch;
        private ParkProgression attractionProgression;
        private Func<ParkBuildingRecord, Vector2Int?> pettingEntry;
        private readonly HashSet<Vector2Int> attractionReachable = new HashSet<Vector2Int>();
        private readonly Dictionary<CreatureAgent, float> theatrePerformances = new Dictionary<CreatureAgent, float>();
        private static readonly Vector2Int[] PettingDirections = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        public int AttractionOccupancy(ParkBuildingRecord building)
        {
            int count = 0;
            foreach (var guest in visitors) if (guest.Actor != null && guest.Actor.Alive && guest.Attraction != null
                && ReferenceEquals(guest.Attraction.Building, building)
                && (guest.Attraction.Phase == VisitorAttractionPhase.Visiting || guest.Attraction.InsideInterior)) count++;
            return count;
        }

        public void ConfigureAttractions(SpeciesResearch research = null, ParkProgression progression = null,
            Func<ParkBuildingRecord, Vector2Int?> pettingEntry = null)
        {
            attractionResearch = research; attractionProgression = progression; this.pettingEntry = pettingEntry;
        }

        public string AttractionAvailabilityReason(ParkBuildingRecord building)
        {
            if (building == null || !building.Footprint.Definition.IsAttraction) return "";
            if (!buildings.Contains(building) || !building.IsOpen) return Loc.Text("attraction.unavailable.path");
            if (attractionProgression != null && !attractionProgression.BuildingUnlocked(building.Footprint.Definition.kind))
                return Loc.Text("attraction.unavailable.research");
            var visit = new VisitorAttractionVisit { Building = building, Access = building.Footprint.AccessCell };
            ConfigureViewingRoom(visit);
            ConfigureInterior(visit);
            if (building.Footprint.Definition.attraction.kind == AttractionKind.PettingZoo)
            {
                var entry = pettingEntry?.Invoke(building);
                if (!entry.HasValue) return Loc.Text("attraction.unavailable.petting");
                visit.HasPettingPortal = true; visit.Interior = entry.Value;
            }
            return EvaluateAttraction(null, visit, out _) ? "" : Loc.Text("attraction.unavailable.creatures");
        }

        private static string AttractionKey(ParkBuildingRecord building) => "b:" + (int)building.Footprint.Definition.kind
            + ":" + building.Footprint.Anchor.x + ":" + building.Footprint.Anchor.y + ":" + building.Footprint.Rotation;
        private static string AttractionKey(FenceWindowRecord window) => "w:" + window.Edge.Start.x + ":" + window.Edge.Start.y + ":" + (window.Edge.AlongX ? 1 : 0);
        private static void ConfigureViewingRoom(VisitorAttractionVisit visit)
        {
            if (visit.Building?.Footprint.Definition.kind != BuildingKind.ViewingWindow) return;
            visit.HasViewingRoom = true; visit.Interior = visit.Building.Footprint.RearAccessCell;
        }
        private AttractionDefinition AttractionSettings(VisitorAttractionVisit visit) => visit.Building != null
            ? visit.Building.Footprint.Definition.attraction : ViewingWindowDefinition.Current.attraction;
        private float AttractionPrice(VisitorAttractionVisit visit) => visit.Building != null ? visit.Building.Price : visit.Window?.Price ?? 0f;
        private int AttractionCapacity(VisitorAttractionVisit visit) => visit.Building != null
            ? IsBatchRide(visit.Building.Footprint.Definition) ? RideFor(visit.Building).Capacity : visit.Building.Footprint.Definition.serviceCapacity
            : ViewingWindowDefinition.Current.capacity;
        private float AttractionDuration(VisitorAttractionVisit visit) => visit.Building != null
            ? visit.Building.Footprint.Definition.serviceSeconds : ViewingWindowDefinition.Current.visitSeconds;

        private bool VenueExists(VisitorAttractionVisit visit)
        {
            if (visit.Building != null)
            {
                if (!buildings.Contains(visit.Building) || !visit.Building.IsOpen || !visit.Building.Footprint.Definition.IsAttraction) return false;
                if (visit.HasViewingRoom)
                    foreach (var edge in visit.Building.Footprint.AttachmentEdges())
                        if (!fences.HasFence(edge) || fences.IsGate(edge) || fences.HasWindow(edge)) return false;
                return true;
            }
            return visit.Window != null && ReferenceEquals(fences.WindowAt(visit.Window.Edge), visit.Window)
                && buildings.IsPathConnected(visit.Access) && navigation.IsWalkable(visit.Access);
        }

        private bool SeatsAvailable(VisitorAttractionVisit visit, VisitorAgent except)
        {
            int occupied = 0;
            foreach (var other in visitors)
                if (other != except && other.Actor != null && other.Actor.Alive && other.Attraction != null
                    && (visit.Building != null ? ReferenceEquals(other.Attraction.Building, visit.Building)
                        : ReferenceEquals(other.Attraction.Window, visit.Window))) occupied++;
            return occupied < Math.Max(1, AttractionCapacity(visit));
        }

        private bool TryAttraction(VisitorAgent guest, List<Vector2Int> paths)
        {
            if (time < guest.NextAttractionDecision || paths.Count == 0) return false;
            bool available = false;
            foreach (var building in buildings.Records) if (building.IsOpen && building.Footprint.Definition.IsAttraction) { available = true; break; }
            if (!available) foreach (var edge in fences.Windows) { available = true; break; }
            if (!available) return false;
            guest.NextAttractionDecision = time + Math.Max(.1f, Definition.attractionDecisionSeconds);
            attractionReachable.Clear(); foreach (var cell in paths) attractionReachable.Add(cell);
            VisitorAttractionVisit best = null; float bestScore = float.MinValue;
            foreach (var building in buildings.Records)
            {
                if (!building.IsOpen || !building.Footprint.Definition.IsAttraction || !attractionReachable.Contains(building.Footprint.AccessCell)) continue;
                if (attractionProgression != null && !attractionProgression.BuildingUnlocked(building.Footprint.Definition.kind)) continue;
                var settings = building.Footprint.Definition.attraction;
                string key = AttractionKey(building);
                if (guest.AttractionHistory.TryGetValue(key, out float until) && time < until) continue;
                var visit = new VisitorAttractionVisit { Building = building, Access = building.Footprint.AccessCell, Key = key };
                ConfigureViewingRoom(visit);
                ConfigureInterior(visit);
                if (settings.kind == AttractionKind.PettingZoo)
                {
                    Vector2Int? entry = pettingEntry?.Invoke(building);
                    if (!entry.HasValue) continue;
                    visit.HasPettingPortal = true; visit.Interior = entry.Value;
                }
                if (!SeatsAvailable(visit, guest) || !EvaluateAttraction(guest, visit, out float quality)) continue;
                float score = AttractionChoiceScore(guest, visit, quality);
                if (score > bestScore) { bestScore = score; best = visit; }
            }
            foreach (var edge in fences.Windows)
            {
                var window = fences.WindowAt(edge);
                if (window == null) continue;
                string key = AttractionKey(window);
                if (guest.AttractionHistory.TryGetValue(key, out float until) && time < until) continue;
                foreach (var access in new[] { edge.CellA, edge.CellB })
                {
                    if (!attractionReachable.Contains(access) || !buildings.IsPathConnected(access)) continue;
                    var visit = new VisitorAttractionVisit { Window = window, Access = access, Key = key };
                    if (!SeatsAvailable(visit, guest) || !EvaluateAttraction(guest, visit, out float quality)) continue;
                    float score = AttractionChoiceScore(guest, visit, quality);
                    if (score > bestScore) { bestScore = score; best = visit; }
                }
            }
            if (best == null || !SetRoute(guest, best.Access)) return false;
            ReserveSeat(guest, best);
            ReleaseService(guest); guest.Service = best.Building; guest.Attraction = best; guest.Watching = null;
            best.Phase = VisitorAttractionPhase.Approaching;
            SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.approaching", AttractionName(best));
            return true;
        }

        private string AttractionName(VisitorAttractionVisit visit) => visit.Building != null
            ? visit.Building.Footprint.Definition.DisplayName : Loc.Text(visit.HasPettingPortal ? "attraction.petting_zoo.name" : "attraction.viewing_window.name");

        private float AttractionChoiceScore(VisitorAgent guest, VisitorAttractionVisit visit, float quality)
        {
            var settings = AttractionSettings(visit);
            float interest = settings.AppealFor(guest.Type);
            visit.Interest = interest;
            if (interest < settings.minimumInterest || !AttractionAffordable(guest, visit)) return float.MinValue;
            return interest * settings.decisionWeight + quality * settings.exhibitBonus
                - ConstructionRouteGraph.CellDistance(guest.Actor.Cell, visit.Access) * Definition.attractionTravelPenalty
                - AttractionPrice(visit) / Mathf.Max(.01f, settings.maximumPrice) * Definition.attractionPricePenalty;
        }

        private bool AttractionAffordable(VisitorAgent guest, VisitorAttractionVisit visit)
        {
            float price = AttractionPrice(visit);
            return !float.IsNaN(price) && !float.IsInfinity(price) && price >= 0f
                && price <= VisitorInterests.NonNegative(AttractionSettings(visit).maximumPrice) * visit.Interest;
        }

        /// <summary>Linked experiences are based on real visible creatures; a window only sees its opposite side.</summary>
        private bool EvaluateAttraction(VisitorAgent guest, VisitorAttractionVisit visit, out float quality)
        {
            var settings = AttractionSettings(visit); quality = 0f; visit.Exhibit = null;
            if (visit.HasPettingPortal && (!VenueExists(visit) || pettingEntry?.Invoke(visit.Building) != visit.Interior)) return false;
            if (visit.HasViewingRoom && !VenueExists(visit)) return false;
            Vector2Int origin = visit.HasPettingPortal || visit.HasViewingRoom || visit.HasInterior ? visit.Interior : visit.Access;
            Vector3 point = grid.CellCenter(origin);
            foreach (var animal in creatures.Agents)
            {
                if (animal == null || !animal.Alive || animal.ExternallyControlled || animal.CarriedBy != null || animal.IsUnderground
                    || animal.Definition.requiresGravestoneForSleep && animal.Activity == CreatureActivity.Sleeping) continue;
                if (settings.requiresPettableCreatures && (!animal.Definition.pettable || animal.Definition.threat > settings.maximumCreatureThreat)) continue;
                if (settings.requiresDangerousCreatures && animal.Definition.threat <= Definition.threat) continue;
                if (settings.requiresFearFeeder && (!animal.Definition.feedsOnFear || animal.IsSedated)) continue;
                if (Distance(point, animal.transform.position) > settings.exhibitRadius) continue;
                if (visit.HasViewingRoom || visit.HasInterior)
                {
                    if (visit.Building == null || !sight.HasLineOfSightFromRoom(origin, animal.Cell, visit.Building.Footprint)
                        || visit.HasViewingRoom && !OppositeSide(visit.Building.Footprint.RearEdge, visit.Access, animal.Cell)) continue;
                }
                else if (!sight.HasLineOfSight(origin, animal.Cell)) continue;
                if (visit.Window != null && !OppositeSide(visit.Window.Edge, visit.Access, animal.Cell)) continue;
                if (visit.HasPettingPortal && !GroundRegion(visit.Interior).Contains(animal.Cell)) continue;
                float value = CreatureVisitorAppeal.Normalize(animal.Definition.rating / 5f);
                if (settings.kind == AttractionKind.CabinetOfCuriosities)
                    value *= attractionResearch == null ? 0f : Mathf.Clamp01(attractionResearch.Progress(animal.Definition) / 100f);
                if (settings.kind == AttractionKind.ExpeditionPavilion && (guest == null || !guest.ObservedOrigins.Contains(animal.Definition.origin))) value += settings.noveltyBonus;
                if (settings.kind == AttractionKind.PredatorFeedingGallery && animal.Activity == CreatureActivity.Feeding) value += settings.feedingBonus;
                if (visit.Exhibit == null || value > quality) { quality = value; visit.Exhibit = animal; }
            }
            return !settings.requiresCreatures || visit.Exhibit != null;
        }

        private static bool OppositeSide(FenceEdge edge, Vector2Int access, Vector2Int target)
            => edge.AlongX ? access.y < edge.Start.y ? target.y >= edge.Start.y : target.y < edge.Start.y
                : access.x < edge.Start.x ? target.x >= edge.Start.x : target.x < edge.Start.x;

        private bool NeedsService(VisitorAgent guest) => guest.LeavingPark
            || guest.Actor.Vitals.Hunger >= Mathf.Clamp(Definition.hungerThreshold * VisitorInterests.Factor(guest.TypeSettings.hungerThresholdMultiplier), 0, 100)
            || guest.Actor.Energy.Energy <= Mathf.Clamp(Definition.restThreshold * VisitorInterests.Factor(guest.TypeSettings.restThresholdMultiplier), 0, 100);

        private void TickAttraction(VisitorAgent guest)
        {
            var visit = guest.Attraction; if (visit == null) return;
            if (TickInteriorTransition(guest)) return;
            bool exiting = visit.Phase == VisitorAttractionPhase.ReturningPetting || visit.Phase == VisitorAttractionPhase.ExitingPetting
                || visit.Phase == VisitorAttractionPhase.ExitingViewingRoom;
            if (visit.Phase == VisitorAttractionPhase.ApproachingPettable && guest.StateTimer <= 0f)
            { CancelAttraction(guest, true); return; }
            if (!exiting && (NeedsService(guest) || !VenueExists(visit)
                || guest.DecisionTimer <= 0f && !EvaluateAttraction(guest, visit, out _)))
            {
                CancelAttraction(guest, true); return;
            }
            if (guest.DecisionTimer <= 0f)
            {
                guest.DecisionTimer = Mathf.Max(.05f, Definition.decisionInterval);
                if (visit.Phase == VisitorAttractionPhase.Visiting && !visit.PerformanceSeen) TryTheatrePerformance(guest);
            }
            if (visit.Phase == VisitorAttractionPhase.Visiting && guest.StateTimer <= 0f)
            {
                if (!EvaluateAttraction(guest, visit, out float quality)) { CancelAttraction(guest, true); return; }
                var settings = AttractionSettings(visit);
                if (settings.requiresFearFeeder && !visit.PerformanceSeen) { CancelAttraction(guest, true); return; }
                if (!visit.Completed)
                {
                    visit.Completed = true;
                    if (visit.Building != null) visit.Building.RecordRevenue(visit.QuotedPrice); else visit.Window.RecordRevenue(visit.QuotedPrice);
                    economy.Earn(visit.QuotedPrice, ParkTransactionCategory.Attractions);
                    guest.Mood.Reward(settings.satisfactionReward * visit.Interest * (1f + quality * settings.exhibitBonus));
                    guest.AttractionHistory[visit.Key] = time + settings.revisitSeconds;
                    guest.NextAttractionDecision = time + Definition.attractionVisitGapSeconds;
                    if (visit.Exhibit != null)
                    {
                        string id = SpeciesId(visit.Exhibit.Definition), origin = visit.Exhibit.Definition.origin;
                        if (!string.IsNullOrEmpty(id)) guest.ObservedSpecies.Add(id);
                        if (!string.IsNullOrEmpty(origin)) guest.ObservedOrigins.Add(origin);
                    }
                }
                if (visit.InsidePetting) BeginPettingExit(guest);
                else if (visit.InsideViewingRoom) SetViewingRoomRoute(guest, false);
                else if (visit.InsideInterior) BeginInteriorExit(guest);
                else FinishAttraction(guest);
            }
            else if (exiting && guest.Actor.Route.Count == 0) CompleteAttractionMovement(guest);
        }

        private bool CompleteAttractionMovement(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (visit == null || guest.Actor.Route.Count != 0) return false;
            if (visit.InsideInterior) return true;
            if (visit.Phase == VisitorAttractionPhase.Approaching)
            {
                if (guest.Actor.Cell != visit.Access || !VenueExists(visit) || !AttractionAffordable(guest, visit)) { CancelAttraction(guest, true); return true; }
                if (visit.HasPettingPortal)
                {
                    if (pettingEntry?.Invoke(visit.Building) != visit.Interior) { CancelAttraction(guest, true); return true; }
                    SetPettingPortalRoute(guest, true);
                }
                else if (visit.HasViewingRoom) SetViewingRoomRoute(guest, true);
                else if (visit.HasInterior) BeginInteriorEntry(guest);
                else BeginAttractionVisit(guest);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.EnteringViewingRoom)
            {
                if (guest.Actor.Cell != visit.Interior) { CancelAttraction(guest, true); return true; }
                visit.InsideViewingRoom = true; BeginAttractionVisit(guest); return true;
            }
            if (visit.Phase == VisitorAttractionPhase.ExitingViewingRoom)
            {
                if (guest.Actor.Cell == visit.Access) FinishAttraction(guest);
                else SetViewingRoomRoute(guest, false);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.EnteringPetting)
            {
                if (guest.Actor.Cell != visit.Interior) { CancelAttraction(guest, true); return true; }
                visit.InsidePetting = true; guest.Actor.PettingHost = visit.Building;
                if (!ApproachPettable(guest)) CancelAttraction(guest, true);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.ApproachingPettable)
            {
                if (visit.Exhibit != null && visit.Exhibit.Alive && ConstructionRouteGraph.CellDistance(guest.Actor.Cell, visit.Exhibit.Cell) <= 1)
                    BeginAttractionVisit(guest);
                else if (!ApproachPettable(guest)) CancelAttraction(guest, true);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.ReturningPetting)
            {
                if (guest.Actor.Cell == visit.Interior) SetPettingPortalRoute(guest, false);
                else BeginPettingExit(guest);
                return true;
            }
            if (visit.Phase == VisitorAttractionPhase.ExitingPetting)
            {
                if (guest.Actor.Cell == visit.Access) FinishAttraction(guest);
                else BeginPettingExit(guest);
                return true;
            }
            return visit.Phase == VisitorAttractionPhase.Visiting;
        }

        private void BeginAttractionVisit(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (!VenueExists(visit) || !visit.HasQuote && !AttractionAffordable(guest, visit) || !EvaluateAttraction(guest, visit, out _))
            { CancelAttraction(guest, true); return; }
            if (!visit.HasQuote) { visit.QuotedPrice = AttractionPrice(visit); visit.HasQuote = true; }
            visit.Phase = VisitorAttractionPhase.Visiting;
            guest.StateTimer = Mathf.Max(.1f, AttractionDuration(visit));
            TryTheatrePerformance(guest);
            SetActivity(guest, VisitorActivity.EnjoyingAttraction, "visitor.attraction.enjoying", AttractionName(visit));
            if (visit.Exhibit != null)
            {
                var direction = visit.Exhibit.transform.position - guest.Actor.transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .001f) guest.Actor.transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        private void TryTheatrePerformance(VisitorAgent guest)
        {
            var visit = guest.Attraction; var settings = AttractionSettings(visit);
            if (!settings.requiresFearFeeder || visit.Exhibit == null) return;
            if (theatrePerformances.TryGetValue(visit.Exhibit, out float until) && time <= until) visit.PerformanceSeen = true;
            else if (creatures.TryAttractionScare(visit.Exhibit, grid.CellCenter(visit.Access), settings.scareFoodRelief, settings.scareCooldownSeconds))
            {
                visit.PerformanceSeen = true;
                theatrePerformances[visit.Exhibit] = time + Mathf.Max(.1f, visit.Exhibit.Definition.scareSeconds);
            }
        }

        private bool ApproachPettable(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (!EvaluateAttraction(guest, visit, out _) || visit.Exhibit == null) return false;
            List<Vector2Int> best = null;
            foreach (var direction in PettingDirections)
            {
                var route = navigation.FindPath(guest.Actor.Cell, visit.Exhibit.Cell + direction, true);
                if (route.Count > 0 && (best == null || route.Count < best.Count)) best = route;
            }
            if (best == null) return false;
            if (visit.Phase != VisitorAttractionPhase.ApproachingPettable) guest.StateTimer = Mathf.Max(.1f, AttractionDuration(visit));
            guest.Actor.Route.Clear(); guest.Actor.Goal = best[best.Count - 1]; guest.GroundRoute = true;
            for (int i = 1; i < best.Count; i++) guest.Actor.Route.Add(best[i]);
            visit.Phase = VisitorAttractionPhase.ApproachingPettable;
            SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.meeting_creatures");
            if (guest.Actor.Route.Count == 0) BeginAttractionVisit(guest);
            return true;
        }

        private void SetPettingPortalRoute(VisitorAgent guest, bool entering)
        {
            var visit = guest.Attraction;
            guest.Actor.Route.Clear(); guest.Actor.Goal = entering ? visit.Interior : visit.Access;
            guest.Actor.Route.Add(guest.Actor.Goal.Value); guest.GroundRoute = true;
            visit.Phase = entering ? VisitorAttractionPhase.EnteringPetting : VisitorAttractionPhase.ExitingPetting;
            if (entering) SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.entering_petting_zoo");
            else SetActivity(guest, guest.Panicked ? VisitorActivity.Fleeing : VisitorActivity.SeekingAttraction, "visitor.attraction.leaving_petting_zoo");
        }

        private void SetViewingRoomRoute(VisitorAgent guest, bool entering)
        {
            var visit = guest.Attraction;
            guest.Actor.Route.Clear(); guest.Actor.Goal = entering ? visit.Interior : visit.Access;
            guest.Actor.Route.Add(guest.Actor.Goal.Value); guest.GroundRoute = true;
            visit.Phase = entering ? VisitorAttractionPhase.EnteringViewingRoom : VisitorAttractionPhase.ExitingViewingRoom;
            SetActivity(guest, guest.Panicked ? VisitorActivity.Fleeing : VisitorActivity.SeekingAttraction,
                entering ? "visitor.attraction.entering_viewing_room" : "visitor.attraction.leaving_viewing_room");
        }

        private bool PettingPortalStep(VisitorAgent guest, Vector2Int from, Vector2Int to)
        {
            var visit = guest.Attraction;
            if (visit != null && visit.HasViewingRoom)
                return visit.Phase == VisitorAttractionPhase.EnteringViewingRoom && from == visit.Access && to == visit.Interior && VenueExists(visit)
                    || visit.Phase == VisitorAttractionPhase.ExitingViewingRoom && from == visit.Interior && to == visit.Access
                        && navigation.IsWalkable(visit.Access, true);
            return visit != null && visit.HasPettingPortal
                && (visit.Phase == VisitorAttractionPhase.EnteringPetting && from == visit.Access && to == visit.Interior
                    && VenueExists(visit) && pettingEntry?.Invoke(visit.Building) == visit.Interior
                    || visit.Phase == VisitorAttractionPhase.ExitingPetting && from == visit.Interior && to == visit.Access);
        }

        private void BeginPettingExit(VisitorAgent guest)
        {
            var visit = guest.Attraction; if (visit == null) return;
            visit.Phase = VisitorAttractionPhase.ReturningPetting; guest.Actor.Route.Clear(); guest.Actor.Goal = null;
            if (guest.Actor.Cell == visit.Interior) { SetPettingPortalRoute(guest, false); return; }
            if (SetRoute(guest, visit.Interior, true))
                SetActivity(guest, guest.Panicked ? VisitorActivity.Fleeing : VisitorActivity.SeekingAttraction, "visitor.attraction.leaving_petting_zoo");
            else
                SetActivity(guest, VisitorActivity.Stranded, "visitor.attraction.petting_exit_blocked");
        }

        private void InterruptAttraction(VisitorAgent guest)
        {
            if (guest.Attraction == null) return;
            guest.Attraction.Interrupted = true;
            if (!guest.Attraction.InsidePetting && !guest.Attraction.InsideViewingRoom) CancelAttraction(guest, false);
        }

        private bool TryPettingEmergencyExit(VisitorAgent guest)
        {
            var visit = guest.Attraction;
            if (visit?.InsideInterior == true) { CancelAttraction(guest, true); return true; }
            if (visit != null && visit.InsideViewingRoom)
            { visit.Interrupted = true; SetViewingRoomRoute(guest, false); return true; }
            if (visit == null || !visit.InsidePetting) return false;
            visit.Interrupted = true;
            if (visit.Phase != VisitorAttractionPhase.ExitingPetting && (visit.Phase != VisitorAttractionPhase.ReturningPetting || guest.Actor.Route.Count == 0))
                BeginPettingExit(guest);
            return guest.Actor.Route.Count > 0;
        }

        private void CancelAttraction(VisitorAgent guest, bool exitPetting)
        {
            var visit = guest.Attraction; if (visit == null) return;
            visit.Interrupted = true;
            guest.NextAttractionDecision = time + Definition.attractionDecisionSeconds;
            if (visit.InsideInterior)
            {
                bool riding = visit.Building != null && IsBatchRide(visit.Building.Footprint.Definition)
                    && RideFor(visit.Building).Phase == AttractionRidePhase.Riding && buildings.Contains(visit.Building);
                if (!riding) BeginInteriorExit(guest);
                return;
            }
            if (exitPetting && visit.InsidePetting) { BeginPettingExit(guest); return; }
            if (exitPetting && visit.InsideViewingRoom) { SetViewingRoomRoute(guest, false); return; }
            FinishAttraction(guest);
        }

        private void FinishAttraction(VisitorAgent guest)
        {
            if (guest.Attraction?.InsideInterior == true) guest.Actor.Cell = guest.Attraction.Access;
            guest.Actor.AttractionHost = null; guest.Actor.Elevation = 0;
            guest.Actor.AttractionPoseControlled = false;
            guest.Actor.AttractionWalking = false;
            guest.Actor.PettingHost = null;
            guest.Attraction = null; guest.Service = null; guest.GroundRoute = false;
            guest.Actor.Route.Clear(); guest.Actor.Goal = null;
            if (guest.Actor.CapturedBy == null && guest.Actor.CarriedBy == null)
                guest.Actor.transform.position = Position(guest.Actor, guest.Actor.Cell);
            guest.DecisionTimer = 0f;
            SetActivity(guest, guest.Panicked ? VisitorActivity.Fleeing : VisitorActivity.Arriving,
                guest.Panicked ? "visitor.status.fleeing_a_predator" : "visitor.attraction.finished");
        }

        public VisitorAttractionSave CaptureAttraction(VisitorAgent guest, IReadOnlyList<ParkBuildingRecord> records)
        {
            var data = new VisitorAttractionSave { choiceCooldown = Mathf.Max(0, guest.NextAttractionDecision - time) };
            foreach (var pair in guest.AttractionHistory)
                if (pair.Value > time) data.history.Add(new VisitorAttractionMemory { id = pair.Key, remaining = pair.Value - time });
            data.history.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            var visit = guest.Attraction;
            if (visit == null) return data;
            if (visit.Building != null) for (int i = 0; i < records.Count; i++) if (ReferenceEquals(records[i], visit.Building)) { data.building = i; break; }
            data.hasWindow = visit.Window != null && ReferenceEquals(fences.WindowAt(visit.Window.Edge), visit.Window);
            if (data.building < 0 && !data.hasWindow && !visit.InsidePetting && !visit.InsideViewingRoom && !visit.InsideInterior) return data;
            if (data.hasWindow) { data.windowCell = visit.Window.Edge.Start; data.windowAlongX = visit.Window.Edge.AlongX; }
            data.hasPettingPortal = visit.HasPettingPortal; data.insidePetting = visit.InsidePetting;
            data.hasViewingRoom = visit.HasViewingRoom; data.insideViewingRoom = visit.InsideViewingRoom;
            data.hasInterior = visit.HasInterior; data.insideInterior = visit.InsideInterior; data.hasQuote = visit.HasQuote;
            data.seat = visit.Seat; data.seatCapacity = visit.SeatCapacity;
            data.transitionOrigin = visit.TransitionOrigin; data.orphanExit = visit.OrphanExit;
            data.access = visit.Access; data.interior = visit.Interior; data.phase = visit.Phase;
            data.remaining = Mathf.Max(0, guest.StateTimer); data.quotedPrice = visit.QuotedPrice; data.interest = visit.Interest;
            data.completed = visit.Completed; data.interrupted = visit.Interrupted; data.performanceSeen = visit.PerformanceSeen;
            if (data.building < 0 && !data.hasWindow && data.insidePetting)
            { data.phase = VisitorAttractionPhase.ReturningPetting; data.interrupted = true; }
            if (data.building < 0 && data.insideViewingRoom)
            { data.phase = VisitorAttractionPhase.ExitingViewingRoom; data.interrupted = true; }
            if (data.building < 0 && data.insideInterior)
            {
                data.phase = VisitorAttractionPhase.LeavingAttraction; data.interrupted = true; data.orphanExit = true;
                if (visit.Phase != VisitorAttractionPhase.LeavingAttraction)
                { data.transitionOrigin = guest.Actor.transform.position; data.remaining = AttractionSettings(visit).exitSeconds; }
            }
            return data;
        }

        public static void ValidateAttractionSave(VisitorAttractionSave data, IReadOnlyList<ParkBuildingRecord> records,
            SquareBuildGrid grid, FenceLayout fences) => VisitorAttractionSaveRules.Validate(data, records, grid, fences);
        public void RestoreAttraction(VisitorAgent guest, VisitorAttractionSave data, IReadOnlyList<ParkBuildingRecord> records)
        {
            if (data == null) return;
            guest.NextAttractionDecision = time + data.choiceCooldown;
            guest.AttractionHistory.Clear();
            if (data.history != null) foreach (var item in data.history) guest.AttractionHistory[item.id] = time + item.remaining;
            if (data.phase == VisitorAttractionPhase.None) return;
            var visit = new VisitorAttractionVisit { Building = data.building >= 0 ? records[data.building] : null,
                Window = data.hasWindow ? fences.WindowAt(new FenceEdge(data.windowCell, data.windowAlongX)) : null,
                Access = data.access, Interior = data.interior, HasPettingPortal = data.hasPettingPortal,
                HasViewingRoom = data.hasViewingRoom, InsideViewingRoom = data.insideViewingRoom,
                HasInterior = data.hasInterior, InsideInterior = data.insideInterior, HasQuote = data.hasQuote || data.phase == VisitorAttractionPhase.Visiting || data.completed,
                Seat = data.seat, SeatCapacity = data.seatCapacity, TransitionOrigin = data.transitionOrigin, OrphanExit = data.orphanExit,
                InsidePetting = data.insidePetting, Phase = data.phase, Completed = data.completed, Interrupted = data.interrupted,
                PerformanceSeen = data.performanceSeen, QuotedPrice = data.quotedPrice, Interest = data.interest };
            visit.Key = visit.Building != null ? AttractionKey(visit.Building) : visit.Window != null ? AttractionKey(visit.Window) : "";
            guest.Attraction = visit; guest.Service = visit.Building; guest.StateTimer = data.remaining;
            if (UsesInterior(visit.Building?.Footprint.Definition) && !visit.HasInterior)
            {
                ConfigureInterior(visit); ReserveSeat(guest, visit);
                if (visit.Phase == VisitorAttractionPhase.Visiting)
                {
                    visit.InsideInterior = true;
                    if (IsBatchRide(visit.Building.Footprint.Definition)) visit.Phase = VisitorAttractionPhase.WaitingForRide;
                }
            }
            if (visit.InsideInterior)
            {
                guest.Actor.Cell = visit.Interior; guest.Actor.AttractionHost = visit.Building; guest.Actor.AttractionPoseControlled = true;
                ApplyPassengerPose(guest);
                SetActivity(guest, VisitorActivity.EnjoyingAttraction, "visitor.attraction.enjoying", AttractionName(visit));
                if (visit.Interrupted || guest.LeavingPark) CancelAttraction(guest, true);
                return;
            }
            if (visit.InsidePetting) guest.Actor.PettingHost = visit.Building;
            if (visit.Interrupted || guest.LeavingPark) { CancelAttraction(guest, true); return; }
            if (visit.Phase == VisitorAttractionPhase.EnteringViewingRoom) SetViewingRoomRoute(guest, true);
            else if (visit.Phase == VisitorAttractionPhase.ExitingViewingRoom) SetViewingRoomRoute(guest, false);
            else if (visit.Phase == VisitorAttractionPhase.EnteringPetting) SetPettingPortalRoute(guest, true);
            else if (visit.Phase == VisitorAttractionPhase.ApproachingPettable)
            {
                if (!ApproachPettable(guest)) CancelAttraction(guest, true);
            }
            else if (visit.Phase == VisitorAttractionPhase.ReturningPetting || visit.Phase == VisitorAttractionPhase.ExitingPetting) BeginPettingExit(guest);
            else if (visit.Phase == VisitorAttractionPhase.Approaching)
            {
                if (!SetRoute(guest, visit.Access)) CancelAttraction(guest, true);
                else SetActivity(guest, VisitorActivity.SeekingAttraction, "visitor.attraction.approaching", AttractionName(visit));
            }
            else
            {
                if (!VenueExists(visit) || !EvaluateAttraction(guest, visit, out _)) CancelAttraction(guest, true);
                else SetActivity(guest, VisitorActivity.EnjoyingAttraction, "visitor.attraction.enjoying", AttractionName(visit));
            }
        }
    }
}

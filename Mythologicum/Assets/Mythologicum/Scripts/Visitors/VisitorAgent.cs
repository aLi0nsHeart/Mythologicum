using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum VisitorActivity { Arriving, Walking, Watching, SeekingFood, Eating, SeekingRest, Resting,
        Fleeing, ReturningToPath, Leaving, Stranded, Captured, SeekingAttraction, EnjoyingAttraction }

    /// <summary>A guest's decisions; the shared actor supplies hunger, energy, fear and predator interactions.</summary>
    public sealed class VisitorAgent : MonoBehaviour
    {
        public CreatureAgent Actor { get; internal set; }
        public VisitorMood Mood { get; internal set; } = new VisitorMood();
        public VisitorActivity Activity { get; internal set; }
        public VisitorType Type { get; internal set; }
        public VisitorTypeSettings TypeSettings { get; internal set; } = VisitorTypeSettings.DefaultFor(VisitorType.Legacy);
        public float LastExhibitInterest { get; internal set; }
        public readonly HashSet<string> ObservedSpecies = new HashSet<string>(System.StringComparer.Ordinal);
        public readonly HashSet<string> ObservedOrigins = new HashSet<string>(System.StringComparer.Ordinal);
        public void RestoreInterests(IEnumerable<string> species, IEnumerable<string> origins)
        {
            ObservedSpecies.Clear(); ObservedOrigins.Clear();
            if (species != null) foreach (var id in species) if (!string.IsNullOrWhiteSpace(id)) ObservedSpecies.Add(id);
            if (origins != null) foreach (var id in origins) if (!string.IsNullOrWhiteSpace(id)) ObservedOrigins.Add(id);
        }
        private readonly StatusMessage status = new StatusMessage("visitor.status.arriving");
        public string Status { get => status.Value; internal set => status.Set(value); }
        internal void SetLocalizedStatus(string id, params object[] arguments) => status.Set(id, arguments);
        public float TimeInPark => Mood.Age;
        internal float DecisionTimer, StateTimer;
        internal bool LeavingPark, GroundRoute, Panicked;
        internal Vector3 LastDanger;
        internal ParkBuildingRecord Service;
        internal float QuotedPrice;
        internal float NextFoodPriceComplaint, NextRestPriceComplaint;
        private string priceComplaint;
        public string PriceComplaint { get => Loc.Resolve(priceComplaint); internal set => priceComplaint = value; }
        internal CreatureAgent Watching;
        internal VisitorAttractionVisit Attraction;
        internal float NextAttractionDecision;
        internal readonly Dictionary<string, float> AttractionHistory = new Dictionary<string, float>(System.StringComparer.Ordinal);
        public string AttractionName => Attraction == null ? "" : Attraction.Building != null
            ? Attraction.Building.Footprint.Definition.DisplayName : Loc.Text(Attraction.HasPettingPortal ? "attraction.petting_zoo.name" : "attraction.viewing_window.name");
        public float AttractionInterest => Attraction?.Interest ?? 0f;
        public float AttractionSecondsRemaining => Attraction != null && Attraction.Phase == VisitorAttractionPhase.Visiting ? Mathf.Max(0, StateTimer) : 0f;
        internal readonly Dictionary<CreatureAgent, float> ViewedAt = new Dictionary<CreatureAgent, float>();
    }
}

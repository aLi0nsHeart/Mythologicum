using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum VisitorActivity { Arriving, Walking, Watching, SeekingFood, Eating, SeekingRest, Resting,
        Fleeing, ReturningToPath, Leaving, Stranded, Captured }

    /// <summary>A guest's decisions; the shared actor supplies hunger, energy, fear and predator interactions.</summary>
    public sealed class VisitorAgent : MonoBehaviour
    {
        public CreatureAgent Actor { get; internal set; }
        public VisitorMood Mood { get; internal set; } = new VisitorMood();
        public VisitorActivity Activity { get; internal set; }
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
        internal readonly Dictionary<CreatureAgent, float> ViewedAt = new Dictionary<CreatureAgent, float>();
    }
}

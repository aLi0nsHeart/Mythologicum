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
        public string Status { get; internal set; } = "Arriving";
        public float TimeInPark => Mood.Age;
        internal float DecisionTimer, StateTimer;
        internal bool LeavingPark, GroundRoute, Panicked;
        internal Vector3 LastDanger;
        internal ParkBuildingRecord Service;
        internal float QuotedPrice;
        internal CreatureAgent Watching;
        internal readonly Dictionary<CreatureAgent, float> ViewedAt = new Dictionary<CreatureAgent, float>();
    }
}

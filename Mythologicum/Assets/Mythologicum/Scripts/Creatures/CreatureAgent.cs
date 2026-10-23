using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum CreatureActivity { Wandering, Searching, Hunting, Fleeing, Grazing, Resting, Attacking, Feeding, Dying,
        SeekingSleep, ClimbingTree, Sleeping, DescendingTree, ClimbingFence, FailedClimb, Socializing }

    /// <summary>Individual runtime state. Species rules live in the shared profile asset.</summary>
    public sealed class CreatureAgent : MonoBehaviour
    {
        public CreatureDefinition Definition { get; internal set; }
        public CreatureVitals Vitals { get; internal set; }
        public Vector2Int Cell { get; internal set; }
        public CreatureActivity Activity { get; internal set; }
        public bool Alive { get; internal set; } = true;
        public CreatureWellbeing Wellbeing { get; internal set; } = new CreatureWellbeing();
        public float AccessibleArea { get; internal set; }
        public int NearbyTrees { get; internal set; }
        public int Companions { get; internal set; }
        public CreatureEnergy Energy { get; internal set; }
        internal bool ExternallyControlled;
        internal string ExternalStatus;
        internal readonly List<Vector2Int> Route = new List<Vector2Int>();
        internal Vector2Int? Goal;
        internal CreatureAgent Threat, Prey;
        internal float DecisionTimer, WanderTimer, GrazeTimer;
        internal float StateTimer, CorpseTimer, PreyLastSeen;
        internal float NeedsTimer;
        internal Vector2Int? LastSeenPreyCell;
        internal Vector3 LastDanger;
        internal bool HasDanger;
        internal CreatureAgent CapturedBy;
        internal Transform Body;
        internal MeshRenderer BodyRenderer;
        internal bool WantsSleep;
        internal Vector2Int? SleepTree;
        internal float Elevation, TraversalDuration, TraversalStartElevation, AttemptAfter;
        internal Vector2Int TraversalFrom, TraversalTo;
        internal FenceEdge? AttemptFence;
        internal CreatureAgent SocialPartner;
        internal TextMesh Label;
        internal GameObject Grass;
        internal ParkBuildingRecord SourceDispenser;
    }
}

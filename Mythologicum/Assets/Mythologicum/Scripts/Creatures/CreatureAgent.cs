using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    public enum CreatureActivity { Wandering, Searching, Hunting, Fleeing, Grazing, Resting, Attacking, Feeding, Dying,
        SeekingSleep, ClimbingTree, Sleeping, DescendingTree, ClimbingFence, FailedClimb, Socializing, Escaping,
        AttackingFence, PhasingFence, Scaring, EnteringGrave, LeavingGrave, Sedated, Carried }

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
        public CreatureHealth Health { get; internal set; }
        public int NearbyGravestones { get; internal set; }
        public bool IsSedated { get; internal set; }
        public float SedationRemaining { get; internal set; }
        public CreatureAgent CarriedBy { get; internal set; }
        public bool RetirementRequested { get; internal set; }
        public bool SedationRequested { get; internal set; }
        public Vector2Int? MoveRequestedDestination { get; internal set; }
        public string StaffStatus { get; internal set; } = "";
        internal float ScareAfter;
        internal bool ScareSucceeded;
        internal FenceEdge? AttackedFence;
        internal int FenceStrikes;
        public float EscapeChancePerMinute { get; internal set; }
        public float Crowding { get; internal set; }
        public string EscapeStatus { get; internal set; } = "No escape intention";
        internal bool EscapeIntent;
        internal float EscapeUntil, EscapeAfter, EscapeCheckTimer;
        internal Vector2Int? EscapeGoal;
        internal FenceEdge? EscapeBarrier;
        internal readonly CreatureExploration Exploration = new CreatureExploration();
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
        internal bool HasVisualModel;
        internal Animator BodyAnimator;
        internal string AnimationState;
        internal bool WantsSleep;
        internal Vector2Int? SleepTree;
        internal float Elevation, TraversalDuration, TraversalStartElevation, AttemptAfter;
        internal Vector2Int TraversalFrom, TraversalTo;
        internal FenceEdge? AttemptFence;
        internal CreatureAgent SocialPartner;
        internal TextMesh Label;

        internal ParkBuildingRecord SourceDispenser;
    }
}

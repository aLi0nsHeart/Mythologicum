using UnityEngine;

namespace Mythologicum
{
    public enum StaffTask { Idle, Patrolling, Researching, Healing, Repairing, Restocking, Regrouping,
        Hunting, Retiring, Sedating, FetchingCreature, CarryingCreature, Returning, Blocked, Fleeing }

    public sealed class StaffAgent : MonoBehaviour
    {
        public CreatureAgent Actor { get; internal set; }
        public ParkBuildingRecord Home { get; internal set; }
        public StaffRole Role { get; internal set; }
        public StaffTask Task { get; internal set; }
        public string Status { get; internal set; } = "Ready for work";
        public CreatureAgent Target { get; internal set; }
        internal FenceEdge? Fence;
        internal ParkBuildingRecord Feeder;
        internal float DecisionTimer, ActionTimer, ShotTimer;
        internal Vector2Int? Destination, MeetingPoint;
    }
}

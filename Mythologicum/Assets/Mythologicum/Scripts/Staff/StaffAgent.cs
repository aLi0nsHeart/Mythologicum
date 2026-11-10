using UnityEngine;

namespace Mythologicum
{
    public enum StaffTask { Idle, Patrolling, Researching, Healing, Repairing, Restocking, Regrouping,
        Hunting, Retiring, Sedating, FetchingCreature, CarryingCreature, Returning, Blocked, Fleeing, TechnologyResearch }

    public sealed class StaffAgent : MonoBehaviour
    {
        public CreatureAgent Actor { get; internal set; }
        public ParkBuildingRecord Home { get; internal set; }
        public StaffRole Role { get; internal set; }
        public StaffTask Task { get; internal set; }
        private readonly StatusMessage status = new StatusMessage("staff.status.ready_for_work");
        public string Status { get => status.Value; internal set => status.Set(value); }
        internal void SetLocalizedStatus(string id, params object[] arguments) => status.Set(id, arguments);
        public CreatureAgent Target { get; internal set; }
        internal FenceEdge? Fence;
        internal ParkBuildingRecord Feeder;
        internal float DecisionTimer, ActionTimer, ShotTimer;
        internal Vector2Int? Destination, MeetingPoint;
    }
}

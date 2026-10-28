using UnityEngine;

namespace Mythologicum
{
    public sealed class BuildingSelection : MonoBehaviour
    {
        public ParkBuildingRecord Record { get; private set; }
        public void Initialize(ParkBuildingRecord record) => Record = record;
    }
}

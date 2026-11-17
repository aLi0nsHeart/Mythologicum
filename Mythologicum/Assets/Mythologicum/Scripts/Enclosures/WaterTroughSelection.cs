using UnityEngine;
namespace Mythologicum
{
    public sealed class WaterTroughSelection : MonoBehaviour
    {
        public WaterTroughRecord Record { get; private set; }
        public void Initialize(WaterTroughRecord record) => Record = record;
    }
}

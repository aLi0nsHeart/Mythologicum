using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Fence visuals", fileName = "FenceVisuals")]
    public sealed class FenceVisualCatalog : ScriptableObject
    {
        public GameObject normal;
        public GameObject spiked;
        public GameObject gate;
        public float normalYaw;
        public float spikedYaw;
        public float gateYaw;
        [Header("Specialized fences")]
        public GameObject heavy;
        public GameObject sanctified;
        public float heavyYaw;
        public float sanctifiedYaw;
    }
}

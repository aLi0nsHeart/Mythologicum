using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class FenceFoundationVisual
    {
        [Min(.01f)] public float width = .32f;
        [Min(.01f)] public float buriedDepth = .6f;
        [Min(.01f)] public float visibleHeight = .18f;
        public Color color = new Color(.42f, .43f, .39f);

        public void Validate()
        {
            width = Positive(width, .32f);
            buriedDepth = Positive(buriedDepth, .6f);
            visibleHeight = Positive(visibleHeight, .18f);
        }
        private static float Positive(float value, float fallback)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(.01f, value);
    }

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
        [Tooltip("Optional above-ground Deep fence model. A visible buried foundation is generated for its fences, gates and windows.")]
        public GameObject deep;
        public float deepYaw;
        [Tooltip("Optional flat-ground aviary framework and aquarium wall models. Terrain-aware versions are generated procedurally.")]
        public GameObject aviary, aquarium;
        public float aviaryYaw, aquariumYaw;
        public FenceFoundationVisual deepFoundation = new FenceFoundationVisual();

        private void OnValidate()
        {
            if (deepFoundation == null) deepFoundation = new FenceFoundationVisual();
            deepFoundation.Validate();
        }
    }
}

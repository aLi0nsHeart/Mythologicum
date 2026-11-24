using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class SpecialEnclosureRules
    {
        [Min(1), Tooltip("Metres above the highest ground point inside a complete aviary at which its roof is created.")]
        public float aviaryHeight = 8f;
        [Min(.01f)] public float cageBarWidth = .045f;
        [Min(.2f)] public float cageMeshSpacing = 1f;
        public Color cageColor = new Color(.32f, .36f, .32f, .75f);
        [Min(.1f), Tooltip("Excavated depth below the pool surface at fully interior terrain vertices.")]
        public float aquariumDepth = 1.2f;
        [Min(0), Tooltip("Water surface above the highest intact rim point, in metres.")]
        public float aquariumWaterLift = .08f;
        [Min(.05f)] public float minimumPoolDepth = .15f;
        [Min(1)] public int minimumPoolCells = 4;
        [Min(.1f), Tooltip("Seconds between retrying a newly closed pool blocked by a temporary object or animal.")]
        public float blockedPoolRetrySeconds = 1f;
        [Min(0)] public float aquariumWallFreeboard = .2f;
        public Color aquariumWallColor = new Color(.36f, .71f, .78f, .28f);
        public void Validate()
        {
            aviaryHeight = Amount(aviaryHeight, 1, 100); cageBarWidth = Amount(cageBarWidth, .01f, .5f);
            cageMeshSpacing = Amount(cageMeshSpacing, .2f, 20); aquariumDepth = Amount(aquariumDepth, .1f, 100);
            aquariumWaterLift = Amount(aquariumWaterLift, 0, 10); minimumPoolDepth = Amount(minimumPoolDepth, .05f, 10);
            minimumPoolCells = Math.Max(1, minimumPoolCells); blockedPoolRetrySeconds = Amount(blockedPoolRetrySeconds, .1f, 60);
            aquariumWallFreeboard = Amount(aquariumWallFreeboard, 0, 10);
        }
        private static float Amount(float value, float min, float max)
            => float.IsNaN(value) || float.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
    }

    [CreateAssetMenu(menuName = "Mythologicum/Enclosures/Aviaries and aquariums", fileName = "DefaultSpecialEnclosures")]
    public sealed class SpecialEnclosureDefinition : ScriptableObject
    {
        private static SpecialEnclosureDefinition current;
        public static SpecialEnclosureDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<SpecialEnclosureDefinition>("Enclosures/DefaultSpecialEnclosures");
                if (current == null) { current = CreateInstance<SpecialEnclosureDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        public SpecialEnclosureRules rules = new SpecialEnclosureRules();
        private void OnValidate() { rules = rules ?? new SpecialEnclosureRules(); rules.Validate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetSession() => current = null;
    }
}

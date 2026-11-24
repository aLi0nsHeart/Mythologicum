using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Enclosures/Shelters", fileName = "Shelters")]
    public sealed class ShelterDefinition : ScriptableObject
    {
        private static ShelterDefinition current;
        public static ShelterDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ShelterDefinition>("Environment/Shelters");
                if (current == null) { current = CreateInstance<ShelterDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        [Min(0)] public float caveConstructionCost = 120f;
        [Min(.5f)] public float caveHeight = 3.6f;
        [Min(1)] public int caveCapacity = 3;
        [Min(1)] public int burrowCapacity = 1;
        [Min(.05f)] public float burrowHeight = .3f;
        public Color rockColor = new Color(.38f, .39f, .35f);
        public Color entranceColor = new Color(.07f, .065f, .05f);
        public Color dirtColor = new Color(.38f, .25f, .13f);
        private void OnValidate()
        {
            caveConstructionCost = Valid(caveConstructionCost, 0); caveHeight = Valid(caveHeight, .5f);
            burrowHeight = Valid(burrowHeight, .05f); caveCapacity = Mathf.Max(1, caveCapacity); burrowCapacity = Mathf.Max(1, burrowCapacity);
        }
        private static float Valid(float value, float minimum) => float.IsNaN(value) || float.IsInfinity(value) ? minimum : Mathf.Max(minimum, value);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetSession() => current = null;
    }
}

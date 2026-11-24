using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Enclosures/Water trough", fileName = "WaterTrough")]
    public sealed class WaterTroughDefinition : ScriptableObject
    {
        private static WaterTroughDefinition current;
        public static WaterTroughDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<WaterTroughDefinition>("Environment/WaterTrough");
                if (current == null) { current = CreateInstance<WaterTroughDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        [Min(0)] public float constructionCost = 75;
        [Min(.1f)] public float capacity = 120;
        [Min(.001f), Tooltip("Water units consumed for each thirst point actually relieved.")]
        public float waterUnitsPerThirstPoint = .1f;
        [Min(0)] public float refillThreshold = 30;
        [Min(.1f)] public float refillSeconds = 3;
        [Min(0)] public float refillCost = 8;
        [Min(.1f)] public float height = .65f;
        public Color frameColor = new Color(.28f, .22f, .15f);
        public Color waterColor = new Color(.17f, .58f, .78f);
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
        private void OnValidate()
        {
            constructionCost = Amount(constructionCost); capacity = Mathf.Max(.1f, Amount(capacity));
            waterUnitsPerThirstPoint = Mathf.Max(.001f, Amount(waterUnitsPerThirstPoint));
            refillThreshold = Mathf.Min(capacity, Amount(refillThreshold)); refillSeconds = Mathf.Max(.1f, Amount(refillSeconds));
            refillCost = Amount(refillCost); height = Mathf.Max(.1f, Amount(height));
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

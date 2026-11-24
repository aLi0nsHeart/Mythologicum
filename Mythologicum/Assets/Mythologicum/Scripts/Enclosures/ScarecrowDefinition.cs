using UnityEngine;

namespace Mythologicum
{
    /// <summary>Designer-owned cost and placeholder artwork for reusable fear-feeding enrichment.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Enclosures/Scarecrow", fileName = "Scarecrow")]
    public sealed class ScarecrowDefinition : ScriptableObject
    {
        private static ScarecrowDefinition current;
        public static ScarecrowDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ScarecrowDefinition>("Environment/Scarecrow");
                if (current == null)
                {
                    current = CreateInstance<ScarecrowDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        [Min(0f), Tooltip("Purchase price in park currency. Scarecrows can be used repeatedly without restocking.")]
        public float constructionCost = 25f;
        [Min(.1f)] public float height = 2f;
        public Color woodColor = new Color(.34f, .19f, .08f);
        public Color clothingColor = new Color(.25f, .42f, .62f);
        public Color strawColor = new Color(.83f, .65f, .31f);

        private void OnValidate()
        {
            constructionCost = float.IsNaN(constructionCost) || float.IsInfinity(constructionCost) ? 25f : Mathf.Max(0f, constructionCost);
            height = float.IsNaN(height) || float.IsInfinity(height) ? 2f : Mathf.Max(.1f, height);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

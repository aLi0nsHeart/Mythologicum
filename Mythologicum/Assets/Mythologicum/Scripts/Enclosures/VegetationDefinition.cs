using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Vegetation", fileName = "Vegetation")]
    public sealed class VegetationDefinition : ScriptableObject
    {
        public GameObject[] trees = new GameObject[0];
        public GameObject[] grasses = new GameObject[0];
        public GameObject[] herbs = new GameObject[0];
        public GameObject[] flowers = new GameObject[0];
        [Range(0, 6)] public int patchesPerCell = 2;
        public int scatterSeed = 271026;
        [Min(0.1f)] public float treeHeight = 3.4f;
        [Tooltip("Fraction of its tile counted as forest by a placed tree. This affects enclosure/environment statistics, independently of the tree's visual size.")]
        [Range(0f, 1f)] public float forestCoveragePerTree = .5f;
    }
}

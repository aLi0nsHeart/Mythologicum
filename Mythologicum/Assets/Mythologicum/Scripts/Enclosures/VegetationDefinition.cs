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
    }
}
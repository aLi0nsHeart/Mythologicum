using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class BuildingVisual
    {
        public BuildingKind kind;
        public GameObject prefab;
        [Min(1)] public int width = 2;
        [Min(1)] public int depth = 2;
        [Min(0.5f)] public float height = 3f;
        public float yaw;
        public int entranceColumn = -1;
    }

    [CreateAssetMenu(menuName = "Mythologicum/Building visuals", fileName = "BuildingVisuals")]
    public sealed class BuildingVisualCatalog : ScriptableObject
    {
        public BuildingVisual[] activeBuildings = new BuildingVisual[0];
        [Header("Reserved for future systems")]
        public GameObject researchCentre;
        public GameObject mercenary;
        public GameObject zookeeper;

        public void ApplyTo(BuildingDefinition[] definitions)
        {
            foreach (BuildingVisual visual in activeBuildings)
                foreach (BuildingDefinition definition in definitions)
                    if (definition.kind == visual.kind)
                    {
                        definition.visualPrefab = visual.prefab;
                        definition.visualYaw = visual.yaw;
                        definition.width = visual.width;
                        definition.depth = visual.depth;
                        definition.height = visual.height;
                        definition.entranceColumn = visual.entranceColumn;
                    }
        }
    }
}
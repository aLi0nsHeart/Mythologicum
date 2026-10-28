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
        [Header("Employee building models")]
        public GameObject researchCentre;
        public GameObject mercenary;
        public GameObject zookeeper;

        public void ApplyTo(BuildingDefinition[] definitions)
        {
            BuildingDefinition[] appearanceDefaults = BuildingDefinition.Defaults();
            foreach (BuildingDefinition definition in definitions)
            {
                // Reuse the imported models without rewriting the existing visual catalogue asset.
                GameObject staffModel = definition.kind == BuildingKind.ResearchCentre ? researchCentre
                    : definition.kind == BuildingKind.MercenaryHQ ? mercenary
                    : definition.kind == BuildingKind.ZookeeperHut ? zookeeper : null;
                if (staffModel != null && definition.visualPrefab == null)
                { definition.visualPrefab = staffModel; definition.visualYaw = 180f; }
                if (staffModel != null)
                {
                    // Existing Inspector catalogues may still contain the previous placeholder sizes.
                    BuildingDefinition appearance = Array.Find(appearanceDefaults, item => item.kind == definition.kind);
                    definition.width = appearance.width; definition.depth = appearance.depth; definition.height = appearance.height;
                }
            }
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

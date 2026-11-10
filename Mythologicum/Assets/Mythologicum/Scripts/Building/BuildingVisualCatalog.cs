using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class BuildingVisual
    {
        public BuildingKind kind;
        public GameObject prefab;
        // Kept only to read earlier asset versions. Physical dimensions belong to DefaultBuildings.
        [HideInInspector] public int width = 2;
        [HideInInspector] public int depth = 2;
        [HideInInspector] public float height = 3f;
        public float yaw;
        [HideInInspector] public int entranceColumn = -1;
    }

    [CreateAssetMenu(menuName = "Mythologicum/Building visuals", fileName = "BuildingVisuals")]
    public sealed class BuildingVisualCatalog : ScriptableObject
    {
        public BuildingVisual[] activeBuildings = new BuildingVisual[0];
        [Header("Employee building models")]
        public GameObject researchCentre;
        public GameObject mercenary;
        public GameObject zookeeper;
        public float researchCentreYaw = 180f;
        public float mercenaryYaw = 180f;
        public float zookeeperYaw = 180f;

        public void ApplyTo(BuildingDefinition[] definitions)
        {
            foreach (BuildingDefinition definition in definitions ?? Array.Empty<BuildingDefinition>())
            {
                if (definition == null || !definition.useVisualCatalogFallback || definition.visualPrefab != null) continue;
                BuildingVisual visual = Array.Find(activeBuildings ?? Array.Empty<BuildingVisual>(),
                    item => item != null && item.kind == definition.kind && item.prefab != null);
                if (visual != null)
                {
                    definition.visualPrefab = visual.prefab;
                    definition.visualYaw = visual.yaw;
                    continue;
                }
                // Legacy fallback art never changes the catalogue's footprint, door or balance.
                GameObject staffModel = definition.kind == BuildingKind.ResearchCentre ? researchCentre
                    : definition.kind == BuildingKind.MercenaryHQ ? mercenary
                    : definition.kind == BuildingKind.ZookeeperHut ? zookeeper : null;
                if (staffModel != null)
                {
                    definition.visualPrefab = staffModel;
                    definition.visualYaw = definition.kind == BuildingKind.ResearchCentre ? researchCentreYaw
                        : definition.kind == BuildingKind.MercenaryHQ ? mercenaryYaw : zookeeperYaw;
                }
            }
        }
    }
}

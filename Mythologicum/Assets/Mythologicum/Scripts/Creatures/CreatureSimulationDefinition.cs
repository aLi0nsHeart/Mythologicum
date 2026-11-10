using UnityEngine;

namespace Mythologicum
{
    /// <summary>Park-wide creature limits and shared food/transport tuning; species needs remain on CreatureDefinition.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Creature simulation settings", fileName = "SimulationSettings")]
    public sealed class CreatureSimulationDefinition : ScriptableObject
    {
        private static CreatureSimulationDefinition current;
        public static CreatureSimulationDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<CreatureSimulationDefinition>("Creatures/SimulationSettings");
                if (current == null)
                {
                    current = CreateInstance<CreatureSimulationDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        [Header("Population and placement")]
        [Min(1), Tooltip("Maximum number of living creature actors, including visitors and staff using the shared creature simulation.")]
        public int populationLimit = 100;
        [Min(0f), Tooltip("Minimum separation from another living actor when spawning or relocating, as a fraction of a grid tile.")]
        public float placementSpacingTiles = .7f;

        [Header("Natural food")]
        [Min(0f), Tooltip("Simulation seconds before grass in a grazed tile can be eaten again.")]
        public float grassRegrowthSeconds = 20f;

        [Header("Transport and removal")]
        [Min(.1f), Tooltip("Maximum distance in metres between a zookeeper and a sedated creature before pickup.")]
        public float carryPickupRange = 2.5f;
        [Min(0f), Tooltip("Seconds a corpse remains visible after death caused by staff or another external strike.")]
        public float corpseSeconds = 3f;
        [Min(0f), Tooltip("Extra seconds a killed prey remains visible after the predator's feeding animation.")]
        public float corpseAfterFeedingSeconds = 1f;

        private void OnValidate()
        {
            populationLimit = Mathf.Max(1, populationLimit);
            placementSpacingTiles = Mathf.Max(0, placementSpacingTiles);
            grassRegrowthSeconds = Mathf.Max(0, grassRegrowthSeconds);
            carryPickupRange = Mathf.Max(.1f, carryPickupRange);
            corpseSeconds = Mathf.Max(0, corpseSeconds);
            corpseAfterFeedingSeconds = Mathf.Max(0, corpseAfterFeedingSeconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

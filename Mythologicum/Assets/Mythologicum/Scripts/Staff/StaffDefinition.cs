using UnityEngine;

namespace Mythologicum
{
    public enum StaffRole { Researcher, Mercenary, Zookeeper }

    /// <summary>Shared employee tuning. Each employee keeps its own assignment and job state.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Staff settings", fileName = "DefaultStaff")]
    public sealed class StaffDefinition : ScriptableObject
    {
        [Header("Travel")]
        [Min(.1f)] public float walkSpeed = 2.2f;
        [Min(.1f)] public float runSpeed = 4f;
        [Min(.1f)] public float perceptionRange = 14f;
        [Min(.1f)] public float threatFleeRange = 3f;
        [Header("Decisions and actor stats")]
        [Tooltip("Maximum grid steps from the workplace entrance where a newly hired employee can appear.")]
        [Min(0)] public int hiringSpawnRadiusCells = 2;
        [Min(.05f)] public float decisionInterval = 1f;
        [Min(.05f)] public float fleeingDecisionInterval = .5f;
        [Min(1)] public float maximumHealth = 100f;
        [Min(0)] public float civilianThreat = 5f;
        [Min(0)] public float fearGainPerSecond = 30f;
        [Min(0)] public float fearRecoveryPerSecond = 20f;
        [Tooltip("How strongly retreating staff avoid long detours when choosing safe ground.")]
        [Min(0)] public float fleeingTravelPenalty = .05f;
        [Min(0)] public float offPathApproachPenalty = .5f;

        [Header("Mercenaries")]
        [Min(.1f)] public float shootingRange = 8f;
        [Min(.1f)] public float shotInterval = 1f;
        [Min(0f)] public float mercenaryDamage = 35f;
        [Min(1f)] public float mercenaryStrength = 35f;
        [Min(.1f)] public float regroupDistance = 3f;
        [Tooltip("Extra metres beyond shooting range at which mercenaries prefer to assemble.")]
        [Min(0)] public float regroupSafetyMargin = 1f;

        [Header("Animal care")]
        [Min(1f)] public float sedationSeconds = 90f;
        [Min(0f)] public float healPerSecond = 20f;
        [Min(0f)] public float repairPerSecond = 30f;
        [Min(.1f)] public float restockSeconds = 3f;
        [Tooltip("Keepers request a supply delivery when stock reaches this quantity. A feeder's own capacity still limits its stock.")]
        [Min(0)] public int feederRestockThreshold = 2;
        [Min(.1f)] public float handsOnRange = 2.5f;

        [Header("Creature research")]
        [Min(.1f)] public float studyRange = 10f;
        [Tooltip("Knowledge percentage points earned per second of active observation.")]
        [Min(0f)] public float researchPerSecond = 1f;
        [Tooltip("Laboratory work units produced by one researcher per second before technology upgrades.")]
        [Min(0)] public float technologyWorkPerSecond = 1f;
        public SpeciesResearchRules discoveries = new SpeciesResearchRules();

        private void OnValidate()
        {
            discoveries ??= new SpeciesResearchRules();
            discoveries.Validate();
            decisionInterval = Mathf.Max(.05f, decisionInterval);
            fleeingDecisionInterval = Mathf.Max(.05f, fleeingDecisionInterval);
        }
    }
}

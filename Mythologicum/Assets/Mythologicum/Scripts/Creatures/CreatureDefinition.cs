using System;
using UnityEngine;

namespace Mythologicum
{
    public enum CreatureDiet { Carnivore, Herbivore, None, Fear }

    [Flags]
    public enum CreatureTags { None = 0, Goat = 1, Predator = 2, Visitor = 4 }

    /// <summary>Editable species rules. Individual needs live in CreatureVitals and CreatureEnergy.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Creature species", fileName = "NewCreature")]
    public sealed class CreatureDefinition : ScriptableObject
    {
        [Header("Species")]
        public string displayName = "New creature";
        public CreatureDiet diet;
        [Tooltip("Categories other agents use to recognize this species.")]
        public CreatureTags tags;
        [Tooltip("A carnivore can hunt agents with any matching tag.")]
        public CreatureTags preyTags;
        [Tooltip("Legacy tag setting retained for existing profiles. Fear now compares threat values.")]
        public CreatureTags fearedTags;
        public bool manuallyPlaceable = true;

        [Header("Health, combat and origin")]
        public string origin = "Unknown";
        [Min(1f)] public float maximumHealth = 100f;
        [Min(0f)] public float damage = 40f;
        [Tooltip("Visible creatures with a higher threat cause fear, subject to perception and safe barriers.")]
        [Min(0f)] public float threat = 30f;

        [Header("Fear feeding")]
        [Tooltip("Relieves hunger by successfully frightening people rather than dealing damage.")]
        public bool feedsOnFear;
        [Min(0.1f)] public float scareRange = 3f;
        [Min(0.1f)] public float scareSeconds = 1.5f;
        [Min(0.1f)] public float scareCooldownSeconds = 8f;

        [Header("Hunger (0 = full; 100 = starving)")]
        [Range(0f, 100f)] public float initialHunger = 35f;
        [Min(0f)] public float hungerPerSecond = 2f;
        [Range(0f, 100f)] public float huntThreshold = 50f;
        [Tooltip("Hunger relieved by one successful meal.")]
        [Range(0f, 100f)] public float foodRelief = 60f;
        [Min(0.1f)] public float grazeSeconds = 3f;

        [Header("Energy (100 = rested; 0 = exhausted)")]
        [Range(0f, 100f)] public float initialEnergy = 100f;
        [Tooltip("Energy consumed each awake second. Sleep replenishes energy instead.")]
        [Min(0f)] public float energyDrainPerSecond = 0.35f;
        [Tooltip("Energy consumption multiplier while hunting, fleeing, or attacking.")]
        [Min(1f)] public float strenuousEnergyMultiplier = 3f;
        [Tooltip("Seek sleep when energy reaches this level.")]
        [Range(0f, 100f)] public float sleepThreshold = 20f;
        [Tooltip("Resume normal activity after recovering this much energy.")]
        [Range(0f, 100f)] public float wakeThreshold = 85f;
        [Min(0f)] public float sleepRecoveryPerSecond = 8f;

        [Header("Sleep and climbing")]
        [Tooltip("Time spent settling into new surroundings before non-urgent fence climbing. Severe hunger, exhaustion and fleeing override this caution.")]
        [Min(0f)] public float settlingSeconds = 45f;
        [Tooltip("Must reach and climb a tree before sleep can restore energy.")]
        public bool requiresTreeForSleep;
        [Tooltip("Can cross ordinary wooden fences by climbing. Spiked fences resist climbing.")]
        public bool canClimbWoodenFences;
        [Tooltip("Can phase through ordinary barriers while very hungry. Sanctified barriers still stop phasing.")]
        public bool canPhaseFences;
        [Range(0f, 100f)] public float phaseHungerThreshold = 80f;
        [Tooltip("Actively seeks to break vulnerable fences when a barrier obstructs its goal.")]
        public bool canBreakFences;
        [Tooltip("Must reach a gravestone before sleeping.")]
        public bool requiresGravestoneForSleep;
        [Min(0.1f)] public float treeClimbSeconds = 1.5f;
        [Min(0.1f)] public float fenceClimbSeconds = 2f;
        [Min(0.1f)] public float failedClimbSeconds = 1.2f;
        [Tooltip("Minimum delay before retrying a failed fence climb.")]
        [Min(0.1f)] public float climbRetrySeconds = 8f;
        [Range(0f, 100f)] public float climbEnergyCost = 5f;

        [Header("Fear (0 = calm; 100 = terrified)")]
        [Min(0f)] public float fearGainPerSecond = 50f;
        [Min(0f)] public float fearRecoveryPerSecond = 15f;
        [Range(0f, 100f)] public float fleeThreshold = 20f;

        [Header("Movement and perception")]
        [Min(0.1f)] public float walkSpeed = 1.5f;
        [Min(0.1f)] public float runSpeed = 4.5f;
        [Min(0.1f)] public float visionRange = 14f;
        [Tooltip("Maximum distance at which visible threats cause fear, independently of food detection.")]
        [Min(0.1f)] public float fearRange = 14f;
        [Min(0.1f)] public float interactionRange = 1.2f;
        [Tooltip("Seconds between expensive perception and route decisions.")]
        [Min(0.05f)] public float decisionInterval = 0.35f;

        [Header("Environment and space")]
        [Tooltip("Minimum physically accessible area, in square metres. Applies inside and outside enclosures.")]
        [Min(0f)] public float minimumSpace = 48f;
        [Tooltip("Trees needed nearby for environmental satisfaction.")]
        [Min(0)] public int requiredTrees;
        [Min(0)] public int requiredGravestones;
        [Min(0.1f)] public float environmentRadius = 12f;

        [Header("Social needs")]
        [Tooltip("Desired nearby companions of the same species, excluding this animal.")]
        [Min(0)] public int minimumCompanions;
        [Tooltip("Maximum nearby companions; -1 means no maximum. Zero means solitary.")]
        public int maximumCompanions = -1;
        [Min(0.1f)] public float herdRange = 12f;
        [Min(0.1f)] public float socializingSeconds = 3f;
        [Min(1f)] public float socializingEnergyMultiplier = 2f;

        [Header("Happiness")]
        [Tooltip("Below this happiness, agitation gradually intensifies.")]
        [Range(0f, 100f)] public float happinessThreshold = 50f;
        [Tooltip("Maximum happiness points gained or lost each second.")]
        [Min(0f)] public float happinessChangePerSecond = 4f;

        [Header("Escape intentions (chance per minute)")]
        [Range(0f, 1f)] public float randomEscapeChance = 0.0005f;
        [Range(0f, 1f)] public float hungryEscapeChance = 0.3f;
        [Range(0f, 1f)] public float agitatedEscapeChance = 0.35f;
        [Range(0f, 1f)] public float crowdedEscapeChance = 0.35f;
        [Min(1f)] public float escapeRetrySeconds = 30f;
        [Header("Behaviour timing")]
        [Min(0f)] public float pauseMinSeconds = 1.5f;
        [Min(0f)] public float pauseMaxSeconds = 4f;
        [Tooltip("Seconds a hunter remembers prey after losing sight of it.")]
        [Min(0f)] public float preyMemorySeconds = 5f;
        [Min(0.1f)] public float attackSeconds = 0.8f;
        [Min(0.1f)] public float feedingSeconds = 4f;
        [Tooltip("Preferred distance from a threat before settling down.")]
        [Min(0.1f)] public float fleeSafeDistance = 6f;
        [Min(1f)] public float agitatedSpeedMultiplier = 1.35f;

        [Header("Presentation")]
        [Tooltip("Optional visual prefab. Movement and collisions remain controlled by the creature simulation.")]
        public GameObject visualPrefab;
        public Vector3 visualScale = Vector3.one;

        [Header("Placeholder appearance and simulation bounds")]
        public Color color = new Color(0.85f, 0.8f, 0.65f);
        public Vector3 bodyScale = new Vector3(0.8f, 0.8f, 1.1f);
    }
}

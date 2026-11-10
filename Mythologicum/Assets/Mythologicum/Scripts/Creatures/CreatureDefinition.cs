using System;
using UnityEngine;

namespace Mythologicum
{
    public enum CreatureDiet { Carnivore, Herbivore, None, Fear }

    [Flags]
    public enum CreatureTags { None = 0, Goat = 1, Predator = 2, Visitor = 4, Staff = 8 }

    /// <summary>Editable species rules. Individual needs live in CreatureVitals and CreatureEnergy.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Creature species", fileName = "NewCreature")]
    public sealed class CreatureDefinition : ScriptableObject
    {
        [Header("Species")]
        public string displayName = "creature.name.new";
        public string DisplayName => Loc.Resolve(displayName);
        public CreatureDiet diet;
        [Tooltip("Categories other agents use to recognize this species.")]
        public CreatureTags tags;
        [Tooltip("A carnivore can hunt agents with any matching tag.")]
        public CreatureTags preyTags;
        [Tooltip("Legacy tag setting retained for existing profiles. Fear now compares threat values.")]
        public CreatureTags fearedTags;
        public bool manuallyPlaceable = true;
        [Tooltip("Expedition rarity and difficulty tier, independent of combat threat.")]
        [Range(1, 5)] public int rating = 1;

        [Header("Health, combat and origin")]
        public string origin = "creature.origin.unknown";
        public string Origin => Loc.Resolve(origin);
        public bool IsDomestic => (tags & CreatureTags.Goat) != 0
            || origin == "creature.origin.domestic" || origin == "Domestic";
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
        [Tooltip("Additional species behaviour and happiness weights. Weights are normalized automatically; they do not need to add up to one.")]
        public CreatureBehaviourSettings behaviour = new CreatureBehaviourSettings();
        public CreatureBehaviourSettings Behaviour => behaviour ?? (behaviour = new CreatureBehaviourSettings());
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

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            damage = Mathf.Max(0f, damage); threat = Mathf.Max(0f, threat);
            pauseMinSeconds = Mathf.Max(0f, pauseMinSeconds);
            pauseMaxSeconds = Mathf.Max(pauseMinSeconds, pauseMaxSeconds);
            sleepThreshold = Mathf.Clamp(sleepThreshold, 0f, 99.9f);
            wakeThreshold = Mathf.Clamp(wakeThreshold, sleepThreshold + .1f, 100f);
            minimumCompanions = Mathf.Max(0, minimumCompanions);
            maximumCompanions = maximumCompanions < 0 ? -1 : Mathf.Max(minimumCompanions, maximumCompanions);
            decisionInterval = Mathf.Max(.05f, decisionInterval);
            minimumSpace = Mathf.Max(0f, minimumSpace);
            requiredTrees = Mathf.Max(0, requiredTrees); requiredGravestones = Mathf.Max(0, requiredGravestones);
            Behaviour.Validate();
        }
    }

    /// <summary>Extra species tuning; plain data so need evaluation remains testable without a Unity scene.</summary>
    [Serializable]
    public sealed class CreatureBehaviourSettings
    {
        [Header("Happiness contribution weights")]
        [Range(0f, 100f), Tooltip("Happiness of newly placed or dispensed creatures. Saved individuals retain their saved happiness.")]
        public float initialHappiness = 75f;
        [Min(0f)] public float hungerWeight = .30f;
        [Min(0f)] public float fearWeight = .10f;
        [Min(0f)] public float environmentWeight = .25f;
        [Min(0f)] public float spaceWeight = .20f;
        [Min(0f)] public float herdWeight = .15f;

        [Header("Socializing and local exploration")]
        [Min(.1f), Tooltip("Maximum distance in metres for starting and continuing a social interaction.")]
        public float socialInteractionRange = 3f;
        [Min(1), Tooltip("Maximum grid steps examined when selecting a normal wander destination.")]
        public int wanderSearchSteps = 4;
        [Min(1), Tooltip("Maximum grid steps examined while looking for edible ground vegetation.")]
        public int grazingSearchSteps = 5;
        [Range(0f, 1f), Tooltip("Multiplier on ordinary pause duration at maximum agitation.")]
        public float agitatedPauseMultiplier = .4f;
        [Min(.05f), Tooltip("Seconds between habitat and herd measurements. Shorter values use more CPU.")]
        public float needsCheckSeconds = .5f;

        [Header("Urgency and escape attempts")]
        [Range(0f, 100f), Tooltip("Hunger that overrides initial settling caution and permits fence attacks.")]
        public float urgentHunger = 80f;
        [Range(0f, 100f), Tooltip("Energy low enough to override settling caution when seeking a sleeping site.")]
        public float urgentEnergy = 8f;
        [Range(0f, 100f), Tooltip("Hunger where hunger-driven escape pressure begins; it reaches full strength at 100.")]
        public float escapeHungerStart = 75f;
        [Range(0f, 1f), Tooltip("Cap on combined escape intention chance per simulation minute.")]
        public float maximumEscapeChance = .95f;
        [Min(.1f), Tooltip("Maximum duration of one escape intention, in seconds.")]
        public float escapeAttemptSeconds = 30f;
        [Min(.05f)] public float crowdingCheckSeconds = .5f;
        [Min(1), Tooltip("Local search distance, in grid steps, when leaving through a gap or moving away from a crowd.")]
        public int escapeSearchSteps = 6;
        [Min(0f), Tooltip("Preference score for ground beyond Flee Safe Distance, reduced by travel steps. Higher values favour reaching safety over moving farther while still exposed.")]
        public float safeEscapeScore = 1000f;

        [Header("Special abilities and combat effort")]
        [Min(.1f), Tooltip("Seconds to phase through a fence.")]
        public float phaseSeconds = 1.5f;
        [Range(0f, 100f), Tooltip("Immediate fear points caused by a successful scare.")]
        public float scareFearImpulse = 15f;
        [Min(0f), Tooltip("Immediate fear after surviving a hit, expressed as seconds of the victim's normal fear gain.")]
        public float attackFearSeconds = .25f;
        [Range(0f, 100f)] public float fenceStrikeEnergyCost = 1f;
        [Min(1), Tooltip("Maximum fence strikes in one attack before pausing and reconsidering.")]
        public int maximumFenceStrikes = 30;

        public void Validate()
        {
            initialHappiness = Mathf.Clamp(initialHappiness, 0, 100);
            hungerWeight = Mathf.Max(0, hungerWeight); fearWeight = Mathf.Max(0, fearWeight);
            environmentWeight = Mathf.Max(0, environmentWeight); spaceWeight = Mathf.Max(0, spaceWeight);
            herdWeight = Mathf.Max(0, herdWeight);
            socialInteractionRange = Mathf.Max(.1f, socialInteractionRange);
            wanderSearchSteps = Mathf.Max(1, wanderSearchSteps); grazingSearchSteps = Mathf.Max(1, grazingSearchSteps);
            agitatedPauseMultiplier = Mathf.Clamp01(agitatedPauseMultiplier);
            needsCheckSeconds = Mathf.Max(.05f, needsCheckSeconds); crowdingCheckSeconds = Mathf.Max(.05f, crowdingCheckSeconds);
            urgentHunger = Mathf.Clamp(urgentHunger, 0, 100); urgentEnergy = Mathf.Clamp(urgentEnergy, 0, 100);
            escapeHungerStart = Mathf.Clamp(escapeHungerStart, 0, 100); maximumEscapeChance = Mathf.Clamp01(maximumEscapeChance);
            escapeAttemptSeconds = Mathf.Max(.1f, escapeAttemptSeconds); escapeSearchSteps = Mathf.Max(1, escapeSearchSteps);
            safeEscapeScore = Mathf.Max(0, safeEscapeScore);
            phaseSeconds = Mathf.Max(.1f, phaseSeconds); scareFearImpulse = Mathf.Clamp(scareFearImpulse, 0, 100);
            attackFearSeconds = Mathf.Max(0, attackFearSeconds); fenceStrikeEnergyCost = Mathf.Clamp(fenceStrikeEnergyCost, 0, 100);
            maximumFenceStrikes = Mathf.Max(1, maximumFenceStrikes);
        }
    }
}

using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class VisitorMoodRules
    {
        [Tooltip("Satisfaction of a newly arriving visitor, before any experiences. 0-100.")]
        [Range(0, 100)] public float initialSatisfaction = 60f;
        [Tooltip("Base patience budget in simulation seconds. Satisfaction changes how quickly it is spent.")]
        [Min(.1f)] public float patienceSeconds = 120f;
        [Range(0, 100)] public float hungerDiscomfortThreshold = 50f;
        [Range(.1f, 100)] public float tiredDiscomfortThreshold = 30f;
        [Tooltip("Satisfaction points gained per second while watching a creature.")]
        [Min(0)] public float watchingGainPerSecond = .8f;
        [Min(0)] public float serviceGainPerSecond = .6f;
        [Min(0)] public float baselineLossPerSecond = .04f;
        [Min(0)] public float maximumHungerLossPerSecond = .9f;
        [Min(0)] public float maximumTiredLossPerSecond = .8f;
        [Min(0)] public float maximumFearLossPerSecond = 2.5f;
        [Tooltip("Patience spent per second by a visitor at zero satisfaction.")]
        [Min(0)] public float unhappyPatienceRate = 2f;
        [Tooltip("Patience spent per second by a visitor at full satisfaction.")]
        [Min(0)] public float happyPatienceRate = .4f;
        [Range(0, 100)] public float leaveSatisfactionThreshold = 15f;
    }

    /// <summary>Editable visitor tuning; each visitor keeps its own needs and satisfaction.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Visitor profile", fileName = "DefaultVisitors")]
    public sealed class VisitorDefinition : ScriptableObject
    {
        [Header("Appearance")]
        public PeopleDefinition people;

        [Header("Arrivals")]
        [Min(.1f)] public float spawnInterval = 8f;
        [Min(0)] public int maximumVisitors = 30;
        [HideInInspector] public float ticketPrice = 15f; // Legacy data; the entrance now owns its editable price.

        [Header("Needs")]
        [Min(1)] public float maximumHealth = 60f;
        [Tooltip("Threat value used when deciding which creatures frighten visitors.")]
        [Min(0)] public float threat = 10f;
        [Range(0f, 100f)] public float initialHunger = 20f;
        [Range(0f, 100f)] public float initialEnergy = 80f;
        [Min(0f)] public float hungerPerSecond = .7f;
        [Min(0f)] public float energyDrainPerSecond = .45f;
        [Range(0f, 100f)] public float hungerThreshold = 50f;
        [Range(0f, 100f)] public float restThreshold = 30f;
        [Range(0f, 100f)] public float comfortableEnergy = 85f;

        [Header("Movement and perception")]
        [Min(.1f)] public float walkSpeed = 1.6f;
        [Min(.1f)] public float runSpeed = 4.8f;
        [Min(.1f)] public float viewRange = 10f;
        [Min(.1f)] public float fearRange = 6f;
        [Min(0f)] public float fearGainPerSecond = 60f;
        [Min(0f)] public float fearRecoveryPerSecond = 20f;
        [Min(.1f)] public float fleeSafeDistance = 10f;
        [Min(.05f)] public float decisionInterval = .5f;
        [Min(1)] public float fleeingEnergyMultiplier = 3f;
        [Range(0, 100)] public float panicFearThreshold = 20f;
        [Range(0, 100)] public float predatorEncounterPenalty = 25f;

        [Header("Visit")]
        [Min(.1f)] public float watchingSeconds = 6f;
        [Min(0f)] public float minimumStaySeconds = 30f;
        [Min(.1f)] public float maximumStaySeconds = 300f;
        [Header("Viewing choices")]
        [Tooltip("Seconds before a visitor wants to watch the same individual again.")]
        [Min(0)] public float exhibitRevisitSeconds = 30f;
        [Min(0), Tooltip("Preferred distance from an exhibit, in metres.")] public float preferredViewingDistance = 5f;
        [Min(0), Tooltip("Viewing-choice score lost per metre away from the preferred distance.")] public float viewingDistancePenalty = 2f;
        [Min(0), Tooltip("Viewing-choice score lost per grid step of travel.")] public float viewingTravelPenalty = 1f;
        [Header("Escape choices")]
        [Min(0)] public float safeEscapeScore = 1000f;
        [Min(0)] public float escapePathPreference = .1f;
        [Header("Satisfaction and patience")]
        public VisitorMoodRules mood = new VisitorMoodRules();
        [Min(0)] public float mealSatisfactionReward = 8f;
        [Min(0)] public float restSatisfactionReward = 6f;
        [Min(0)] public float exhibitSatisfactionReward = 4f;
        [Min(0)] public float expensiveServicePenalty = 2f;
        [Tooltip("Minimum seconds between complaints about the same type of expensive service.")]
        [Min(0)] public float priceComplaintCooldownSeconds = 30f;
        [Header("Simulation updates")]
        [Tooltip("Seconds between visibility recounts for park reputation. Construction changes refresh immediately.")]
        [Min(.05f)] public float reputationRefreshSeconds = 5f;

        private void OnValidate()
        {
            maximumStaySeconds = Mathf.Max(minimumStaySeconds, maximumStaySeconds);
            comfortableEnergy = Mathf.Max(restThreshold, comfortableEnergy);
            mood ??= new VisitorMoodRules();
            mood.patienceSeconds = Mathf.Max(.1f, mood.patienceSeconds);
            mood.tiredDiscomfortThreshold = Mathf.Max(.1f, mood.tiredDiscomfortThreshold);
        }
    }
}

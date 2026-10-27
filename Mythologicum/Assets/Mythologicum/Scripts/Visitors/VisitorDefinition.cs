using UnityEngine;

namespace Mythologicum
{
    /// <summary>Editable visitor tuning; each visitor keeps its own needs and satisfaction.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Visitor profile", fileName = "DefaultVisitors")]
    public sealed class VisitorDefinition : ScriptableObject
    {
        [Header("Appearance")]
        public PeopleDefinition people;

        [Header("Arrivals")]
        [Min(.1f)] public float spawnInterval = 8f;
        [Min(0)] public int maximumVisitors = 30;
        [Min(0f)] public float ticketPrice = 15f;

        [Header("Needs")]
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

        [Header("Visit")]
        [Min(.1f)] public float watchingSeconds = 6f;
        [Min(0f)] public float minimumStaySeconds = 30f;
        [Min(.1f)] public float maximumStaySeconds = 300f;
    }
}

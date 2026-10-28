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

        [Header("Mercenaries")]
        [Min(.1f)] public float shootingRange = 8f;
        [Min(.1f)] public float shotInterval = 1f;
        [Min(0f)] public float mercenaryDamage = 35f;
        [Min(1f)] public float mercenaryStrength = 35f;
        [Min(.1f)] public float regroupDistance = 3f;

        [Header("Animal care")]
        [Min(1f)] public float sedationSeconds = 90f;
        [Min(0f)] public float healPerSecond = 20f;
        [Min(0f)] public float repairPerSecond = 30f;
        [Min(.1f)] public float restockSeconds = 3f;

        [Header("Creature research")]
        [Min(.1f)] public float studyRange = 10f;
        [Tooltip("Knowledge percentage points earned per second of active observation.")]
        [Min(0f)] public float researchPerSecond = 1f;
    }
}

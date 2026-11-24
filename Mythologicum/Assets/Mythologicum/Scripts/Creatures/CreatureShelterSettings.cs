using System;
using UnityEngine;

namespace Mythologicum
{
    public enum CreatureShelterMode { Legacy, Ground, TreeCanopy, TreeShade, Gravestone, Burrow, Cave, Water }

    /// <summary>Species preferences, independent of enclosure identity and individual sleep state.</summary>
    [Serializable]
    public sealed class CreatureShelterSettings
    {
        public CreatureShelterMode mode;
        public bool requiredForSleep;
        public bool retreatWhenAfraid = true;
        [Min(.1f), Tooltip("Maximum distance in metres at which the creature looks for an existing sleep or retreat site.")]
        public float searchRadius = 32f;
        [Min(0), Tooltip("A ground shade retreat must be at least this far from the last threat. Elevated and enclosed shelters provide their own protection.")]
        public float retreatSafeDistance = 4f;
        [Min(0), Tooltip("Height above the tree tile used by canopy sleepers.")]
        public float treeHeight = 1.8f;
        [Range(0, 100), Tooltip("Hidden or protected creatures stop retreating once fear falls to this level and the threat has left.")]
        public float calmFearThreshold = 10f;
        [Min(1), Tooltip("Fear recovery multiplier while a protected creature is hiding in its shelter.")]
        public float fearRecoveryMultiplier = 3f;

        private static readonly CreatureShelterSettings Ground = new CreatureShelterSettings { mode = CreatureShelterMode.Ground, retreatWhenAfraid = false };
        private static readonly CreatureShelterSettings Canopy = Legacy(CreatureShelterMode.TreeCanopy, true);
        private static readonly CreatureShelterSettings Shade = Legacy(CreatureShelterMode.TreeShade, false);
        private static readonly CreatureShelterSettings Grave = Legacy(CreatureShelterMode.Gravestone, true);
        private static readonly CreatureShelterSettings Burrow = Legacy(CreatureShelterMode.Burrow, true);
        private static CreatureShelterSettings Legacy(CreatureShelterMode mode, bool required) => new CreatureShelterSettings
            { mode = mode, requiredForSleep = required, retreatWhenAfraid = false, searchRadius = 100000f };

        public static CreatureShelterSettings Resolve(CreatureDefinition definition)
        {
            if (definition?.shelter != null && definition.shelter.mode != CreatureShelterMode.Legacy) return definition.shelter;
            if (definition == null) return Ground;
            if (definition.requiresBurrowForSleep) return Burrow;
            if (definition.requiresGravestoneForSleep) return Grave;
            if (definition.requiresTreeForSleep) return Canopy;
            return definition.prefersTreeShadeForSleep ? Shade : Ground;
        }
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(CreatureShelterMode), mode)) mode = CreatureShelterMode.Ground;
            searchRadius = Positive(searchRadius, .1f); retreatSafeDistance = Amount(retreatSafeDistance);
            treeHeight = Amount(treeHeight); calmFearThreshold = Mathf.Clamp(Amount(calmFearThreshold), 0, 100);
            fearRecoveryMultiplier = Positive(fearRecoveryMultiplier, 1);
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
        private static float Positive(float value, float minimum) => Math.Max(minimum, Amount(value));
    }
}

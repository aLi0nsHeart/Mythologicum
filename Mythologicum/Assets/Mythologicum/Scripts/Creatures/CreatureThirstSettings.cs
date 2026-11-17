using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Species hydration needs; high thirst means a greater need to drink.</summary>
    [Serializable]
    public sealed class CreatureThirstSettings
    {
        public bool needsWater = true;
        [Range(0, 100)] public float initialThirst = 15f;
        [Min(0)] public float thirstPerSecond = .15f;
        [Range(0, 100)] public float drinkThreshold = 55f;
        [Range(0, 100)] public float criticalThirst = 85f;
        [Range(0, 100)] public float satisfiedThirst = 5f;
        [Min(0)] public float drinkReliefPerSecond = 25f;
        [Min(.1f)] public float drinkSeconds = 3f;
        [Min(.1f), Tooltip("Maximum distance in metres to search for reachable water. Fences and solid obstacles still block access.")]
        public float searchRadius = 40f;
        public float StartingThirst => needsWater ? Stat(initialThirst) : 0f;
        public float Rate => needsWater ? Amount(thirstPerSecond) : 0f;
        public void Validate()
        {
            initialThirst = Stat(initialThirst); thirstPerSecond = Amount(thirstPerSecond);
            drinkThreshold = Stat(drinkThreshold); criticalThirst = Mathf.Max(drinkThreshold, Stat(criticalThirst));
            satisfiedThirst = Mathf.Min(drinkThreshold, Stat(satisfiedThirst));
            drinkReliefPerSecond = Amount(drinkReliefPerSecond); drinkSeconds = Mathf.Max(.1f, Amount(drinkSeconds));
            searchRadius = Mathf.Max(.1f, Amount(searchRadius));
        }
        private static float Stat(float value) => Mathf.Min(100, Amount(value));
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    }
}

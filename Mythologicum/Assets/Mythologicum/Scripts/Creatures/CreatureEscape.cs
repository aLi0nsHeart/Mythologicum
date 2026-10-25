using System;

namespace Mythologicum
{
    /// <summary>Chance of forming an escape intention, distinct from successfully crossing a barrier.</summary>
    public static class CreatureEscape
    {
        public static float Chance(float hunger, float agitation, float crowding, float rareChance,
            float hungerChance, float agitationChance, float crowdingChance) =>
            Math.Min(.95f, Clamp(rareChance) + Clamp((hunger - 75f) / 25f) * Clamp(hungerChance)
                + Clamp(agitation) * Clamp(agitationChance) + Clamp(crowding) * Clamp(crowdingChance));
        public static float Overcrowding(float area, float requiredArea) => requiredArea > 0f
            ? Clamp(1f - Math.Max(0f, area) / requiredArea) : 0f;
        public static float Probability(float perMinute, float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return 0f;
            return (float)(1d - Math.Pow(1d - Clamp(perMinute), seconds / 60d));
        }
        private static float Clamp(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
    }
}

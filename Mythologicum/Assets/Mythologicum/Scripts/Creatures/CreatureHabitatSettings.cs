using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class CreatureBiomePreference
    {
        public HabitatBiome biome;
        [Range(0, 1)] public float minimumFraction;
        [Range(0, 1)] public float maximumFraction = 1;
        [Min(0)] public float weight = 1;
    }

    /// <summary>Ground preferences concern the creature's local reachable habitat, not an enclosure assignment.</summary>
    [Serializable]
    public sealed class CreatureHabitatSettings
    {
        public CreatureBiomePreference[] preferences = Array.Empty<CreatureBiomePreference>();
        public bool HasPreferences => preferences != null && preferences.Length > 0;

        public float Evaluate(float[] fractions)
        {
            double score = 0, weights = 0;
            foreach (var item in preferences ?? Array.Empty<CreatureBiomePreference>())
            {
                if (item == null || !Enum.IsDefined(typeof(HabitatBiome), item.biome)) continue;
                float weight = Amount(item.weight), minimum = Mathf.Clamp01(Amount(item.minimumFraction));
                float maximum = Mathf.Max(minimum, Mathf.Clamp01(Amount(item.maximumFraction)));
                float fraction = fractions != null && (int)item.biome < fractions.Length ? Mathf.Clamp01(Amount(fractions[(int)item.biome])) : 0;
                float value = fraction < minimum ? fraction / Mathf.Max(.0001f, minimum)
                    : fraction > maximum ? (1 - fraction) / Mathf.Max(.0001f, 1 - maximum) : 1;
                score += Mathf.Clamp01(value) * weight; weights += weight;
            }
            return weights > 0 ? (float)(100 * score / weights) : 100;
        }
        public void Validate()
        {
            preferences ??= Array.Empty<CreatureBiomePreference>();
            foreach (var item in preferences)
                if (item != null)
                {
                    item.minimumFraction = Mathf.Clamp01(Amount(item.minimumFraction));
                    item.maximumFraction = Mathf.Max(item.minimumFraction, Mathf.Clamp01(Amount(item.maximumFraction)));
                    item.weight = Amount(item.weight);
                }
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    }
}

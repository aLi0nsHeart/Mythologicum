using System;
using UnityEngine;

namespace Mythologicum
{
    // Zero retains the original visitor behaviour for old saves and validation fixtures.
    public enum VisitorType { Legacy, ThrillSeekers, Wonderlust, Adventurers, Children }

    [Serializable]
    public sealed class VisitorTypeSettings
    {
        private static readonly VisitorTypeSettings legacy = new VisitorTypeSettings { type = VisitorType.Legacy, arrivalWeight = 0,
            interestChoiceWeight = 0, minimumExhibitReward = 1, maximumExhibitReward = 1 };
        public VisitorType type;
        public string displayName = "visitor.type.legacy", description = "visitor.type.legacy.description";
        public string DisplayName => Loc.Resolve(displayName);
        public string Description => Loc.Resolve(description);
        [Min(0), Tooltip("Relative share of new arrivals. Zero disables this type; weights need not add up to one.")]
        public float arrivalWeight = 1f;
        [Header("Needs and movement multipliers")]
        [Min(0)] public float hungerMultiplier = 1f;
        [Min(0)] public float energyMultiplier = 1f;
        [Min(0)] public float hungerThresholdMultiplier = 1f;
        [Min(0)] public float restThresholdMultiplier = 1f;
        [Min(0)] public float walkSpeedMultiplier = 1f;
        [Min(0)] public float runSpeedMultiplier = 1f;
        [Min(0)] public float fearGainMultiplier = 1f;
        [Min(0)] public float fearLossMultiplier = 1f;
        [Min(.1f)] public float patienceMultiplier = 1f;
        [Min(.1f)] public float watchingSecondsMultiplier = 1f;
        [Tooltip("Actor/collider dimensions when using this type. Model feet remain on the ground.")]
        public Vector3 bodyScale = new Vector3(.5f, .85f, .5f);
        [Header("Exhibit interests")]
        [Min(0), Tooltip("Viewing-choice points awarded for full interest, before travel and distance penalties.")]
        public float interestChoiceWeight = 12f;
        [Min(0), Tooltip("Weight of this type's species appeal value.")]
        public float appealWeight = .8f;
        [Min(0), Tooltip("Weight of expedition rarity (rating), normalized from rating 1-5.")]
        public float rarityWeight = .1f;
        [Min(0)] public float newSpeciesBonus = .1f;
        [Min(0)] public float newOriginBonus;
        [Min(0)] public float minimumExhibitReward = .35f;
        [Min(0)] public float maximumExhibitReward = 1.6f;
        [Min(0)] public float exhibitRewardMultiplier = 1f;
        [Min(0)] public float serviceRewardMultiplier = 1f;
        [Min(0)] public float revisitMultiplier = 1f;

        public float RewardMultiplier(float interest) => type == VisitorType.Legacy ? 1f
            : (VisitorInterests.NonNegative(minimumExhibitReward)
                + (Math.Max(VisitorInterests.NonNegative(minimumExhibitReward), VisitorInterests.NonNegative(maximumExhibitReward))
                    - VisitorInterests.NonNegative(minimumExhibitReward)) * VisitorInterests.Unit(interest))
                * VisitorInterests.Factor(exhibitRewardMultiplier);

        public void Validate()
        {
            arrivalWeight = VisitorInterests.NonNegative(arrivalWeight);
            hungerMultiplier = VisitorInterests.Factor(hungerMultiplier); energyMultiplier = VisitorInterests.Factor(energyMultiplier);
            hungerThresholdMultiplier = VisitorInterests.Factor(hungerThresholdMultiplier); restThresholdMultiplier = VisitorInterests.Factor(restThresholdMultiplier);
            walkSpeedMultiplier = VisitorInterests.Factor(walkSpeedMultiplier); runSpeedMultiplier = VisitorInterests.Factor(runSpeedMultiplier);
            fearGainMultiplier = VisitorInterests.Factor(fearGainMultiplier); fearLossMultiplier = VisitorInterests.Factor(fearLossMultiplier);
            patienceMultiplier = Math.Max(.1f, VisitorInterests.Factor(patienceMultiplier));
            watchingSecondsMultiplier = Math.Max(.1f, VisitorInterests.Factor(watchingSecondsMultiplier));
            bodyScale = new Vector3(Math.Max(.1f, VisitorInterests.Factor(bodyScale.x)), Math.Max(.1f, VisitorInterests.Factor(bodyScale.y)), Math.Max(.1f, VisitorInterests.Factor(bodyScale.z)));
            interestChoiceWeight = VisitorInterests.NonNegative(interestChoiceWeight);
            appealWeight = VisitorInterests.NonNegative(appealWeight); rarityWeight = VisitorInterests.NonNegative(rarityWeight);
            newSpeciesBonus = VisitorInterests.NonNegative(newSpeciesBonus); newOriginBonus = VisitorInterests.NonNegative(newOriginBonus);
            minimumExhibitReward = VisitorInterests.NonNegative(minimumExhibitReward);
            maximumExhibitReward = Math.Max(minimumExhibitReward, VisitorInterests.NonNegative(maximumExhibitReward));
            exhibitRewardMultiplier = VisitorInterests.Factor(exhibitRewardMultiplier); serviceRewardMultiplier = VisitorInterests.Factor(serviceRewardMultiplier);
            revisitMultiplier = VisitorInterests.Factor(revisitMultiplier);
        }

        public static VisitorTypeSettings[] Defaults() => new[]
        {
            new VisitorTypeSettings { type = VisitorType.ThrillSeekers, displayName = "visitor.type.thrill_seekers", description = "visitor.type.thrill_seekers.description",
                appealWeight = .85f, rarityWeight = .15f, newSpeciesBonus = 0, fearGainMultiplier = .7f, fearLossMultiplier = .7f, watchingSecondsMultiplier = 1.1f },
            new VisitorTypeSettings { type = VisitorType.Wonderlust, displayName = "visitor.type.wonderlust", description = "visitor.type.wonderlust.description",
                appealWeight = .9f, rarityWeight = 0, newSpeciesBonus = .05f, newOriginBonus = .05f, patienceMultiplier = 1.05f, watchingSecondsMultiplier = 1.25f },
            new VisitorTypeSettings { type = VisitorType.Adventurers, displayName = "visitor.type.adventurers", description = "visitor.type.adventurers.description",
                appealWeight = .45f, rarityWeight = .2f, newSpeciesBonus = .2f, newOriginBonus = .15f,
                patienceMultiplier = 1.15f, energyMultiplier = .9f, watchingSecondsMultiplier = .9f, revisitMultiplier = 2f },
            new VisitorTypeSettings { type = VisitorType.Children, displayName = "visitor.type.children", description = "visitor.type.children.description",
                appealWeight = .95f, rarityWeight = 0, newSpeciesBonus = .05f, hungerMultiplier = 1.2f, energyMultiplier = 1.3f,
                hungerThresholdMultiplier = .85f, restThresholdMultiplier = 1.15f, walkSpeedMultiplier = .9f,
                fearGainMultiplier = 1.25f, fearLossMultiplier = 1.25f, patienceMultiplier = .75f, watchingSecondsMultiplier = .8f,
                serviceRewardMultiplier = 1.2f, revisitMultiplier = .75f, bodyScale = new Vector3(.4f, .55f, .4f) }
        };

        public static VisitorTypeSettings DefaultFor(VisitorType type)
        {
            if (!VisitorInterests.IsRequestedType(type)) return legacy;
            foreach (var entry in Defaults()) if (entry.type == type) return entry;
            return legacy;
        }
    }

    /// <summary>Pure interest and arrival selection. Real danger is handled separately by visitor safety.</summary>
    public static class VisitorInterests
    {
        public static bool IsRequestedType(VisitorType type) => type >= VisitorType.ThrillSeekers && type <= VisitorType.Children;
        public static float Unit(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value));
        public static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        public static float Factor(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, value);

        public static float Interest(VisitorTypeSettings settings, float appeal, int rating, bool seenSpecies, bool seenOrigin)
        {
            if (settings == null || settings.type == VisitorType.Legacy) return 1f;
            double score = Unit(appeal) * (double)NonNegative(settings.appealWeight)
                + Unit((rating - 1f) / 4f) * (double)NonNegative(settings.rarityWeight)
                + (seenSpecies ? 0d : NonNegative(settings.newSpeciesBonus))
                + (seenOrigin ? 0d : NonNegative(settings.newOriginBonus));
            return (float)Math.Max(0d, Math.Min(1d, score));
        }

        public static VisitorType Pick(VisitorTypeSettings[] settings, double sample)
        {
            double total = 0;
            if (settings == null) return VisitorType.Legacy;
            for (int i = 0; i < settings.Length; i++) if (Selectable(settings, i)) total += NonNegative(settings[i].arrivalWeight);
            if (total <= 0) return VisitorType.Legacy;
            sample = double.IsNaN(sample) || double.IsInfinity(sample) ? 0 : Math.Max(0, Math.Min(1, sample));
            double point = sample * total;
            VisitorType last = VisitorType.Legacy;
            for (int i = 0; i < settings.Length; i++)
            {
                if (!Selectable(settings, i)) continue;
                double weight = NonNegative(settings[i].arrivalWeight);
                if (weight <= 0) continue;
                last = settings[i].type;
                if (point < weight) return last;
                point -= weight;
            }
            return last;
        }
        private static bool Selectable(VisitorTypeSettings[] settings, int index)
        {
            var entry = settings[index];
            if (entry == null || !IsRequestedType(entry.type)) return false;
            for (int i = 0; i < index; i++) if (settings[i] != null && settings[i].type == entry.type) return false;
            return true;
        }
    }
}

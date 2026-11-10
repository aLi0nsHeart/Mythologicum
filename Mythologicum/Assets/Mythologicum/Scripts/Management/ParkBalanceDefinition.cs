using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class ParkEconomyRules
    {
        [Tooltip("Cash available when starting a new park. Loading a save keeps its saved balance.")]
        [Min(0)] public float startingFunds = 5000f;
        [Tooltip("New parks begin with purchases and running expenses exempt from the cash balance. Can also be changed in Park Management.")]
        public bool startWithUnlimitedFunds;

        public void Validate() => startingFunds = ParkReputationRules.NonNegative(startingFunds, 5000f);
    }

    /// <summary>Plain managed rules can also be used by simulation checks without a Unity scene.</summary>
    [Serializable]
    public sealed class ParkReputationRules
    {
        [Header("Guest reputation (0–100)")]
        [Tooltip("Reputation before the first review, and for older saves without reputation data.")]
        [Range(0, 100)] public float initialGuestScore = 60f;
        [Tooltip("Weight of each departing visitor's review. 0 ignores new reviews; 1 replaces the previous score.")]
        [Range(0, 1)] public float reviewWeight = .15f;
        [Tooltip("Guest reputation points lost per visitor death.")]
        [Min(0)] public float deathGuestPenalty = 8f;

        [Header("Exhibition appeal (0–100)")]
        [Tooltip("Appeal for the first specimen of a mythical species, multiplied by its exhibition rating.")]
        [Min(0)] public float appealPerCreatureRating = 6f;
        [Tooltip("Extra appeal fraction per additional specimen of the same species.")]
        [Min(0)] public float extraSpecimenBonus = .05f;
        [Tooltip("Only this many additional specimens contribute their extra appeal bonus.")]
        [Min(0)] public int extraSpecimenLimit = 3;
        [Tooltip("Appeal per distinct mythical species after the first.")]
        [Min(0)] public float speciesVarietyBonus = 10f;
        [Min(0)] public float domesticSpeciesAppeal = 2f;
        [Min(0)] public float domesticExtraSpecimenAppeal = 1f;
        [Min(0)] public int domesticExtraSpecimenLimit = 2;
        [Tooltip("Maximum combined appeal from all domestic animals.")]
        [Range(0, 100)] public float maximumDomesticAppeal = 4f;

        [Header("Five-star rating")]
        [Tooltip("Fraction of exhibition appeal retained even with zero guest reputation.")]
        [Range(0, 1)] public float exhibitionQualityFloor = .6f;
        [Tooltip("Additional exhibition multiplier at 100 guest reputation.")]
        [Range(0, 1)] public float guestQualityWeight = .4f;
        [Tooltip("Quality points required per star after subtracting safety penalties. The rating remains a five-star scale.")]
        [Min(.01f)] public float qualityPointsPerStar = 20f;

        [Header("Safety penalty (0–100)")]
        [Min(0)] public float deathSafetyPenalty = 20f;
        [Tooltip("Safety penalty points recovered per simulation second.")]
        [Min(0)] public float safetyRecoveryPerSecond = .01f;
        [Tooltip("Reviews above this satisfaction also repair safety reputation.")]
        [Range(0, 100)] public float safetyReviewThreshold = 60f;
        [Tooltip("Penalty recovered per satisfaction point above the threshold, for each departing visitor.")]
        [Min(0)] public float safetyRecoveryPerReviewPoint = .01f;

        [Header("Fair service prices (money)")]
        [Min(0)] public float baseAdmissionPrice = 15f;
        [Min(0)] public float admissionPricePerStar = 5f;
        [Min(0)] public float baseFoodPrice = 8f;
        [Min(0)] public float foodPricePerStar = 1f;
        [Min(0)] public float baseRestPrice = 3f;
        [Min(0)] public float restPricePerStar = .5f;
        [Tooltip("Admission premium above the fair price at which all visitors refuse. 0.8 means 80% overpriced. Acceptance decreases gradually up to this limit.")]
        [Min(.001f)] public float admissionRefusalPremium = .8f;
        [Tooltip("Maximum accepted kiosk/rest price as a multiple of its fair price.")]
        [Min(0)] public float servicePriceTolerance = 1.5f;

        [Header("Visitor demand multipliers")]
        [Tooltip("Arrival multiplier with zero guest reputation, before stars and safety apply.")]
        [Min(0)] public float baseDemand = .7f;
        [Tooltip("Additional arrival multiplier at 100 guest reputation.")]
        [Min(0)] public float guestDemandBonus = .5f;
        [Tooltip("Additional demand fraction per star.")]
        [Min(0)] public float starDemandBonus = .12f;
        [Tooltip("Demand fraction lost per safety penalty point, clamped so demand cannot become negative.")]
        [Range(0, 1)] public float safetyDemandLossPerPoint = .006f;

        public void Validate()
        {
            initialGuestScore = Bounded(initialGuestScore, 0, 100, 60);
            reviewWeight = Bounded(reviewWeight, 0, 1, .15f);
            deathGuestPenalty = NonNegative(deathGuestPenalty, 8);
            appealPerCreatureRating = NonNegative(appealPerCreatureRating, 6);
            extraSpecimenBonus = NonNegative(extraSpecimenBonus, .05f);
            extraSpecimenLimit = Math.Max(0, extraSpecimenLimit);
            speciesVarietyBonus = NonNegative(speciesVarietyBonus, 10);
            domesticSpeciesAppeal = NonNegative(domesticSpeciesAppeal, 2);
            domesticExtraSpecimenAppeal = NonNegative(domesticExtraSpecimenAppeal, 1);
            domesticExtraSpecimenLimit = Math.Max(0, domesticExtraSpecimenLimit);
            maximumDomesticAppeal = Bounded(maximumDomesticAppeal, 0, 100, 4);
            exhibitionQualityFloor = Bounded(exhibitionQualityFloor, 0, 1, .6f);
            guestQualityWeight = Bounded(guestQualityWeight, 0, 1, .4f);
            qualityPointsPerStar = Math.Max(.01f, NonNegative(qualityPointsPerStar, 20));
            deathSafetyPenalty = NonNegative(deathSafetyPenalty, 20);
            safetyRecoveryPerSecond = NonNegative(safetyRecoveryPerSecond, .01f);
            safetyReviewThreshold = Bounded(safetyReviewThreshold, 0, 100, 60);
            safetyRecoveryPerReviewPoint = NonNegative(safetyRecoveryPerReviewPoint, .01f);
            baseAdmissionPrice = NonNegative(baseAdmissionPrice, 15);
            admissionPricePerStar = NonNegative(admissionPricePerStar, 5);
            baseFoodPrice = NonNegative(baseFoodPrice, 8);
            foodPricePerStar = NonNegative(foodPricePerStar, 1);
            baseRestPrice = NonNegative(baseRestPrice, 3);
            restPricePerStar = NonNegative(restPricePerStar, .5f);
            admissionRefusalPremium = Math.Max(.001f, NonNegative(admissionRefusalPremium, .8f));
            servicePriceTolerance = NonNegative(servicePriceTolerance, 1.5f);
            baseDemand = NonNegative(baseDemand, .7f);
            guestDemandBonus = NonNegative(guestDemandBonus, .5f);
            starDemandBonus = NonNegative(starDemandBonus, .12f);
            safetyDemandLossPerPoint = Bounded(safetyDemandLossPerPoint, 0, 1, .006f);
        }

        internal static float NonNegative(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(0, value);
        private static float Bounded(float value, float min, float max, float fallback)
            => Math.Max(min, Math.Min(max, NonNegative(value, fallback)));
    }

    [CreateAssetMenu(menuName = "Mythologicum/Park balance settings", fileName = "DefaultParkBalance")]
    public sealed class ParkBalanceDefinition : ScriptableObject
    {
        private static ParkBalanceDefinition current;
        public ParkEconomyRules economy = new ParkEconomyRules();
        public ParkReputationRules reputation = new ParkReputationRules();

        public static ParkBalanceDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ParkBalanceDefinition>("Management/DefaultParkBalance");
                if (current == null)
                {
                    current = CreateInstance<ParkBalanceDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        private void OnValidate()
        {
            if (economy == null) economy = new ParkEconomyRules();
            if (reputation == null) reputation = new ParkReputationRules();
            economy.Validate(); reputation.Validate();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

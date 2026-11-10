using System;
using System.Collections.Generic;
using System.IO;

namespace Mythologicum
{
    [Serializable]
    public sealed class ParkExhibit
    {
        public string species;
        public int rating = 1, count = 1;
        public bool domestic;

        public ParkExhibit() { }
        public ParkExhibit(string species, int rating, int count = 1, bool domestic = false)
        { this.species = species; this.rating = rating; this.count = count; this.domestic = domestic; }
    }

    [Serializable]
    public sealed class ParkReputationSnapshot
    {
        public float guestScore = 60f, safetyPenalty;
        public int reviewedVisitors, fatalities;
        public List<ParkExhibit> exhibits = new List<ParkExhibit>();
    }

    /// <summary>Park quality and willingness to pay, independent of scenes and visitor movement.</summary>
    public sealed class ParkReputation
    {
        private readonly Dictionary<string, ParkExhibit> exhibits = new Dictionary<string, ParkExhibit>(StringComparer.Ordinal);
        public ParkReputationRules Rules { get; }
        public ParkReputation(ParkReputationRules rules = null)
        {
            Rules = rules ?? new ParkReputationRules();
            Rules.Validate();
            GuestScore = Rules.initialGuestScore;
        }
        public float ExhibitionScore { get; private set; }
        public float GuestScore { get; private set; }
        public float SafetyPenalty { get; private set; }
        public int ReviewedVisitors { get; private set; }
        public int Fatalities { get; private set; }
        public int DisplayedSpecies { get; private set; }
        public int DisplayedCreatures { get; private set; }
        public int DisplayedMythicalSpecies { get; private set; }
        public float Stars => Clamp((ExhibitionScore * (Rules.exhibitionQualityFloor + Rules.guestQualityWeight * GuestScore / 100f)
            - SafetyPenalty) / Math.Max(.01f, Rules.qualityPointsPerStar), 0f, 5f);
        public float RecommendedAdmissionPrice => Rules.baseAdmissionPrice + Rules.admissionPricePerStar * Stars;
        public float RecommendedFoodPrice => Rules.baseFoodPrice + Rules.foodPricePerStar * Stars;
        public float RecommendedRestPrice => Rules.baseRestPrice + Rules.restPricePerStar * Stars;
        public float DemandMultiplier => (Rules.baseDemand + Rules.guestDemandBonus * GuestScore / 100f)
            * (1f + Rules.starDemandBonus * Stars) * Math.Max(0f, 1f - Rules.safetyDemandLossPerPoint * SafetyPenalty);

        /// <summary>Exhibits are viewable creatures, regardless of whether a fence surrounds them.</summary>
        public void UpdateExhibition(IEnumerable<ParkExhibit> displayed)
        {
            exhibits.Clear();
            if (displayed != null)
                foreach (ParkExhibit item in displayed)
                {
                    if (item == null || string.IsNullOrEmpty(item.species) || item.count <= 0) continue;
                    if (exhibits.TryGetValue(item.species, out ParkExhibit previous))
                    {
                        previous.count = (int)Math.Min(1000000L, (long)previous.count + item.count);
                        previous.rating = Math.Max(previous.rating, Math.Max(1, Math.Min(5, item.rating)));
                        previous.domestic &= item.domestic;
                    }
                    else exhibits.Add(item.species, new ParkExhibit(item.species,
                        Math.Max(1, Math.Min(5, item.rating)), Math.Min(1000000, item.count), item.domestic));
                }
            double value = 0d, domesticValue = 0d;
            DisplayedCreatures = 0; DisplayedMythicalSpecies = 0; DisplayedSpecies = exhibits.Count;
            foreach (ParkExhibit item in exhibits.Values)
            {
                DisplayedCreatures = (int)Math.Min(int.MaxValue, (long)DisplayedCreatures + item.count);
                if (item.domestic) domesticValue += Rules.domesticSpeciesAppeal
                    + Rules.domesticExtraSpecimenAppeal * Math.Min(Rules.domesticExtraSpecimenLimit, item.count - 1);
                else
                {
                    DisplayedMythicalSpecies++;
                    value += item.rating * (double)Rules.appealPerCreatureRating
                        * (1d + Rules.extraSpecimenBonus * Math.Min(Rules.extraSpecimenLimit, item.count - 1));
                }
            }
            value += Math.Max(0, DisplayedMythicalSpecies - 1) * (double)Rules.speciesVarietyBonus
                + Math.Min(Rules.maximumDomesticAppeal, domesticValue);
            ExhibitionScore = (float)Math.Min(100d, value);
        }

        public void ReportDeparture(float satisfaction)
        {
            if (!Finite(satisfaction)) return;
            float score = Clamp(satisfaction, 0f, 100f);
            GuestScore = Clamp(GuestScore * (1f - Rules.reviewWeight) + score * Rules.reviewWeight, 0f, 100f);
            ReviewedVisitors = Increment(ReviewedVisitors);
            // Strong positive experiences help repair trust after an accident, over many visits.
            if (score > Rules.safetyReviewThreshold) SafetyPenalty = Math.Max(0f,
                SafetyPenalty - (score - Rules.safetyReviewThreshold) * Rules.safetyRecoveryPerReviewPoint);
        }

        public void ReportDeath()
        {
            Fatalities = Increment(Fatalities);
            GuestScore = Math.Max(0f, GuestScore - Rules.deathGuestPenalty);
            SafetyPenalty = Math.Min(100f, SafetyPenalty + Rules.deathSafetyPenalty);
        }

        public void Tick(float dt)
        {
            if (!Finite(dt) || dt <= 0f) return;
            SafetyPenalty = (float)Math.Max(0d, SafetyPenalty - (double)dt * Rules.safetyRecoveryPerSecond);
        }

        /// <summary>Fraction of potential visitors accepting the current admission price.</summary>
        public float PriceAcceptance(float price)
        {
            if (!Finite(price) || price < 0f) return 0f;
            if (price <= RecommendedAdmissionPrice) return 1f;
            if (RecommendedAdmissionPrice <= 0f) return 0f;
            return Clamp(1f - (price / RecommendedAdmissionPrice - 1f) / Math.Max(.001f, Rules.admissionRefusalPremium), 0f, 1f);
        }

        public bool ServiceAffordable(float price, bool food) => Finite(price) && price >= 0f
            && price <= (food ? RecommendedFoodPrice : RecommendedRestPrice) * Rules.servicePriceTolerance;

        public ParkReputationSnapshot Capture()
        {
            var result = new ParkReputationSnapshot { guestScore = GuestScore, safetyPenalty = SafetyPenalty,
                reviewedVisitors = ReviewedVisitors, fatalities = Fatalities };
            foreach (ParkExhibit item in exhibits.Values)
                result.exhibits.Add(new ParkExhibit(item.species, item.rating, item.count, item.domestic));
            return result;
        }

        public static void ValidateSnapshot(ParkReputationSnapshot saved)
        {
            if (saved == null) return; // Optional field on saves written before park reputation existed.
            if (!Finite(saved.guestScore) || saved.guestScore < 0f || saved.guestScore > 100f
                || !Finite(saved.safetyPenalty) || saved.safetyPenalty < 0f || saved.safetyPenalty > 100f
                || saved.reviewedVisitors < 0 || saved.fatalities < 0 || saved.exhibits == null || saved.exhibits.Count > 1024)
                throw new InvalidDataException(Loc.Text("park.park_reputation.invalid_park_reputation"));
            var species = new HashSet<string>(StringComparer.Ordinal);
            foreach (ParkExhibit item in saved.exhibits)
                if (item == null || string.IsNullOrEmpty(item.species) || item.species.Length > 128 || !species.Add(item.species)
                    || item.rating < 1 || item.rating > 5 || item.count < 1 || item.count > 1000000)
                    throw new InvalidDataException(Loc.Text("park.park_reputation.invalid_displayed_creature_reputation"));
        }

        public void Restore(ParkReputationSnapshot saved)
        {
            ValidateSnapshot(saved);
            GuestScore = saved?.guestScore ?? Rules.initialGuestScore; SafetyPenalty = saved?.safetyPenalty ?? 0f;
            ReviewedVisitors = saved?.reviewedVisitors ?? 0; Fatalities = saved?.fatalities ?? 0;
            UpdateExhibition(saved?.exhibits);
        }

        private static int Increment(int value) => value < int.MaxValue ? value + 1 : value;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}

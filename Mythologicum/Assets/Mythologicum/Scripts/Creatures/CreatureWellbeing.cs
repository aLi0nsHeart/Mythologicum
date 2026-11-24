using System;

namespace Mythologicum
{
    [Flags]
    public enum CreatureUnmetNeeds
    {
        None = 0, Hunger = 1, Thirst = 2, Exhaustion = 4, Fear = 8,
        Trees = 16, Gravestones = 32, Ground = 64, Shelter = 128,
        Space = 256, Herd = 512, OpenGround = 1024
    }

    /// <summary>Satisfaction scores: higher always means better, from 0 to 100.</summary>
    public readonly struct CreatureNeedScores
    {
        public float Environment { get; }
        public float Space { get; }
        public float Herd { get; }
        public float Happiness { get; }
        public float Energy { get; }
        public float Distress { get; }
        public CreatureUnmetNeeds UnmetNeeds { get; }

        public CreatureNeedScores(float environment, float space, float herd, float happiness)
            : this(environment, space, herd, happiness, 100f, 0f, CreatureUnmetNeeds.None) { }

        public CreatureNeedScores(float environment, float space, float herd, float happiness,
            float energy, float distress, CreatureUnmetNeeds unmetNeeds)
        {
            Environment = CreatureWellbeing.ClampScore(environment);
            Space = CreatureWellbeing.ClampScore(space);
            Herd = CreatureWellbeing.ClampScore(herd);
            Happiness = CreatureWellbeing.ClampScore(happiness);
            Energy = CreatureWellbeing.ClampScore(energy);
            Distress = CreatureWellbeing.ClampScore(distress * 100f) / 100f;
            UnmetNeeds = unmetNeeds;
        }
    }

    /// <summary>Pure need evaluation and gradual happiness changes; no enclosure ownership.</summary>
    public sealed class CreatureWellbeing
    {
        public float Happiness { get; private set; }
        public CreatureWellbeing(float initialHappiness = 75f) { Happiness = ClampScore(initialHappiness); }
        public float Agitation { get; private set; }
        public void Restore(float happiness, float agitation) { Happiness = ClampScore(happiness); Agitation = ClampScore(agitation * 100f) / 100f; }
        public CreatureNeedScores Scores { get; private set; }

        public static CreatureNeedScores Evaluate(float hunger, float fear, float accessibleArea,
            int nearbyTrees, int companions, float minimumSpace, int requiredTrees,
            int minCompanions, int maxCompanions, CreatureBehaviourSettings settings = null,
            int nearbyGravestones = 0, int requiredGravestones = 0,
            float nearbyOpenGroundFraction = 1f, float minimumOpenGroundFraction = 0f,
            float thirst = 0, bool needsWater = false, float biomeScore = -1, float shelterScore = -1,
            float energy = 100f, bool needsFood = true, bool needsRest = false)
        {
            settings = settings ?? DefaultSettings;
            float food = 100f - ClampScore(hunger), water = 100f - ClampScore(thirst);
            float rest = ClampScore(energy), calm = 100f - ClampScore(fear);
            // Each configured habitat feature contributes equally. Extra trees cannot compensate for missing graves, or vice versa.
            float environmentTotal = 0f;
            int environmentNeeds = 0;
            if (requiredTrees > 0)
            { environmentTotal += ClampScore(100f * Math.Max(0, nearbyTrees) / requiredTrees); environmentNeeds++; }
            if (requiredGravestones > 0)
            { environmentTotal += ClampScore(100f * Math.Max(0, nearbyGravestones) / requiredGravestones); environmentNeeds++; }
            if (!float.IsNaN(minimumOpenGroundFraction) && minimumOpenGroundFraction > 0f)
            {
                // Open meadow is a separate requirement; excess grass cannot make up
                // for missing shade trees, and a forest cannot satisfy meadow need.
                float required = Math.Min(1f, minimumOpenGroundFraction);
                float available = ClampScore(nearbyOpenGroundFraction * 100f) / 100f;
                environmentTotal += ClampScore(100f * available / required); environmentNeeds++;
            }
            if (biomeScore >= 0) { environmentTotal += ClampScore(biomeScore); environmentNeeds++; }
            if (shelterScore >= 0) { environmentTotal += ClampScore(shelterScore); environmentNeeds++; }
            float environment = environmentNeeds > 0 ? environmentTotal / environmentNeeds : 100f;
            float space = minimumSpace <= 0f ? 100f : ClampScore(100f * Math.Max(0f, accessibleArea) / minimumSpace);
            companions = Math.Max(0, companions);
            minCompanions = Math.Max(0, minCompanions);
            float herd = 100f;
            if (companions < minCompanions)
                herd = 100f * companions / minCompanions;
            else if (maxCompanions >= 0 && companions > maxCompanions)
                herd = ClampScore(100f * (1f - (float)(companions - maxCompanions) / Math.Max(1, maxCompanions)));

            double hungerWeight = needsFood ? Weight(settings.hungerWeight) : 0d, fearWeight = Weight(settings.fearWeight),
                environmentWeight = Weight(settings.environmentWeight), spaceWeight = Weight(settings.spaceWeight),
                herdWeight = Weight(settings.herdWeight);
            double thirstWeight = needsWater ? Weight(settings.thirstWeight) : 0;
            double energyWeight = needsRest ? Weight(settings.energyWeight) : 0;
            double total = hungerWeight + fearWeight + environmentWeight + spaceWeight + herdWeight + thirstWeight + energyWeight;
            // Relative weights can be edited independently. With all disabled, no unmet need reduces happiness.
            float happiness = total > 0d ? (float)((food * hungerWeight + calm * fearWeight + environment * environmentWeight
                + space * spaceWeight + herd * herdWeight + water * thirstWeight + rest * energyWeight) / total) : 100f;

            // Evaluate each applicable requirement separately: good neighbours or spare
            // trees cannot compensate for starvation, exhaustion or an absent sleeping site.
            float critical = ClampScore(settings.criticalNeedThreshold);
            float listed = ClampScore(settings.unmetNeedThreshold);
            float maximumSeverity = 0f;
            double calmProduct = 1d;
            var unmet = CreatureUnmetNeeds.None;
            void Consider(float satisfaction, double weight, CreatureUnmetNeeds need)
            {
                if (weight <= 0d) return;
                satisfaction = ClampScore(satisfaction);
                if (satisfaction < listed) unmet |= need;
                if (critical <= 0f || satisfaction >= critical) return;
                float severity = (critical - satisfaction) / critical;
                maximumSeverity = Math.Max(maximumSeverity, severity);
                calmProduct *= 1d - ClampScore(settings.criticalAgitation * 100f) / 100f * severity;
            }
            Consider(food, hungerWeight, CreatureUnmetNeeds.Hunger);
            Consider(water, thirstWeight, CreatureUnmetNeeds.Thirst);
            Consider(rest, energyWeight, CreatureUnmetNeeds.Exhaustion);
            Consider(calm, fearWeight, CreatureUnmetNeeds.Fear);
            Consider(space, spaceWeight, CreatureUnmetNeeds.Space);
            Consider(herd, herdWeight, CreatureUnmetNeeds.Herd);
            if (requiredTrees > 0) Consider(100f * Math.Max(0, nearbyTrees) / requiredTrees, environmentWeight, CreatureUnmetNeeds.Trees);
            if (requiredGravestones > 0) Consider(100f * Math.Max(0, nearbyGravestones) / requiredGravestones, environmentWeight, CreatureUnmetNeeds.Gravestones);
            if (minimumOpenGroundFraction > 0f)
                Consider(100f * Math.Max(0, nearbyOpenGroundFraction) / Math.Min(1f, minimumOpenGroundFraction), environmentWeight, CreatureUnmetNeeds.OpenGround);
            if (biomeScore >= 0f) Consider(biomeScore, environmentWeight, CreatureUnmetNeeds.Ground);
            if (shelterScore >= 0f) Consider(shelterScore, environmentWeight, CreatureUnmetNeeds.Shelter);
            happiness = Math.Min(happiness, 100f - (100f - ClampScore(settings.criticalHappinessCap)) * maximumSeverity);
            return new CreatureNeedScores(environment, space, herd, happiness, rest, (float)(1d - calmProduct), unmet);
        }

        public void Update(CreatureNeedScores scores, float dt, float changePerSecond, float threshold,
            CreatureBehaviourSettings settings = null)
        {
            if (dt < 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Scores = scores;
            if (dt == 0f) return; // Refresh inspection after loading without advancing emotional state.
            settings = settings ?? DefaultSettings;
            double step = Weight(changePerSecond) * dt;
            float difference = scores.Happiness - Happiness;
            Happiness = ClampScore((float)(Happiness + Math.Sign(difference) * Math.Min(Math.Abs(difference), step)));
            threshold = ClampScore(threshold);
            float target = Math.Max(scores.Distress, threshold > 0f ? Math.Max(0f, (threshold - Happiness) / threshold) : 0f);
            double agitationStep = Weight(target > Agitation ? settings.agitationBuildPerSecond : settings.agitationRecoveryPerSecond) * dt;
            Agitation = ClampScore((float)(Agitation + Math.Sign(target - Agitation)
                * Math.Min(Math.Abs(target - Agitation), agitationStep)) * 100f) / 100f;
        }

        internal static float ClampScore(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(100f, value));
        private static readonly CreatureBehaviourSettings DefaultSettings = new CreatureBehaviourSettings();
        private static double Weight(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0d : Math.Max(0d, value);
    }
}

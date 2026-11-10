using System;

namespace Mythologicum
{
    /// <summary>Satisfaction scores: higher always means better, from 0 to 100.</summary>
    public readonly struct CreatureNeedScores
    {
        public float Environment { get; }
        public float Space { get; }
        public float Herd { get; }
        public float Happiness { get; }

        public CreatureNeedScores(float environment, float space, float herd, float happiness)
        {
            Environment = CreatureWellbeing.ClampScore(environment);
            Space = CreatureWellbeing.ClampScore(space);
            Herd = CreatureWellbeing.ClampScore(herd);
            Happiness = CreatureWellbeing.ClampScore(happiness);
        }
    }

    /// <summary>Pure need evaluation and gradual happiness changes; no enclosure ownership.</summary>
    public sealed class CreatureWellbeing
    {
        public float Happiness { get; private set; }
        public CreatureWellbeing(float initialHappiness = 75f) { Happiness = ClampScore(initialHappiness); }
        public float Agitation { get; private set; }
        public void Restore(float happiness, float agitation) { Happiness = ClampScore(happiness); Agitation = Math.Max(0f, Math.Min(1f, agitation)); }
        public CreatureNeedScores Scores { get; private set; }

        public static CreatureNeedScores Evaluate(float hunger, float fear, float accessibleArea,
            int nearbyTrees, int companions, float minimumSpace, int requiredTrees,
            int minCompanions, int maxCompanions, CreatureBehaviourSettings settings = null,
            int nearbyGravestones = 0, int requiredGravestones = 0)
        {
            // Each configured habitat feature contributes equally. Extra trees cannot compensate for missing graves, or vice versa.
            float environmentTotal = 0f;
            int environmentNeeds = 0;
            if (requiredTrees > 0)
            { environmentTotal += ClampScore(100f * Math.Max(0, nearbyTrees) / requiredTrees); environmentNeeds++; }
            if (requiredGravestones > 0)
            { environmentTotal += ClampScore(100f * Math.Max(0, nearbyGravestones) / requiredGravestones); environmentNeeds++; }
            float environment = environmentNeeds > 0 ? environmentTotal / environmentNeeds : 100f;
            float space = minimumSpace <= 0f ? 100f : ClampScore(100f * Math.Max(0f, accessibleArea) / minimumSpace);
            companions = Math.Max(0, companions);
            minCompanions = Math.Max(0, minCompanions);
            float herd = 100f;
            if (companions < minCompanions)
                herd = 100f * companions / minCompanions;
            else if (maxCompanions >= 0 && companions > maxCompanions)
                herd = ClampScore(100f * (1f - (float)(companions - maxCompanions) / Math.Max(1, maxCompanions)));

            settings = settings ?? DefaultSettings;
            double hungerWeight = Weight(settings.hungerWeight), fearWeight = Weight(settings.fearWeight),
                environmentWeight = Weight(settings.environmentWeight), spaceWeight = Weight(settings.spaceWeight),
                herdWeight = Weight(settings.herdWeight);
            double total = hungerWeight + fearWeight + environmentWeight + spaceWeight + herdWeight;
            // Relative weights can be edited independently. With all disabled, no unmet need reduces happiness.
            float happiness = total > 0d ? (float)(((100f - ClampScore(hunger)) * hungerWeight
                + (100f - ClampScore(fear)) * fearWeight + environment * environmentWeight
                + space * spaceWeight + herd * herdWeight) / total) : 100f;
            return new CreatureNeedScores(environment, space, herd, happiness);
        }

        public void Update(CreatureNeedScores scores, float dt, float changePerSecond, float threshold)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Scores = scores;
            float step = float.IsNaN(changePerSecond) ? 0f : Math.Max(0f, changePerSecond) * dt;
            float difference = scores.Happiness - Happiness;
            Happiness = ClampScore(Happiness + Math.Sign(difference) * Math.Min(Math.Abs(difference), step));
            threshold = ClampScore(threshold);
            Agitation = threshold > 0f ? Math.Max(0f, (threshold - Happiness) / threshold) : 0f;
        }

        internal static float ClampScore(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(100f, value));
        private static readonly CreatureBehaviourSettings DefaultSettings = new CreatureBehaviourSettings();
        private static double Weight(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0d : Math.Max(0d, value);
    }
}

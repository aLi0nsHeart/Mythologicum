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
        public float Happiness { get; private set; } = 75f;
        public float Agitation { get; private set; }
        public CreatureNeedScores Scores { get; private set; }

        public static CreatureNeedScores Evaluate(float hunger, float fear, float accessibleArea,
            int nearbyTrees, int companions, float minimumSpace, int requiredTrees,
            int minCompanions, int maxCompanions)
        {
            float environment = requiredTrees <= 0 ? 100f : ClampScore(100f * Math.Max(0, nearbyTrees) / requiredTrees);
            float space = minimumSpace <= 0f ? 100f : ClampScore(100f * Math.Max(0f, accessibleArea) / minimumSpace);
            companions = Math.Max(0, companions);
            minCompanions = Math.Max(0, minCompanions);
            float herd = 100f;
            if (companions < minCompanions)
                herd = 100f * companions / minCompanions;
            else if (maxCompanions >= 0 && companions > maxCompanions)
                herd = ClampScore(100f * (1f - (float)(companions - maxCompanions) / Math.Max(1, maxCompanions)));

            // The fixed weights sum to one; hunger and fear are inverse needs.
            float happiness = (100f - ClampScore(hunger)) * 0.30f
                + (100f - ClampScore(fear)) * 0.10f
                + environment * 0.25f + space * 0.20f + herd * 0.15f;
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
    }
}

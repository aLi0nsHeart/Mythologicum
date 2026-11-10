using System;

namespace Mythologicum
{
    /// <summary>Visit satisfaction and patience, independent of rendering and navigation.</summary>
    public sealed class VisitorMood
    {
        private readonly VisitorMoodRules rules;
        public float Satisfaction { get; private set; }
        public float Age { get; private set; }
        /// <summary>Normalized patience consumed, from zero to one; high satisfaction spends it slowly.</summary>
        public float VisitProgress { get; private set; }
        public float RemainingPatience => Math.Max(.1f, rules.patienceSeconds) * (1f - VisitProgress);
        /// <summary>Remaining willingness to stay, from zero to one.</summary>
        public float Willingness => (1f - VisitProgress) * Satisfaction / 100f;
        public bool ShouldLeave { get; private set; }
        public void Restore(float satisfaction, float age, float progress, bool leaving)
        { Satisfaction = Score(satisfaction); Age = Math.Max(0f, age); VisitProgress = Math.Max(0f, Math.Min(1f, progress)); ShouldLeave = leaving; }

        public VisitorMood(float? initialSatisfaction = null, VisitorMoodRules rules = null)
        { this.rules = rules ?? new VisitorMoodRules(); Satisfaction = Score(initialSatisfaction ?? this.rules.initialSatisfaction); }

        public void Tick(float dt, float hunger, float energy, float fear, bool watching,
            bool receivingService, float minStay, float maxStay)
        {
            if (!Finite(dt) || dt <= 0f) return;
            float maximum = Finite(maxStay) ? Math.Max(0f, maxStay) : 300f;
            float minimum = Finite(minStay) ? Math.Min(maximum, Math.Max(0f, minStay)) : 0f;
            Age = (float)Math.Min(float.MaxValue, (double)Age + dt);
            float hungry = Math.Max(0f, Score(hunger) - rules.hungerDiscomfortThreshold) / Math.Max(.1f, 100f - rules.hungerDiscomfortThreshold);
            float tired = Math.Max(0f, rules.tiredDiscomfortThreshold - Score(energy)) / Math.Max(.1f, rules.tiredDiscomfortThreshold);
            float scared = Score(fear) / 100f;
            float gain = (watching ? rules.watchingGainPerSecond : 0f) + (receivingService ? rules.serviceGainPerSecond : 0f);
            float loss = rules.baselineLossPerSecond + hungry * rules.maximumHungerLossPerSecond
                + tired * rules.maximumTiredLossPerSecond + scared * rules.maximumFearLossPerSecond;
            Satisfaction = Score(Satisfaction + (double)dt * (gain - loss));
            // Happy guests stay longer; no amount of positive experiences defeats the absolute stay limit.
            float patienceRate = rules.unhappyPatienceRate + (rules.happyPatienceRate - rules.unhappyPatienceRate) * Satisfaction / 100f;
            VisitProgress = (float)Math.Min(1d, VisitProgress + (double)dt * Math.Max(0f, patienceRate) / Math.Max(.1f, rules.patienceSeconds));
            if (Age >= maximum || (Age >= minimum && (VisitProgress >= 1f || Satisfaction <= rules.leaveSatisfactionThreshold)))
                ShouldLeave = true;
        }

        public void Punish(float amount) => Satisfaction = Score(Satisfaction - Rate(amount));
        public void Reward(float amount) => Satisfaction = Score(Satisfaction + Rate(amount));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Rate(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
        private static float Score(double value) => double.IsNaN(value) ? 0f : (float)Math.Max(0d, Math.Min(100d, value));
    }
}

using System;

namespace Mythologicum
{
    /// <summary>Visit satisfaction and patience, independent of rendering and navigation.</summary>
    public sealed class VisitorMood
    {
        private const float PatienceSeconds = 120f;
        public float Satisfaction { get; private set; }
        public float Age { get; private set; }
        /// <summary>Normalized patience consumed, from zero to one; high satisfaction spends it slowly.</summary>
        public float VisitProgress { get; private set; }
        public float RemainingPatience => PatienceSeconds * (1f - VisitProgress);
        /// <summary>Remaining willingness to stay, from zero to one.</summary>
        public float Willingness => (1f - VisitProgress) * Satisfaction / 100f;
        public bool ShouldLeave { get; private set; }

        public VisitorMood(float initialSatisfaction = 60f) => Satisfaction = Score(initialSatisfaction);

        public void Tick(float dt, float hunger, float energy, float fear, bool watching,
            bool receivingService, float minStay, float maxStay)
        {
            if (!Finite(dt) || dt <= 0f) return;
            float maximum = Finite(maxStay) ? Math.Max(0f, maxStay) : 300f;
            float minimum = Finite(minStay) ? Math.Min(maximum, Math.Max(0f, minStay)) : 0f;
            Age = (float)Math.Min(float.MaxValue, (double)Age + dt);
            float hungry = Math.Max(0f, Score(hunger) - 50f) / 50f;
            float tired = Math.Max(0f, 30f - Score(energy)) / 30f;
            float scared = Score(fear) / 100f;
            float gain = (watching ? .8f : 0f) + (receivingService ? .6f : 0f);
            float loss = .04f + hungry * .9f + tired * .8f + scared * 2.5f;
            Satisfaction = Score(Satisfaction + (double)dt * (gain - loss));
            // Happy guests stay longer; no amount of positive experiences defeats the absolute stay limit.
            float patienceRate = 2f - 1.6f * Satisfaction / 100f;
            VisitProgress = (float)Math.Min(1d, VisitProgress + (double)dt * patienceRate / PatienceSeconds);
            if (Age >= maximum || (Age >= minimum && (VisitProgress >= 1f || Satisfaction <= 15f)))
                ShouldLeave = true;
        }

        public void Punish(float amount) => Satisfaction = Score(Satisfaction - Rate(amount));
        public void Reward(float amount) => Satisfaction = Score(Satisfaction + Rate(amount));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Rate(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
        private static float Score(double value) => double.IsNaN(value) ? 0f : (float)Math.Max(0d, Math.Min(100d, value));
    }
}

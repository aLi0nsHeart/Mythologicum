using System;

namespace Mythologicum
{
    /// <summary>Per-agent state, independent of Unity, species assets and enclosure ownership.</summary>
    public sealed class CreatureVitals
    {
        public float Hunger { get; private set; }
        public float Fear { get; private set; }

        public CreatureVitals(float initialHunger)
        {
            Hunger = ClampStat(initialHunger);
        }

        public void Tick(float dt, float hungerRate, float fearGainRate, float fearRecoveryRate, bool seesThreat)
        {
            // Invalid or reversed time must never corrupt persistent agent state.
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Hunger = ClampStat(Hunger + dt * NonNegative(hungerRate));
            Fear = ClampStat(seesThreat
                ? Fear + dt * NonNegative(fearGainRate)
                : Fear - dt * NonNegative(fearRecoveryRate));
        }

        public void Eat(float nutrition)
        {
            Hunger = ClampStat(Hunger - NonNegative(nutrition));
        }

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
        private static float ClampStat(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(100f, value));
    }
}

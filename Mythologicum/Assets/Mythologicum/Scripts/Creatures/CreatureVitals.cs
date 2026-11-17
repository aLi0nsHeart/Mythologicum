using System;

namespace Mythologicum
{
    /// <summary>Per-agent state, independent of Unity, species assets and enclosure ownership.</summary>
    public sealed class CreatureVitals
    {
        public float Hunger { get; private set; }
        public float Fear { get; private set; }
        public float Thirst { get; private set; }
        public void Restore(float hunger, float fear, float? thirst = null)
        { Hunger = ClampStat(hunger); Fear = ClampStat(fear); if (thirst.HasValue) Thirst = ClampStat(thirst.Value); }

        public CreatureVitals(float initialHunger, float initialThirst = 0)
        {
            Hunger = ClampStat(initialHunger);
            Thirst = ClampStat(initialThirst);
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

        public void TickThirst(float dt, CreatureThirstSettings settings)
        {
            if (settings == null || !settings.needsWater) { Thirst = 0; return; }
            if (dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            Thirst = ClampStat(Thirst + dt * NonNegative(settings.thirstPerSecond));
        }

        /// <returns>The actual thirst relieved, so finite trough water can be charged proportionally.</returns>
        public float Drink(float relief)
        {
            float before = Thirst;
            Thirst = ClampStat(Thirst - NonNegative(relief));
            return before - Thirst;
        }

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
        private static float ClampStat(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(100f, value));
    }
}

using System;

namespace Mythologicum
{
    /// <summary>Energy reserve: 100 is rested, zero is exhausted. Independent of Unity and species data.</summary>
    public sealed class CreatureEnergy
    {
        public float Energy { get; private set; }
        public float Exhaustion => 100f - Energy;

        public CreatureEnergy(float initial)
        {
            Energy = Clamp(initial);
        }

        public void Tick(float dt, float drainPerSecond, float activityMultiplier, bool sleeping,
            float recoveryPerSecond)
        {
            if (!Finite(dt) || dt <= 0f) return;
            // Double intermediates prevent overflow even for extreme but finite tuning values.
            double change = sleeping
                ? (double)dt * Rate(recoveryPerSecond)
                : -(double)dt * Rate(drainPerSecond) * Rate(activityMultiplier);
            Energy = Clamp(Energy + change);
        }

        /// <summary>One-off effort, such as a fence climb. Invalid or negative costs have no effect.</summary>
        public void Spend(float amount)
        {
            Energy = Clamp(Energy - Rate(amount));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Rate(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
        private static float Clamp(double value) => double.IsNaN(value) ? 0f : (float)Math.Max(0d, Math.Min(100d, value));
    }
}

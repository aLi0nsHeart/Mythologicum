using System;

namespace Mythologicum
{
    /// <summary>Per-creature hit points. Combat timing and death presentation belong to the simulation.</summary>
    public sealed class CreatureHealth
    {
        public float Maximum { get; }
        public float Current { get; private set; }
        public bool Alive => Current > 0f;

        public CreatureHealth(float maximum)
        {
            Maximum = Finite(maximum) ? Math.Max(1f, maximum) : 100f;
            Current = Maximum;
        }

        /// <summary>Returns damage actually applied, never more than the remaining health.</summary>
        public float Damage(float amount)
        {
            float applied = Math.Min(Current, NonNegative(amount));
            Current -= applied;
            return applied;
        }

        /// <summary>Restores living creatures only; zero health cannot be revived by passive recovery.</summary>
        public void Restore(float amount)
        {
            if (!Alive) return;
            Current = (float)Math.Min(Maximum, (double)Current + NonNegative(amount));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float NonNegative(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    }
}

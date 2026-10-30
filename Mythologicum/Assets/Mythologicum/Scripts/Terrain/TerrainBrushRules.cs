using System;

namespace Mythologicum
{
    public enum TerrainBrushMode { Raise, Lower, Smooth, Flatten }

    /// <summary>Pure circular brush falloff and bounded movement toward a sampled target.</summary>
    public static class TerrainBrushRules
    {
        public static float Falloff(float distance, float radius)
        {
            if (!Finite(distance) || !Finite(radius) || radius <= 0f || distance < 0f || distance >= radius) return 0f;
            float weight = 1f - distance / radius;
            return weight * weight * (3f - 2f * weight);
        }

        public static float MoveTowards(float current, float target, float maximumChange)
        {
            if (maximumChange <= 0f) return current;
            float difference = target - current;
            return Math.Abs(difference) <= maximumChange ? target : current + Math.Sign(difference) * maximumChange;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

using System;
using UnityEngine;

namespace Mythologicum
{
    public readonly struct CreatureScarePose
    {
        public Vector3 Offset { get; }
        public Vector3 Scale { get; }
        public float Tilt { get; }
        public CreatureScarePose(Vector3 offset, Vector3 scale, float tilt)
        { Offset = offset; Scale = scale; Tilt = tilt; }
    }

    /// <summary>Visual-only anticipation, jump and landing; never changes a creature's simulated position.</summary>
    public static class CreatureScareMotion
    {
        public static CreatureScarePose Sample(float progress, float height, float reach, float squash)
        {
            float p = Clamp(progress, 0f, 1f);
            height = Positive(height); reach = Positive(reach); squash = Clamp(squash, 0f, .8f);
            float jump = p <= .2f || p >= .8f ? 0f : (float)Math.Sin(Math.PI * (p - .2f) / .6f);
            float crouch = p < .2f ? (float)Math.Sin(Math.PI * p / .2f)
                : p > .8f ? (float)Math.Sin(Math.PI * (p - .8f) / .2f) * .5f : 0f;
            var offset = new Vector3(0f, height * (jump - .1f * crouch), reach * jump);
            var scale = new Vector3(1f + squash * (.4f * crouch - .18f * jump),
                1f + squash * (.55f * jump - crouch), 1f + squash * (.4f * crouch - .18f * jump));
            return new CreatureScarePose(offset, scale, -20f * jump + 8f * crouch);
        }
        private static float Clamp(float value, float min, float max) => float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value));
        private static float Positive(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
    }
}

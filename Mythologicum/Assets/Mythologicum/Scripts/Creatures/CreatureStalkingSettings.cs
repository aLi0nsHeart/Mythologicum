using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class CreatureStalkingSettings
    {
        public bool enabled;
        [Min(1)] public int coverSearchSteps = 10;
        [Min(0)] public int coverTreeRadius = 1;
        [Min(1)] public int minimumCoverTrees = 1;
        [Range(.05f, 1f)] public float stalkSpeedMultiplier = .45f;
        [Min(0)] public float minimumStalkSeconds = 2f;
        [Min(.1f)] public float maximumStalkSeconds = 9f;
        [Min(.1f)] public float ambushRange = 3f;
        [Min(.1f)] public float preferredCoverDistance = 5f;
        [Min(0)] public float coverWaitSeconds = 1f;
        [Min(0)] public float crouchOffset = .28f;

        public void Validate()
        {
            coverSearchSteps = Math.Max(1, Math.Min(64, coverSearchSteps));
            coverTreeRadius = Math.Max(0, Math.Min(8, coverTreeRadius)); minimumCoverTrees = Math.Max(1, minimumCoverTrees);
            stalkSpeedMultiplier = Mathf.Clamp(Amount(stalkSpeedMultiplier), .05f, 1);
            minimumStalkSeconds = Amount(minimumStalkSeconds);
            maximumStalkSeconds = Mathf.Max(.1f, minimumStalkSeconds, Amount(maximumStalkSeconds));
            ambushRange = Mathf.Max(.1f, Amount(ambushRange)); preferredCoverDistance = Mathf.Max(.1f, Amount(preferredCoverDistance));
            coverWaitSeconds = Amount(coverWaitSeconds); crouchOffset = Amount(crouchOffset);
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
    }
}

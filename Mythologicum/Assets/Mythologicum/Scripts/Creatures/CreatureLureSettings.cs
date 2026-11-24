using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class CreatureLureSettings
    {
        public bool enabled;
        [Tooltip("Only these visitor groups respond to this creature's call. Ordinary predation remains separate.")]
        public VisitorType[] visitorTypes = { VisitorType.Children };
        [Min(.1f)] public float range = 14f;
        [Min(.1f)] public float retrySeconds = 5f;
        [Min(.1f)] public float cooldownSeconds = 30f;
        [Min(.1f)] public float durationSeconds = 20f;
        [Min(1)] public int shorelineSearchSteps = 10;
        [Min(1)] public int maximumApproachSteps = 24;
        [Min(1)] public int maximumVisitorCandidates = 8;
        [Min(0)] public float submergeOffset = .45f;

        public bool Permits(VisitorType type)
        {
            if (!enabled || visitorTypes == null) return false;
            foreach (var allowed in visitorTypes) if (allowed == type) return true;
            return false;
        }
        public void Validate()
        {
            visitorTypes ??= Array.Empty<VisitorType>();
            range = Mathf.Max(.1f, Amount(range)); retrySeconds = Mathf.Max(.1f, Amount(retrySeconds));
            cooldownSeconds = Mathf.Max(.1f, Amount(cooldownSeconds)); durationSeconds = Mathf.Max(.1f, Amount(durationSeconds));
            shorelineSearchSteps = Math.Max(1, Math.Min(64, shorelineSearchSteps));
            maximumApproachSteps = Math.Max(1, Math.Min(64, maximumApproachSteps));
            maximumVisitorCandidates = Math.Max(1, Math.Min(32, maximumVisitorCandidates)); submergeOffset = Amount(submergeOffset);
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, value);
    }
}

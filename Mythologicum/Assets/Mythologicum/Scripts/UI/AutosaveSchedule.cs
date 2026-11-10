using System;

namespace Mythologicum
{
    /// <summary>Counts real active-play time; pausing never creates catch-up autosaves.</summary>
    public sealed class AutosaveSchedule
    {
        private double elapsed;
        private bool retrying;
        public bool Advance(float realSeconds, bool active, float interval, float retrySeconds)
        {
            if (!active || realSeconds <= 0 || float.IsNaN(realSeconds) || float.IsInfinity(realSeconds)) return false;
            elapsed += realSeconds;
            float target = retrying ? retrySeconds : interval;
            if (float.IsNaN(target) || float.IsInfinity(target)) return false;
            return elapsed >= Math.Max(1f, target);
        }
        public void Completed(bool successful) { elapsed = 0; retrying = !successful; }
        public void Restart() { elapsed = 0; retrying = false; }
    }
}

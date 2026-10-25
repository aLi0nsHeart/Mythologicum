using System;

namespace Mythologicum
{
    /// <summary>Initial caution about effortful travel; emergencies override settling in.</summary>
    public sealed class CreatureExploration
    {
        public float AwakeSeconds { get; private set; }
        public void Tick(float dt)
        {
            if (dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt)) AwakeSeconds += dt;
        }
        public bool WillClimb(float settlingSeconds, float hunger, float energy, bool fleeing) =>
            fleeing || hunger >= 80f || energy <= 8f || AwakeSeconds >= Math.Max(0f, settlingSeconds);
    }
}

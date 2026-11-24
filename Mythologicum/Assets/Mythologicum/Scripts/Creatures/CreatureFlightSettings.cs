using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Optional airborne locomotion and attack presentation; disabled on existing species.</summary>
    [Serializable]
    public sealed class CreatureFlightSettings
    {
        public bool canFly;
        [Min(0), Tooltip("Desired altitude above the creature's ordinary body-height origin.")]
        public float cruiseHeight = 5f;
        [Min(0), Tooltip("Maximum free-flight altitude when clearing tall walls. A completed aviary ceiling still takes priority.")]
        public float maximumHeight = 14f;
        [Min(.1f)] public float flightSpeed = 4.5f;
        [Min(.1f)] public float pursuitSpeed = 8f;
        [Min(.1f)] public float riseSpeed = 4f;
        [Min(.1f)] public float descentSpeed = 6f;
        [Min(0), Tooltip("Space above the body origin reserved for its head and moving wings beneath a roof.")]
        public float bodyHeadroom = 1.5f;
        [Min(0)] public float roofClearance = .35f;
        [Min(.05f), Tooltip("Half-size of the obstacle clearance test at cruise height.")]
        public float clearanceRadius = .35f;
        [Min(.1f)] public float failedEscapeSeconds = 1.4f;
        [Min(0)] public float wingBeatFrequency = 2.5f;
        [Range(0, 80)] public float wingBeatDegrees = 28f;
        [Range(0, 45)] public float wingSweepDegrees = 8f;
        public bool lightningAttack;
        [Min(.02f)] public float lightningSeconds = .22f;
        [Min(.005f)] public float lightningWidth = .07f;
        public Color lightningColor = new Color(.65f, .9f, 1f, 1f);

        public void Validate()
        {
            cruiseHeight = Amount(cruiseHeight); maximumHeight = Mathf.Max(cruiseHeight, Amount(maximumHeight));
            flightSpeed = Mathf.Max(.1f, Amount(flightSpeed));
            pursuitSpeed = Mathf.Max(flightSpeed, Amount(pursuitSpeed));
            riseSpeed = Mathf.Max(.1f, Amount(riseSpeed)); descentSpeed = Mathf.Max(.1f, Amount(descentSpeed));
            bodyHeadroom = Amount(bodyHeadroom); roofClearance = Amount(roofClearance);
            clearanceRadius = Mathf.Max(.05f, Amount(clearanceRadius));
            failedEscapeSeconds = Mathf.Max(.1f, Amount(failedEscapeSeconds));
            wingBeatFrequency = Amount(wingBeatFrequency); wingBeatDegrees = Mathf.Clamp(Amount(wingBeatDegrees), 0, 80);
            wingSweepDegrees = Mathf.Clamp(Amount(wingSweepDegrees), 0, 45);
            lightningSeconds = Mathf.Max(.02f, Amount(lightningSeconds)); lightningWidth = Mathf.Max(.005f, Amount(lightningWidth));
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    }
}

using System;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class ParkAlertRules
    {
        [Header("Monitoring")]
        public bool enabled = true;
        [Min(.1f), Tooltip("Simulation seconds between park condition checks. Pausing also pauses these checks.")]
        public float checkIntervalSeconds = 1f;
        [Min(0), Tooltip("Minimum seconds between separate occurrences of the same problem. An unresolved problem sends only one letter.")]
        public float recurrenceCooldownSeconds = 30f;
        [Header("Creature safety")]
        public bool creatureEscapes = true;
        public bool publicPathExposure = true;
        [Min(0), Tooltip("Minimum threat that produces a warning when ordinary ground movement can reach an entrance-connected visitor path.")]
        public float publicPathThreat = 20f;
        [Min(0), Tooltip("Seconds after a new placement or transport before checking visitor-path exposure. Free-roaming placement is never itself labelled an escape.")]
        public float placementGraceSeconds = 5f;
        [Header("Visitors")]
        public bool visitorInjuries = true;
        public bool visitorDeaths = true;
        [Range(0, 1), Tooltip("Warn when remaining health falls to this fraction of maximum health.")]
        public float injuryHealthFraction = .75f;
        [Range(0, 1), Tooltip("The injury warning is cleared after healing above this fraction, allowing a later injury to create another warning.")]
        public float injuryRecoveryFraction = .95f;
        [Header("Operations")]
        public bool emptyFeeders = true;
        [Min(0), Tooltip("An empty feeder must remain empty for this many simulation seconds before warning.")]
        public float emptyFeederDelaySeconds = 5f;
        public bool blockedStaff = true;
        [Min(0), Tooltip("A worker must remain blocked for this many simulation seconds before warning.")]
        public float blockedStaffDelaySeconds = 10f;

        public void Validate()
        {
            checkIntervalSeconds = Mathf.Max(.1f, checkIntervalSeconds);
            recurrenceCooldownSeconds = Mathf.Max(0, recurrenceCooldownSeconds);
            publicPathThreat = Mathf.Max(0, publicPathThreat);
            placementGraceSeconds = Mathf.Max(0, placementGraceSeconds);
            injuryHealthFraction = Mathf.Clamp01(injuryHealthFraction);
            injuryRecoveryFraction = Mathf.Clamp(injuryRecoveryFraction, injuryHealthFraction, 1);
            emptyFeederDelaySeconds = Mathf.Max(0, emptyFeederDelaySeconds);
            blockedStaffDelaySeconds = Mathf.Max(0, blockedStaffDelaySeconds);
        }
    }

    [CreateAssetMenu(menuName = "Mythologicum/Park alerts", fileName = "DefaultAlerts")]
    public sealed class ParkAlertDefinition : ScriptableObject
    {
        private static ParkAlertDefinition current;
        public ParkAlertRules rules = new ParkAlertRules();
        public static ParkAlertDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ParkAlertDefinition>("Management/DefaultAlerts");
                if (current == null)
                {
                    current = CreateInstance<ParkAlertDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }
        private void OnValidate() { if (rules == null) rules = new ParkAlertRules(); rules.Validate(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

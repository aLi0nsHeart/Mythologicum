using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Save settings", fileName = "DefaultSaveSettings")]
    public sealed class SaveSettingsDefinition : ScriptableObject
    {
        private static SaveSettingsDefinition current;
        public static SaveSettingsDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<SaveSettingsDefinition>("Saving/DefaultSaveSettings");
                if (current == null) { current = CreateInstance<SaveSettingsDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        public bool autosaveEnabled = true;
        [Tooltip("Real seconds of unpaused play between autosaves, independent of simulation speed. Menus that pause the park do not count.")]
        [Min(1)] public float autosaveIntervalSeconds = 300f;
        [Tooltip("Autosaves rotate through these separate files and never replace manual saves.")]
        [Range(1, 99)] public int autosaveSlots = 3;
        [Tooltip("Real seconds of active play to wait before retrying a failed autosave.")]
        [Min(1)] public float failureRetrySeconds = 30f;
        [Min(0)] public float statusDisplaySeconds = 4f;
        private void OnValidate()
        {
            autosaveIntervalSeconds = Mathf.Max(1, autosaveIntervalSeconds);
            autosaveSlots = Mathf.Clamp(autosaveSlots, 1, 99);
            failureRetrySeconds = Mathf.Max(1, failureRetrySeconds);
            statusDisplaySeconds = Mathf.Max(0, statusDisplaySeconds);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

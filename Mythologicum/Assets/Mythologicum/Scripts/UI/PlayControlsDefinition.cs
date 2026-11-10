using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Play control settings", fileName = "DefaultPlayControls")]
    public sealed class PlayControlsDefinition : ScriptableObject
    {
        private static PlayControlsDefinition current;
        public static PlayControlsDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<PlayControlsDefinition>("Interface/DefaultPlayControls");
                if (current == null) { current = CreateInstance<PlayControlsDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        [Header("Simulation speed")]
        [Tooltip("Simulation seconds per real second for the three play buttons. Camera movement and interface input stay in real time.")]
        [Range(.1f, 6)] public float normalSpeed = 1f;
        [Range(.1f, 6)] public float doubleSpeed = 2f;
        [Range(.1f, 6)] public float tripleSpeed = 3f;
        [Header("Player interface scaling")]
        [Range(.5f, 1)] public float minimumUIScale = .75f;
        [Range(1, 2)] public float maximumUIScale = 1.5f;
        [Header("Speed control dock")]
        public Vector2 speedAnchor = new Vector2(.5f, 0);
        public Vector2 speedPosition = new Vector2(0, 22);
        public Vector2 speedButtonSize = new Vector2(46, 32);
        [Min(0)] public float speedButtonGap = 6;
        private void OnValidate()
        {
            normalSpeed = Mathf.Clamp(normalSpeed, .1f, 6); doubleSpeed = Mathf.Clamp(doubleSpeed, .1f, 6);
            tripleSpeed = Mathf.Clamp(tripleSpeed, .1f, 6);
            minimumUIScale = Mathf.Clamp(minimumUIScale, .5f, 1); maximumUIScale = Mathf.Clamp(maximumUIScale, 1, 2);
            speedButtonSize = Vector2.Max(new Vector2(32, 24), speedButtonSize); speedButtonGap = Mathf.Max(0, speedButtonGap);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

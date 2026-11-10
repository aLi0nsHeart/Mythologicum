using UnityEngine;
namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Park operations settings", fileName = "DefaultOperations")]
    public sealed class ParkOperationsDefinition : ScriptableObject
    {
        private static ParkOperationsDefinition current;
        public static ParkOperationsDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ParkOperationsDefinition>("Management/DefaultOperations");
                if (current == null) { current = CreateInstance<ParkOperationsDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        [Header("Diagnostic map overlays")]
        [Min(.1f), Tooltip("Minimum real seconds between rebuilding a changed overlay. Unchanged maps reuse their meshes.")]
        public float overlayRefreshSeconds = .5f;
        [Min(.01f)] public float overlayGroundOffset = .18f;
        public Color connectedColor = new Color(.05f, .8f, .65f, .65f);
        public Color disconnectedColor = new Color(1f, .2f, .15f, .65f);
        public Color securedColor = new Color(.3f, .65f, 1f, .55f);
        [Header("Construction history")]
        [Range(1, 100), Tooltip("Maximum construction/terrain actions retained for Undo in the current park session.")]
        public int undoCapacity = 20;
        private void OnValidate()
        { overlayRefreshSeconds = Mathf.Max(.1f, overlayRefreshSeconds); overlayGroundOffset = Mathf.Max(.01f, overlayGroundOffset); undoCapacity = Mathf.Clamp(undoCapacity, 1, 100); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => current = null;
    }
}

using UnityEngine;

namespace Mythologicum
{
    /// <summary>Uses the small cursor texture prepared from the theme's atlas sprite.</summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitionCursor : MonoBehaviour
    {
        private static ExhibitionCursor instance;
        private Texture2D appliedTexture;
        private Vector2 appliedHotspot;
        private bool focused;

        public static bool IsApplied => instance != null && instance.appliedTexture != null;
        public static Texture2D CurrentTexture => instance != null ? instance.appliedTexture : null;
        public static Vector2 CurrentHotspot => instance != null ? instance.appliedHotspot : Vector2.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var host = new GameObject("Exhibition cursor");
            host.AddComponent<ExhibitionCursor>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnEnable()
        {
            if (instance != this) return;
            focused = Application.isFocused;
            ApplyThemeCursor();
        }

        private void Update()
        {
            if (!focused) return;
            var theme = UITheme.Current;
            if ((theme != null ? theme.cursorTexture : null) != appliedTexture
                || (theme != null && theme.cursorHotspot != appliedHotspot)) ApplyThemeCursor();
        }

        public void ApplyThemeCursor()
        {
            if (instance != this) return;
            var theme = UITheme.Current;
            if (!isActiveAndEnabled || !focused || theme == null || theme.cursorTexture == null) { RestoreSystemCursor(); return; }
            var texture = theme.cursorTexture;
            var hotspot = new Vector2(Mathf.Clamp(theme.cursorHotspot.x, 0, texture.width - 1),
                Mathf.Clamp(theme.cursorHotspot.y, 0, texture.height - 1));
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            appliedTexture = texture; appliedHotspot = hotspot;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            ApplyThemeCursor();
        }

        private void OnApplicationPause(bool paused)
        {
            focused = !paused && Application.isFocused;
            ApplyThemeCursor();
        }

        private void RestoreSystemCursor()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            appliedTexture = null; appliedHotspot = Vector2.zero;
        }

        private void OnDisable() { if (instance == this) RestoreSystemCursor(); }
        private void OnApplicationQuit() { if (instance == this) RestoreSystemCursor(); }
        private void OnDestroy()
        {
            if (instance != this) return;
            RestoreSystemCursor(); instance = null;
        }
    }
}

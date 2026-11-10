using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Scales a canvas from its authored baseline, preserving frame aspect ratios.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerUIScale : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Vector2 authoredResolution;
        private float authoredFactor;
        private bool initialized;
        public static void Attach(Canvas canvas)
        {
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) return;
            var component = canvas.GetComponent<PlayerUIScale>() ?? canvas.gameObject.AddComponent<PlayerUIScale>();
            component.Refresh();
        }
        public static void RefreshAll()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Attach(canvas);
            Canvas.ForceUpdateCanvases();
            foreach (var frame in Object.FindObjectsByType<UIFramedPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None)) frame.Refresh();
            foreach (var controller in Object.FindObjectsByType<BuildModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) controller.RefreshInterfacePresentation();
            Canvas.ForceUpdateCanvases();
        }
        private void Refresh()
        {
            if (!initialized)
            {
                scaler = GetComponent<CanvasScaler>();
                if (scaler == null) return;
                authoredResolution = scaler.referenceResolution; authoredFactor = scaler.scaleFactor; initialized = true;
            }
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize) scaler.referenceResolution = authoredResolution / GameSettings.UIScale;
            else if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize) scaler.scaleFactor = authoredFactor * GameSettings.UIScale;
        }
    }
}

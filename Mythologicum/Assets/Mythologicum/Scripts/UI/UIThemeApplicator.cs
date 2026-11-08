using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Styles authored controls and newly built runtime panels without replacing their behaviour or layout.</summary>
    [DefaultExecutionOrder(30000)]
    [DisallowMultipleComponent]
    public sealed class UIThemeApplicator : MonoBehaviour
    {
        private readonly List<UIThemeElement> elements = new List<UIThemeElement>();
        private readonly List<UIOverflowScroll> scrolls = new List<UIOverflowScroll>();
        private float nextScan;
        private Canvas canvas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= Loaded; SceneManager.sceneLoaded += Loaded;
        }
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) ApplyAll(canvas);
        }
        public static void ApplyAll(Canvas canvas)
        {
            if (canvas == null) return;
            var applicator = canvas.GetComponent<UIThemeApplicator>() ?? canvas.gameObject.AddComponent<UIThemeApplicator>();
            applicator.canvas = canvas; applicator.Scan(); applicator.Apply();
        }
        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextScan) { Scan(); nextScan = Time.unscaledTime + .2f; }
            Apply();
        }
        private void OnTransformChildrenChanged() => nextScan = 0f;
        private void Scan()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) return;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true)) UIIcon.AttachAutomatic(button);
            scrolls.Clear();
            foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>(true)) scrolls.Add(UIOverflowScroll.Attach(scroll));
            elements.Clear();
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(true))
            {
                var element = graphic.GetComponent<UIThemeElement>() ?? graphic.gameObject.AddComponent<UIThemeElement>();
                element.Initialize(graphic); elements.Add(element);
            }
        }
        private void Apply()
        {
            var theme = UITheme.Current; if (theme == null) return;
            foreach (var element in elements) if (element != null) element.Apply(theme);
            foreach (var scroll in scrolls) if (scroll != null) scroll.Refresh();
        }
    }
}

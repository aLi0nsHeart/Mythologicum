using System.Collections.Generic;
using Unity.Profiling;
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
        private static readonly ProfilerMarker MaintenanceMarker = new ProfilerMarker("Mythologicum.InterfaceMaintenance");
        private readonly List<UIThemeElement> elements = new List<UIThemeElement>();
        private readonly List<UIOverflowScroll> scrolls = new List<UIOverflowScroll>();
        private readonly List<UILayoutElement> layoutElements = new List<UILayoutElement>();
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<ScrollRect> scrollRects = new List<ScrollRect>();
        private readonly List<Graphic> graphics = new List<Graphic>();
        private readonly List<RectTransform> rects = new List<RectTransform>();
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
            PlayerUIScale.Attach(canvas);
            var applicator = canvas.GetComponent<UIThemeApplicator>() ?? canvas.gameObject.AddComponent<UIThemeApplicator>();
            applicator.canvas = canvas; applicator.Scan(); applicator.Apply(true);
        }
        private void LateUpdate()
        {
            using var sample = MaintenanceMarker.Auto();
            if (Time.unscaledTime >= nextScan) { Scan(); nextScan = Time.unscaledTime + .2f; }
            Apply(false);
        }
        private void OnTransformChildrenChanged() => nextScan = 0f;
        private void Scan()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas == null) return;
            // Reuse discovery buffers: generated reports can contain hundreds of controls.
            canvas.GetComponentsInChildren(true, buttons);
            foreach (var button in buttons) UIIcon.AttachAutomatic(button);
            scrolls.Clear();
            canvas.GetComponentsInChildren(true, scrollRects);
            foreach (var scroll in scrollRects)
            {
                var overflow = UIOverflowScroll.Attach(scroll);
                overflow.Maintainer = this;
                scrolls.Add(overflow);
            }
            elements.Clear();
            canvas.GetComponentsInChildren(true, graphics);
            foreach (var graphic in graphics)
            {
                var element = graphic.GetComponent<UIThemeElement>() ?? graphic.gameObject.AddComponent<UIThemeElement>();
                element.Initialize(graphic); elements.Add(element);
            }
            layoutElements.Clear();
            canvas.GetComponentsInChildren(true, rects);
            var settings = UILayoutSettings.Current;
            foreach (var rect in rects)
            {
                var element = rect.GetComponent<UILayoutElement>();
                // Bindings and responsive callbacks attach themselves. Ordinary labels
                // need no Update component unless the designer gives them an override.
                if (element == null && settings != null && settings.Element(rect) != null) element = UILayoutElement.Attach(rect);
                if (element != null)
                {
                    element.Maintainer = this;
                    layoutElements.Add(element);
                }
            }
        }
        private void Apply(bool includeInactive)
        {
            var theme = UITheme.Current; if (theme == null) return;
            foreach (var element in elements)
                if (element != null && (includeInactive || element.HasVisibleGraphic)) element.Apply(theme);
            // Hierarchy order runs responsive parent reflows before child exceptions.
            // Managed components skip their own LateUpdate to avoid doing this twice.
            // Explicit refresh also includes hidden controls for setup and designer edits.
            foreach (var element in layoutElements)
                if (element != null && element.Maintainer == this && (includeInactive || element.isActiveAndEnabled)) element.Refresh();
            foreach (var scroll in scrolls)
                if (scroll != null && scroll.Maintainer == this && (includeInactive || scroll.isActiveAndEnabled)) scroll.Refresh();
        }
    }
}

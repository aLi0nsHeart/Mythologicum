using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Checks visible-only UI maintenance and live designer rules without changing authored assets.</summary>
public static class UIMaintenanceNativeChecks
{
    public static IEnumerator Run(Canvas canvas, Action<bool, string> check)
    {
        var probe = new GameObject("UI maintenance probe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        probe.transform.SetParent(canvas.transform, false);
        var label = probe.GetComponent<Text>(); label.font = UITheme.BodyFont; label.fontSize = 18;
        label.text = "UI maintenance probe"; label.raycastTarget = false;
        UIThemeElement.Mark(probe, UIThemeRole.Text);
        var settings = UILayoutSettings.Current;
        var originalOverrides = settings.elementOverrides;
        var rect = (RectTransform)probe.transform;
        var originalSize = new Vector2(220, 60); rect.sizeDelta = originalSize;
        var applicator = canvas.GetComponent<UIThemeApplicator>();
        var originalEnabled = applicator.enabled;
        var constructionButton = canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "Buildings (B)");
        var constructionGroup = constructionButton.transform.parent.gameObject;
        bool constructionWasActive = constructionGroup.activeSelf;
        try
        {
            UIThemeApplicator.ApplyAll(canvas);
            check(probe.GetComponent<UILayoutElement>() == null,
                "Ordinary UI labels do not acquire an unnecessary per-frame layout component");
            probe.SetActive(false); label.fontSize = 1;
            yield return null;
            yield return null;
            check(label.fontSize == 1, "Hidden UI labels are not restyled on every runtime frame");
            probe.SetActive(true);
            yield return null;
            yield return null;
            check(label.fontSize > 1, "Reopening a hidden UI label applies the current theme again");
            probe.SetActive(false); label.fontSize = 1;
            UIThemeApplicator.ApplyAll(canvas);
            check(label.fontSize > 1, "Explicit designer/localization refresh still includes hidden labels");
            probe.SetActive(true);
            settings.elementOverrides = (originalOverrides ?? Array.Empty<UIElementLayoutOverride>()).Concat(new[]
            {
                new UIElementLayoutOverride { elementNameOrPath = probe.name, overridePosition = true,
                    position = new Vector2(123, 37), overrideSize = true, size = new Vector2(177, 43) }
            }).ToArray();
            // Request the same periodic discovery used during live Inspector edits.
            typeof(UIThemeApplicator).GetField("nextScan", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(applicator, -1f);
            yield return null;
            yield return null;
            check(probe.GetComponent<UILayoutElement>() != null
                && Vector2.Distance(rect.anchoredPosition, new Vector2(123, 37)) < .1f
                && Vector2.Distance(rect.rect.size, new Vector2(177, 43)) < .1f,
                "A new geometry override is discovered and applied during play without restyling hidden menus");
            applicator.enabled = false;
            settings.elementOverrides[settings.elementOverrides.Length - 1].position = new Vector2(101, 29);
            yield return null;
            yield return null;
            check(Vector2.Distance(rect.anchoredPosition, new Vector2(101, 29)) < .1f,
                "A managed layout retains its standalone refresh when the canvas applicator is disabled");
            applicator.enabled = true;
            settings.elementOverrides = originalOverrides;
            yield return null;
            yield return null;
            check(Vector2.Distance(rect.rect.size, originalSize) < .1f,
                "Removing a live geometry override restores the original control size");

            // The palette reflows its children every frame. Its parent callback must run
            // before the designer's child exception, including in ordinary runtime frames.
            constructionGroup.SetActive(true);
            settings.elementOverrides = (originalOverrides ?? Array.Empty<UIElementLayoutOverride>()).Concat(new[]
            {
                new UIElementLayoutOverride { elementNameOrPath = constructionButton.name, screenName = "Construction Palette",
                    overridePosition = true, position = new Vector2(71, -19), overrideSize = true, size = new Vector2(119, 33) }
            }).ToArray();
            typeof(UIThemeApplicator).GetField("nextScan", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(applicator, -1f);
            yield return null;
            yield return null;
            yield return null;
            var constructionRect = (RectTransform)constructionButton.transform;
            check(Vector2.Distance(constructionRect.anchoredPosition, new Vector2(71, -19)) < .1f
                && Vector2.Distance(constructionRect.rect.size, new Vector2(119, 33)) < .1f,
                "Construction child geometry exceptions survive repeated responsive parent reflows");
        }
        finally
        {
            settings.elementOverrides = originalOverrides;
            applicator.enabled = originalEnabled;
            constructionGroup.SetActive(constructionWasActive);
            UnityEngine.Object.DestroyImmediate(probe);
            UIThemeApplicator.ApplyAll(canvas);
        }
    }
}

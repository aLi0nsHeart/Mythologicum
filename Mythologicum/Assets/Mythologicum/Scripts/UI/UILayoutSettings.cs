using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Persistent artist controls for the exhibition's generated interface.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/UI layout settings", fileName = "ExhibitionLayout")]
    public sealed class UILayoutSettings : ScriptableObject
    {
        private static UILayoutSettings current;
        public static UILayoutSettings Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<UILayoutSettings>("ExhibitionLayout");
                if (current == null)
                {
                    current = CreateInstance<UILayoutSettings>();
                    current.name = "Default exhibition layout";
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        [Header("Active appearance")]
        [Tooltip("The theme used by the game. Leave empty to use Resources/SteampunkTheme. Assign a duplicate theme here to switch appearances without replacing the original.")]
        public UITheme activeTheme;

        [Header("Screen edges")]
        [Tooltip("Minimum space between an illustrated panel and the screen edges, in UI units.")]
        [Min(0)] public float canvasMargin = 24f;

        [Header("Frame backing")]
        [Tooltip("Fill the enclosed interior of the frame artwork, including its curved corners and ornaments. Disable to use the manual lining insets instead.")]
        public bool matchFrameBackingToArtwork = true;
        [Tooltip("Extend the backing underneath the artwork by this many source-image pixels to hide seams. Does not change the text safe areas.")]
        [Range(0, 8)] public int frameBackingOverlapPixels = 2;

        [Header("Vertical frame safe areas")]
        [Tooltip("Proportional space reserved for the frame's ornament. 0.1 means ten percent of the frame.")]
        public UIFrameInsets verticalBodyInsets = new UIFrameInsets(.13f, .12f, .13f, .21f);
        [Tooltip("Manual rectangular backing margins. Used only when artwork matching is disabled or the artwork has no enclosed interior.")]
        public UIFrameInsets verticalLiningInsets = new UIFrameInsets(.075f, .075f, .075f, .16f);
        public UIFrameInsets verticalHeadingInsets = new UIFrameInsets(.25f, .79f, .25f, .12f);

        [Header("Horizontal frame safe areas")]
        public UIFrameInsets horizontalBodyInsets = new UIFrameInsets(.07f, .13f, .07f, .22f);
        [Tooltip("Manual rectangular backing margins. Used only when artwork matching is disabled or the artwork has no enclosed interior.")]
        public UIFrameInsets horizontalLiningInsets = new UIFrameInsets(.055f, .11f, .055f, .18f);
        public UIFrameInsets horizontalHeadingInsets = new UIFrameInsets(.28f, .80f, .28f, .08f);

        [Header("Panel settings")]
        [Tooltip("Identify a panel by its stable hierarchy name. Changing width preserves the source image's aspect ratio. Position and anchor changes are optional.")]
        public UIPanelLayout[] panels = DefaultPanels();

        [Header("Bottom icon dock")]
        [Min(16)] public float toolbarIconSize = 64f;
        public Vector2 buildingIconPosition = new Vector2(56, 150);
        public Vector2 managementIconPosition = new Vector2(126, 146);
        public Vector2 encyclopediaIconPosition = new Vector2(196, 126);
        public Vector2 mailIconPosition = new Vector2(266, 90);
        public Vector2 pauseIconPosition = new Vector2(336, 44);

        [Header("Funds and stars")]
        [Tooltip("Position from the top-left corner. Negative Y moves down.")]
        public Vector2 fundsPosition = new Vector2(15, -12);
        public Vector2 statusSize = new Vector2(300, 82);
        public Vector2 fundsSize = new Vector2(300, 44);
        [Tooltip("Position inside the funds display. Positive Y moves down.")]
        public Vector2 starsPosition = new Vector2(0, 45);
        public Vector2 starsSize = new Vector2(154, 28);

        [Header("Construction flyouts")]
        public Vector2 categoryPosition = new Vector2(24, 210);
        public Vector2 categoryCellSize = new Vector2(134, 44);
        [Min(1)] public int categoryColumns = 4;
        public Vector2 choicePosition = new Vector2(24, 342);
        public Vector2 choiceCellSize = new Vector2(184, 72);
        [Min(1)] public int choiceColumns = 4;
        [Tooltip("Below this canvas height, the choices move beside the construction categories.")]
        [Min(0)] public float compactThreshold = 600f;
        public Vector2 compactPosition = new Vector2(616, 210);
        [Min(1)] public int compactColumns = 3;
        [Min(0)] public float flyoutPadding = 10f;
        [Min(0)] public float flyoutGap = 8f;

        [Header("Scroll views")]
        [Tooltip("How far one wheel input scrolls an overflowing report.")]
        [Min(0)] public float scrollSensitivity = 25f;
        [Min(1)] public float scrollbarWidth = 9f;
        [Tooltip("Width reserved beside report text. Keep this at least as wide as the scrollbar.")]
        [Min(0)] public float scrollbarGutter = 14f;
        [Tooltip("Content must exceed the viewport by this many UI units before scrolling activates.")]
        [Range(0, 5)] public float overflowTolerance = .5f;

        [Header("Hover names")]
        [Min(0)] public float tooltipDelay = .4f;
        public Vector2 tooltipPadding = new Vector2(13, 9);
        [Min(20)] public float tooltipMinimumWidth = 90f;
        [Min(20)] public float tooltipMaximumWidth = 340f;
        [Min(0)] public float tooltipScreenMargin = 8f;
        public Vector2 tooltipOffset = new Vector2(0, 10);

        [Header("Individual element adjustments")]
        [Tooltip("Persistent position and size changes for generated controls. Match a stable object name, or a hierarchy path ending in that name; optionally limit the rule to one screen. Later matching entries take precedence.")]
        public UIElementLayoutOverride[] elementOverrides = Array.Empty<UIElementLayoutOverride>();

        public UIPanelLayout Panel(string key)
        {
            if (panels == null || string.IsNullOrEmpty(key)) return null;
            foreach (var panel in panels)
                if (panel != null && string.Equals(panel.key, key, StringComparison.OrdinalIgnoreCase)) return panel;
            return null;
        }

        public UIElementLayoutOverride Element(Transform target)
        {
            if (elementOverrides == null || target == null) return null;
            for (int i = elementOverrides.Length - 1; i >= 0; i--)
                if (elementOverrides[i] != null && elementOverrides[i].Matches(target)) return elementOverrides[i];
            return null;
        }

        private void OnValidate()
        {
            canvasMargin = Mathf.Max(0, canvasMargin);
            frameBackingOverlapPixels = Mathf.Clamp(frameBackingOverlapPixels, 0, 8);
            toolbarIconSize = Mathf.Max(16, toolbarIconSize);
            categoryColumns = Mathf.Max(1, categoryColumns);
            choiceColumns = Mathf.Max(1, choiceColumns);
            compactColumns = Mathf.Max(1, compactColumns);
            scrollbarWidth = Mathf.Max(1, scrollbarWidth);
            scrollbarGutter = Mathf.Max(scrollbarWidth, scrollbarGutter);
            scrollSensitivity = Mathf.Max(0, scrollSensitivity);
            overflowTolerance = Mathf.Clamp(overflowTolerance, 0, 5);
            verticalBodyInsets = verticalBodyInsets.Clamped();
            verticalLiningInsets = verticalLiningInsets.Clamped();
            verticalHeadingInsets = verticalHeadingInsets.Clamped();
            horizontalBodyInsets = horizontalBodyInsets.Clamped();
            horizontalLiningInsets = horizontalLiningInsets.Clamped();
            horizontalHeadingInsets = horizontalHeadingInsets.Clamped();
        }

        private static UIPanelLayout[] DefaultPanels() => new[]
        {
            new UIPanelLayout("Building information", 500, new Vector2(-24, -24), Vector2.one),
            new UIPanelLayout("Creature needs", 500, new Vector2(-24, -24), Vector2.one),
            new UIPanelLayout("Visitor needs", 500, new Vector2(-24, -24), Vector2.one),
            new UIPanelLayout("Enclosure information", 640, new Vector2(24, -180), new Vector2(0, 1)),
            new UIPanelLayout("Notification inbox", 720, new Vector2(24, 210), Vector2.zero),
            new UIPanelLayout("Terrain editing controls", 640, new Vector2(-24, 24), new Vector2(1, 0)),
            new UIPanelLayout("Pause window", 500, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Confirmation window", 900, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Main menu", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Settings window", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("New park setup", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Park management window", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Encyclopedia window", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Expedition world map", 1400, Vector2.zero, new Vector2(.5f, .5f)),
            new UIPanelLayout("Technology research", 1400, Vector2.zero, new Vector2(.5f, .5f))
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }

    [Serializable]
    public struct UIFrameInsets
    {
        [Range(0, .95f)] public float left, bottom, right, top;
        public UIFrameInsets(float left, float bottom, float right, float top)
        { this.left = left; this.bottom = bottom; this.right = right; this.top = top; }

        public UIFrameInsets Clamped()
        {
            var value = new UIFrameInsets(Mathf.Clamp(left, 0, .95f), Mathf.Clamp(bottom, 0, .95f),
                Mathf.Clamp(right, 0, .95f), Mathf.Clamp(top, 0, .95f));
            float horizontal = value.left + value.right, vertical = value.top + value.bottom;
            if (horizontal > .95f) { value.left *= .95f / horizontal; value.right *= .95f / horizontal; }
            if (vertical > .95f) { value.top *= .95f / vertical; value.bottom *= .95f / vertical; }
            return value;
        }
    }

    [Serializable]
    public sealed class UIPanelLayout
    {
        [Tooltip("Stable panel or screen name. Keep this name unchanged when adjusting a built-in panel.")]
        public string key;
        [Tooltip("Logical width before proportional screen fitting. Zero keeps the screen's original width.")]
        [Min(0)] public float preferredWidth;
        [Tooltip("Additional proportional size adjustment. Source image proportions remain unchanged.")]
        [Range(.1f, 3)] public float widthScale = 1f;
        public bool overridePosition;
        public Vector2 position;
        public bool overrideAnchor;
        [Tooltip("0,0 = bottom left; 1,1 = top right; 0.5,0.5 = centre.")]
        public Vector2 anchor;
        public Vector2 pivot;

        public UIPanelLayout() { }
        public UIPanelLayout(string key, float width, Vector2 position, Vector2 anchor)
        { this.key = key; preferredWidth = width; this.position = position; this.anchor = this.pivot = anchor; }
    }

    [Serializable]
    public sealed class UIElementLayoutOverride
    {
        public bool enabled = true;
        [Tooltip("Exact object name or a hierarchy suffix such as Building information/Panel content/Building name.")]
        public string elementNameOrPath;
        [Tooltip("Optional exact screen ancestor or Unity scene name. Leave empty to match all screens.")]
        public string screenName;
        public bool overridePosition;
        public Vector2 position;
        public bool overrideSize;
        public Vector2 size;

        public bool Matches(Transform target)
        {
            if (!enabled || target == null || string.IsNullOrWhiteSpace(elementNameOrPath)) return false;
            if (!string.IsNullOrWhiteSpace(screenName))
            {
                bool inScreen = string.Equals(target.gameObject.scene.name, screenName, StringComparison.OrdinalIgnoreCase);
                for (Transform parent = target; parent != null && !inScreen; parent = parent.parent)
                    inScreen = string.Equals(parent.name, screenName, StringComparison.OrdinalIgnoreCase);
                if (!inScreen) return false;
            }
            string wanted = elementNameOrPath.Trim().Replace('\\', '/').Trim('/');
            if (wanted.IndexOf('/') < 0) return string.Equals(target.name, wanted, StringComparison.OrdinalIgnoreCase);
            var names = new List<string>();
            for (Transform part = target; part != null; part = part.parent) names.Add(part.name);
            names.Reverse();
            string path = string.Join("/", names);
            return string.Equals(path, wanted, StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase);
        }
    }
}

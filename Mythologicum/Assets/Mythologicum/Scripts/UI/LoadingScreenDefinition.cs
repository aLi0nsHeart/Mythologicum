using UnityEngine;

namespace Mythologicum
{
    [CreateAssetMenu(menuName = "Mythologicum/Loading screen settings", fileName = "DefaultLoadingScreen")]
    public sealed class LoadingScreenDefinition : ScriptableObject
    {
        private static LoadingScreenDefinition current;
        public static LoadingScreenDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<LoadingScreenDefinition>("Interface/DefaultLoadingScreen");
                if (current == null)
                {
                    current = CreateInstance<LoadingScreenDefinition>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }

        [Header("Transition timing — real seconds")]
        [Min(0)] public float fadeInSeconds = .15f;
        [Min(0)] public float fadeOutSeconds = .2f;
        [Min(0), Tooltip("Minimum time for a readable transition. Loading can take longer; this does not alter reported progress.")]
        public float minimumVisibleSeconds = .4f;
        [Min(1), Tooltip("Maximum time to wait for scene preparation before offering a return to the main menu.")]
        public float preparationTimeoutSeconds = 180f;

        [Header("Background")]
        [Tooltip("Always opaque. The coordinator fades the complete screen using its CanvasGroup.")]
        public Color backgroundColor = new Color(.025f, .018f, .012f, 1);
        public bool useThemeBackground = true;
        [Tooltip("Optional background instead of the active UI theme's texture.")]
        public Texture2D backgroundTexture;
        [Range(0, 1)] public float backgroundTextureOpacity = .16f;

        [Header("Responsive layout")]
        public Vector2 referenceResolution = new Vector2(1920, 1080);
        [Range(0, 1)] public float matchWidthOrHeight = .5f;
        [Min(0)] public float screenMargin = 32;
        public Vector2 contentSize = new Vector2(1100, 620);
        public Vector2 contentOffset = Vector2.zero;
        public Vector2 brandPosition = new Vector2(0, 210);
        public Vector2 brandSize = new Vector2(1040, 80);
        public Vector2 subtitlePosition = new Vector2(0, 145);
        public Vector2 subtitleSize = new Vector2(1040, 42);
        public Vector2 titlePosition = new Vector2(0, 70);
        public Vector2 titleSize = new Vector2(1040, 56);
        public Vector2 stagePosition = new Vector2(0, 12);
        public Vector2 stageSize = new Vector2(1040, 60);

        [Header("Typography — fonts follow the active UI theme unless overridden")]
        public UITextAppearance brandText = CenteredText(48, 24);
        public UITextAppearance subtitleText = CenteredText(26, 15);
        public UITextAppearance titleText = CenteredText(34, 18);
        public UITextAppearance stageText = CenteredText(23, 15);
        public UITextAppearance progressText = CenteredText(22, 14);
        public UITextAppearance returnButtonText = CenteredText(22, 14);

        [Header("Progress through loading stages")]
        public Sprite barFrame;
        public Sprite barFill;
        public Vector2 barPosition = new Vector2(0, -110);
        [Min(80)] public float barWidth = 780;
        [Min(.1f), Tooltip("Width divided by height. The integrated bronze frame uses 563 / 96.")]
        public float barAspectRatio = 563f / 96f;
        [Tooltip("Fill bounds inside the frame, from bottom left, as fractions of the bar rectangle.")]
        public Rect fillBounds = new Rect(.052f, .345f, .896f, .31f);
        public Color frameTint = Color.white;
        public Color fillTint = new Color(1f, .78f, .36f, 1);
        public Vector2 progressPosition = new Vector2(0, -202);
        public Vector2 progressSize = new Vector2(320, 36);
        [Tooltip("Optional completion percentage of the loading stages. The default shows only the bar and current stage; this is not a prediction of time remaining.")]
        public bool showPercentage;

        [Header("Activity gear — decorative only")]
        public bool showSpinner = true;
        [Tooltip("Empty uses the active UI theme's Settings icon.")]
        public Sprite spinnerSprite;
        public Vector2 spinnerPosition = new Vector2(0, -258);
        public Vector2 spinnerSize = new Vector2(48, 48);
        [Min(0)] public float spinnerDegreesPerSecond = 35;
        public Color spinnerTint = new Color(1f, .82f, .52f, .8f);

        [Header("Error recovery")]
        public Vector2 returnButtonPosition = new Vector2(0, -150);
        public Vector2 returnButtonSize = new Vector2(340, 52);

        private static UITextAppearance CenteredText(int size, int minimum) => new UITextAppearance
        {
            fontSize = size, fitMode = UITextFitMode.ShrinkToFit, minimumFontSize = minimum,
            shrinkRange = size - minimum, overrideAlignment = true, alignment = TextAnchor.MiddleCenter
        };

        private void OnValidate()
        {
            fadeInSeconds = Mathf.Max(0, fadeInSeconds); fadeOutSeconds = Mathf.Max(0, fadeOutSeconds);
            minimumVisibleSeconds = Mathf.Max(0, minimumVisibleSeconds); backgroundColor.a = 1;
            preparationTimeoutSeconds = Mathf.Max(1, preparationTimeoutSeconds);
            backgroundTextureOpacity = Mathf.Clamp01(backgroundTextureOpacity);
            referenceResolution = Vector2.Max(Vector2.one, referenceResolution);
            matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight); screenMargin = Mathf.Max(0, screenMargin);
            contentSize = Vector2.Max(Vector2.one, contentSize); barWidth = Mathf.Max(80, barWidth);
            barAspectRatio = Mathf.Max(.1f, barAspectRatio);
            fillBounds.x = Mathf.Clamp01(fillBounds.x); fillBounds.y = Mathf.Clamp01(fillBounds.y);
            fillBounds.width = Mathf.Clamp(fillBounds.width, 0, 1 - fillBounds.x);
            fillBounds.height = Mathf.Clamp(fillBounds.height, 0, 1 - fillBounds.y);
            spinnerDegreesPerSecond = Mathf.Max(0, spinnerDegreesPerSecond);
            spinnerSize = Vector2.Max(Vector2.one, spinnerSize);
            returnButtonSize = Vector2.Max(Vector2.one, returnButtonSize);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

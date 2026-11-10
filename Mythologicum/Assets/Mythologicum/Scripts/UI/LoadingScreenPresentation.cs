using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Presentation only. The transition coordinator supplies progress and owns scene/input state.</summary>
    public sealed class LoadingScreenPresentation : MonoBehaviour
    {
        private LoadingScreenDefinition settings;
        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform content;
        private RawImage background;
        private Image fill, spinner;
        private GameObject bar;
        private Text title, stage, progressLabel;
        private Button returnButton;
        private Action returnToMenu;
        private Vector2 lastCanvasSize;
        private Texture lastBackgroundTexture;

        public Canvas Canvas => canvas;
        public CanvasGroup Group => group;
        public GameObject Root => gameObject;
        public bool IsVisible => gameObject.activeInHierarchy && Alpha > 0;
        public Text TitleText => title;
        public Text StageText => stage;
        public Text ProgressText => progressLabel;
        public Image ProgressImage => fill;
        public Button ReturnButton => returnButton;
        public float Progress { get; private set; }
        public bool ErrorVisible { get; private set; }
        public float Alpha
        {
            get => group == null ? 0 : group.alpha;
            set { if (group != null) group.alpha = float.IsNaN(value) ? 0 : Mathf.Clamp01(value); }
        }

        public static LoadingScreenPresentation Create(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("Loading screen", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var presentation = root.AddComponent<LoadingScreenPresentation>();
            presentation.Build();
            return presentation;
        }

        public void Show(string titleId, string stageId)
        {
            ErrorVisible = false; returnToMenu = null; Progress = 0;
            bar.SetActive(true); progressLabel.gameObject.SetActive(settings.showPercentage); returnButton.gameObject.SetActive(false);
            spinner.gameObject.SetActive(settings.showSpinner && spinner.sprite != null);
            Loc.Set(title, string.IsNullOrEmpty(titleId) ? "ui.loading.title" : titleId);
            SetProgress(0, string.IsNullOrEmpty(stageId) ? "ui.loading.stage.preparing" : stageId);
            UIThemeApplicator.ApplyAll(canvas);
            UnityEngine.Canvas.ForceUpdateCanvases();
            RefreshLayout();
        }

        public void SetProgress(float value, string stageId)
        {
            if (ErrorVisible) return;
            Progress = float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
            fill.fillAmount = Progress;
            Loc.Set(progressLabel, "ui.loading.progress_percent", Progress * 100f);
            if (!string.IsNullOrEmpty(stageId)) Loc.Set(stage, stageId);
        }

        public void SetError(string messageId, Action returnToMenu)
        {
            ErrorVisible = true; this.returnToMenu = returnToMenu;
            bar.SetActive(false); progressLabel.gameObject.SetActive(false); spinner.gameObject.SetActive(false);
            returnButton.gameObject.SetActive(true); returnButton.interactable = returnToMenu != null;
            Loc.Set(title, "ui.loading.error_title");
            Loc.Set(stage, messageId);
            UIThemeApplicator.ApplyAll(canvas);
        }

        private void Build()
        {
            settings = LoadingScreenDefinition.Current;
            canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true; canvas.sortingOrder = 32000;
            group = GetComponent<CanvasGroup>(); group.alpha = 1; group.blocksRaycasts = true; group.interactable = true;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = settings.referenceResolution; scaler.matchWidthOrHeight = settings.matchWidthOrHeight;

            var cover = Image("Loading cover", transform, Vector2.zero, Vector2.zero);
            Stretch(cover.rectTransform); var coverColor = settings.backgroundColor; coverColor.a = 1; cover.color = coverColor;
            cover.raycastTarget = true; UIThemeElement.Mark(cover.gameObject, UIThemeRole.Ignore);
            var backgroundRect = Rect("Loading background", transform, Vector2.zero, Vector2.zero); Stretch(backgroundRect);
            background = backgroundRect.gameObject.AddComponent<RawImage>(); background.raycastTarget = false;
            background.texture = settings.backgroundTexture != null ? settings.backgroundTexture
                : settings.useThemeBackground && UITheme.Current != null ? UITheme.Current.background : null;
            background.color = new Color(1, 1, 1, settings.backgroundTextureOpacity);
            UIThemeElement.Mark(background.gameObject, UIThemeRole.Ignore);

            content = Rect("Loading content", transform, settings.contentOffset, settings.contentSize);
            var brand = Label("Loading brand", settings.brandPosition, settings.brandSize, settings.brandText, UIThemeRole.Title);
            Loc.Set(brand, "ui.loading.brand");
            var subtitle = Label("Loading subtitle", settings.subtitlePosition, settings.subtitleSize, settings.subtitleText, UIThemeRole.Heading);
            Loc.Set(subtitle, "ui.loading.subtitle");
            title = Label("Loading title", settings.titlePosition, settings.titleSize, settings.titleText, UIThemeRole.Heading);
            stage = Label("Loading stage", settings.stagePosition, settings.stageSize, settings.stageText, UIThemeRole.Text);

            var barRect = Rect("Loading progress bar", content, settings.barPosition,
                new Vector2(settings.barWidth, settings.barWidth / Mathf.Max(.1f, settings.barAspectRatio)));
            bar = barRect.gameObject;
            fill = Image("Loading progress fill", barRect, Vector2.zero, Vector2.zero);
            var bounds = settings.fillBounds;
            fill.rectTransform.anchorMin = new Vector2(bounds.xMin, bounds.yMin);
            fill.rectTransform.anchorMax = new Vector2(bounds.xMax, bounds.yMax);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.sprite = settings.barFill; fill.color = settings.fillTint;
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            var rim = Image("Loading progress frame", barRect, Vector2.zero, Vector2.zero); Stretch(rim.rectTransform);
            rim.sprite = settings.barFrame; rim.preserveAspect = true; rim.color = settings.frameTint;
            rim.transform.SetAsFirstSibling();
            progressLabel = Label("Loading percentage", settings.progressPosition, settings.progressSize,
                settings.progressText, UIThemeRole.Text);

            spinner = Image("Loading activity gear", content, settings.spinnerPosition, settings.spinnerSize);
            spinner.sprite = settings.spinnerSprite != null ? settings.spinnerSprite : UITheme.Current?.Icon(UIIconKind.Settings)?.normal;
            spinner.preserveAspect = true; spinner.color = settings.spinnerTint;

            var buttonRect = Rect("Loading return to menu", content, settings.returnButtonPosition, settings.returnButtonSize);
            var buttonImage = buttonRect.gameObject.AddComponent<Image>(); buttonImage.color = Color.white;
            returnButton = buttonRect.gameObject.AddComponent<Button>(); returnButton.targetGraphic = buttonImage;
            returnButton.navigation = new Navigation { mode = Navigation.Mode.None };
            returnButton.onClick.AddListener(() => this.returnToMenu?.Invoke());
            UIThemeElement.Mark(buttonRect.gameObject, UIThemeRole.Button);
            var caption = Label("Loading return caption", Vector2.zero, settings.returnButtonSize,
                settings.returnButtonText, UIThemeRole.Text, buttonRect);
            Loc.Set(caption, "ui.loading.return_to_menu");
            Show("ui.loading.title", "ui.loading.stage.preparing");
        }

        private Text Label(string name, Vector2 position, Vector2 size, UITextAppearance appearance,
            UIThemeRole role, Transform parent = null)
        {
            var rect = Rect(name, parent != null ? parent : content, position, size);
            var label = rect.gameObject.AddComponent<Text>(); label.raycastTarget = false; label.supportRichText = false;
            label.font = UITheme.BodyFont; label.fontSize = appearance != null && appearance.fontSize > 0 ? appearance.fontSize : 22;
            label.alignment = TextAnchor.MiddleCenter; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            var element = UIThemeElement.Mark(label.gameObject, role);
            element.LocalAppearanceEnabled = appearance != null; element.LocalAppearance = appearance;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform)); root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>(); image.raycastTarget = false;
            UIThemeElement.Mark(image.gameObject, UIThemeRole.Ignore); return image;
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private void LateUpdate()
        {
            RefreshLayout();
            if (spinner != null && spinner.gameObject.activeSelf && !ErrorVisible)
                spinner.rectTransform.Rotate(0, 0, -settings.spinnerDegreesPerSecond * Time.unscaledDeltaTime);
        }

        private void RefreshLayout()
        {
            if (canvas == null || content == null) return;
            Vector2 size = ((RectTransform)canvas.transform).rect.size;
            if (size == lastCanvasSize && background.texture == lastBackgroundTexture) return;
            lastCanvasSize = size; lastBackgroundTexture = background.texture;
            float availableWidth = Mathf.Max(1, size.x - settings.screenMargin * 2);
            float availableHeight = Mathf.Max(1, size.y - settings.screenMargin * 2);
            content.localScale = Vector3.one * Mathf.Min(1, availableWidth / settings.contentSize.x, availableHeight / settings.contentSize.y);
            if (background.texture == null || size.y <= 0) return;
            float displayAspect = size.x / size.y, textureAspect = (float)background.texture.width / background.texture.height;
            background.uvRect = displayAspect > textureAspect
                ? new Rect(0, (1 - textureAspect / displayAspect) * .5f, 1, textureAspect / displayAspect)
                : new Rect((1 - displayAspect / textureAspect) * .5f, 0, displayAspect / textureAspect, 1);
        }
    }
}

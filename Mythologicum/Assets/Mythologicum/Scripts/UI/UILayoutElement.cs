using UnityEngine;

namespace Mythologicum
{
    /// <summary>Applies saved geometry edits after a generated screen has laid out its controls.</summary>
    [DefaultExecutionOrder(30000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UILayoutElement : MonoBehaviour
    {
        [SerializeField] private UILayoutBinding binding;
        private RectTransform rect;
        private bool positioned, sized;
        private Vector2 originalPosition, originalSize, appliedPosition, appliedSize;
        // Runtime ownership deliberately does not survive cloning/serialization.
        internal UIThemeApplicator Maintainer { get; set; }
        public System.Action RefreshLayout { get; set; }

        public static UILayoutElement Attach(RectTransform target)
        {
            var element = target.GetComponent<UILayoutElement>() ?? target.gameObject.AddComponent<UILayoutElement>();
            element.rect = target;
            return element;
        }

        public static UILayoutElement Bind(RectTransform target, UILayoutBinding value)
        {
            var element = Attach(target);
            element.binding = value;
            element.Refresh();
            return element;
        }

        public void Refresh()
        {
            if (rect == null) rect = (RectTransform)transform;
            RefreshLayout?.Invoke();
            var settings = UILayoutSettings.Current;
            if (settings != null) ApplyBinding(settings);
            // Illustrated frames have dedicated, aspect-safe panel settings.
            if (GetComponent<UIFramedPanel>() != null || IsFramePart()) return;
            var entry = settings != null ? settings.Element(transform) : null;
            if (entry != null && entry.overridePosition)
            {
                if (!positioned) originalPosition = rect.anchoredPosition;
                rect.anchoredPosition = appliedPosition = entry.position;
                positioned = true;
            }
            else if (positioned)
            {
                if (rect.anchoredPosition == appliedPosition) rect.anchoredPosition = originalPosition;
                positioned = false;
            }
            if (entry != null && entry.overrideSize)
            {
                if (!sized) originalSize = rect.rect.size;
                Vector2 size = Vector2.Max(Vector2.one, entry.size);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
                appliedSize = rect.rect.size;
                sized = true;
            }
            else if (sized)
            {
                if (rect.rect.size == appliedSize)
                {
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, originalSize.x);
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, originalSize.y);
                }
                sized = false;
            }
        }

        private void ApplyBinding(UILayoutSettings settings)
        {
            Vector2 position;
            switch (binding)
            {
                case UILayoutBinding.BuildingIcon: position = settings.buildingIconPosition; break;
                case UILayoutBinding.ManagementIcon: position = settings.managementIconPosition; break;
                case UILayoutBinding.EncyclopediaIcon: position = settings.encyclopediaIconPosition; break;
                case UILayoutBinding.MailIcon: position = settings.mailIconPosition; break;
                case UILayoutBinding.PauseIcon: position = settings.pauseIconPosition; break;
                case UILayoutBinding.FundsPanel: rect.anchoredPosition = settings.fundsPosition; SetSize(settings.statusSize); return;
                case UILayoutBinding.FundsText: SetSize(settings.fundsSize); return;
                case UILayoutBinding.Stars:
                    rect.anchoredPosition = new Vector2(settings.starsPosition.x, -settings.starsPosition.y);
                    SetSize(settings.starsSize); return;
                default: return;
            }
            rect.anchoredPosition = position;
            SetSize(Vector2.one * Mathf.Max(16f, settings.toolbarIconSize));
        }

        private void SetSize(Vector2 size)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, size.x));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1f, size.y));
        }

        private bool IsFramePart()
        {
            if (GetComponentInParent<UIFramedPanel>(true) == null) return false;
            return name == "Panel artwork" || name == "Panel lining" || name == "Panel content" || name == "Panel heading";
        }

        private void LateUpdate()
        {
            if (Maintainer == null || !Maintainer.isActiveAndEnabled) Refresh();
        }
    }

    public enum UILayoutBinding { None, BuildingIcon, ManagementIcon, EncyclopediaIcon, MailIcon, PauseIcon, FundsPanel, FundsText, Stars }
}

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>The park's two headline figures, backed by the existing simulation.</summary>
    public sealed class ParkStatusPresentation : IDisposable
    {
        private readonly GameObject root;
        private readonly ParkEconomy economy;
        private readonly VisitorWorld visitors;
        private readonly Text funds, rating;
        private readonly StarRatingGraphic stars;
        private readonly Image moneyIcon;

        public ParkStatusPresentation(Canvas canvas, ParkEconomy economy, VisitorWorld visitors)
        {
            this.economy = economy; this.visitors = visitors;
            root = ManagementWidgets.Box(canvas.transform, "Park status", Vector2.zero, new Vector2(300, 96));
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(15, -15);
            UIThemeElement.Mark(root, UIThemeRole.Window);

            var iconRect = ManagementWidgets.Rect(root.transform, "Funds icon", new Vector2(14, 10), new Vector2(38, 38));
            moneyIcon = iconRect.gameObject.AddComponent<Image>();
            moneyIcon.preserveAspect = true; moneyIcon.raycastTarget = false;
            UIThemeElement.Mark(iconRect.gameObject, UIThemeRole.Icon);
            funds = ManagementWidgets.Label(root.transform, "Park funds", new Vector2(62, 13), new Vector2(224, 33), 20);
            funds.resizeTextForBestFit = true; funds.resizeTextMinSize = 14; funds.resizeTextMaxSize = 20;
            var starRect = ManagementWidgets.Rect(root.transform, "Park stars", new Vector2(14, 54), new Vector2(140, 28));
            stars = starRect.gameObject.AddComponent<StarRatingGraphic>(); stars.raycastTarget = false;
            rating = ManagementWidgets.Label(root.transform, "Park rating", new Vector2(168, 55), new Vector2(116, 28), 18);
            Refresh();
        }

        public void Refresh()
        {
            if (root == null) return;
            funds.text = $"Funds: {economy.Balance:0.00}";
            float value = visitors.Reputation.Stars;
            rating.text = $"{value:0.0} / 5";
            stars.SetRating(value);
            var icon = UITheme.Current?.Icon(UIIconKind.Funds)?.normal;
            moneyIcon.sprite = icon; moneyIcon.enabled = icon != null;
        }

        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    }
}

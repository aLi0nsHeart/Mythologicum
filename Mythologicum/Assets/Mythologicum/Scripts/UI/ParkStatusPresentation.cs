using System;
using System.Globalization;
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
        private readonly Text funds;
        private readonly StarRatingGraphic stars;

        public ParkStatusPresentation(Canvas canvas, ParkEconomy economy, VisitorWorld visitors)
        {
            this.economy = economy; this.visitors = visitors;
            root = ManagementWidgets.Rect(canvas.transform, "Park status", Vector2.zero, new Vector2(300, 82)).gameObject;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(15, -12);
            funds = ManagementWidgets.Label(root.transform, "Park funds", Vector2.zero, new Vector2(300, 44), 30);
            funds.resizeTextForBestFit = true; funds.resizeTextMinSize = 20; funds.resizeTextMaxSize = 30;
            var outline = funds.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.055f, .035f, .01f, 1);
            outline.effectDistance = new Vector2(1, -1);
            var starRect = ManagementWidgets.Rect(root.transform, "Park stars", new Vector2(0, 45), new Vector2(154, 28));
            stars = starRect.gameObject.AddComponent<StarRatingGraphic>(); stars.raycastTarget = false;
            var starOutline = starRect.gameObject.AddComponent<Outline>();
            starOutline.effectColor = new Color(.055f, .035f, .01f, 1);
            starOutline.effectDistance = new Vector2(1, -1);
            Refresh();
        }

        public void Refresh()
        {
            if (root == null) return;
            funds.text = economy.Balance.ToString("$#,##0;-$#,##0;$0", CultureInfo.InvariantCulture);
            float value = visitors.Reputation.Stars;
            stars.SetRating(value);
        }

        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }
    }
}

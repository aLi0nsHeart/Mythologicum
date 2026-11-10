using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>The selected guest's needs. Park totals are available in Park Management.</summary>
    public sealed class VisitorPresentation
    {
        private readonly GameObject panel;
        private readonly Text details;
        private readonly UIFramedPanel frame;
        private readonly ScrollRect detailsScroll;
        private VisitorAgent selected;
        private bool inspecting = true;
        public RectTransform PanelRect => (RectTransform)panel.transform;

        public VisitorPresentation(Canvas canvas, Font font, VisitorWorld world, ParkEconomy economy)
        {
            panel = Box(canvas, "Visitor needs", new Vector2(1f, 1f), new Vector2(-24f, -24f), Vector2.zero);
            frame=UIFramedPanel.Attach(panel,true,500f);var body=frame.Content;
            Text title=Label(body,"Visitor",font,22);Loc.Set(title,"visitor.name");
            title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.pivot=new Vector2(.5f,1);title.rectTransform.sizeDelta=new Vector2(0,38);
            var content=ManagementWidgets.Scroll(body,"Visitor details",Vector2.zero,new Vector2(370,400),0);
            detailsScroll=content.GetComponentInParent<ScrollRect>();
            var scrollRect=(RectTransform)detailsScroll.transform;
            scrollRect.anchorMin=Vector2.zero;scrollRect.anchorMax=Vector2.one;
            scrollRect.offsetMin=Vector2.zero;scrollRect.offsetMax=new Vector2(0,-46);
            details = Label(content, "Selected visitor", font, 18);
            details.rectTransform.anchorMin=new Vector2(0,1);details.rectTransform.anchorMax=Vector2.one;
            details.rectTransform.pivot=new Vector2(.5f,1);details.rectTransform.anchoredPosition=Vector2.zero;
            UIOverflowScroll.Attach(detailsScroll);
            Refresh();
        }

        public void Select(VisitorAgent visitor)
        {
            if(selected!=visitor){detailsScroll.StopMovement();detailsScroll.verticalNormalizedPosition=1f;}
            selected = visitor; Refresh();
        }
        public void SetInspecting(bool value) { inspecting = value; Refresh(); }

        public void Refresh()
        {
            if (panel == null) return;
            bool show = inspecting && selected != null && selected.Actor != null && selected.Actor.Alive;
            panel.SetActive(show);
            if (!show) return;
            frame.Refresh();
            CreatureAgent actor = selected.Actor;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(selected.TimeInPark));
            details.text = Loc.Format("visitor.info.needs", Loc.Enum(selected.Activity), selected.Mood.Satisfaction,
                actor.Health.Current, actor.Health.Maximum, actor.Vitals.Hunger, actor.Vitals.Fear,
                actor.Energy.Energy, seconds / 60, seconds % 60, 100f * (1f - selected.Mood.VisitProgress),
                100f * selected.Mood.Willingness)
                + selected.Status + (string.IsNullOrEmpty(selected.PriceComplaint) ? "" : "\n" + selected.PriceComplaint)
                + Loc.Text("visitor.info.deselect");
            float height=Mathf.Ceil(details.preferredHeight);
            details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            detailsScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            UIOverflowScroll.Attach(detailsScroll).Refresh();
        }

        public void Dispose()
        {
            if (panel != null) Object.Destroy(panel);
        }

        private static GameObject Box(Canvas canvas, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var box = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            box.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)box.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            box.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.09f, 0.96f);
            return box;
        }

        private static Text Label(Transform parent, string name, Font font, int size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            root.transform.SetParent(parent, false);
            Text label = root.GetComponent<Text>();
            label.font = UITheme.BodyFont; label.fontSize = size; label.raycastTarget = false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;
            label.alignment = TextAnchor.UpperLeft; label.color = new Color(0.94f, 0.86f, 0.66f);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

    }
}

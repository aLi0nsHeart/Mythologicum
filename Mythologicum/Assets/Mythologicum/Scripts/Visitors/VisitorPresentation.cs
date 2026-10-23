using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Park money, visitor overview, and the selected guest's needs.</summary>
    public sealed class VisitorPresentation
    {
        private readonly GameObject hud, panel;
        private readonly Text overview, details, fundsLabel;
        private readonly VisitorWorld world;
        private readonly ParkEconomy economy;
        private VisitorAgent selected;
        private bool inspecting = true;

        public VisitorPresentation(Canvas canvas, Font font, VisitorWorld world, ParkEconomy economy)
        {
            this.world = world;
            this.economy = economy;
            hud = Box(canvas, "Park overview", new Vector2(0f, 1f), new Vector2(15f, -15f), new Vector2(315f, 230f));
            overview = Label(hud.transform, "Park finances and guests", font, 18);
            overview.rectTransform.offsetMin = new Vector2(12f, 52f);
            overview.rectTransform.offsetMax = new Vector2(-12f, -10f);

            var toggle = new GameObject("Infinite funds", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            toggle.transform.SetParent(hud.transform, false);
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = new Vector2(0f, 0f); toggleRect.anchorMax = new Vector2(1f, 0f);
            toggleRect.offsetMin = new Vector2(10f, 10f); toggleRect.offsetMax = new Vector2(-10f, 44f);
            Image toggleImage = toggle.GetComponent<Image>();
            toggleImage.color = new Color(0.2f, 0.29f, 0.32f);
            Button button = toggle.GetComponent<Button>(); button.targetGraphic = toggleImage;
            fundsLabel = Label(toggle.transform, "Funds mode", font, 18);
            fundsLabel.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(() => { economy.UnlimitedFunds = !economy.UnlimitedFunds; Refresh(); });

            panel = Box(canvas, "Visitor needs", new Vector2(1f, 1f), new Vector2(-15f, -170f), new Vector2(350f, 365f));
            details = Label(panel.transform, "Selected visitor", font, 18);
            details.rectTransform.offsetMin = new Vector2(12f, 10f);
            details.rectTransform.offsetMax = new Vector2(-12f, -10f);
            Refresh();
        }

        public void Select(VisitorAgent visitor) { selected = visitor; Refresh(); }
        public void SetInspecting(bool value) { inspecting = value; Refresh(); }

        public void Refresh()
        {
            if (hud == null || panel == null) return;
            overview.text = $"Funds: {economy.Balance:0.00}\n"
                + $"Income: {economy.Income:0.00} | Costs: {economy.Expenses:0.00}\n"
                + $"Visitors: {world.Population}\n"
                + $"Average satisfaction: {world.AverageSatisfaction:0}/100\n"
                + $"Departed: {world.DepartedCount} | Lost: {world.LostCount}\n"
                + $"Stranded: {world.StrandedCount}";
            fundsLabel.text = "Infinite funds: " + (economy.UnlimitedFunds ? "on" : "off");
            bool show = inspecting && selected != null && selected.Actor != null && selected.Actor.Alive;
            panel.SetActive(show);
            if (!show) return;
            CreatureAgent actor = selected.Actor;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(selected.TimeInPark));
            details.text = $"Visitor - {Readable(selected.Activity.ToString())}\n"
                + $"Satisfaction: {selected.Mood.Satisfaction:0}/100\n"
                + $"Hunger: {actor.Vitals.Hunger:0}/100\n"
                + $"Fear: {actor.Vitals.Fear:0}/100\n"
                + $"Energy: {actor.Energy.Energy:0}/100\n"
                + $"Time in park: {seconds / 60}m {seconds % 60:00}s\n"
                + $"Patience left: {100f * (1f - selected.Mood.VisitProgress):0}%\n"
                + $"Willingness to stay: {100f * selected.Mood.Willingness:0}%\n\n"
                + selected.Status + "\n\nClick elsewhere to deselect.";
        }

        public void Dispose()
        {
            if (hud != null) Object.Destroy(hud);
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
            label.font = font; label.fontSize = size; label.raycastTarget = false;
            label.alignment = TextAnchor.UpperLeft; label.color = new Color(0.94f, 0.86f, 0.66f);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        private static string Readable(string value)
        {
            var result = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && char.IsLower(value[i - 1])) result.Append(' ');
                result.Append(value[i]);
            }
            return result.ToString();
        }
    }
}

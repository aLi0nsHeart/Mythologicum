using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed class CreatureInspection
    {
        private readonly GameObject panel;
        private readonly Text text;
        private CreatureAgent selected;
        private bool inspecting = true;

        public CreatureInspection(Canvas canvas, Font font)
        {
            panel = new GameObject("Creature needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-15f, -140f); rect.sizeDelta = new Vector2(420f, 560f);
            panel.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.09f, 0.96f);
            var label = new GameObject("Needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            text = label.GetComponent<Text>(); text.font = font; text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
            text.color = new Color(0.94f, 0.86f, 0.66f);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 10f); text.rectTransform.offsetMax = new Vector2(-12f, -10f);
            panel.SetActive(false);
        }

        public void Select(CreatureAgent agent) { selected = agent; Refresh(); }
        public void SetInspecting(bool value) { inspecting = value; if (panel != null) Refresh(); }
        public void Refresh()
        {
            if (panel == null) return;
            bool show = inspecting && selected != null && selected.Alive;
            panel.SetActive(show);
            if (!show) return;
            CreatureDefinition definition = selected.Definition;
            CreatureNeedScores needs = selected.Wellbeing.Scores;
            string herd = definition.maximumCompanions < 0 ? $"{definition.minimumCompanions + 1}+"
                : $"{definition.minimumCompanions + 1}-{definition.maximumCompanions + 1}";
            string reasons = "Rare curiosity";
            if (selected.Vitals.Hunger > 75f) reasons += ", hunger";
            if (selected.Wellbeing.Agitation > 0f) reasons += ", agitation";
            if (selected.Crowding > 0f) reasons += ", space pressure";
            text.text = $"{definition.displayName} - {selected.Activity}\n"
                + $"Origin: {definition.origin}\n"
                + $"Health: {selected.Health.Current:0}/{selected.Health.Maximum:0}\n"
                + $"Damage: {definition.damage:0} | Threat: {definition.threat:0}\n"
                + $"Happiness: {selected.Wellbeing.Happiness:0}/100"
                + (selected.Wellbeing.Agitation > 0f ? "  AGITATED\n" : "\n")
                + $"Hunger: {selected.Vitals.Hunger:0} | Fear: {selected.Vitals.Fear:0}\n"
                + $"Energy: {selected.Energy.Energy:0} | Exhaustion: {selected.Energy.Exhaustion:0}\n"
                + (definition.requiresGravestoneForSleep ? "Sleep site: a reachable gravestone\n"
                    : definition.requiresTreeForSleep ? "Sleep site: climb a reachable tree\n" : "Sleep site: any safe ground\n")
                + $"Environment: {needs.Environment:0}/100\n"
                + $"Nearby trees: {selected.NearbyTrees}/{definition.requiredTrees}\n"
                + (definition.requiredGravestones > 0 || definition.requiresGravestoneForSleep
                    ? $"Nearby gravestones: {selected.NearbyGravestones}/{definition.requiredGravestones}\n" : "")
                + $"Space: {needs.Space:0}/100\n"
                + $"Reachable: {selected.AccessibleArea:0} / {definition.minimumSpace:0} m²\n"
                + $"Herd: {needs.Herd:0}/100\n"
                + $"Group: {selected.Companions + 1} (preferred {herd}, including self)\n"
                + $"Space pressure: {selected.Crowding * 100f:0}%\n"
                + $"Escape urge: {selected.EscapeChancePerMinute * 100f:0.00}% / minute\n"
                + $"Reasons: {reasons}\n"
                + $"{selected.EscapeStatus}\n"
                + "Urge is not escape success.\nSleep, urgent actions and retries pause attempts.";
        }
        public void Dispose() { if (panel != null) Object.Destroy(panel); }
    }
}

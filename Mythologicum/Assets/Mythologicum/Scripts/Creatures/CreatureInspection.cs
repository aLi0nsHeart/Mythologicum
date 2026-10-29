using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed class CreatureInspection
    {
        private readonly GameObject panel;
        private readonly Text text, retireLabel, sedateLabel, moveLabel;
        private readonly RectTransform viewport;
        private readonly Button retireButton, sedateButton, moveButton;
        private Action<CreatureAgent> retire, sedate, move;
        private Func<CreatureDefinition, string> researchNotes;
        private CreatureAgent selected;
        private bool inspecting = true;

        public CreatureInspection(Canvas canvas, Font font)
        {
            panel = new GameObject("Creature needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-15f, -140f); rect.sizeDelta = new Vector2(420f, 620f);
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .09f, .96f);

            var scrollObject = new GameObject("Creature details", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(panel.transform, false);
            var scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10f, 10f); scrollRect.offsetMax = new Vector2(-10f, -64f);
            var view = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            view.transform.SetParent(scrollObject.transform, false); viewport = (RectTransform)view.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-14f, 0f);
            view.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);

            var label = new GameObject("Needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(view.transform, false);
            text = label.GetComponent<Text>(); text.font = font; text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(.94f, .86f, .66f);
            text.rectTransform.anchorMin = new Vector2(0f, 1f); text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, 1f); text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.verticalScrollbar = CreateScrollbar(scrollObject.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            retireButton = MakeButton(font, "Retire", 0, out retireLabel, () => { if (selected != null) retire?.Invoke(selected); Refresh(); });
            sedateButton = MakeButton(font, "Sedate", 1, out sedateLabel, () => { if (selected != null) sedate?.Invoke(selected); Refresh(); });
            moveButton = MakeButton(font, "Move", 2, out moveLabel, () => { if (selected != null) move?.Invoke(selected); Refresh(); });
            panel.SetActive(false);
        }

        private Scrollbar CreateScrollbar(Transform parent)
        {
            var barObject = new GameObject("Details scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(parent, false);
            var rect = (RectTransform)barObject.transform;
            rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(10f, 0f);
            barObject.GetComponent<Image>().color = new Color(.1f, .16f, .18f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(barObject.transform, false);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            handle.GetComponent<Image>().color = new Color(.45f, .55f, .56f);
            var bar = barObject.GetComponent<Scrollbar>();
            bar.handleRect = handleRect; bar.targetGraphic = handle.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            return bar;
        }

        private Button MakeButton(Font font, string title, int column, out Text label, Action clicked)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(panel.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2((column + .5f) / 3f, 1f); rect.anchoredPosition = new Vector2(0f, -30f);
            rect.sizeDelta = new Vector2(126f, 38f);
            go.GetComponent<Image>().color = new Color(.2f, .29f, .32f);
            Button button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);
            label = labelObject.GetComponent<Text>(); label.font = font; label.fontSize = 17;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; label.text = title;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        public void ConfigureStaffActions(Action<CreatureAgent> retire, Action<CreatureAgent> sedate, Action<CreatureAgent> move)
        { this.retire = retire; this.sedate = sedate; this.move = move; Refresh(); }
        public void ConfigureResearch(Func<CreatureDefinition, string> notes) { researchNotes = notes; Refresh(); }
        public void Select(CreatureAgent agent) { selected = agent; Refresh(); }
        public void SetInspecting(bool value) { inspecting = value; if (panel != null) Refresh(); }
        public void Refresh()
        {
            if (panel == null) return;
            var parentRect = panel.transform.parent as RectTransform;
            if (parentRect != null)
            {
                var panelRect = (RectTransform)panel.transform;
                float width = Mathf.Min(420f, Mathf.Max(180f,parentRect.rect.width - 30f));
                panelRect.sizeDelta = new Vector2(width,Mathf.Min(620f,Mathf.Max(180f,parentRect.rect.height - 240f)));
                foreach (Button action in new[] { retireButton, sedateButton, moveButton })
                    ((RectTransform)action.transform).sizeDelta = new Vector2(width / 3f - 12f,38f);
            }
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
                + $"Origin: {definition.origin} | Rating: {definition.rating}/5\n"
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
                + $"Reasons: {reasons}\n{selected.EscapeStatus}\n"
                + "Urge is not escape success.\nSleep, urgent actions and retries pause attempts.\n";
            if (selected.IsSedated) text.text += $"\nSedation remaining: {selected.SedationRemaining:0}s\n";
            if (!string.IsNullOrEmpty(selected.StaffStatus)) text.text += "\nStaff: " + selected.StaffStatus + "\n";
            if (researchNotes != null) text.text += "\nResearch notes\n" + researchNotes(definition);
            text.rectTransform.sizeDelta = new Vector2(0f, Mathf.Max(viewport.rect.height, text.preferredHeight + 8f));
            bool controllable = !selected.ExternallyControlled && selected.CarriedBy == null && !selected.RetirementRequested;
            retireButton.interactable = controllable && retire != null;
            sedateButton.interactable = controllable && !selected.IsSedated && !selected.SedationRequested && sedate != null;
            moveButton.gameObject.SetActive(selected.IsSedated && !selected.ExternallyControlled);
            moveButton.interactable = controllable && !selected.MoveRequestedDestination.HasValue && move != null;
            retireLabel.text = selected.RetirementRequested ? "Retire queued" : "Retire";
            sedateLabel.text = selected.IsSedated ? "Sedated" : selected.SedationRequested ? "Sedate queued" : "Sedate";
            moveLabel.text = selected.CarriedBy != null ? "Being moved" : selected.MoveRequestedDestination.HasValue ? "Move queued" : "Move";
        }
        public void Dispose() { if (panel != null) UnityEngine.Object.Destroy(panel); }
    }
}

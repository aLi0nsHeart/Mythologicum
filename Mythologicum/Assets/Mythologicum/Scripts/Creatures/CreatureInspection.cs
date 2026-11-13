using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed class CreatureInspection
    {
        private readonly GameObject panel;
        private readonly Text text, title, retireLabel, sedateLabel, moveLabel;
        private readonly RectTransform viewport;
        private readonly RectTransform body;
        private readonly UIFramedPanel frame;
        private readonly ScrollRect detailsScroll;
        private readonly Button retireButton, sedateButton, moveButton;
        private Action<CreatureAgent> retire, sedate, move;
        private Func<CreatureDefinition, string> researchNotes;
        private CreatureAgent selected;
        private bool inspecting = true;
        public RectTransform PanelRect => (RectTransform)panel.transform;

        public CreatureInspection(Canvas canvas, Font font)
        {
            panel = new GameObject("Creature needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            panel.GetComponent<Image>().color = new Color(.04f, .07f, .09f, .96f);
            frame=UIFramedPanel.Attach(panel,true,500f);body=frame.Content;
            var heading=new GameObject("Creature name",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
            heading.transform.SetParent(body,false);title=heading.GetComponent<Text>();
            title.font=UITheme.BodyFont;title.fontSize=22;title.alignment=TextAnchor.UpperLeft;title.raycastTarget=false;
            title.rectTransform.anchorMin=new Vector2(0,1);title.rectTransform.anchorMax=Vector2.one;
            title.rectTransform.pivot=new Vector2(.5f,1);title.rectTransform.sizeDelta=new Vector2(0,38);

            var scrollObject = new GameObject("Creature details", typeof(RectTransform), typeof(ScrollRect));
            scrollObject.transform.SetParent(body, false);
            var scrollRect = (RectTransform)scrollObject.transform;
            scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = Vector2.zero; scrollRect.offsetMax = new Vector2(0f, -94f);
            var view = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            view.transform.SetParent(scrollObject.transform, false); viewport = (RectTransform)view.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero; viewport.offsetMax = Vector2.zero;
            view.GetComponent<Image>().color = new Color(0f, 0f, 0f, .001f);

            var label = new GameObject("Needs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(view.transform, false);
            text = label.GetComponent<Text>(); text.font = UITheme.BodyFont; text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(.94f, .86f, .66f);
            text.rectTransform.anchorMin = new Vector2(0f, 1f); text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.pivot = new Vector2(.5f, 1f); text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = Vector2.zero;
            ScrollRect scroll = detailsScroll=scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            UIOverflowScroll.Attach(scroll);

            retireButton = MakeButton(font, "creature.action.retire", 0, out retireLabel, () => { if (selected != null) retire?.Invoke(selected); Refresh(); });
            sedateButton = MakeButton(font, "creature.action.sedate", 1, out sedateLabel, () => { if (selected != null) sedate?.Invoke(selected); Refresh(); });
            moveButton = MakeButton(font, "creature.action.move", 2, out moveLabel, () => { if (selected != null) move?.Invoke(selected); Refresh(); });
            panel.SetActive(false);
        }

        private Button MakeButton(Font font, string title, int column, out Text label, Action clicked)
        {
            var go = new GameObject(Loc.InternalName(title), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(body, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2((column + .5f) / 3f, 1f); rect.anchoredPosition = new Vector2(0f, -30f);
            rect.sizeDelta = new Vector2(126f, 38f);
            go.GetComponent<Image>().color = new Color(.2f, .29f, .32f);
            Button button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(go.transform, false);
            label = labelObject.GetComponent<Text>(); label.font = UITheme.BodyFont; label.fontSize = 17;
            label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; Loc.Set(label, title);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        public void ConfigureStaffActions(Action<CreatureAgent> retire, Action<CreatureAgent> sedate, Action<CreatureAgent> move)
        { this.retire = retire; this.sedate = sedate; this.move = move; Refresh(); }
        public void ConfigureResearch(Func<CreatureDefinition, string> notes) { researchNotes = notes; Refresh(); }
        public void Select(CreatureAgent agent)
        {
            if(selected!=agent){detailsScroll.StopMovement();detailsScroll.verticalNormalizedPosition=1f;}
            selected = agent; Refresh();
        }
        public void SetInspecting(bool value) { inspecting = value; if (panel != null) Refresh(); }
        public void Refresh()
        {
            if (panel == null) return;
            bool show = inspecting && selected != null && selected.Alive;
            panel.SetActive(show);
            if (!show) return;
            frame.Refresh();
            CreatureDefinition definition = selected.Definition;
            CreatureNeedScores needs = selected.Wellbeing.Scores;
            string herd = definition.maximumCompanions < 0
                ? Loc.Format("creature.group.unbounded", definition.minimumCompanions + 1)
                : Loc.Format("creature.group.range", definition.minimumCompanions + 1, definition.maximumCompanions + 1);
            string reasons = Loc.Text("creature.reason.curiosity");
            if (selected.Vitals.Hunger > definition.Behaviour.escapeHungerStart) reasons += Loc.Text("creature.reason.hunger");
            if (selected.Wellbeing.Agitation > 0f) reasons += Loc.Text("creature.reason.agitation");
            if (selected.Crowding > 0f) reasons += Loc.Text("creature.reason.space");
            title.text=definition.DisplayName;
            text.text = Loc.Format("creature.info.vitals", Loc.Enum(selected.Activity), definition.Origin, definition.rating,
                selected.Health.Current, selected.Health.Maximum, definition.damage, definition.threat,
                selected.Wellbeing.Happiness, selected.Wellbeing.Agitation > 0f ? Loc.Text("creature.info.agitated") : "",
                selected.Vitals.Hunger, selected.Vitals.Fear, selected.Energy.Energy, selected.Energy.Exhaustion)
                + (!definition.needsFood ? Loc.Text("creature.info.no_food_needed") : "")
                + Loc.Text(definition.requiresGravestoneForSleep ? "creature.info.sleep.grave"
                    : definition.requiresTreeForSleep ? "creature.info.sleep.tree" : "creature.info.sleep.ground")
                + Loc.Format("creature.info.environment", needs.Environment, selected.NearbyTrees, definition.requiredTrees)
                + (definition.requiredGravestones > 0 || definition.requiresGravestoneForSleep
                    ? Loc.Format("creature.info.gravestones", selected.NearbyGravestones, definition.requiredGravestones) : "")
                + Loc.Format("creature.info.space_herd_escape", needs.Space, selected.AccessibleArea, definition.minimumSpace,
                    needs.Herd, selected.Companions + 1, herd, selected.Crowding * 100f,
                    selected.EscapeChancePerMinute * 100f, reasons, selected.EscapeStatus);
            if (selected.IsSedated) text.text += Loc.Format("creature.info.sedation", selected.SedationRemaining);
            if (selected.ScarecrowTarget.HasValue && (selected.Activity == CreatureActivity.Hunting || selected.Activity == CreatureActivity.Scaring))
                text.text += Loc.Text("creature.scare.practice");
            if (!string.IsNullOrEmpty(selected.StaffStatus)) text.text += Loc.Format("creature.info.staff", selected.StaffStatus);
            if (researchNotes != null) text.text += Loc.Format("creature.info.research_notes", researchNotes(definition));
            text.rectTransform.sizeDelta = new Vector2(0f, Mathf.Ceil(text.preferredHeight));
            bool controllable = !selected.ExternallyControlled && selected.CarriedBy == null && !selected.RetirementRequested;
            retireButton.interactable = controllable && retire != null;
            sedateButton.interactable = controllable && !selected.IsSedated && !selected.SedationRequested && sedate != null;
            moveButton.gameObject.SetActive(selected.IsSedated && !selected.ExternallyControlled);
            moveButton.interactable = controllable && !selected.MoveRequestedDestination.HasValue && move != null;
            Loc.Set(retireLabel, selected.RetirementRequested ? "creature.action.retire_queued" : "creature.action.retire");
            Loc.Set(sedateLabel, selected.IsSedated ? "creature.action.sedated" : selected.SedationRequested ? "creature.action.sedate_queued" : "creature.action.sedate");
            Loc.Set(moveLabel, selected.CarriedBy != null ? "creature.action.being_moved" : selected.MoveRequestedDestination.HasValue ? "creature.action.move_queued" : "creature.action.move");
            int columns=moveButton.gameObject.activeSelf?3:2;
            float buttonWidth=(body.rect.width-12f*(columns-1))/columns;
            int column=0;
            foreach(Button action in new[]{retireButton,sedateButton,moveButton})
            {
                if(!action.gameObject.activeSelf)continue;
                var actionRect=(RectTransform)action.transform;
                actionRect.anchorMin=actionRect.anchorMax=actionRect.pivot=new Vector2(0,1);
                actionRect.anchoredPosition=new Vector2(column++*(buttonWidth+12f),-46f);
                actionRect.sizeDelta=new Vector2(buttonWidth,38);
            }
            UIOverflowScroll.Attach(detailsScroll).Refresh();
        }
        public void Dispose() { if (panel != null) UnityEngine.Object.Destroy(panel); }
    }
}

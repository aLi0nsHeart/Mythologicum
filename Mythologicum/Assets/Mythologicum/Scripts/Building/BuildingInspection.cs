using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Per-building prices, operating accounts, personnel and creature-study assignments.</summary>
    public sealed class BuildingInspection : IDisposable
    {
        private readonly GameObject panel, priceGroup, staffGroup, researchGroup;
        private readonly Text title, accounts, priceCaption, message, employees, researchNotes;
        private readonly InputField price;
        private readonly Button hire, fire;
        private readonly Dropdown researchChoice;
        private readonly StaffWorld staff;
        private readonly SpeciesResearch research;
        private readonly CreatureDefinition[] species;
        private ParkBuildingRecord selected;
        private bool inspecting = true;

        public BuildingInspection(Canvas canvas, Font font, StaffWorld staff, SpeciesResearch research, CreatureDefinition[] species)
        {
            this.staff = staff; this.research = research; this.species = species ?? Array.Empty<CreatureDefinition>();
            panel = Box(canvas.transform, "Building information", new Vector2(1f,1f), new Vector2(-15f,-140f), new Vector2(430f,650f));
            title = Label(panel.transform, "Building name", font, 22, new Vector2(12f,10f), new Vector2(406f,48f));
            accounts = Label(panel.transform, "Operating accounts", font, 18, new Vector2(12f,63f), new Vector2(406f,168f));
            message = Label(panel.transform, "Action result", font, 16, new Vector2(12f,284f), new Vector2(406f,36f));

            priceGroup = Group(panel.transform, "Price controls", new Vector2(12f,235f), new Vector2(406f,45f));
            priceCaption = Label(priceGroup.transform, "Price type", font, 17, Vector2.zero, new Vector2(150f,38f));
            price = PriceInput(priceGroup.transform, font, new Vector2(155f,0f), new Vector2(120f,36f));
            MakeButton(priceGroup.transform, "Apply price", font, new Vector2(285f,0f), new Vector2(112f,36f), ApplyPrice);

            staffGroup = Group(panel.transform, "Personnel controls", new Vector2(12f,325f), new Vector2(406f,118f));
            hire = MakeButton(staffGroup.transform, "Hire", font, Vector2.zero, new Vector2(194f,36f), Hire);
            fire = MakeButton(staffGroup.transform, "Dismiss one", font, new Vector2(208f,0f), new Vector2(194f,36f), Fire);
            employees = Label(staffGroup.transform, "Personnel activity", font, 16, new Vector2(0f,43f), new Vector2(406f,100f));

            researchGroup = Group(panel.transform, "Creature research", new Vector2(12f,478f), new Vector2(406f,160f));
            researchChoice = ResearchDropdown(researchGroup.transform, font, this.species);
            researchChoice.onValueChanged.AddListener(index =>
            {
                if (selected != null && index >= 0 && index < this.species.Length)
                    staff.SetResearchTarget(selected, this.species[index]);
                Refresh();
            });
            researchNotes = ResearchNotes(researchGroup.transform, font);
            panel.SetActive(false);
        }

        public void Select(ParkBuildingRecord building)
        {
            selected = building; message.text = "";
            if (selected != null) price.text = selected.Price.ToString("0.##", CultureInfo.InvariantCulture);
            if (selected != null && selected.Footprint.Definition.kind == BuildingKind.ResearchCentre
                && staff.ResearchTarget(selected) == null && species.Length > 0)
                staff.SetResearchTarget(selected,species[0]);
            Refresh();
        }
        public void SetInspecting(bool value) { inspecting = value; Refresh(); }
        public void Refresh()
        {
            if (panel == null) return;
            bool show = inspecting && selected != null && staff.Buildings.Contains(selected);
            panel.SetActive(show);
            if (!show) return;
            BuildingDefinition definition = selected.Footprint.Definition;
            title.text = definition.displayName;
            accounts.text = (selected.IsOpen ? "Open" : "Closed: reconnect its entrance path") + "\n"
                + $"Base upkeep: {definition.operatingCostPerSecond * 60f:0.##} / minute\n"
                + $"Salaries: {selected.HiredCount * definition.staffSalaryPerSecond * 60f:0.##} / minute\n"
                + $"Total upkeep: {selected.UpkeepPerSecond * 60f:0.##} / minute\n"
                + $"Income: {selected.Revenue:0.##} | Expenses: {selected.Expenses:0.##}\n"
                + $"Operating profit: {selected.Profit:0.##}\n";
            if (definition.IsEmployeeBuilding)
                accounts.text += $"Personnel: {selected.HiredCount}/{ParkBuildingRecord.EmployeeCapacity} | Each: {definition.staffSalaryPerSecond * 60f:0.##} / minute";
            else if (definition.kind == BuildingKind.GoatDispenser)
                accounts.text += $"Feed stock: {selected.FeedStock}/{definition.feederCapacity}\nRestock delivery: {definition.restockCost:0.##}";
            else accounts.text += "Accounts cover this building's current session.";

            priceGroup.SetActive(definition.CanSetPrice);
            if (definition.CanSetPrice)
            {
                priceCaption.text = definition.kind == BuildingKind.ParkEntrance ? "Entry ticket"
                    : definition.kind == BuildingKind.Kiosk ? "Price per meal" : "Price per rest";
                if (!price.isFocused) price.text = selected.Price.ToString("0.##", CultureInfo.InvariantCulture);
            }
            staffGroup.SetActive(definition.IsEmployeeBuilding);
            if (definition.IsEmployeeBuilding)
            {
                hire.interactable = selected.IsOpen && selected.HiredCount < ParkBuildingRecord.EmployeeCapacity;
                hire.GetComponentInChildren<Text>().text = $"Hire ({definition.hireCost:0.##})";
                fire.interactable = selected.HiredCount > 0;
                employees.text = "";
                foreach (StaffAgent worker in staff.Staff)
                {
                    if (worker.Home != selected || worker.Actor == null || !worker.Actor.Alive) continue;
                    string status = worker.Status ?? "Ready";
                    if (status.Length > 54) status = status.Substring(0,51) + "...";
                    employees.text += status + "\n";
                }
                if (selected.HiredCount == 0) employees.text = "Hire personnel to activate this building's work.";
            }
            bool studies = definition.kind == BuildingKind.ResearchCentre;
            researchGroup.SetActive(studies);
            if (studies)
            {
                CreatureDefinition target = staff.ResearchTarget(selected);
                int index = Array.IndexOf(species, target);
                researchChoice.interactable = species.Length > 0;
                researchChoice.SetValueWithoutNotify(Math.Max(0,index));
                researchNotes.text = target == null ? "Choose a creature species to study."
                    : "Care notes (scroll to read)\n" + research.Description(target);
                researchNotes.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(110f,researchNotes.preferredHeight));
            }
        }

        private void ApplyPrice()
        {
            if (selected == null || !staff.Buildings.Contains(selected)) return;
            bool parsed = float.TryParse(price.text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
            message.text = parsed && selected.SetPrice(value) ? "Price updated for new customers." : "Enter a price of zero or higher.";
            Refresh();
        }
        private void Hire()
        {
            if (selected == null) return;
            message.text = staff.TryHire(selected) ? "Personnel hired. Salary is included in upkeep."
                : "Cannot hire: check funds, access and personnel capacity.";
            Refresh();
        }
        private void Fire()
        {
            if (selected == null) return;
            message.text = staff.Fire(selected) ? "One employee dismissed." : "No employee is available to dismiss.";
            Refresh();
        }

        private static GameObject Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.04f,.07f,.09f,.97f);
            return go;
        }
        private static GameObject Group(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            TopLeft((RectTransform)go.transform, position, size); return go;
        }
        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f,1f);
            rect.anchoredPosition = new Vector2(position.x,-position.y); rect.sizeDelta = size;
        }
        private static Text Label(Transform parent, string name, Font font, int fontSize, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false); Text text = go.GetComponent<Text>();
            TopLeft(text.rectTransform,position,size); text.font = font; text.fontSize = fontSize;
            text.color = new Color(.94f,.86f,.66f); text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false;
            return text;
        }
        private static Button MakeButton(Transform parent, string name, Font font, Vector2 position, Vector2 size, Action clicked)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));
            go.transform.SetParent(parent,false); TopLeft((RectTransform)go.transform,position,size);
            Image background = go.GetComponent<Image>(); background.color = new Color(.2f,.29f,.32f);
            Button button = go.GetComponent<Button>(); button.targetGraphic = background;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            Text label = Label(go.transform,name + " label",font,17,Vector2.zero,size);
            label.text = name; label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static InputField PriceInput(Transform parent, Font font, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Price",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(InputField));
            go.transform.SetParent(parent,false); TopLeft((RectTransform)go.transform,position,size);
            Image background = go.GetComponent<Image>(); background.color = new Color(.15f,.2f,.23f);
            Text input = Label(go.transform,"Entered price",font,18,new Vector2(8f,5f),size-new Vector2(16f,10f));
            input.supportRichText = false;
            InputField field = go.GetComponent<InputField>(); field.targetGraphic = background; field.textComponent = input;
            field.characterLimit = 12; field.lineType = InputField.LineType.SingleLine;
            return field;
        }
        private static Dropdown ResearchDropdown(Transform parent, Font font, CreatureDefinition[] options)
        {
            var go = new GameObject("Study species",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Dropdown));
            go.transform.SetParent(parent,false); TopLeft((RectTransform)go.transform,Vector2.zero,new Vector2(406f,36f));
            go.GetComponent<Image>().color = new Color(.2f,.29f,.32f);
            Dropdown dropdown = go.GetComponent<Dropdown>(); dropdown.targetGraphic = go.GetComponent<Image>();
            Text caption = Label(go.transform,"Selected species",font,17,new Vector2(10f,6f),new Vector2(380f,28f));
            dropdown.captionText = caption;
            var template = new GameObject("Choices",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(ScrollRect));
            template.transform.SetParent(go.transform,false); var templateRect=(RectTransform)template.transform;
            templateRect.anchorMin=new Vector2(0f,0f); templateRect.anchorMax=new Vector2(1f,0f); templateRect.pivot=new Vector2(.5f,1f);
            templateRect.anchoredPosition=new Vector2(0f,-2f); templateRect.sizeDelta=new Vector2(0f,160f);
            template.GetComponent<Image>().color=new Color(.08f,.12f,.14f);
            var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Mask));
            viewport.transform.SetParent(template.transform,false); var viewportRect=(RectTransform)viewport.transform;
            viewportRect.anchorMin=Vector2.zero; viewportRect.anchorMax=Vector2.one; viewportRect.offsetMin=viewportRect.offsetMax=Vector2.zero;
            viewport.GetComponent<Mask>().showMaskGraphic=false;
            var content=new GameObject("Content",typeof(RectTransform)); content.transform.SetParent(viewport.transform,false);
            var contentRect=(RectTransform)content.transform; contentRect.anchorMin=new Vector2(0f,1f); contentRect.anchorMax=new Vector2(1f,1f);
            contentRect.pivot=new Vector2(.5f,1f); contentRect.sizeDelta=new Vector2(0f,32f);
            var item=new GameObject("Item",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Toggle));
            item.transform.SetParent(content.transform,false); var itemRect=(RectTransform)item.transform;
            itemRect.anchorMin=new Vector2(0f,1f); itemRect.anchorMax=new Vector2(1f,1f); itemRect.pivot=new Vector2(.5f,1f);
            itemRect.sizeDelta=new Vector2(0f,32f); item.GetComponent<Image>().color=new Color(.16f,.23f,.26f);
            item.GetComponent<Toggle>().targetGraphic=item.GetComponent<Image>();
            dropdown.itemText=Label(item.transform,"Species label",font,17,new Vector2(10f,5f),new Vector2(380f,27f));
            ScrollRect scroll=template.GetComponent<ScrollRect>(); scroll.viewport=viewportRect; scroll.content=contentRect;
            scroll.horizontal=false; scroll.movementType=ScrollRect.MovementType.Clamped;
            dropdown.template=templateRect;
            foreach (CreatureDefinition profile in options) dropdown.options.Add(new Dropdown.OptionData(profile.displayName));
            dropdown.RefreshShownValue(); template.SetActive(false);
            return dropdown;
        }
        private static Text ResearchNotes(Transform parent, Font font)
        {
            var go = new GameObject("Care notes - scroll to read",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(ScrollRect));
            go.transform.SetParent(parent,false); TopLeft((RectTransform)go.transform,new Vector2(0f,43f),new Vector2(406f,119f));
            go.GetComponent<Image>().color = new Color(.06f,.10f,.12f);
            var viewport = new GameObject("Viewport",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Mask));
            viewport.transform.SetParent(go.transform,false);
            var viewRect=(RectTransform)viewport.transform;
            viewRect.anchorMin=Vector2.zero; viewRect.anchorMax=Vector2.one;
            viewRect.offsetMin=new Vector2(4f,4f); viewRect.offsetMax=new Vector2(-4f,-4f);
            viewport.GetComponent<Mask>().showMaskGraphic=false;
            Text text=Label(viewport.transform,"Unlocked notes",font,16,Vector2.zero,new Vector2(398f,110f));
            text.rectTransform.anchorMin=new Vector2(0f,1f); text.rectTransform.anchorMax=new Vector2(1f,1f);
            text.rectTransform.pivot=new Vector2(.5f,1f); text.rectTransform.anchoredPosition=Vector2.zero;
            text.rectTransform.sizeDelta=new Vector2(0f,110f);
            ScrollRect scroll=go.GetComponent<ScrollRect>(); scroll.viewport=viewRect; scroll.content=text.rectTransform;
            scroll.horizontal=false; scroll.vertical=true; scroll.scrollSensitivity=20f;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            return text;
        }
        public void Dispose() { if (panel != null) UnityEngine.Object.Destroy(panel); }
    }
}

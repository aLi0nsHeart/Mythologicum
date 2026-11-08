using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public enum ConstructionCategory { Buildings, Paths, Fences, Creatures, Enclosures, Terrain }

    /// <summary>A curved icon dock with compact construction flyouts.</summary>
    public sealed class ConstructionPalette
    {
        public GameObject Root { get; }
        public Text Status { get; }
        public bool IsOpen => buildingMenu.activeSelf;
        private readonly GameObject buildingMenu;
        private readonly Dictionary<ConstructionCategory, GameObject> groups = new Dictionary<ConstructionCategory, GameObject>();
        private readonly List<KeyValuePair<BuildingDefinition, Button>> buildingButtons = new List<KeyValuePair<BuildingDefinition, Button>>();
        private readonly Button pathsCategory, fencesCategory, pathChoice, fenceChoice, spikedFenceChoice, heavyFenceChoice, sanctifiedFenceChoice, gateChoice;
        private readonly Button creaturesCategory;
        private readonly Button enclosuresCategory, treeChoice, gravestoneChoice;
        private readonly List<KeyValuePair<CreatureDefinition,Button>> creatureButtons = new List<KeyValuePair<CreatureDefinition,Button>>();
        private ParkProgression progression;
        public void ConfigureProgression(ParkProgression value) { progression=value; Refresh(hasEntrance); }
        private readonly Action categoryOpened;
        private bool hasEntrance;
        private ConstructionCategory? expanded;

        public ConstructionPalette(Canvas canvas, Button template, BuildingDefinition[] definitions,
            Action<BuildingDefinition> selectBuilding, Action selectPath, Action<FenceKind> selectFence, Action selectGate, Action demolish, Action categoryOpened,
            CreatureDefinition[] creatures, Action<CreatureDefinition> selectCreature, Action selectTree, Action selectGravestone = null,Action<TerrainBrushMode> selectTerrain=null,Action openParkManagement=null,
            Action openEncyclopedia=null,Action openPause=null)
        {
            this.categoryOpened = categoryOpened;
            Root = new GameObject("Construction Palette", typeof(RectTransform));
            Root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)Root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            MakeDockButton(template, "Building", new Vector2(56, 150), UIIconKind.Building, ToggleBuilding);
            MakeDockButton(template, "Park Management", new Vector2(126, 146), UIIconKind.ParkManagement,
                () => { Close(); openParkManagement?.Invoke(); });
            MakeDockButton(template, "Encyclopedia", new Vector2(196, 126), UIIconKind.Encyclopedia,
                () => { Close(); openEncyclopedia?.Invoke(); });
            // Mail occupies the fourth point of the same arc, at (266, 90).
            MakeDockButton(template, "Pause", new Vector2(336, 44), UIIconKind.Pause,
                () => { Close(); openPause?.Invoke(); });
            buildingMenu = new GameObject("Building tools", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            buildingMenu.transform.SetParent(Root.transform, false);
            var menuRect = (RectTransform)buildingMenu.transform;
            menuRect.anchorMin = menuRect.anchorMax = menuRect.pivot = Vector2.zero;
            menuRect.anchoredPosition = new Vector2(24,210);
            buildingMenu.GetComponent<Image>().color = new Color(.05f,.08f,.1f,.95f);
            MakeButton(template, buildingMenu.transform, "Buildings (B)", new Vector2(-420f,0f), () => Toggle(ConstructionCategory.Buildings),true,134);
            enclosuresCategory = MakeButton(template, buildingMenu.transform, "Enclosures (H)", new Vector2(-280f,0f), () => Toggle(ConstructionCategory.Enclosures),true,134);
            MakeButton(template,buildingMenu.transform,"Terrain (T)",new Vector2(-140f,0f),()=>Toggle(ConstructionCategory.Terrain),true,134);
            pathsCategory = MakeButton(template, buildingMenu.transform, "Paths (P)", Vector2.zero, () => Toggle(ConstructionCategory.Paths),true,134);
            fencesCategory = MakeButton(template, buildingMenu.transform, "Fences (F)", new Vector2(140f,0f), () => Toggle(ConstructionCategory.Fences),true,134);
            creaturesCategory = MakeButton(template, buildingMenu.transform, "Creatures (C)", new Vector2(280f,0f), () => Toggle(ConstructionCategory.Creatures),true,134);
            MakeButton(template, buildingMenu.transform, "Demolish (X)", new Vector2(420f,0f), () => { Close(); demolish(); },true,134);
            buildingMenu.SetActive(false);
            foreach (ConstructionCategory category in Enum.GetValues(typeof(ConstructionCategory)))
            {
                var group = new GameObject(category + " Choices", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                group.transform.SetParent(Root.transform, false);
                var rect = (RectTransform)group.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(24, 342);
                group.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.95f);
                groups.Add(category, group);
                group.SetActive(false);
            }
            foreach(TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {
                var selected=mode;
                MakeButton(template,groups[ConstructionCategory.Terrain].transform,mode.ToString(),Vector2.zero,
                    ()=>{Close();selectTerrain?.Invoke(selected);},true);
            }
            treeChoice = MakeButton(template, groups[ConstructionCategory.Enclosures].transform, "Place trees",Vector2.zero,
                () => { Close(); selectTree(); }, true);
            gravestoneChoice = MakeButton(template, groups[ConstructionCategory.Enclosures].transform, "Place gravestones",Vector2.zero,
                () => { Close(); selectGravestone?.Invoke(); }, true);
            for (int i = 0; i < definitions.Length; i++)
            {
                BuildingDefinition definition = definitions[i];
                bool enrichment = definition.kind == BuildingKind.GoatDispenser;
                Button button = MakeButton(template, groups[enrichment ? ConstructionCategory.Enclosures : ConstructionCategory.Buildings].transform,
                    $"{definition.displayName}\n{definition.width} x {definition.depth} tiles | Cost {definition.constructionCost:0}",
                    Vector2.zero, () => { Close(); selectBuilding(definition); }, true);
                buildingButtons.Add(new KeyValuePair<BuildingDefinition, Button>(definition, button));
            }
            pathChoice = MakeButton(template, groups[ConstructionCategory.Paths].transform, "Basic path", Vector2.zero,
                () => { Close(); selectPath(); }, true);
            fenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Wooden fence", Vector2.zero,
                () => { Close(); selectFence(FenceKind.Wooden); }, true);
            spikedFenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Spiked fence", Vector2.zero,
                () => { Close(); selectFence(FenceKind.Spiked); }, true);
            heavyFenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Reinforced fence", Vector2.zero,
                () => { Close(); selectFence(FenceKind.Heavy); }, true);
            sanctifiedFenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Sanctified fence", Vector2.zero,
                () => { Close(); selectFence(FenceKind.Sanctified); }, true);
            gateChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Staff gate", Vector2.zero,
                () => { Close(); selectGate(); }, true);
            var placeable = new List<CreatureDefinition>();
            foreach (CreatureDefinition creature in creatures) if (creature.manuallyPlaceable) placeable.Add(creature);
            for (int i = 0; i < placeable.Count; i++)
            {
                CreatureDefinition creature = placeable[i];
                creatureButtons.Add(new KeyValuePair<CreatureDefinition,Button>(creature,MakeButton(template, groups[ConstructionCategory.Creatures].transform, creature.displayName,
                    Vector2.zero, () => { Close(); selectCreature(creature); }, true)));
            }
            var statusObject = new GameObject("Construction status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            statusObject.transform.SetParent(Root.transform, false);
            Status = statusObject.GetComponent<Text>();
            Status.font = template.GetComponentInChildren<Text>().font;
            Status.fontSize = 21;
            Status.color = new Color(0.94f, 0.86f, 0.66f);
            Status.alignment = TextAnchor.MiddleCenter;
            Status.raycastTarget = false;
            Status.rectTransform.anchorMin = Status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            Status.rectTransform.anchoredPosition = new Vector2(0f, 320f);
            Status.rectTransform.sizeDelta = new Vector2(1100f, 65f);
            UILayoutElement.Attach(rootRect).RefreshLayout = LayoutFlyouts;
            Refresh(false);
        }

        private void MakeDockButton(Button template, string title, Vector2 position, UIIconKind icon, Action clicked)
        {
            var button = MakeButton(template, Root.transform, title, position, clicked);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position; rect.sizeDelta = new Vector2(64, 64);
            UIIcon.AttachStandalone(button, icon, title);
            UILayoutBinding binding = icon == UIIconKind.Building ? UILayoutBinding.BuildingIcon
                : icon == UIIconKind.ParkManagement ? UILayoutBinding.ManagementIcon
                : icon == UIIconKind.Encyclopedia ? UILayoutBinding.EncyclopediaIcon : UILayoutBinding.PauseIcon;
            UILayoutElement.Bind(rect, binding);
        }

        private void LayoutFlyouts()
        {
            var canvasSize = ((RectTransform)Root.transform.parent).rect.size;
            var layout = UILayoutSettings.Current;
            LayoutGrid(buildingMenu, layout.categoryColumns, layout.categoryCellSize);
            ((RectTransform)buildingMenu.transform).anchoredPosition = layout.categoryPosition;
            bool besideCategories = canvasSize.y < layout.compactThreshold;
            foreach (var group in groups.Values)
            {
                int columns = besideCategories ? layout.compactColumns : layout.choiceColumns;
                LayoutGrid(group, columns, layout.choiceCellSize);
                var rect = (RectTransform)group.transform;
                rect.anchoredPosition = besideCategories ? layout.compactPosition : layout.choicePosition;
                float availableWidth = Mathf.Max(100, canvasSize.x - rect.anchoredPosition.x - 20);
                float availableHeight = Mathf.Max(80, canvasSize.y - rect.anchoredPosition.y - 100);
                rect.localScale = Vector3.one * Mathf.Min(1f, availableWidth / rect.rect.width, availableHeight / rect.rect.height);
            }
        }

        private static void LayoutGrid(GameObject group, int maximumColumns, Vector2 cellSize)
        {
            float padding = UILayoutSettings.Current.flyoutPadding, gap = UILayoutSettings.Current.flyoutGap;
            var buttons = new List<Button>();
            foreach (Transform child in group.transform)
                if (child.TryGetComponent<Button>(out var button)) buttons.Add(button);
            int columns = Mathf.Clamp(buttons.Count, 1, Mathf.Max(1,maximumColumns));
            int rows = Mathf.Max(1, Mathf.CeilToInt(buttons.Count / (float)columns));
            ((RectTransform)group.transform).sizeDelta = new Vector2(
                padding * 2 + columns * cellSize.x + (columns - 1) * gap,
                padding * 2 + rows * cellSize.y + (rows - 1) * gap);
            for (int i = 0; i < buttons.Count; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(padding + i % columns * (cellSize.x + gap),
                    -padding - i / columns * (cellSize.y + gap));
                rect.sizeDelta = cellSize;
            }
        }

        private static Button MakeButton(Button template, Transform parent, string title, Vector2 position,
            Action clicked, bool submenu = false, float width = 0f)
        {
            Button button = UnityEngine.Object.Instantiate(template, parent);
            button.name = title;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => clicked());
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, submenu ? 0.5f : 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width > 0f ? width : submenu ? 290f : 165f, 52f);
            Text label = button.GetComponentInChildren<Text>();
            label.text = title;
            label.fontSize = submenu ? 18 : 19;
            label.rectTransform.sizeDelta = rect.sizeDelta;
            return button;
        }

        public void Toggle(ConstructionCategory category)
        {
            if (!hasEntrance && category != ConstructionCategory.Buildings && category != ConstructionCategory.Terrain) return;
            bool wasOpen = expanded == category;
            categoryOpened?.Invoke();
            CloseChoices();
            buildingMenu.SetActive(true);
            if (!wasOpen) { expanded = category; groups[category].SetActive(true); }
        }

        public void ToggleBuilding()
        {
            bool wasOpen = IsOpen;
            categoryOpened?.Invoke();
            Close();
            if (!wasOpen) buildingMenu.SetActive(true);
        }

        public void Close()
        {
            CloseChoices();
            buildingMenu.SetActive(false);
        }

        private void CloseChoices()
        {
            expanded = null;
            foreach (GameObject group in groups.Values) if (group != null) group.SetActive(false);
        }

        public void Refresh(bool entranceExists)
        {
            LayoutFlyouts();
            hasEntrance = entranceExists;
            pathsCategory.interactable = fencesCategory.interactable = pathChoice.interactable = fenceChoice.interactable
                = spikedFenceChoice.interactable = heavyFenceChoice.interactable = sanctifiedFenceChoice.interactable = gateChoice.interactable = hasEntrance;
            creaturesCategory.interactable = hasEntrance;
            enclosuresCategory.interactable = treeChoice.interactable = gravestoneChoice.interactable = hasEntrance;
            spikedFenceChoice.interactable &= progression?.FenceUnlocked(FenceKind.Spiked) ?? true;
            heavyFenceChoice.interactable &= progression?.FenceUnlocked(FenceKind.Heavy) ?? true;
            sanctifiedFenceChoice.interactable &= progression?.FenceUnlocked(FenceKind.Sanctified) ?? true;
            SetFenceLabel(spikedFenceChoice,"Spiked fence",FenceKind.Spiked);
            SetFenceLabel(heavyFenceChoice,"Reinforced fence",FenceKind.Heavy);
            SetFenceLabel(sanctifiedFenceChoice,"Sanctified fence",FenceKind.Sanctified);
            foreach (var pair in creatureButtons)
            {
                pair.Value.interactable = hasEntrance && (progression==null || progression.Stock(pair.Key.name)>0);
                if(progression!=null) pair.Value.GetComponentInChildren<Text>().text=pair.Key.displayName
                    +(pair.Key.origin=="Domestic"?"\nUse the goat dispenser":$" x{progression.Stock(pair.Key.name)} | Rating {pair.Key.rating}/5");
            }
            foreach (var pair in buildingButtons)
            {
                bool unlocked=progression?.BuildingUnlocked(pair.Key.kind)??true;
                pair.Value.interactable = unlocked && (pair.Key.kind == BuildingKind.ParkEntrance ? !hasEntrance : hasEntrance);
                pair.Value.GetComponentInChildren<Text>().text=unlocked?$"{pair.Key.displayName}\n{pair.Key.width} x {pair.Key.depth} tiles | Cost {pair.Key.constructionCost:0}"
                    :pair.Key.displayName+" [locked]\nResearch: "+progression.BuildingResearch(pair.Key.kind);
            }
        }
        private void SetFenceLabel(Button button,string title,FenceKind kind)
        {button.GetComponentInChildren<Text>().text=title+((progression?.FenceUnlocked(kind)??true)?"":"\nResearch required");}
    }
}

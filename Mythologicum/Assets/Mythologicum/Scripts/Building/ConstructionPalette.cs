using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    public enum ConstructionCategory { Buildings, Paths, Fences, Creatures, Enclosures }

    /// <summary>Bottom categories with expandable lists; future variants belong in these lists.</summary>
    public sealed class ConstructionPalette
    {
        public GameObject Root { get; }
        public Text Status { get; }
        private readonly Dictionary<ConstructionCategory, GameObject> groups = new Dictionary<ConstructionCategory, GameObject>();
        private readonly List<KeyValuePair<BuildingDefinition, Button>> buildingButtons = new List<KeyValuePair<BuildingDefinition, Button>>();
        private readonly Button pathsCategory, fencesCategory, pathChoice, fenceChoice, spikedFenceChoice, gateChoice;
        private readonly Button creaturesCategory;
        private readonly Button enclosuresCategory, treeChoice;
        private readonly List<Button> creatureButtons = new List<Button>();
        private readonly Action categoryOpened;
        private bool hasEntrance;
        private ConstructionCategory? expanded;

        public ConstructionPalette(Canvas canvas, Button template, BuildingDefinition[] definitions,
            Action<BuildingDefinition> selectBuilding, Action selectPath, Action<FenceKind> selectFence, Action selectGate, Action demolish, Action categoryOpened,
            CreatureDefinition[] creatures, Action<CreatureDefinition> selectCreature, Action selectTree)
        {
            this.categoryOpened = categoryOpened;
            Root = new GameObject("Construction Palette", typeof(RectTransform));
            Root.transform.SetParent(canvas.transform, false);
            var rootRect = (RectTransform)Root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            MakeButton(template, Root.transform, "Buildings (B)", new Vector2(-437.5f, 40f), () => Toggle(ConstructionCategory.Buildings));
            enclosuresCategory = MakeButton(template, Root.transform, "Enclosures (H)", new Vector2(-262.5f, 40f), () => Toggle(ConstructionCategory.Enclosures));
            pathsCategory = MakeButton(template, Root.transform, "Paths (P)", new Vector2(-87.5f, 40f), () => Toggle(ConstructionCategory.Paths));
            fencesCategory = MakeButton(template, Root.transform, "Fences (F)", new Vector2(87.5f, 40f), () => Toggle(ConstructionCategory.Fences));
            creaturesCategory = MakeButton(template, Root.transform, "Creatures (C)", new Vector2(262.5f, 40f), () => Toggle(ConstructionCategory.Creatures));
            MakeButton(template, Root.transform, "Demolish (X)", new Vector2(437.5f, 40f), demolish);
            foreach (ConstructionCategory category in Enum.GetValues(typeof(ConstructionCategory)))
            {
                var group = new GameObject(category + " Choices", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                group.transform.SetParent(Root.transform, false);
                var rect = (RectTransform)group.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 112f);
                rect.sizeDelta = new Vector2(1000f, 72f);
                group.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.95f);
                groups.Add(category, group);
                group.SetActive(false);
            }
            int ordinaryCount = 0, enrichmentCount = 1;
            foreach (BuildingDefinition definition in definitions)
                if (definition.kind == BuildingKind.GoatDispenser) enrichmentCount++; else ordinaryCount++;
            int ordinaryIndex = 0, enrichmentIndex = 1;
            treeChoice = MakeButton(template, groups[ConstructionCategory.Enclosures].transform, "Place trees",
                new Vector2(-(enrichmentCount - 1) / 2f * Mathf.Min(310f, 960f / enrichmentCount), 0f),
                () => { Close(); selectTree(); }, true);
            for (int i = 0; i < definitions.Length; i++)
            {
                BuildingDefinition definition = definitions[i];
                bool enrichment = definition.kind == BuildingKind.GoatDispenser;
                int count = enrichment ? enrichmentCount : ordinaryCount;
                int index = enrichment ? enrichmentIndex++ : ordinaryIndex++;
                Button button = MakeButton(template, groups[enrichment ? ConstructionCategory.Enclosures : ConstructionCategory.Buildings].transform,
                    $"{definition.displayName}\n{definition.width} x {definition.depth} tiles | Cost {definition.constructionCost:0}",
                    new Vector2((index - (count - 1) / 2f) * Mathf.Min(310f, 960f / count), 0f),
                    () => { Close(); selectBuilding(definition); }, true, Mathf.Min(290f, 960f / count - 10f));
                buildingButtons.Add(new KeyValuePair<BuildingDefinition, Button>(definition, button));
            }
            pathChoice = MakeButton(template, groups[ConstructionCategory.Paths].transform, "Basic path", Vector2.zero,
                () => { Close(); selectPath(); }, true);
            fenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Wooden fence", new Vector2(-320f, 0f),
                () => { Close(); selectFence(FenceKind.Wooden); }, true);
            spikedFenceChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Spiked fence", Vector2.zero,
                () => { Close(); selectFence(FenceKind.Spiked); }, true);
            gateChoice = MakeButton(template, groups[ConstructionCategory.Fences].transform, "Staff gate", new Vector2(320f, 0f),
                () => { Close(); selectGate(); }, true);
            var placeable = new List<CreatureDefinition>();
            foreach (CreatureDefinition creature in creatures) if (creature.manuallyPlaceable) placeable.Add(creature);
            for (int i = 0; i < placeable.Count; i++)
            {
                CreatureDefinition creature = placeable[i];
                creatureButtons.Add(MakeButton(template, groups[ConstructionCategory.Creatures].transform, creature.displayName,
                    new Vector2((i - (placeable.Count - 1) / 2f) * Mathf.Min(310f, 960f / placeable.Count), 0f),
                    () => { Close(); selectCreature(creature); }, true, Mathf.Min(290f, 960f / placeable.Count - 10f)));
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
            Status.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            Status.rectTransform.sizeDelta = new Vector2(1100f, 65f);
            Refresh(false);
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
            if (!hasEntrance && category != ConstructionCategory.Buildings) return;
            bool wasOpen = expanded == category;
            categoryOpened();
            Close();
            if (!wasOpen) { expanded = category; groups[category].SetActive(true); }
        }

        public void Close()
        {
            expanded = null;
            foreach (GameObject group in groups.Values) if (group != null) group.SetActive(false);
        }

        public void Refresh(bool entranceExists)
        {
            hasEntrance = entranceExists;
            pathsCategory.interactable = fencesCategory.interactable = pathChoice.interactable = fenceChoice.interactable
                = spikedFenceChoice.interactable = gateChoice.interactable = hasEntrance;
            creaturesCategory.interactable = hasEntrance;
            enclosuresCategory.interactable = treeChoice.interactable = hasEntrance;
            foreach (Button button in creatureButtons) button.interactable = hasEntrance;
            foreach (var pair in buildingButtons)
                pair.Value.interactable = pair.Key.kind == BuildingKind.ParkEntrance ? !hasEntrance : hasEntrance;
        }
    }
}

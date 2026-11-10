using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>The park handbook and the same species knowledge earned by researchers.</summary>
    public sealed class EncyclopediaPresentation : IDisposable
    {
        private sealed class Entry
        {
            internal readonly string Title, Summary, Article;
            internal readonly CreatureDefinition Species;
            internal Entry(string title, string summary, string article)
            { Title = title; Summary = summary; Article = article; }
            internal Entry(CreatureDefinition species)
            { Title = species.displayName; Species = species; }
        }

        private readonly Canvas canvas;
        private readonly GameObject root;
        private readonly SpeciesResearch research;
        private readonly List<Entry>[] sections = { new List<Entry>(), new List<Entry>(), new List<Entry>() };
        private readonly Button[] tabs = new Button[3];
        private readonly List<KeyValuePair<Entry, Button>> entryButtons = new List<KeyValuePair<Entry, Button>>();
        private readonly RectTransform entries, articleContent;
        private readonly ScrollRect entriesScroll, articleScroll;
        private readonly Text listTitle, articleTitle, articleSummary, article;
        private int lastKnowledge = -1, lastLocalizationRevision = -1;
        private Entry selected;
        public bool IsOpen => root != null && root.activeSelf;

        public EncyclopediaPresentation(Canvas canvas, CreatureDefinition[] species, SpeciesResearch research)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.research = research ?? throw new ArgumentNullException(nameof(research));
            AddGuides();
            var included = new HashSet<CreatureDefinition>();
            if (species != null)
                foreach (var creature in species)
                    if (creature != null && included.Add(creature)) sections[1].Add(new Entry(creature));

            root = ManagementWidgets.Modal(canvas, "Encyclopedia window");
            UIThemeElement.Mark(root, UIThemeRole.Overlay);
            var card = ManagementWidgets.Card(root, canvas);
            ManagementWidgets.Label(card, "ui.encyclopedia.encyclopedia", new Vector2(24, 14), new Vector2(960, 44), 28);
            var closeButton = ManagementWidgets.Button(card, "ui.encyclopedia.close_encyclopedia", new Vector2(1032, 12), new Vector2(104, 38), Close, 17);
            Loc.Set(closeButton.GetComponentInChildren<Text>(), "ui.common.close");
            string[] names = { "ui.encyclopedia.section.park", "ui.encyclopedia.section.creatures", "ui.encyclopedia.section.management" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                tabs[i] = ManagementWidgets.Button(card, names[i], new Vector2(24 + i * 372, 8),
                    new Vector2(366, 44), () => SelectSection(index), 20);
            }
            listTitle = ManagementWidgets.Label(card, "ui.encyclopedia.encyclopedia_list_heading", new Vector2(24, 64), new Vector2(286, 30), 20);
            entries = ManagementWidgets.Scroll(card, "Encyclopedia entries", new Vector2(24, 104), new Vector2(286, 382), 0);
            entriesScroll = entries.GetComponentInParent<ScrollRect>(true);
            articleTitle = ManagementWidgets.Label(card, "ui.encyclopedia.encyclopedia_article_title", new Vector2(334, 64), new Vector2(798, 32), 24);
            articleSummary = ManagementWidgets.Label(card, "ui.encyclopedia.encyclopedia_article_summary", new Vector2(334, 104), new Vector2(798, 48), 17);
            articleContent = ManagementWidgets.Scroll(card, "Encyclopedia article scroll", new Vector2(334, 160), new Vector2(798, 326), 0);
            articleScroll = articleContent.GetComponentInParent<ScrollRect>(true);
            article = ManagementWidgets.Label(articleContent, "ui.encyclopedia.encyclopedia_article", new Vector2(14, 10), new Vector2(752, 0), 19);
            article.horizontalOverflow = HorizontalWrapMode.Wrap;
            article.verticalOverflow = VerticalWrapMode.Overflow;
            SelectSection(0);
            Loc.Changed += Refresh;
        }

        public void Open()
        {
            root.SetActive(true); root.transform.SetAsLastSibling();
            RefreshArticle(false); UIThemeApplicator.ApplyAll(canvas);
        }
        public void Close() => root.SetActive(false);
        public void Dispose() { Loc.Changed -= Refresh; if (root != null) UnityEngine.Object.Destroy(root); }

        public void Refresh()
        {
            if (!IsOpen) return;
            if (lastLocalizationRevision != Loc.Revision
                || Mathf.Abs(article.rectTransform.rect.height - article.preferredHeight) > .5f) RefreshArticle(false);
            if (selected?.Species == null) return;
            int knowledge = Mathf.FloorToInt(research.Progress(selected.Species));
            if (knowledge != lastKnowledge) RefreshArticle(false);
        }

        private void SelectSection(int index)
        {
            string[] headings = { "ui.encyclopedia.heading.park", "ui.encyclopedia.heading.species", "ui.encyclopedia.heading.management" };
            Loc.Set(listTitle, headings[index]);
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].GetComponent<Image>().color = i == index ? new Color(.42f, .34f, .16f) : new Color(.18f, .28f, .31f);
            ManagementWidgets.Clear(entries); entryButtons.Clear();
            float y = 8;
            foreach (var entry in sections[index])
            {
                var item = entry;
                var button = ManagementWidgets.Button(entries, item.Title, new Vector2(8, y), new Vector2(252, 58), () => SelectEntry(item), 18);
                entryButtons.Add(new KeyValuePair<Entry, Button>(item, button)); y += 66;
            }
            entries.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0,y-8));
            UIOverflowScroll.Attach(entriesScroll).Refresh();
            entriesScroll.verticalNormalizedPosition = 1;
            SelectEntry(sections[index].Count > 0 ? sections[index][0] : null);
        }

        private void SelectEntry(Entry entry)
        {
            selected = entry; lastKnowledge = -1;
            foreach (var item in entryButtons)
                item.Value.GetComponent<Image>().color = item.Key == entry ? new Color(.42f, .34f, .16f) : new Color(.18f, .28f, .31f);
            RefreshArticle(true);
            if (IsOpen) UIThemeApplicator.ApplyAll(canvas);
        }

        private void RefreshArticle(bool resetScroll)
        {
            lastLocalizationRevision = Loc.Revision;
            Vector2 oldPosition = articleContent.anchoredPosition;
            if (selected == null)
            {
                Loc.Set(articleTitle, "ui.encyclopedia.no_entries");
                Loc.Set(articleSummary, "ui.encyclopedia.new_entries_hint");
                Loc.Set(article, "ui.encyclopedia.select_section_hint");
                lastKnowledge = -1;
            }
            else if (selected.Species != null)
            {
                CreatureDefinition species = selected.Species;
                lastKnowledge = Mathf.FloorToInt(research.Progress(species));
                articleTitle.text = species.DisplayName;
                Loc.Set(articleSummary, "ui.encyclopedia.species_summary", species.Origin, species.rating, lastKnowledge, research.UnlockedStages(species), SpeciesResearch.StageCount);
                article.text = research.Description(species);
            }
            else
            {
                Loc.Set(articleTitle, selected.Title); Loc.Set(articleSummary, selected.Summary);
                Loc.Set(article, selected.Article, research.Rules.foodDiscovery, research.Rules.habitatDiscovery, research.Rules.socialDiscovery, research.Rules.containmentDiscovery);
                lastKnowledge = -1;
            }
            float height = article.preferredHeight;
            article.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            articleContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 20);
            if (resetScroll) articleScroll.verticalNormalizedPosition = 1;
            else articleContent.anchoredPosition = oldPosition;
            UIOverflowScroll.Attach(articleScroll).Refresh();
        }

        private void AddGuides()
        {
            sections[0].Add(new Entry("ui.encyclopedia.guide.getting_started.title", "ui.encyclopedia.guide.getting_started.summary", "ui.encyclopedia.guide.getting_started.article"));
            sections[0].Add(new Entry("ui.encyclopedia.guide.paths_building_access.title", "ui.encyclopedia.guide.paths_building_access.summary", "ui.encyclopedia.guide.paths_building_access.article"));
            sections[0].Add(new Entry("ui.encyclopedia.guide.terrain_enclosures.title", "ui.encyclopedia.guide.terrain_enclosures.summary", "ui.encyclopedia.guide.terrain_enclosures.article"));
            sections[0].Add(new Entry("ui.encyclopedia.guide.expeditions.title", "ui.encyclopedia.guide.expeditions.summary", "ui.encyclopedia.guide.expeditions.article"));
            sections[0].Add(new Entry("ui.encyclopedia.guide.research.title", "ui.encyclopedia.guide.research.summary", "ui.encyclopedia.guide.research.article"));

            sections[2].Add(new Entry("ui.encyclopedia.guide.economy_prices.title", "ui.encyclopedia.guide.economy_prices.summary", "ui.encyclopedia.guide.economy_prices.article"));
            sections[2].Add(new Entry("ui.encyclopedia.guide.staff.title", "ui.encyclopedia.guide.staff.summary", "ui.encyclopedia.guide.staff.article"));
            sections[2].Add(new Entry("ui.encyclopedia.guide.creature_care.title", "ui.encyclopedia.guide.creature_care.summary", "ui.encyclopedia.guide.creature_care.article"));
            sections[2].Add(new Entry("ui.encyclopedia.guide.visitor_welfare.title", "ui.encyclopedia.guide.visitor_welfare.summary", "ui.encyclopedia.guide.visitor_welfare.article"));
        }
    }
}

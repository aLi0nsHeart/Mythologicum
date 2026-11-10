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
        private int lastKnowledge = -1;
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
            ManagementWidgets.Label(card, "Encyclopedia", new Vector2(24, 14), new Vector2(960, 44), 28);
            ManagementWidgets.Button(card, "Close encyclopedia", new Vector2(1032, 12), new Vector2(104, 38), Close, 17)
                .GetComponentInChildren<Text>().text = "Close";
            string[] names = { "Park", "Creatures", "Management" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                tabs[i] = ManagementWidgets.Button(card, names[i], new Vector2(24 + i * 372, 8),
                    new Vector2(366, 44), () => SelectSection(index), 20);
            }
            listTitle = ManagementWidgets.Label(card, "Encyclopedia list heading", new Vector2(24, 64), new Vector2(286, 30), 20);
            entries = ManagementWidgets.Scroll(card, "Encyclopedia entries", new Vector2(24, 104), new Vector2(286, 382), 0);
            entriesScroll = entries.GetComponentInParent<ScrollRect>(true);
            articleTitle = ManagementWidgets.Label(card, "Encyclopedia article title", new Vector2(334, 64), new Vector2(798, 32), 24);
            articleSummary = ManagementWidgets.Label(card, "Encyclopedia article summary", new Vector2(334, 104), new Vector2(798, 48), 17);
            articleContent = ManagementWidgets.Scroll(card, "Encyclopedia article scroll", new Vector2(334, 160), new Vector2(798, 326), 0);
            articleScroll = articleContent.GetComponentInParent<ScrollRect>(true);
            article = ManagementWidgets.Label(articleContent, "Encyclopedia article", new Vector2(14, 10), new Vector2(752, 0), 19);
            article.horizontalOverflow = HorizontalWrapMode.Wrap;
            article.verticalOverflow = VerticalWrapMode.Overflow;
            SelectSection(0);
        }

        public void Open()
        {
            root.SetActive(true); root.transform.SetAsLastSibling();
            RefreshArticle(false); UIThemeApplicator.ApplyAll(canvas);
        }
        public void Close() => root.SetActive(false);
        public void Dispose() { if (root != null) UnityEngine.Object.Destroy(root); }

        public void Refresh()
        {
            if (!IsOpen || selected?.Species == null) return;
            int knowledge = Mathf.FloorToInt(research.Progress(selected.Species));
            if (knowledge != lastKnowledge) RefreshArticle(false);
        }

        private void SelectSection(int index)
        {
            string[] headings = { "Park guide", "Species register", "Management guide" };
            listTitle.text = headings[index];
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
            Vector2 oldPosition = articleContent.anchoredPosition;
            if (selected == null)
            {
                articleTitle.text = "No entries yet";
                articleSummary.text = "New entries will appear as the creature catalogue grows.";
                article.text = "Select another section to browse the park handbook.";
                lastKnowledge = -1;
            }
            else if (selected.Species != null)
            {
                CreatureDefinition species = selected.Species;
                lastKnowledge = Mathf.FloorToInt(research.Progress(species));
                articleTitle.text = species.displayName;
                articleSummary.text = $"Origin: {species.origin}  |  Exhibition rating: {species.rating}/5\nResearch: {lastKnowledge}%  |  Discoveries: {research.UnlockedStages(species)}/{SpeciesResearch.StageCount}";
                article.text = research.Description(species);
            }
            else
            {
                articleTitle.text = selected.Title; articleSummary.text = selected.Summary; article.text = selected.Article;
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
            sections[0].Add(new Entry("Getting started", "Plan the exhibition around its entrance, visitors and creatures.",
                "Place the park entrance first. Its position is your choice. Open Building to find buildings, enclosure features, terrain tools, paths, fences, creatures and demolition.\n\n"
                + "Connect the entrance to the park with paths. Visitor services and staff buildings need an unbroken path route back to that entrance before they can operate.\n\n"
                + "Start with useful visitor services and a manageable exhibition. Use Park Management to follow finances, prices, visitor satisfaction and the park's star rating.\n\n"
                + "Open the expedition centre to acquire creatures. Successful missions add specimens to the Creatures placement list. Creature care information is recorded here as your researchers discover it.\n\n"
                + "The letter button holds notifications about events such as opened fences, completed expeditions and finished research. Pause contains saving, loading, settings and exit options."));
            sections[0].Add(new Entry("Paths & building access", "A working park depends on connected entrances and reachable services.",
                "Buildings occupy several grid tiles and have a dedicated entrance. Rotate a building while placing it so its door faces a path.\n\n"
                + "A building's entrance path tile must connect continuously to the main park entrance. A disconnected visitor or staff building cannot provide its usual service. Select the building to inspect its status.\n\n"
                + "Paths and fences use a start point and an end point. The construction preview fills the route between those points. Terrain and obstacles can affect the route.\n\n"
                + "Goat dispensers are an exception to the entrance path requirement. They can operate inside an enclosure, but zookeepers must still be able to reach them to deliver supplies.\n\n"
                + "Staff gates let employees reach enclosure jobs. Keep routes to creatures, damaged fences and feeders accessible. A completely destroyed fence needs replacement rather than repair."));
            sections[0].Add(new Entry("Terrain & enclosures", "Fences are physical barriers; creatures respond to their surroundings.",
                "Terrain tools reshape the map. Placed buildings flatten the ground under their footprint. Paths and fences follow the terrain, while slopes can limit movement and construction.\n\n"
                + "Fences divide space into enclosures and block movement. An enclosure is useful for inspecting an area, but it does not bind a creature to that area. A creature placed outside a fence still makes its own decisions.\n\n"
                + "Use enclosure features such as trees, gravestones and goat dispensers to create suitable surroundings. Habitat features must be physically reachable when a creature needs to use them.\n\n"
                + "Different species can challenge different barriers. Research the species to discover its containment requirements, then unlock suitable fence types through technology research.\n\n"
                + "Demolish can remove placed objects, including paths, fences and buildings. Removing a fence can open a route for both creatures and staff; check containment afterwards."));
            sections[0].Add(new Entry("Expeditions", "Acquire specimens through the expedition centre.",
                "Select an operating expedition centre to open the world map. Purchase expedition teams and permits for the regions you want to explore.\n\n"
                + "Choose a region and mission difficulty, then assign teams. More difficult missions offer higher-rated discoveries but have lower success chances. Sending multiple teams on the same mission improves the chance of success.\n\n"
                + "The mission screen shows its cost, duration, success chance and possible rewards before departure. Assigned teams are unavailable for other missions until they return.\n\n"
                + "A successful mission adds its discovered creatures to your placement stock. Select Building, then Creatures, to place an available specimen. Placing it consumes one specimen from that stock.\n\n"
                + "The inbox records expedition success or failure. Allow room in the budget for teams, permits and future missions as well as the park's regular expenses."));
            sections[0].Add(new Entry("Research", "Creature knowledge and technology are separate kinds of progress.",
                "Hire researchers at an operating research centre. Select a creature species there so researchers can observe a living, reachable specimen.\n\n"
                + $"Creature research reveals care notes in four stages. The Creatures section of this encyclopedia uses the same park-wide knowledge as the research centre. Notes expand at {research.Rules.foodDiscovery:0.#}%, {research.Rules.habitatDiscovery:0.#}%, {research.Rules.socialDiscovery:0.#}% and {research.Rules.containmentDiscovery:0.#}%.\n\n"
                + "The discoveries cover food, habitat and sleep, space and social needs, and containment. Before a discovery is made, its species-specific details remain undisclosed here.\n\n"
                + "Technology research unlocks additional fence types, visitor services, staff improvements and expedition improvements. Open the research centre's technology screen to choose an available project.\n\n"
                + "Technology projects and creature observation use your research staff. Watch their workplace connection and assigned work when progress is slow."));

            sections[2].Add(new Entry("Economy & prices", "Use Park Management to balance regular income against running costs.",
                "Admission, kiosk meals and priced rest services bring in money. Each placed revenue building has its own price, which you can edit by selecting the building or using Park Management.\n\n"
                + "Buildings have upkeep. Hired staff add salaries, and goat feeder deliveries consume supplies. Construction, hiring, expeditions and research also require money.\n\n"
                + "The Finances page separates operating profit from total cash flow. Its per-minute figures cover the most recent 60 simulation seconds. A large new investment can reduce cash flow even while ordinary operations remain profitable.\n\n"
                + "Higher park reputation supports higher prices. Visitors can refuse overpriced admission or services, so an increased price does not guarantee increased income.\n\n"
                + "Satisfied visitors and accessible creature exhibitions improve the park's star rating. Dangerous or rare exhibits have appeal, but visitor deaths and poor departing reviews damage reputation.\n\n"
                + "Infinite funds can be switched on in Park Management when you want to build freely in the sandbox."));
            sections[2].Add(new Entry("Staff", "Hire employees at their workplace and keep jobs physically accessible.",
                "Research centres, mercenary headquarters and zookeeper stations have individual hiring limits shown in their information panels. Hiring has a cost, and every employee adds a salary to that building's upkeep.\n\n"
                + "Researchers observe selected creature species or work on technology projects. Their home research centre must be operating, and creature observation requires a suitable specimen they can reach.\n\n"
                + "Mercenaries patrol visitor paths and respond to escaped creatures. They can group up against a threat that is too dangerous for one mercenary. The Retire action in a creature's information panel requests lethal removal.\n\n"
                + "Zookeepers heal injured creatures, repair damaged fence segments and restock goat dispensers. They can also sedate a creature. After sedation, choose Move in the creature's information panel and select a destination for a keeper to carry it there.\n\n"
                + "A request needs an available employee and a usable route. Staff gates provide enclosure access. Destroyed fences must be rebuilt; staff can repair only surviving segments.\n\n"
                + "Staff can be endangered by creatures too. Consider access routes and containment when placing workplaces."));
            sections[2].Add(new Entry("Creature care", "Support natural behaviour instead of treating an enclosure as a fixed assignment.",
                "Select a creature to inspect its current activity and needs. Hunger, fear, energy, environment, reachable space and social surroundings all influence its wellbeing.\n\n"
                + "Creatures seek food when hungry and rest when exhausted. Different species have different diets, sleeping places and preferred group sizes. Research a species to reveal its particular care requirements in the Creatures section.\n\n"
                + "A creature notices nearby threats and may flee from something more dangerous than itself. Movement, food searches and shelter searches follow physically usable routes rather than enclosure ownership.\n\n"
                + "Poor happiness increases agitation. Hunger, agitation and overcrowding can increase the urge to escape; rare curiosity can contribute too. A creature's panel reports its current escape pressure and reasons.\n\n"
                + "Provide food, rest, suitable surroundings and enough accessible space. Enrichment on the other side of an impassable barrier cannot meet a need that requires the creature to use it.\n\n"
                + "Containment is only one part of care. Use researched species information to choose barriers while maintaining staff access for treatment and supplies."));
            sections[2].Add(new Entry("Visitor welfare", "Comfort and safety determine how long guests want to stay.",
                "Visitors enter through the park entrance and travel on connected paths. They seek viewable creatures and use reachable services.\n\n"
                + "Hungry visitors can buy food from kiosks. Tired visitors need rest areas. Their satisfaction and willingness to remain depend on the experience they can actually reach and afford.\n\n"
                + "Satisfying visitors encourages longer stays. Unavailable, unreachable or overpriced services can leave needs unmet and lead to complaints or early departures.\n\n"
                + "Visitors can fear dangerous creatures, flee and become prey. Safe exhibition layouts, appropriate containment and staff response protect guests and the park's reputation.\n\n"
                + "Use Park Management to review visitor satisfaction, admission refusals, service price complaints and losses. Satisfied departures support reputation; unhappy departures and visitor deaths reduce it."));
        }
    }
}

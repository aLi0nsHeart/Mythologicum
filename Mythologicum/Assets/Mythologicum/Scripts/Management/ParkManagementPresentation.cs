using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>Park statistics, recent cash flow, editable prices and the reasons behind its stars.</summary>
    public sealed class ParkManagementPresentation : IDisposable
    {
        private readonly GameObject root;
        private readonly ParkBuildingNetwork buildings;
        private readonly VisitorWorld visitors;
        private readonly CreatureWorld creatures;
        private readonly ParkEconomy economy;
        private readonly Text summary, statistics, finances, breakdown, budget, rating, priceMessage;
        private readonly Text parkStatistics, financeNotes, ratingGuide;
        private readonly GameObject[] pages = new GameObject[4];
        private readonly Button[] tabs = new Button[4];
        private readonly RectTransform priceContent;
        private readonly StarRatingGraphic stars;
        private readonly Button fundsMode;
        private readonly List<KeyValuePair<ParkBuildingRecord, InputField>> prices = new List<KeyValuePair<ParkBuildingRecord, InputField>>();
        private string buildingSignature;
        public bool IsOpen => root != null && root.activeSelf;

        public ParkManagementPresentation(Canvas canvas, ParkBuildingNetwork buildings, VisitorWorld visitors,
            CreatureWorld creatures, ParkEconomy economy)
        {
            this.buildings = buildings; this.visitors = visitors; this.creatures = creatures; this.economy = economy;
            root = ManagementWidgets.Modal(canvas, "Park management window");
            var card = ManagementWidgets.Card(root, canvas);
            ManagementWidgets.Label(card, "Park management", new Vector2(16, 10), new Vector2(900, 38), 27);
            ManagementWidgets.Button(card, "Close park management", new Vector2(1034, 8), new Vector2(110, 38), Close, 16)
                .GetComponentInChildren<Text>().text = "Close";
            summary = ManagementWidgets.Label(card, "Park management summary", new Vector2(16, 4), new Vector2(996, 32), 20);
            string[] names = { "Statistics", "Finances", "Prices & budget", "Star rating" };
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                tabs[i] = ManagementWidgets.Button(card, names[i], new Vector2(16 + i * 282, 48), new Vector2(270, 42), () => ShowPage(index));
                pages[i] = ManagementWidgets.Rect(card, names[i] + " page", new Vector2(16, 104), new Vector2(1128, 396)).gameObject;
            }
            statistics = ManagementWidgets.Label(pages[0].transform, "Park statistics", Vector2.zero, new Vector2(530, 380), 20);
            parkStatistics = ManagementWidgets.Label(pages[0].transform, "Exhibition statistics", new Vector2(560,0), new Vector2(550,380), 20);
            finances = ManagementWidgets.Label(pages[1].transform, "Recent finances", Vector2.zero, new Vector2(530, 346), 18);
            breakdown = ManagementWidgets.Label(pages[1].transform, "Cash flow breakdown", new Vector2(555, 0), new Vector2(555, 346), 18);
            financeNotes = ManagementWidgets.Label(pages[1].transform,"Finance period",new Vector2(0,354),new Vector2(1110,42),16);
            budget = ManagementWidgets.Label(pages[2].transform, "Pricing and budget", Vector2.zero, new Vector2(810, 90), 18);
            fundsMode = ManagementWidgets.Button(pages[2].transform, "Park funds mode", new Vector2(826, 0), new Vector2(284, 38),
                () => { economy.UnlimitedFunds = !economy.UnlimitedFunds; Refresh(); }, 17);
            priceContent = ManagementWidgets.Scroll(pages[2].transform, "Revenue building prices", new Vector2(0, 100), new Vector2(1110, 248), 0);
            priceMessage = ManagementWidgets.Label(pages[2].transform, "Prices apply to new admissions and new service orders.", new Vector2(0, 358), new Vector2(1110, 38), 17);
            var graphic = ManagementWidgets.Rect(pages[3].transform, "Park stars", Vector2.zero, new Vector2(300, 50));
            stars = graphic.gameObject.AddComponent<StarRatingGraphic>(); stars.raycastTarget = false;
            rating = ManagementWidgets.Label(pages[3].transform, "Rating explanation", new Vector2(0, 65), new Vector2(530, 325), 20);
            ratingGuide = ManagementWidgets.Label(pages[3].transform,"Rating guide",new Vector2(560,0),new Vector2(550,390),19);
            ShowPage(0);
        }

        public void Open()
        {
            root.SetActive(true); root.transform.SetAsLastSibling(); ShowPage(0); Refresh();
            UIThemeApplicator.ApplyAll(root.GetComponentInParent<Canvas>());
        }
        public void Close() => root.SetActive(false);
        private void ShowPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                tabs[i].GetComponent<Image>().color = i == index ? new Color(.42f, .34f, .16f) : new Color(.18f, .28f, .31f);
            }
            Refresh();
        }
        public void Refresh()
        {
            if (!IsOpen) return;
            var reputation = visitors.Reputation;
            int employees = 0, living = 0; float upkeep = 0;
            foreach (var building in buildings.Records) { employees += building.HiredCount; upkeep += building.UpkeepPerSecond * 60f; }
            foreach (var creature in creatures.Agents) if (creature.Alive && !creature.ExternallyControlled) living++;
            summary.text = $"Funds: {economy.Balance:0.00}  |  Park rating: {reputation.Stars:0.00} / 5 stars  |  Visitors: {visitors.Population}";
            statistics.text = $"VISITORS\nIn the park: {visitors.Population} / {visitors.Definition.maximumVisitors}\n"
                + $"Current satisfaction: {visitors.AverageSatisfaction:0}/100\nDeparted: {visitors.DepartedCount}  |  Visitor deaths: {visitors.LostCount}\n"
                + $"Stranded visitors: {visitors.StrandedCount}\nAdmission refusals: {visitors.RejectedAdmissions}\nService price complaints: {visitors.ExpensiveServiceRefusals}";
            parkStatistics.text=$"PARK\nBuildings: {buildings.Records.Count}  |  Employees: {employees}\nLiving creatures: {living}\n"
                + $"Viewable exhibition: {reputation.DisplayedSpecies} species / {reputation.DisplayedCreatures} creatures\n"
                + $"Potential paid arrivals: {visitors.ExpectedArrivalsPerMinute:0.0} / minute before capacity limits\n"
                + $"\nBudgeted building upkeep and salaries: {upkeep:0.00} / minute\nSupplies, new construction and projects are charged separately.";
            finances.text = $"CASH FLOW / MINUTE\nIncome: {economy.IncomePerMinute:0.00}\nExpenses: {economy.ExpensesPerMinute:0.00}\n"
                + $"Net cash flow: {economy.ProfitPerMinute:0.00}\n\nOPERATIONS / MINUTE\n"
                + $"Operating expenses: {economy.OperatingExpensesPerMinute:0.00}\nOperating profit: {economy.OperatingProfitPerMinute:0.00}\n"
                + $"One-off investments: {economy.InvestmentExpensesPerMinute:0.00}\n\nLIFETIME\n"
                + $"Income: {economy.Income:0.00}\nExpenses: {economy.Expenses:0.00}";
            financeNotes.text=$"Actual totals from the last 60 simulation seconds. Recorded so far: {economy.TrackedSeconds:0} / 60 seconds.\nOperating profit excludes one-off investments.";
            var b = new StringBuilder("INCOME / MINUTE\n");
            Add(b, "Admission", economy.IncomeInWindow(ParkTransactionCategory.Admission));
            Add(b, "Food sales", economy.IncomeInWindow(ParkTransactionCategory.Food));
            Add(b, "Rest services", economy.IncomeInWindow(ParkTransactionCategory.Rest));
            Add(b, "Other income", economy.IncomeInWindow(ParkTransactionCategory.Other));
            b.Append("EXPENSES / MINUTE\n");
            Add(b, "Building upkeep", economy.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep));
            Add(b, "Salaries", economy.ExpensesInWindow(ParkTransactionCategory.Salary));
            Add(b, "Feeder supplies", economy.ExpensesInWindow(ParkTransactionCategory.Supplies));
            Add(b, "Construction", economy.ExpensesInWindow(ParkTransactionCategory.Construction));
            Add(b, "Hiring", economy.ExpensesInWindow(ParkTransactionCategory.Hiring));
            Add(b, "Research", economy.ExpensesInWindow(ParkTransactionCategory.Research));
            Add(b, "Expeditions, teams and permits", economy.ExpensesInWindow(ParkTransactionCategory.Expeditions));
            Add(b, "Other expenses", economy.OtherExpensesPerMinute);
            breakdown.text = b.ToString().TrimEnd('\n');
            budget.text = $"Committed upkeep and salaries: {upkeep:0.00} / minute\n"
                + $"Fair entry price: {reputation.RecommendedAdmissionPrice:0.00}  |  Admission acceptance: {visitors.AdmissionAcceptance:P0}\n"
                + $"Potential paid arrivals: {visitors.ExpectedArrivalsPerMinute:0.0} / minute before capacity limits";
            fundsMode.GetComponentInChildren<Text>().text = "Infinite funds: " + (economy.UnlimitedFunds ? "on" : "off");
            RefreshPrices();
            stars.SetRating(reputation.Stars);
            rating.text = $"PARK RATING: {reputation.Stars:0.00} / 5 STARS\n"
                + $"Exhibition appeal: {reputation.ExhibitionScore:0}/100  |  Visitor reputation: {reputation.GuestScore:0}/100\n"
                + $"Safety penalty: {reputation.SafetyPenalty:0}/100\nVisitor reviews: {reputation.ReviewedVisitors}\nFatalities: {reputation.Fatalities}\n\n"
                + $"CURRENT FAIR PRICES\nAdmission: {reputation.RecommendedAdmissionPrice:0.00}\nMeal: {reputation.RecommendedFoodPrice:0.00}\nRest: {reputation.RecommendedRestPrice:0.00}";
            ratingGuide.text="EXHIBITION\nDisplay different species with accessible viewing spots to increase appeal. Higher-rated creatures contribute more; extra specimens give diminishing returns.\n\n"
                + "VISITORS & SAFETY\nSatisfied departures improve reputation; unhappy departures reduce it. Visitor deaths reduce reputation and demand. Safe operation allows gradual recovery.\n\n"
                + "PRICES\nHigher stars support higher prices. Guests can refuse overpriced entry or services. Exhibition value comes from what visitors can see, wherever creatures choose to live.";
        }
        private static void Add(StringBuilder b, string name, float value) => b.Append(name).Append(": ").Append(value.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');

        private void RefreshPrices()
        {
            string signature = "";
            foreach (var building in buildings.Records) if (building.Footprint.Definition.CanSetPrice) signature += building.RuntimeId + ":" + building.HiredCount + ";";
            if (signature != buildingSignature)
            {
                buildingSignature = signature; prices.Clear(); ManagementWidgets.Clear(priceContent);
                float y = 0;
                foreach (var building in buildings.Records)
                {
                    if (!building.Footprint.Definition.CanSetPrice) continue;
                    var record = building;
                    var row = ManagementWidgets.Box(priceContent, "Price row " + record.RuntimeId, new Vector2(0, y), new Vector2(1090, 70), new Color(.07f, .12f, .14f));
                    ManagementWidgets.Label(row.transform, record.Footprint.Definition.displayName + "  [" + record.Footprint.Anchor.x + ", " + record.Footprint.Anchor.y + "]",
                        new Vector2(10, 7), new Vector2(600, 26), 20);
                    ManagementWidgets.Label(row.transform, $"Upkeep incl. salaries: {record.UpkeepPerSecond * 60f:0.00} / minute",
                        new Vector2(10, 37), new Vector2(600, 26), 17);
                    var input = PriceInput(row.transform, "Price input " + record.RuntimeId, new Vector2(660, 15));
                    input.text = record.Price.ToString("0.##", CultureInfo.InvariantCulture);
                    prices.Add(new KeyValuePair<ParkBuildingRecord, InputField>(record, input));
                    ManagementWidgets.Button(row.transform, "Apply price " + record.RuntimeId, new Vector2(860, 15), new Vector2(210, 40), () =>
                    {
                        bool valid = float.TryParse(input.text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
                        priceMessage.text = valid && record.SetPrice(value) ? "Price updated for new customers: " + record.Footprint.Definition.displayName
                            : "Enter a finite price of zero or higher.";
                        Refresh();
                    }).GetComponentInChildren<Text>().text = "Apply price";
                    y += 78;
                }
                if (prices.Count == 0)
                { ManagementWidgets.Label(priceContent, "Place the park entrance to manage admission prices.", new Vector2(12, 12), new Vector2(1060, 50)); y = 80; }
                priceContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
            }
            foreach (var pair in prices)
                if (!pair.Value.isFocused) pair.Value.text = pair.Key.Price.ToString("0.##", CultureInfo.InvariantCulture);
        }
        private static InputField PriceInput(Transform parent, string name, Vector2 position)
        {
            var box = ManagementWidgets.Box(parent, name, position, new Vector2(180, 40), new Color(.12f, .19f, .21f));
            var input = box.AddComponent<InputField>(); input.targetGraphic = box.GetComponent<Image>();
            input.contentType = InputField.ContentType.Standard; input.characterLimit = 16;
            var text = ManagementWidgets.Label(box.transform, "Entered price", new Vector2(8, 3), new Vector2(164, 34), 19);
            text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false; input.textComponent = text;
            return input;
        }
        public void Dispose() => UnityEngine.Object.Destroy(root);
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StarRatingGraphic : MaskableGraphic
    {
        private float rating;
        public void SetRating(float value)
        { float next = float.IsNaN(value) ? 0 : Mathf.Clamp(value, 0, 5); if (next == rating) return; rating = next; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            for (int star = 0; star < 5; star++)
            {
                Vector2 center = new Vector2(rect.xMin + (star + .5f) * rect.width / 5, rect.center.y);
                float radius = Mathf.Min(rect.height * .45f, rect.width / 5 * .43f);
                Color tint = Color.Lerp(new Color(.18f, .24f, .25f), new Color(1f, .78f, .2f), Mathf.Clamp01(rating - star));
                int first = vh.currentVertCount; vh.AddVert(center, tint, Vector2.zero);
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI * .5f + i * Mathf.PI / 5;
                    float r = i % 2 == 0 ? radius : radius * .45f;
                    vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r, tint, Vector2.zero);
                }
                for (int i = 0; i < 10; i++) vh.AddTriangle(first, first + i + 1, first + (i + 1) % 10 + 1);
            }
        }
    }
}

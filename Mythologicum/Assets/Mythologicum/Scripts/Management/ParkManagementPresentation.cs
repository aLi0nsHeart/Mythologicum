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
        private readonly GameObject[] pages = new GameObject[5];
        private readonly Button[] tabs = new Button[5];
        private Action<ParkOverlayMode> chooseOverlay;
        public void ConfigureOverlays(Action<ParkOverlayMode> choose) => chooseOverlay = choose;
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
            ManagementWidgets.Label(card, "park.park_management_presentation.park_management", new Vector2(16, 10), new Vector2(900, 38), 27);
            ManagementWidgets.Button(card, "park.park_management_presentation.close_park_management", new Vector2(1034, 8), new Vector2(110, 38), Close, 16)
                .GetComponentInChildren<Text>().text = Loc.Text("park.park_management_presentation.close");
            summary = ManagementWidgets.Label(card, "park.park_management_presentation.park_management_summary", new Vector2(16, 4), new Vector2(996, 32), 20);
            string[] names = { "park.park_management_presentation.statistics", "park.park_management_presentation.finances", "park.park_management_presentation.prices_budget", "park.park_management_presentation.star_rating", "park.park_management_presentation.map_overlays" };
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                tabs[i] = ManagementWidgets.Button(card, names[i], new Vector2(16 + i * 226, 48), new Vector2(214, 42), () => ShowPage(index));
                pages[i] = ManagementWidgets.Rect(card, Loc.English(names[i]) + " page", new Vector2(16, 104), new Vector2(1128, 396)).gameObject;
            }
            statistics = ManagementWidgets.Label(pages[0].transform, "park.park_management_presentation.park_statistics", Vector2.zero, new Vector2(530, 380), 20);
            parkStatistics = ManagementWidgets.Label(pages[0].transform, "park.park_management_presentation.exhibition_statistics", new Vector2(560,0), new Vector2(550,380), 20);
            finances = ManagementWidgets.Label(pages[1].transform, "park.park_management_presentation.recent_finances", Vector2.zero, new Vector2(530, 346), 18);
            breakdown = ManagementWidgets.Label(pages[1].transform, "park.park_management_presentation.cash_flow_breakdown", new Vector2(555, 0), new Vector2(555, 346), 18);
            financeNotes = ManagementWidgets.Label(pages[1].transform,"park.park_management_presentation.finance_period",new Vector2(0,354),new Vector2(1110,42),16);
            budget = ManagementWidgets.Label(pages[2].transform, "park.park_management_presentation.pricing_and_budget", Vector2.zero, new Vector2(810, 90), 18);
            fundsMode = ManagementWidgets.Button(pages[2].transform, "park.park_management_presentation.park_funds_mode", new Vector2(826, 0), new Vector2(284, 38),
                () => { economy.UnlimitedFunds = !economy.UnlimitedFunds; Refresh(); }, 17);
            priceContent = ManagementWidgets.Scroll(pages[2].transform, "Revenue building prices", new Vector2(0, 100), new Vector2(1110, 248), 0);
            priceMessage = ManagementWidgets.Label(pages[2].transform, "park.park_management_presentation.prices_apply_to_new_admissions_and_new_service_orders", new Vector2(0, 358), new Vector2(1110, 38), 17);
            var graphic = ManagementWidgets.Rect(pages[3].transform, "Park stars", Vector2.zero, new Vector2(300, 50));
            stars = graphic.gameObject.AddComponent<StarRatingGraphic>(); stars.raycastTarget = false;
            rating = ManagementWidgets.Label(pages[3].transform, "park.park_management_presentation.rating_explanation", new Vector2(0, 65), new Vector2(530, 325), 20);
            ratingGuide = ManagementWidgets.Label(pages[3].transform,"park.park_management_presentation.rating_guide",new Vector2(560,0),new Vector2(550,390),19);
            ManagementWidgets.Label(pages[4].transform, "park.park_management_presentation.choose_a_temporary_map_overlay_it_follows_the_terrain_and", Vector2.zero, new Vector2(1100, 60), 20);
            string[] overlayNames = { "park.park_management_presentation.hide_overlays", "park.park_management_presentation.path_connections", "park.park_management_presentation.enclosure_space", "park.park_management_presentation.staff_access" };
            string[] explanations = { "park.park_management_presentation.return_to_the_normal_park_view", "park.park_management_presentation.find_paths_disconnected_from_the_main_entrance", "park.park_management_presentation.see_secured_enclosure_areas_and_breached_perimeters", "park.park_management_presentation.see_ground_reachable_through_gates_from_open_workplaces_wi" };
            for (int i = 0; i < overlayNames.Length; i++)
            {
                var mode = (ParkOverlayMode)i;
                ManagementWidgets.Button(pages[4].transform, overlayNames[i], new Vector2(0, 76 + i * 72), new Vector2(300, 48),
                    () => { chooseOverlay?.Invoke(mode); Close(); }, 19);
                ManagementWidgets.Label(pages[4].transform, explanations[i], new Vector2(324, 82 + i * 72), new Vector2(775, 48), 18);
            }
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
            summary.text = Loc.Format("park.park_management_presentation.funds_park_rating_5_stars_visitors", economy.Balance, reputation.Stars, visitors.Population);
            statistics.text = Loc.Format("park.park_management_presentation.visitors_in_the_park_current_satisfaction_100_departed_vis", visitors.Population, visitors.Definition.maximumVisitors, visitors.AverageSatisfaction, visitors.DepartedCount, visitors.LostCount, visitors.StrandedCount, visitors.RejectedAdmissions, visitors.ExpensiveServiceRefusals);
            parkStatistics.text=Loc.Format("park.park_management_presentation.park_buildings_employees_living_creatures_viewable_exhibit", buildings.Records.Count, employees, living, reputation.DisplayedSpecies, reputation.DisplayedCreatures, visitors.ExpectedArrivalsPerMinute, upkeep);
            finances.text = Loc.Format("park.park_management_presentation.cash_flow_minute_income_expenses_net_cash_flow_operations", economy.IncomePerMinute, economy.ExpensesPerMinute, economy.ProfitPerMinute, economy.OperatingExpensesPerMinute, economy.OperatingProfitPerMinute, economy.InvestmentExpensesPerMinute, economy.Income, economy.Expenses);
            financeNotes.text=Loc.Format("park.park_management_presentation.actual_totals_from_the_last_60_simulation_seconds_recorded", economy.TrackedSeconds);
            var b = new StringBuilder(Loc.Text("park.park_management_presentation.income_minute"));
            Add(b, "park.park_management_presentation.admission", economy.IncomeInWindow(ParkTransactionCategory.Admission));
            Add(b, "park.park_management_presentation.food_sales", economy.IncomeInWindow(ParkTransactionCategory.Food));
            Add(b, "park.park_management_presentation.rest_services", economy.IncomeInWindow(ParkTransactionCategory.Rest));
            Add(b, "park.park_management_presentation.other_income", economy.IncomeInWindow(ParkTransactionCategory.Other));
            b.Append(Loc.Text("park.park_management_presentation.expenses_minute"));
            Add(b, "park.park_management_presentation.building_upkeep", economy.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep));
            Add(b, "park.park_management_presentation.salaries", economy.ExpensesInWindow(ParkTransactionCategory.Salary));
            Add(b, "park.park_management_presentation.feeder_supplies", economy.ExpensesInWindow(ParkTransactionCategory.Supplies));
            Add(b, "park.park_management_presentation.construction", economy.ExpensesInWindow(ParkTransactionCategory.Construction));
            Add(b, "park.park_management_presentation.hiring", economy.ExpensesInWindow(ParkTransactionCategory.Hiring));
            Add(b, "park.park_management_presentation.research", economy.ExpensesInWindow(ParkTransactionCategory.Research));
            Add(b, "park.park_management_presentation.expeditions_teams_and_permits", economy.ExpensesInWindow(ParkTransactionCategory.Expeditions));
            Add(b, "park.park_management_presentation.other_expenses", economy.OtherExpensesPerMinute);
            breakdown.text = b.ToString().TrimEnd('\n');
            budget.text = Loc.Format("park.park_management_presentation.committed_upkeep_and_salaries_minute_fair_entry_price_admi", upkeep, reputation.RecommendedAdmissionPrice, visitors.AdmissionAcceptance, visitors.ExpectedArrivalsPerMinute);
            fundsMode.GetComponentInChildren<Text>().text = Loc.Format("park.park_management_presentation.infinite_funds", (economy.UnlimitedFunds ? Loc.Text("park.park_management_presentation.on") : Loc.Text("park.park_management_presentation.off")));
            RefreshPrices();
            stars.SetRating(reputation.Stars);
            rating.text = Loc.Format("park.park_management_presentation.park_rating_5_stars_exhibition_appeal_100_visitor_reputati", reputation.Stars, reputation.ExhibitionScore, reputation.GuestScore, reputation.SafetyPenalty, reputation.ReviewedVisitors, reputation.Fatalities, reputation.RecommendedAdmissionPrice, reputation.RecommendedFoodPrice, reputation.RecommendedRestPrice);
            ratingGuide.text=Loc.Text("park.park_management_presentation.exhibition_display_different_species_with_accessible_viewi");
        }
        private static void Add(StringBuilder b, string name, float value) => b.Append(Loc.Resolve(name)).Append(": ").Append(value.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');

        private void RefreshPrices()
        {
            string signature = Loc.Revision.ToString();
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
                    ManagementWidgets.Label(row.transform, Loc.Format("park.park_management_presentation.format", record.Footprint.Definition.DisplayName, record.Footprint.Anchor.x, record.Footprint.Anchor.y),
                        new Vector2(10, 7), new Vector2(600, 26), 20);
                    ManagementWidgets.Label(row.transform, Loc.Format("park.park_management_presentation.upkeep_incl_salaries_minute", record.UpkeepPerSecond * 60f),
                        new Vector2(10, 37), new Vector2(600, 26), 17);
                    var input = PriceInput(row.transform, "Price input " + record.RuntimeId, new Vector2(660, 15));
                    input.text = record.Price.ToString("0.##", Loc.Culture);
                    prices.Add(new KeyValuePair<ParkBuildingRecord, InputField>(record, input));
                    ManagementWidgets.Button(row.transform, Loc.Format("park.park_management_presentation.apply_price", record.RuntimeId), new Vector2(860, 15), new Vector2(210, 40), () =>
                    {
                        bool valid = float.TryParse(input.text,NumberStyles.Float,Loc.Culture,out float value)
                            ||float.TryParse(input.text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                        priceMessage.text = valid && record.SetPrice(value) ? Loc.Format("park.park_management_presentation.price_updated_for_new_customers", record.Footprint.Definition.DisplayName)
                            : Loc.Text("park.park_management_presentation.enter_a_finite_price_of_zero_or_higher");
                        Refresh();
                    }).GetComponentInChildren<Text>().text = Loc.Text("park.park_management_presentation.apply_price_2");
                    y += 78;
                }
                if (prices.Count == 0)
                { ManagementWidgets.Label(priceContent, "park.park_management_presentation.place_the_park_entrance_to_manage_admission_prices", new Vector2(12, 12), new Vector2(1060, 50)); y = 80; }
                priceContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
            }
            foreach (var pair in prices)
                if (!pair.Value.isFocused) pair.Value.text = pair.Key.Price.ToString("0.##", Loc.Culture);
        }
        private static InputField PriceInput(Transform parent, string name, Vector2 position)
        {
            var box = ManagementWidgets.Box(parent, name, position, new Vector2(180, 40), new Color(.12f, .19f, .21f));
            var input = box.AddComponent<InputField>(); input.targetGraphic = box.GetComponent<Image>();
            input.contentType = InputField.ContentType.Standard; input.characterLimit = 16;
            var text = ManagementWidgets.Label(box.transform, "park.park_management_presentation.entered_price", new Vector2(8, 3), new Vector2(164, 34), 19);
            text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false; input.textComponent = text;
            return input;
        }
        public void Dispose() => UnityEngine.Object.Destroy(root);
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StarRatingGraphic : MaskableGraphic
    {
        private float rating;
        private Color filledColor = new Color(1f, .78f, .2f), emptyColor = new Color(.18f, .24f, .25f);
        public void SetRating(float value)
        {
            float next = float.IsNaN(value) ? 0 : Mathf.Clamp(value, 0, 5);
            var theme = UITheme.Current;
            Color filled = theme != null ? theme.starFilledColor : new Color(1f, .78f, .2f);
            Color empty = theme != null ? theme.starEmptyColor : new Color(.18f, .24f, .25f);
            if (next == rating && filled == filledColor && empty == emptyColor) return;
            rating = next; filledColor = filled; emptyColor = empty; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            for (int star = 0; star < 5; star++)
            {
                Vector2 center = new Vector2(rect.xMin + (star + .5f) * rect.width / 5, rect.center.y);
                float radius = Mathf.Min(rect.height * .45f, rect.width / 5 * .43f);
                Color tint = Color.Lerp(emptyColor, filledColor, Mathf.Clamp01(rating - star));
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

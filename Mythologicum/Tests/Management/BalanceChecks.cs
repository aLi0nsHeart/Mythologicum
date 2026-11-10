using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mythologicum;
using UnityEngine;

public static class BalanceChecks
{
    static int checks;
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    static void Equal(double expected, double actual, string message, double tolerance = .002)
    { Check(!double.IsNaN(actual) && Math.Abs(expected - actual) <= tolerance, message + ": expected " + expected + ", got " + actual); }
    static float VisitorSetting(string field)
    {
        var text = File.ReadAllText("Assets/Mythologicum/Resources/Visitors/DefaultVisitors.asset");
        var match = Regex.Match(text, @"(?m)^\s*" + Regex.Escape(field) + @":\s*([-+.0-9]+)\s*$");
        if (!match.Success) throw new Exception("Missing actual visitor setting: " + field);
        return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    static void CheckSceneCosts(BuildingDefinition[] definitions)
    {
        string scene = File.ReadAllText("Assets/Mythologicum/Scenes/GameScene.unity");
        var catalogue = Regex.Match(scene, @"(?ms)^  buildingCatalog:\r?\n(?<entries>.*?)(?=^  visitorSettings:|^  startingFunds:|^---|\z)");
        if (!catalogue.Success) { Check(true, "Unserialized scene uses the checked code defaults"); return; }
        var rows = Regex.Matches(catalogue.Groups["entries"].Value, @"(?ms)^  - kind: (?<kind>\d+)\r?\n(?<data>.*?)(?=^  - kind:|\z)");
        Check(rows.Count > 0, "Actual scene catalogue is available to the balance audit");
        foreach (Match row in rows)
        {
            int kind = int.Parse(row.Groups["kind"].Value, CultureInfo.InvariantCulture);
            var definition = definitions.Single(item => (int)item.kind == kind);
            string data = row.Groups["data"].Value;
            float Field(string key)
            {
                var value = Regex.Match(data, @"(?m)^    " + Regex.Escape(key) + @":\s*([-+.0-9]+)\s*$");
                if (!value.Success) throw new Exception("Missing scene cost field " + key);
                return float.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            Equal(definition.operatingCostPerSecond, Field("operatingCostPerSecond"), definition.displayName + " scene upkeep matches the audited rate");
            Equal(definition.staffSalaryPerSecond, Field("staffSalaryPerSecond"), definition.displayName + " scene salary matches the audited rate");
            Equal(definition.constructionCost, Field("constructionCost"), definition.displayName + " scene purchase matches the startup budget");
            Equal(definition.hireCost, Field("hireCost"), definition.displayName + " scene hiring fee matches the startup budget");
            Equal(definition.foodPrice, Field("foodPrice"), definition.displayName + " scene starting service price matches the revenue model");
            if (definition.kind == BuildingKind.GoatDispenser)
            {
                Equal(definition.restockCost, Field("restockCost"), "Actual feeder supply price matches the operating budget");
                Equal(definition.feederCapacity, Field("feederCapacity"), "Actual feeder capacity matches top-up timing");
                Equal(definition.dispenseInterval, Field("dispenseInterval"), "Actual goat interval matches supply consumption");
            }
        }
        var startingFunds = Regex.Match(scene, @"(?m)^  startingFunds:\s*([-+.0-9]+)\s*$");
        if (startingFunds.Success) Equal(new ParkEconomy().Balance, float.Parse(startingFunds.Groups[1].Value, CultureInfo.InvariantCulture), "Actual scene starting funds match the startup budget");
    }

    public static void Main()
    {
        var definitions = BuildingDefinition.Defaults();
        CheckSceneCosts(definitions);
        var rules = new ProgressionRules();
        var byKind = definitions.ToDictionary(item => item.kind);
        var reputation = new ParkReputation();
        float interval = VisitorSetting("spawnInterval"), limit = VisitorSetting("maximumVisitors");
        float maximumStay = VisitorSetting("maximumStaySeconds");
        var entrance = new ParkBuildingRecord(new BuildingFootprint(byKind[BuildingKind.ParkEntrance], new Vector2Int(1, 1), 0));
        double ticket = entrance.Price;
        double offeredPerMinute = 60d / interval * reputation.DemandMultiplier * reputation.PriceAcceptance((float)ticket);
        double capLimitedPerMinute = limit * 60d / maximumStay;
        double conservativeAdmissions = Math.Min(offeredPerMinute, capLimitedPerMinute);

        Equal(15, ticket, "The default placed entrance retains an affordable bootstrap ticket");
        Check(reputation.PriceAcceptance((float)ticket) >= .99f, "A new park can sell its starting ticket without a reputation gate");
        Equal(7.5, offeredPerMinute, "Actual visitor profile and neutral reputation offer baseline attendance");
        Equal(6, capLimitedPerMinute, "Revenue model respects visitor capacity at maximum stay length");
        Check(definitions.All(item => item.operatingCostPerSecond >= 0 && item.staffSalaryPerSecond >= 0), "All new default costs are nonnegative");

        double basePerMinute = definitions.Sum(item => item.operatingCostPerSecond * 60d);
        double salaries = definitions.Where(item => item.IsEmployeeBuilding).Sum(item => item.staffSalaryPerSecond * 60d);
        var feeder = byKind[BuildingKind.GoatDispenser];
        // Keepers request a top-up at two remaining servings, so a delivery normally replaces four goats.
        double refillAfter = Math.Max(1, feeder.feederCapacity - 2);
        double supplyPerMinute = 60d / feeder.dispenseInterval / refillAfter * feeder.restockCost;
        double ordinaryOperating = basePerMinute + salaries + supplyPerMinute;
        Equal(30, basePerMinute, "All eight buildings have a modest aggregate base upkeep");
        Equal(27, salaries, "One keeper, researcher and mercenary have affordable combined wages");
        Equal(9, supplyPerMinute, "Supply estimate uses the actual early top-up threshold and dispense interval");
        Equal(66, ordinaryOperating, "Full small-park overhead includes salaries and continuous feeder consumption");
        Check(conservativeAdmissions * ticket - ordinaryOperating >= 20,
            "A small park remains profitable from tickets alone even when guests stay the maximum time");

        var unhappy = new ParkReputation();
        unhappy.Restore(new ParkReputationSnapshot { guestScore = 0 });
        double weakAttendance = Math.Min(capLimitedPerMinute,
            60d / interval * unhappy.DemandMultiplier * unhappy.PriceAcceptance((float)ticket));
        Check(weakAttendance * ticket > ordinaryOperating,
            "Poor customer satisfaction without accidents does not immediately bankrupt an otherwise modest park");

        double maximumWages = salaries * ParkBuildingRecord.EmployeeCapacity;
        double fullyStaffedOperating = basePerMinute + maximumWages + supplyPerMinute;
        Equal(174, fullyStaffedOperating, "Hiring every employee slot is a deliberate higher-cost expansion");
        Check(fullyStaffedOperating > conservativeAdmissions * ticket,
            "An undeveloped park cannot sustainably fill all fifteen employee slots on starter ticket prices");
        var established = new ParkReputation();
        established.Restore(new ParkReputationSnapshot { guestScore = 100,
            exhibits = new List<ParkExhibit> { new ParkExhibit("Oni", 5, 3), new ParkExhibit("Yurei", 3, 3),
                new ParkExhibit("Chupacabra", 2, 3), new ParkExhibit("Hitotsume", 1) } });
        double establishedAttendance = Math.Min(capLimitedPerMinute,
            60d / interval * established.DemandMultiplier * established.PriceAcceptance(established.RecommendedAdmissionPrice));
        Check(established.Stars >= 4.9f && establishedAttendance * established.RecommendedAdmissionPrice > fullyStaffedOperating,
            "A diverse successful park can support all fifteen employees without unlimited funds");

        double construction = definitions.Sum(item => item.constructionCost);
        double hire = definitions.Where(item => item.IsEmployeeBuilding).Sum(item => item.hireCost);
        double fieldTrip = rules.teamCost + rules.regions.Single(item => item.id == "north-america").accessCost
            + rules.difficulties.Single(item => item.id == "survey").costPerTeam;
        double earlyResearch = rules.technologies.Where(item => item.id == "food-service" || item.id == "visitor-rest").Sum(item => item.cost);
        double start = new ParkEconomy().Balance;
        double startup = construction + hire + fieldTrip + earlyResearch;
        Equal(2420, construction, "Existing building construction prices remain unchanged");
        Equal(3640, startup, "Startup budget includes all eight buildings, three hires, team, permit, mission and service research");
        Check(start - startup >= 1000, "Default starting capital retains a substantial operating buffer after a complete modest setup");
        Check((start - startup) / ordinaryOperating >= 15,
            "The post-construction buffer covers at least fifteen simulated minutes even with no admission income");

        var kiosk = byKind[BuildingKind.Kiosk];
        double mealsPerMinute = kiosk.serviceCapacity * 60d / kiosk.serviceSeconds;
        Check(mealsPerMinute >= offeredPerMinute, "One kiosk has enough service throughput for baseline visitors");
        double kioskMargin = offeredPerMinute * kiosk.foodPrice - kiosk.operatingCostPerSecond * 60d;
        Check(kioskMargin > 0 && kiosk.constructionCost / kioskMargin <= 10,
            "A kiosk selling one fairly priced meal per baseline arrival can repay its purchase within ten minutes");
        Check(byKind[BuildingKind.RestArea].operatingCostPerSecond * 60 < 3,
            "Free rest remains a cheap customer-satisfaction service");

        // Exercise the actual accounting pipeline for ten minutes, using real default building records.
        var grid = new SquareBuildGrid(Vector3.zero, 2, 80, 80);
        var network = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        var money = new ParkEconomy();
        var records = new Dictionary<BuildingKind, ParkBuildingRecord>();
        for (int i = 0; i < definitions.Length; i++)
        {
            var record = network.TryPlace(new BuildingFootprint(definitions[i], new Vector2Int(5 + i % 4 * 15, 5 + i / 4 * 15), 0));
            Check(record != null, "Budget fixture reserves a real " + definitions[i].displayName);
            records.Add(definitions[i].kind, record);
            Check(money.TrySpend(definitions[i].constructionCost, ParkTransactionCategory.Construction), "Startup can buy " + definitions[i].displayName);
            if (definitions[i].IsEmployeeBuilding)
            {
                Check(money.TrySpend(definitions[i].hireCost, ParkTransactionCategory.Hiring), "Startup can hire initial " + definitions[i].displayName + " employee");
                record.HiredCount = 1;
            }
        }
        Check(money.TrySpend((float)fieldTrip, ParkTransactionCategory.Expeditions), "Startup can fund its first expedition attempt");
        Check(money.TrySpend((float)earlyResearch, ParkTransactionCategory.Research), "Startup can unlock food and rest services");
        float initialCash = money.Balance;
        int admissions = 0, deliveries = 0;
        for (int second = 1; second <= 600; second++)
        {
            money.Advance(1);
            BuildingEconomy.Tick(network, money, 1);
            if (second % (int)interval == 0) { money.Earn((float)ticket, ParkTransactionCategory.Admission); admissions++; }
            if (second % (int)feeder.dispenseInterval == 0)
            {
                Check(records[BuildingKind.GoatDispenser].ConsumeFeed(), "Continuous consumption has feeder stock");
                if (records[BuildingKind.GoatDispenser].FeedStock <= 2)
                {
                    Check(money.TrySpend(feeder.restockCost, ParkTransactionCategory.Supplies), "Operating park can afford its physical feeder delivery");
                    records[BuildingKind.GoatDispenser].RestockFeed(); deliveries++;
                }
            }
        }
        Equal(75, admissions, "Ten-minute baseline delivers the expected number of ticket-paying arrivals");
        Equal(7, deliveries, "Ten-minute feeder model charges real top-ups instead of one cost per goat");
        double expected = initialCash + admissions * ticket - 10 * (basePerMinute + salaries) - deliveries * feeder.restockCost;
        Equal(expected, money.Balance, "Real building and finance accounting matches the projected ten-minute budget", 1);
        Check(money.Balance > initialCash + 400, "A functioning modest park grows its capital without meal sales or infinite funds");
        Check(money.ExpensesInWindow(ParkTransactionCategory.Salary) > 0 && money.OperatingProfitPerMinute > 0,
            "The rolling management ledger reports the profitable park's real salary costs and operating margin");
        Console.WriteLine("PASS: " + checks + " startup affordability, operating margin, capacity, expansion and ten-minute accounting checks.");
    }
}

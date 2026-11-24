using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class AfricanProgressionChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 8);
        var network = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 2), 0));
        var centre = network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ExpeditionCentre, width = 1, depth = 1 }, new Vector2Int(3, 2), 0));
        for (int x = 1; x <= 3; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
        network.Refresh();
        Check(centre != null && centre.IsOpen, "The expedition centre has production path access.");
        var ratings = new Dictionary<string, int> {
            { "MokeleMbembe", 4 }, { "NinkiNanka", 3 }, { "Abada", 1 },
            { "NandiBear", 3 }, { "Dingonek", 4 }, { "Marozi", 2 },
            { "Grootslang", 5 }, { "Inkanyamba", 3 }, { "Aigamuxa", 2 },
            { "Manticore", 4 }, { "Chupacabra", 2 }
        };
        var pools = new Dictionary<string, string[]> {
            { "west-africa", new[] { "NinkiNanka" } },
            { "central-africa", new[] { "MokeleMbembe", "Abada" } },
            { "east-africa", new[] { "NandiBear", "Dingonek", "Marozi" } },
            { "south-africa", new[] { "Grootslang", "Inkanyamba", "Aigamuxa" } },
            { "west-asia", new[] { "Manticore" } }
        };
        var rolls = new Queue<double>();
        foreach (string id in ratings.Keys.Where(id => id != "Chupacabra"))
            Check(File.ReadAllLines("Assets/Mythologicum/Resources/Creatures/" + id + ".asset")
                .All(line => !string.IsNullOrWhiteSpace(line)),
                "The Unity asset stays contiguous so importing does not discard fields after a blank separator: " + id);
        var rules = new ProgressionRules();
        var economy = new ParkEconomy(100000);
        Func<double> roll = () => rolls.Count > 0 ? rolls.Dequeue() : 0d;
        var park = new ParkProgression(rules, economy, network, ratings, roll);
        Check(park.Region("west-africa").name == "expedition.region.west_africa"
            && park.Region("central-africa").name == "expedition.region.central_africa",
            "West Africa and Central Africa have separate canonical expedition labels.");
        Check(pools.Count(pair => pair.Key != "west-asia") == 4
            && pools.Where(pair => pair.Key != "west-asia").SelectMany(pair => pair.Value).Distinct().Count() == 9,
            "All nine African creatures remain available across four populated African regions.");
        Check(park.BuyTeam(centre), "A team can be bought through the ordinary centre.");
        foreach (var pool in pools)
        {
            Check(park.Region(pool.Key).species.SequenceEqual(pool.Value), "The expedition pool contains exactly the intended regional roster: " + pool.Key);
            Check(park.RegionHasCreatures(pool.Key) && park.BuyAccess(centre, pool.Key), "The populated region can be unlocked: " + pool.Key);
            if (pool.Key != "west-asia" && pool.Key != "west-africa")
                Check(park.Eligible(pool.Key, "survey").Count > 0 && park.Eligible(pool.Key, "dangerous").Count > 0,
                    "Both entry-level and high-difficulty discoveries are available: " + pool.Key);
            if (pool.Key == "west-africa")
                Check(park.Eligible(pool.Key, "survey").Count == 0
                    && park.Eligible(pool.Key, "capture").SequenceEqual(new[] { "NinkiNanka" })
                    && park.Eligible(pool.Key, "dangerous").SequenceEqual(new[] { "NinkiNanka" }),
                    "West Africa's rating-three Ninki Nanka requires a capture or dangerous mission.");
            foreach (var difficulty in rules.difficulties)
                Check(park.Eligible(pool.Key, difficulty.id).SequenceEqual(pool.Value.Where(id => ratings[id] >= difficulty.minimumRating && ratings[id] <= difficulty.maximumRating)),
                    "Production rarity filtering respects the mission tier: " + pool.Key + "/" + difficulty.id);
            foreach (string species in pool.Value)
            {
                Check(park.Stock(species) == 0, "New species are not granted free specimens: " + species);
                string tier = ratings[species] <= 2 ? "survey" : ratings[species] == 3 ? "capture" : "dangerous";
                var eligible = park.Eligible(pool.Key, tier);
                rolls.Enqueue(0d); // Successful mission.
                rolls.Enqueue((eligible.IndexOf(species) + .5d) / eligible.Count);
                Check(park.Launch(centre, pool.Key, tier, 1), "A mission can be dispatched for " + species);
                var mission = park.Missions.Single();
                Check(mission.species == species && mission.succeeds, "The actual eligibility picker can award " + species);
                int quantity = mission.quantity;
                var active = park.Capture(network.Records);
                park = new ParkProgression(rules, economy, network, ratings, roll);
                park.Restore(active, network.Records);
                Check(park.Missions.Single().species == species && park.Stock(species) == 0 && park.HasAccess(pool.Key),
                    "An active mission and region survive restoration without an early reward: " + species);
                park.Tick(mission.remaining + 1);
                Check(park.Stock(species) == quantity && park.AvailableTeams == 1, "Completion returns its team and awards specimens: " + species);
                Check(!park.TryPlace(species, () => false) && park.Stock(species) == quantity, "Blocked placement preserves specimen stock: " + species);
                Check(park.TryPlace(species, () => true) && park.Stock(species) == quantity - 1, "Successful placement consumes one specimen: " + species);
                var complete = park.Capture(network.Records);
                park = new ParkProgression(rules, economy, network, ratings, roll);
                park.Restore(complete, network.Records);
                Check(park.Stock(species) == quantity - 1 && !park.Missions.Any(), "Loading completed progress does not duplicate the reward: " + species);
            }
        }
        Check(!park.RegionHasCreatures("north-africa") && !park.BuyAccess(centre, "north-africa"), "An empty region is still not sold.");

        var oldRules = new ProgressionRules {
            startingRegions = new[] { "south-america" }, startingTeams = 1,
            startingCreatures = new[] { new CreatureStock { species = "Chupacabra", count = 2 } }
        };
        var oldPark = new ParkProgression(oldRules, economy, network, new Dictionary<string, int> { { "Chupacabra", 2 } }, () => 0d);
        var oldSave = oldPark.Capture(network.Records);
        var expandedPark = new ParkProgression(rules, economy, network, ratings, roll);
        expandedPark.Restore(oldSave, network.Records);
        Check(expandedPark.Stock("Chupacabra") == 2 && expandedPark.HasAccess("south-america"), "Pre-expansion stock and access remain intact.");
        Check(ratings.Keys.Where(id => id != "Chupacabra").All(id => expandedPark.Stock(id) == 0), "An older save gains no unearned new creatures.");
        Check(pools.Keys.All(expandedPark.RegionHasCreatures), "Newly populated regions become available in an older park.");
        Console.WriteLine("PASS: " + checks + " African and Persian region, tier, acquisition, inventory and save compatibility checks.");
    }
}

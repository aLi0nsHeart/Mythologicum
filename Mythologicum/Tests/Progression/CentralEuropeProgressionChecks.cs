using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class CentralEuropeProgressionChecks
{
    private static int checks;
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 8);
        var buildings = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        buildings.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 2), 0));
        var home = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ExpeditionCentre, width = 1, depth = 1 }, new Vector2Int(3, 2), 0));
        for (int x = 1; x <= 3; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
        buildings.Refresh();
        Check(home != null && home.IsOpen, "The expedition fixture has a connected centre");
        var rules = new ProgressionRules();
        var ratings = new Dictionary<string, int> { { "Wolpertinger", 1 }, { "Unicorn", 2 }, { "Waldschrat", 3 } };
        var economy = new ParkEconomy(100000);
        var park = new ParkProgression(rules, economy, buildings, ratings, () => 0d);
        Check(park.Region("western-europe").species.Where(ratings.ContainsKey).SequenceEqual(ratings.Keys),
            "Western Europe retains the original three species IDs and their mission tie order alongside additions");
        Check(park.BuyAccess(home, "western-europe") && park.BuyTeam(home), "A player can buy Western Europe access and a team");
        string[] difficulties = { "survey", "capture", "dangerous" };
        int index = 0;
        foreach (string id in ratings.Keys)
        {
            string difficulty = difficulties[index++];
            Check(park.Stock(id) == 0, "New mythical creatures start with no free stock: " + id);
            Check(park.Launch(home, "western-europe", difficulty, 1), "A normal mission can be dispatched for " + id);
            var mission = park.Missions.Single();
            Check(mission.species == id && mission.succeeds, "Tier filtering can select the intended species: " + id);
            var snapshot = park.Capture(buildings.Records);
            park = new ParkProgression(rules, economy, buildings, ratings, () => 0d);
            park.Restore(snapshot, buildings.Records);
            Check(park.Missions.Single().species == id && park.Stock(id) == 0, "An active expedition survives saving without an early reward");
            park.Tick(mission.remaining + 1);
            Check(park.Stock(id) == mission.quantity && park.AvailableTeams == 1, "Successful mission grants stock and returns its team: " + id);
            Check(!park.TryPlace(id, () => false) && park.Stock(id) == mission.quantity, "Failed placement preserves the specimen: " + id);
            Check(park.TryPlace(id, () => true) && park.Stock(id) == mission.quantity - 1, "Successful placement consumes one specimen: " + id);
        }
        Console.WriteLine("PASS: " + checks + " Western Europe expedition, save and specimen-placement checks.");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class AquaticProgressionChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 14, 8);
        var network = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        network.TryPlace(new BuildingFootprint(new BuildingDefinition
            { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 2), 0));
        var centre = network.TryPlace(new BuildingFootprint(new BuildingDefinition
            { kind = BuildingKind.ExpeditionCentre, width = 1, depth = 1 }, new Vector2Int(3, 2), 0));
        for (int x = 1; x < 5; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
        network.Refresh(); Check(centre != null && centre.IsOpen, "The expedition centre has ordinary connected access");
        var rules = new ProgressionRules();
        var ratings = new Dictionary<string, int> { { "Kappa", 2 }, { "Nokk", 3 } };
        var economy = new ParkEconomy(100000);
        var park = new ParkProgression(rules, economy, network, ratings, () => 0d);
        Check(park.Region("east-asia").species.Contains("Kappa") && !park.Region("east-asia").species.Contains("Nokk"),
            "Kappa is discoverable in East Asia without moving the Nordic species there");
        Check(park.Region("northern-europe").species.Contains("Nokk") && !park.Region("northern-europe").species.Contains("Kappa"),
            "Nokk is discoverable in Northern Europe");
        Check(park.Stock("Kappa") == 0 && park.Stock("Nokk") == 0, "Neither species grants free starting specimens");
        Check(park.Eligible("east-asia", "survey").SequenceEqual(new[] { "Kappa" }), "Survey missions accept the rating-two Kappa");
        Check(park.Eligible("northern-europe", "survey").Count == 0, "Survey missions cannot grant a rating-three Nokk");
        Check(park.Eligible("northern-europe", "capture").SequenceEqual(new[] { "Nokk" }), "Capture missions can grant a Nokk");
        Check(park.BuyTeam(centre), "An ordinary expedition team can be purchased");
        foreach (var request in new[] { new[] { "east-asia", "survey", "Kappa" }, new[] { "northern-europe", "capture", "Nokk" } })
        {
            Check(park.BuyAccess(centre, request[0]), "The appropriate region can be unlocked: " + request[0]);
            Check(park.Launch(centre, request[0], request[1], 1), "A mission launches through the existing expedition flow");
            var mission = park.Missions.Single();
            Check(mission.species == request[2] && mission.succeeds, "The expedition selects the region's eligible aquatic species");
            var saved = park.Capture(network.Records);
            park = new ParkProgression(rules, economy, network, ratings, () => 0d); park.Restore(saved, network.Records);
            Check(park.Missions.Single().species == request[2] && park.Stock(request[2]) == 0,
                "An active aquatic expedition restores without an early inventory reward");
            park.Tick(mission.remaining + 1);
            Check(park.Stock(request[2]) == mission.quantity && park.AvailableTeams == 1,
                "The completed mission adds its specimens and returns the team");
            Check(!park.TryPlace(request[2], () => false) && park.Stock(request[2]) == mission.quantity,
                "A blocked placement preserves the captured specimen");
            Check(park.TryPlace(request[2], () => true) && park.Stock(request[2]) == mission.quantity - 1,
                "Successful placement consumes one specimen");
        }
        var oldPark = new ParkProgression(rules, economy, network, new Dictionary<string, int> { { "Goat", 1 } }, () => 0d);
        var oldSave = oldPark.Capture(network.Records);
        var upgraded = new ParkProgression(rules, economy, network, ratings, () => 0d); upgraded.Restore(oldSave, network.Records);
        Check(upgraded.Stock("Kappa") == 0 && upgraded.Stock("Nokk") == 0,
            "An older park with no aquatic inventory remains valid and does not duplicate specimens");
        Check(upgraded.RegionHasCreatures("east-asia") && upgraded.RegionHasCreatures("northern-europe"),
            "The existing park can discover both newly populated regions after an upgrade");
        Console.WriteLine("PASS: " + checks + " aquatic expedition eligibility, inventory, placement and old-save progression checks.");
    }
}

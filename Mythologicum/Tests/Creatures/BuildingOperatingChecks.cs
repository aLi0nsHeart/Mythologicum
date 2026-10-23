using System;
using Mythologicum;
using UnityEngine;

public static class BuildingOperatingChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 15, 10);
        var network = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        var entrance = new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 };
        var dispenser = new BuildingDefinition { kind = BuildingKind.GoatDispenser, width = 1, depth = 1 };
        var kiosk = new BuildingDefinition { kind = BuildingKind.Kiosk, width = 1, depth = 1 };
        Check(dispenser.dispenseInterval == 20f, "Slower dispenser default");
        var gate = network.TryPlace(new BuildingFootprint(entrance, new Vector2Int(1, 2), 0));
        var farm = network.TryPlace(new BuildingFootprint(dispenser, new Vector2Int(6, 2), 0));
        var shop = network.TryPlace(new BuildingFootprint(kiosk, new Vector2Int(10, 2), 0));
        Check(farm != null && farm.IsOpen, "Dispenser opens without paths");
        Check(!gate.IsOpen && !shop.IsOpen, "Ordinary buildings still require paths");
        for (int x = 1; x <= 10; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
        network.Refresh();
        Check(gate.IsOpen && farm.IsOpen && shop.IsOpen, "Connected buildings open");
        grid.TryRemove(new Vector2Int(4, 1), BuildCellContent.Path);
        network.Refresh();
        Check(farm.IsOpen, "Path demolition cannot close dispenser");
        Check(!shop.IsOpen, "Path demolition still closes disconnected kiosk");
        network.Remove(gate);
        Check(farm.IsOpen, "Dispenser operates independently of park entrance reachability");
        Console.WriteLine("PASS: " + checks + " building operation checks.");
    }
}

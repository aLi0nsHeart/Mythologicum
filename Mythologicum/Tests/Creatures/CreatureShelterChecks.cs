using System;
using System.Collections.Generic;
using System.IO;
using Mythologicum;
using UnityEngine;

public static class CreatureShelterChecks
{
    private static int checks;
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
    private static void Reject(Action action, string why)
    { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, why); }
    public static void Main()
    {
        Check((int)CreatureShelterMode.Legacy == 0 && (int)CreatureShelterMode.Burrow == 5 && (int)CreatureShelterMode.Cave == 6,
            "Shelter modes append stable species values and preserve legacy profiles");
        var settings = new CreatureShelterSettings { mode = (CreatureShelterMode)99, searchRadius = float.NaN,
            retreatSafeDistance = -4, treeHeight = float.PositiveInfinity, calmFearThreshold = 500, fearRecoveryMultiplier = -.5f };
        settings.Validate();
        Check(settings.mode == CreatureShelterMode.Ground && settings.searchRadius >= .1f && settings.retreatSafeDistance == 0
            && settings.treeHeight == 0 && settings.calmFearThreshold == 100 && settings.fearRecoveryMultiplier == 1,
            "Invalid designer shelter values become finite and bounded");
        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 12);
        var sites = new CreatureShelterRegistry(grid, 3, 1);
        Check(sites.CaveCapacity == 3 && sites.BurrowCapacity == 1, "Caves can share a herd while small burrows have one resting place");
        var cave = new Vector2Int(3, 3); var burrow = new Vector2Int(7, 8);
        Check(sites.TryAdd(cave, ConstructedShelterKind.Cave) && sites.Contains(cave, ConstructedShelterKind.Cave), "A cave is a persistent typed physical site");
        Check(grid.IsAvailable(cave) && grid.GroundWalkable(cave), "Shelter entrances remain traversable ground rather than buildings");
        Check(!sites.TryAdd(cave, ConstructedShelterKind.Burrow), "Two shelters cannot overlap one feature tile");
        Check(sites.TryAdd(burrow, ConstructedShelterKind.Burrow) && sites.Contains(burrow, ConstructedShelterKind.Burrow), "A dug home persists independently of its creature");
        Check(!sites.TryAdd(new Vector2Int(-1, 2), ConstructedShelterKind.Cave), "Shelters stay within map bounds");
        var paved = new Vector2Int(2, 2); grid.TryOccupy(paved, BuildCellContent.Path);
        Check(!sites.TryAdd(paved, ConstructedShelterKind.Cave), "A public path cannot be replaced by a cave");
        var building = new Vector2Int(5, 5); grid.TryOccupy(building, BuildCellContent.Building);
        Check(!sites.TryAdd(building, ConstructedShelterKind.Burrow), "Buildings cannot contain dug sleeping burrows");
        var snapshot = sites.Capture(); CreatureShelterRegistry.ValidateSaved(snapshot, grid);
        Check(snapshot.Count == 2 && snapshot[0].cell == cave && snapshot[1].cell == burrow, "Saved shelters are deterministic and include caves and abandoned homes");
        var copy = new CreatureShelterRegistry(grid);
        foreach (var entry in snapshot) Check(copy.TryAdd(entry.cell, entry.kind), "Typed home records restore without creature ownership");
        Check(copy.Contains(burrow, ConstructedShelterKind.Burrow) && copy.Contains(cave, ConstructedShelterKind.Cave), "Restore preserves each kind");
        Reject(() => CreatureShelterRegistry.ValidateSaved(new[] { snapshot[0], snapshot[0] }, grid), "Duplicate saved sites are rejected");
        Reject(() => CreatureShelterRegistry.ValidateSaved(new[] { new SavedShelter { cell = cave, kind = (ConstructedShelterKind)9 } }, grid), "Unknown saved shelter kinds are rejected");
        Reject(() => CreatureShelterRegistry.ValidateSaved(new[] { new SavedShelter { cell = new Vector2Int(12, 0) } }, grid), "Out-of-bounds saved homes are rejected");
        Reject(() => CreatureShelterRegistry.ValidateSaved(new[] { new SavedShelter { cell = paved } }, grid), "Saved caves cannot cover paths");
        Reject(() => CreatureShelterRegistry.ValidateSaved(new SavedShelter[] { null }, grid), "Null saved shelter entries are rejected");
        int before = sites.Revision;
        Check(sites.TryRemove(cave) && sites.Revision == before + 1 && !sites.Occupies(cave), "Removing a cave updates the registry once");
        Check(!sites.TryRemove(cave) && sites.Contains(burrow, ConstructedShelterKind.Burrow), "Removing a cave does not delete burrow homes");
        grid.Habitat.PaintBiome(new Vector2(7.5f, 8.5f), .75f, HabitatBiome.Stone, null, out _);
        Check(!CreatureBurrowing.CanDigGround(grid, burrow), "Stone ground invalidates sleeping and escape burrowing");
        Check(sites.Contains(burrow, ConstructedShelterKind.Burrow), "A changed surface does not silently delete a persistent mound");
        grid.Habitat.PaintBiome(new Vector2(7.5f, 8.5f), .75f, HabitatBiome.Dirt, null, out _);
        Check(CreatureBurrowing.CanDigGround(grid, burrow), "Dirt ground makes the existing home usable again");
        grid.Habitat.TrySetWater(new Vector2Int(8, 8), 1);
        Check(!CreatureBurrowing.CanDigGround(grid, new Vector2Int(8, 8)), "Active water cannot host a land burrow");
        Check(!sites.TryAdd(new Vector2Int(8, 8), ConstructedShelterKind.Cave), "Caves cannot be added below standing water");
        Console.WriteLine("PASS: " + checks + " shelter settings, persistent sites, shared capacity, soil and save checks.");
    }
}

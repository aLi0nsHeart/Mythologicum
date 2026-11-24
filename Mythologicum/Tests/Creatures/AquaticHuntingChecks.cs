using System;
using Mythologicum;
using UnityEngine;

public static class AquaticHuntingChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 10);
        var definition = (CreatureDefinition)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CreatureDefinition));
        definition.Aquatic.canSwim = definition.Aquatic.dragPreyToWater = true;
        var water = new Vector2Int(6, 4); var bank = new Vector2Int(5, 4);
        Check(grid.Habitat.TrySetWater(water, 1), "fixture has real water");
        var saved = new AquaticHuntSave { phase = CreatureActivity.DraggingPrey, waterCell = water, releaseCell = bank, remaining = 0 };
        Check(CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "a living drag has a legal water target and rescue bank");
        foreach (var phase in new[] { CreatureActivity.Attacking, CreatureActivity.DraggingPrey, CreatureActivity.DrowningPrey, CreatureActivity.Feeding })
        { saved.phase = phase; Check(CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "all explicit capture phases can persist"); }
        saved.phase = CreatureActivity.Hunting;
        Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "ordinary pursuit is not a persisted capture");
        saved.phase = CreatureActivity.DrowningPrey;
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
        { saved.remaining = invalid; Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "invalid timers cannot enter saves"); }
        saved.remaining = 2;
        saved.releaseCell = water;
        Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "a land victim needs a dry rescue bank");
        saved.releaseCell = bank; saved.waterCell = bank;
        Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "feeding and drowning require actual water");
        saved.waterCell = water; definition.Aquatic.canSwim = false;
        Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "ordinary land species cannot restore aquatic predation");
        definition.Aquatic.canSwim = true; definition.Aquatic.dragPreyToWater = false;
        Check(!CreatureWorld.ValidAquaticHuntSave(grid, definition, saved), "optional swimming does not itself enable live dragging");
        Console.WriteLine($"PASS: {checks} aquatic capture persistence and water/bank validation checks.");
    }
}

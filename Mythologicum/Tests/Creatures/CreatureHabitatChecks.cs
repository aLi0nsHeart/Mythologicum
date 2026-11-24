using System;
using Mythologicum;
using UnityEngine;

public static class CreatureHabitatChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static bool Near(float a, float b) => Math.Abs(a - b) < .001f;
    public static void Main()
    {
        var preferences = new CreatureHabitatSettings { preferences = new[]
        {
            new CreatureBiomePreference { biome = HabitatBiome.Grass, minimumFraction = .4f },
            new CreatureBiomePreference { biome = HabitatBiome.Thicket, minimumFraction = .2f }
        }};
        var fractions = new[] { .4f, .4f, .2f, 0, 0 };
        Check(Near(preferences.Evaluate(fractions), 100), "Both configured ground needs can be satisfied together");
        fractions[2] = 0;
        Check(Near(preferences.Evaluate(fractions), 50), "Excess grass does not erase a missing thicket requirement");
        fractions[0] = .2f;
        Check(Near(preferences.Evaluate(fractions), 25), "Partially met needs contribute proportionally");
        preferences.preferences = new[] { new CreatureBiomePreference { biome = HabitatBiome.Stone, maximumFraction = .2f } };
        Check(Near(preferences.Evaluate(new[] { 0f, 0f, 0f, 1f, 0f }), 0), "An entirely unsuitable stone habitat is unsatisfying");
        Check(Near(preferences.Evaluate(new[] { 0f, 0f, 0f, .2f, 0f }), 100), "A species can tolerate a limited fraction of stone");
        Check(Near(new CreatureHabitatSettings().Evaluate(null), 100), "Species without ground preferences remain neutral");
        preferences.preferences[0].weight = float.NaN; preferences.Validate();
        Check(Near(preferences.Evaluate(fractions), 100), "Invalid or disabled weights do not corrupt scores");

        var weights = new CreatureBehaviourSettings { hungerWeight = 0, fearWeight = 0, environmentWeight = 0,
            spaceWeight = 0, herdWeight = 0, thirstWeight = 1 };
        var hydrated = CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 0, 0, 0, -1, weights, thirst: 0, needsWater: true);
        var thirsty = CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 0, 0, 0, -1, weights, thirst: 100, needsWater: true);
        Check(Near(hydrated.Happiness, 100) && Near(thirsty.Happiness, 0), "Thirst affects happiness through its editable weight");
        Check(Near(CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 0, 0, 0, -1, weights, thirst: 100, needsWater: false).Happiness, 100),
            "Water-free species are not penalized for hydration");
        weights.thirstWeight = 0; weights.environmentWeight = 1;
        Check(Near(CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 0, 0, 0, -1, weights, biomeScore: 40, shelterScore: 80).Environment, 60),
            "Ground suitability and shelter both contribute to environmental satisfaction");

        var grid = new SquareBuildGrid(Vector3.zero, 2, 8, 8, new GridHeightField(8, 8, 2));
        var grazing = new GrazingField(grid); var cell = new Vector2Int(2, 2);
        Check(grazing.HasFood(cell, 0), "Unpainted grass remains edible for older parks");
        Check(grid.Habitat.PaintBiome(new Vector2(2.5f, 2.5f), .6f, HabitatBiome.Stone, _ => true, out _), "A stone patch can be painted");
        Check(!grazing.HasFood(cell, 0), "Stone is not a source of grazing food");
        grid.Habitat.PaintBiome(new Vector2(2.5f, 2.5f), .6f, HabitatBiome.Thicket, _ => true, out _);
        Check(grazing.HasFood(cell, 0), "Thicket supplies vegetation to herbivores");
        var before = grid.Habitat.Capture();
        grid.Habitat.PaintBiome(new Vector2(2.5f, 2.5f), .6f, HabitatBiome.Sand, _ => true, out _);
        var after = grid.Habitat.Capture(); var patch = new HabitatUndoPatch(before, after);
        Check(patch.Count == 1, "One biome edit produces one-cell undo metadata");
        grid.Habitat.PaintBiome(new Vector2(5.5f, 5.5f), .6f, HabitatBiome.Dirt, _ => true, out _);
        Check(patch.TryRevert(grid.Habitat.Capture(), out var reverted), "Later changes to unrelated cells permit safe Undo");
        grid.Habitat.Restore(reverted);
        Check(grid.Habitat.BiomeAt(cell) == HabitatBiome.Thicket && grid.Habitat.BiomeAt(new Vector2Int(5, 5)) == HabitatBiome.Dirt,
            "Undo preserves later ground edits elsewhere");
        grid.Habitat.Restore(after); grid.Habitat.PaintBiome(new Vector2(2.5f, 2.5f), .6f, HabitatBiome.Dirt, _ => true, out _);
        Check(!patch.TryRevert(grid.Habitat.Capture(), out _), "Undo refuses to overwrite a changed edited cell");
        var dry = grid.Habitat.Capture(); Check(grid.Habitat.TrySetWater(new Vector2Int(3, 3), 1), "A pond surface can be set in a pure fixture");
        var waterPatch = new HabitatUndoPatch(dry, grid.Habitat.Capture());
        Check(!grazing.HasFood(new Vector2Int(3, 3), 0), "Underwater terrain cannot be grazed by land creatures");
        var navigation = new CreatureNavigation(grid, new FenceLayout(grid));
        Check(!navigation.IsWalkable(new Vector2Int(3, 3)) && navigation.HasLineOfSight(new Vector2Int(1, 3), new Vector2Int(5, 3)),
            "Water blocks land movement while remaining transparent to sight");
        Check(waterPatch.TryRevert(grid.Habitat.Capture(), out reverted), "Water surface edits can be reversed");
        grid.Habitat.Restore(reverted);
        Check(!grid.Habitat.HasWaterSurface(new Vector2Int(3, 3)) && navigation.IsWalkable(new Vector2Int(3, 3)),
            "Undo removes the surface and restores land navigation");
        Console.WriteLine("PASS: " + checks + " habitat preferences, hydration happiness, grazing, sight and safe habitat Undo checks.");
    }
}

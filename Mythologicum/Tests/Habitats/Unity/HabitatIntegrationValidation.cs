using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Terrain habitats, care objects, real creature needs and production scene persistence.</summary>
[InitializeOnLoad]
public static class HabitatIntegrationValidation
{
    private const string Running = "HabitatIntegrationValidation.running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator steps;
    private static double started;
    private static int checks;
    private static string slot;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "HabitatIntegrationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("HABITAT INTEGRATION PASS: " + message); }
    static HabitatIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); checks = 0; started = EditorApplication.timeSinceStartup; steps = Run();
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "HabitatIntegrationValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (steps == null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 240 || SceneTransition.HasFailed) throw new Exception("Habitat park preparation failed or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup(); File.WriteAllText(Result, "PASS: " + checks + " habitat terrain, drinking, refill care, shelters, UI, Undo and production scene save/load checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        { steps = null; Cleanup(); File.WriteAllText(Result, "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 50, depth = 50, seed = 781335, relief = 0, forestDensity = 0, featureSize = 28 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        park.Economy.UnlimitedFunds = true; park.Visitors.Definition.maximumVisitors = 0;
        Call(park, "SelectTool", Enum.ToObject(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), 0));
        foreach (var name in new[] { "Enrichment trees", "Natural rocks" })
        {
            var group = GameObject.Find(name);
            if (group != null) foreach (var target in group.GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        }
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var grid = Field<SquareBuildGrid>(park, "grid"); var buildings = Field<BuildingConstruction>(park, "buildingConstruction");
        var paths = Field<PathConstruction>(park, "paths"); var world = Field<CreatureWorld>(park, "creatureWorld");
        var fences = Field<FenceConstruction>(park, "fences");
        var troughs = Field<WaterTroughConstruction>(park, "waterTroughs"); var shelters = Field<ShelterConstruction>(park, "shelters");
        var definitions = Field<BuildingDefinition[]>(park, "buildingCatalog");
        ParkBuildingRecord Place(BuildingKind kind, Vector2Int cell)
        {
            var definition = definitions.Single(item => item.kind == kind).Copy(); definition.width = definition.depth = 1; definition.entranceColumn = 0;
            Check(buildings.TryPlace(new BuildingFootprint(definition, cell, 0)), "The habitat fixture can place " + kind);
            return buildings.Network.Records.Last();
        }
        Place(BuildingKind.ParkEntrance, new Vector2Int(2, 2));
        for (int x = 1; x < 18; x++) paths.TryPlace(new Vector2Int(x, 1));
        var keeperHome = Place(BuildingKind.ZookeeperHut, new Vector2Int(4, 2)); buildings.Network.Refresh();
        Check(buildings.Network.Entrance.IsOpen && keeperHome.IsOpen, "The habitat park begins with an ordinary connected entrance and keeper house");
        for (int x = 5; x < 11; x++)
        {
            Check(fences.TryPlace(new FenceEdge(new Vector2Int(x, 6), true)), "The care enclosure has a real lower fence");
            Check(fences.TryPlace(new FenceEdge(new Vector2Int(x, 12), true)), "The care enclosure has a real upper fence");
        }
        for (int y = 6; y < 12; y++)
        {
            Check(fences.TryPlace(new FenceEdge(new Vector2Int(5, y), false)), "The care enclosure has a real left fence");
            Check(fences.TryPlace(new FenceEdge(new Vector2Int(11, y), false)), "The care enclosure has a real right fence");
        }
        Check(fences.TryPlaceGate(new FenceEdge(new Vector2Int(7, 6), true)), "A staff gate provides physical keeper access to the enclosure");

        var caveCell = new Vector2Int(12, 8);
        Call(park, "BeginConstructionAction", "habitat.cave.place");
        Check(shelters.TryPlaceCave(caveCell), "A cave is a placeable enclosure object"); Call(park, "CommitConstructionAction", 0f);
        Check(park.UndoLastConstruction() && !shelters.Occupies(caveCell), "Unused cave construction can be undone");
        Check(shelters.TryPlaceCave(caveCell), "The cave can be replaced after Undo");
        var troughCell = new Vector2Int(8, 8);
        Check(!troughs.CanPlace(caveCell), "A trough cannot overlap an existing cave");
        Call(park, "BeginConstructionAction", "habitat.trough.place");
        Check(troughs.TryPlace(troughCell), "A water trough can be placed independently of public paths"); Call(park, "CommitConstructionAction", 0f);
        Check(!shelters.CanPlaceCave(troughCell), "A cave cannot overlap a water trough");

        CreatureDefinition Profile(string name, bool sleepy)
        {
            var profile = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/" + name)); profile.name = name;
            profile.needsFood = false; profile.hungerPerSecond = 0; profile.initialHunger = 0; profile.threat = profile.damage = 0;
            profile.energyDrainPerSecond = 0; profile.initialEnergy = sleepy ? 0 : 100; profile.sleepRecoveryPerSecond = 0;
            profile.Thirst.needsWater = !sleepy; profile.Thirst.initialThirst = sleepy ? 0 : 90; profile.Thirst.thirstPerSecond = 0;
            profile.Thirst.drinkThreshold = 40; profile.Thirst.drinkReliefPerSecond = 25; profile.Thirst.drinkSeconds = 3;
            profile.fearRange = 0; profile.minimumSpace = 0; profile.walkSpeed = profile.runSpeed = 8; profile.decisionInterval = .05f;
            profile.minimumCompanions = 0; profile.maximumCompanions = -1;
            profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
            return profile;
        }
        var goatProfile = Profile("Goat", false);
        Check(world.TrySpawn(goatProfile, new Vector2Int(6, 8)), "A real living creature begins with a water need");
        var goat = world.Agents.Last();
        for (int step = 0; step < 300 && goat.Vitals.Thirst > 50; step++) { world.Tick(.05f); troughs.Refresh(); }
        Check(goat.Vitals.Thirst <= 50 && troughs.RecordAt(troughCell).Stock < troughs.RecordAt(troughCell).Capacity,
            "Creature thirst drives actual movement and finite trough consumption");
        Check(!park.CanUndoLastConstruction(out _), "Consumed trough stock protects the construction from Undo");
        Call(park, "ResetConstructionHistory", "park.build_mode_controller__history.loaded_history");
        goat.Vitals.Restore(goat.Vitals.Hunger, goat.Vitals.Fear, 0);
        var trough = troughs.RecordAt(troughCell); trough.Restore(2, 0);
        Check(park.Personnel.TryHire(keeperHome), "The production keeper service can hire a worker for water care");
        for (int step = 0; step < 1000 && trough.Expenses < trough.Definition.refillCost; step++)
        { world.Tick(.05f); park.Personnel.Tick(.05f); troughs.Refresh(); }
        Check(trough.Expenses == trough.Definition.refillCost && trough.Stock == trough.Capacity,
            "The configured park keeper enters through staff access and refills its actual trough");
        var keeper = park.Personnel.Staff.Single();
        Check(keeper.Actor.Cell.x >= 5 && keeper.Actor.Cell.x < 11 && keeper.Actor.Cell.y >= 6 && keeper.Actor.Cell.y < 12,
            "A trough refill happens inside the physical enclosure");
        for (int step = 0; step < 20; step++) { world.Tick(.05f); park.Personnel.Tick(.05f); }
        Check(trough.Expenses == trough.Definition.refillCost, "The completed production refill does not deliver or charge a second time");
        trough.Consume(7.25f); troughs.Refresh();

        Predicate<Vector2Int> waterEditable = cell => (bool)Call(park, "HabitatWaterEditable", cell);
        Check(!grid.Habitat.AddWater(new Vector2(3.5f, 1.5f), .6f, grid.Origin.y, 1, waterEditable, out _),
            "Water painting cannot flood a public path");
        Check(!grid.Habitat.AddWater(new Vector2(caveCell.x + .5f, caveCell.y + .5f), .6f, grid.Origin.y, 1, waterEditable, out _),
            "Water painting cannot flood a constructed cave");
        Check(!grid.Habitat.AddWater(new Vector2(troughCell.x + .5f, troughCell.y + .5f), .6f, grid.Origin.y, 1, waterEditable, out _),
            "Water painting cannot flood a water trough");
        Check(!grid.Habitat.AddWater(new Vector2(goat.Cell.x + .5f, goat.Cell.y + .5f), .6f, grid.Origin.y, 1, waterEditable, out _),
            "Water painting cannot flood a living creature's occupied cell");
        var temporaryPond = new Vector2(29.5f, 29.5f);
        Call(park, "BeginConstructionAction", "park.build_mode_controller__terrain_editing.edit_terrain");
        Check(grid.Habitat.AddWater(temporaryPond, 2, grid.Origin.y, 1, waterEditable, out _), "A water stroke carves a bounded basin");
        Call(park, "CommitConstructionAction", 0f);
        Check(park.UndoLastConstruction() && !grid.Habitat.HasWaterSurface(new Vector2Int(29, 29))
            && Mathf.Abs(grid.GroundHeight(grid.CellCenter(new Vector2Int(29, 29))) - grid.Origin.y) < .001f,
            "Undo removes both the pond surface and its excavation");
        foreach (HabitatBiome biome in Enum.GetValues(typeof(HabitatBiome)))
        {
            var cell = new Vector2Int(18 + (int)biome * 3, 23);
            Check(grid.Habitat.PaintBiome(new Vector2(cell.x + .5f, cell.y + .5f), .6f, biome, _ => true, out _)
                && grid.Habitat.BiomeAt(cell) == biome, "Terraform can author the visible ground type " + biome);
        }
        var originalGround = grid.Habitat.Capture();
        goatProfile.minimumOpenGroundFraction = .5f;
        grid.Habitat.PaintBiome(new Vector2(8, 9), 10, HabitatBiome.Stone,
            cell => cell.x >= 5 && cell.x < 11 && cell.y >= 6 && cell.y < 12, out _);
        world.Tick(.6f);
        Check(goat.NearbyOpenGroundFraction < .001f, "Painted stone cannot satisfy the existing open-meadow need");
        grid.Habitat.Restore(originalGround); world.Tick(.6f); goatProfile.minimumOpenGroundFraction = 0;
        Check(goat.NearbyOpenGroundFraction > .9f, "Restoring grass restores the accessible meadow habitat");
        Check(grid.Habitat.AddWater(new Vector2(21.5f, 17.5f), 2, grid.Origin.y, 1, waterEditable, out _), "The production park contains a carved natural pond");
        Check(grid.Habitat.WaterCells.Any() && grid.Habitat.WaterCells.All(cell => !grid.GroundWalkable(cell)),
            "Pond water has separate surface levels and excludes ordinary land travel");

        var wolpertingerProfile = Profile("Wolpertinger", true); var oniProfile = Profile("Oni", true);
        Check(world.TrySpawn(wolpertingerProfile, new Vector2Int(16, 9)), "A Wolpertinger can seek its own persistent shelter"); var wolpertinger = world.Agents.Last();
        Check(world.TrySpawn(oniProfile, new Vector2Int(13, 8)), "An Oni can seek the constructed cave"); var oni = world.Agents.Last();
        for (int step = 0; step < 300 && (!wolpertinger.InBurrow || oni.Activity != CreatureActivity.Sleeping); step++) world.Tick(.05f);
        Check(wolpertinger.InBurrow && wolpertinger.Activity == CreatureActivity.Sleeping
            && world.CaptureValidShelterHome(wolpertinger).HasValue, "An exhausted Wolpertinger digs and sleeps in a reusable mound");
        Check(oni.Activity == CreatureActivity.Sleeping && world.CaptureValidShelterHome(oni) == caveCell && world.IsHiddenInShelter(oni),
            "An exhausted Oni physically reaches and sleeps in its cave");

        var palette = Field<ConstructionPalette>(park, "palette"); palette.Refresh(true); palette.Toggle(ConstructionCategory.Enclosures);
        yield return null;
        var labels = palette.Root.GetComponentsInChildren<Text>(true).Select(item => item.text).ToArray();
        Check(labels.Any(text => text.IndexOf("Water trough", StringComparison.OrdinalIgnoreCase) >= 0)
            && labels.Any(text => text.IndexOf("Cave", StringComparison.OrdinalIgnoreCase) >= 0),
            "The enclosure construction menu exposes water troughs and cave shelters");
        palette.Close();

        var saved = park.CapturePark(); park.ValidateSave(saved);
        Check(saved.hasHabitat && saved.habitat.biomes.Count == 5 && saved.habitat.water.Count > 0,
            "The park save explicitly records all authored biomes and pond surfaces");
        Check(saved.waterTroughs.Count == 1 && saved.shelters.Count == 2 && saved.creatures.Count == 3,
            "The park save contains separate trough, cave, mound and creature state");
        Check(saved.staff.Count == 1 && saved.waterTroughs.Single().expenses == trough.Definition.refillCost,
            "The saved care park retains its keeper and completed refill expense");
        Check(saved.creatures.Single(item => item.species == "Wolpertinger").hasShelterHome
            && saved.creatures.Single(item => item.species == "Wolpertinger").shelterSleeping
            && saved.creatures.Single(item => item.species == "Oni").shelterHome == caveCell,
            "Persistent burrow and cave homes retain their actual sleeping association");
        var invalid = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        invalid.shelters.Add(new SavedShelter { cell = troughCell, kind = ConstructedShelterKind.Cave }); bool rejected = false;
        try { park.ValidateSave(invalid); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Save validation rejects overlapping cave and water-trough features");
        var legacy = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        legacy.hasHabitat = false; legacy.habitat = null; legacy.waterTroughs.Clear(); legacy.shelters.Clear(); legacy.creatures.Clear();
        park.ValidateSave(legacy); Check(true, "Older parks without optional habitat data remain valid");

        checks += CreatureWaterNativeChecks.RunChecks();
        checks += CreatureShelterNativeChecks.RunChecks();
        checks += HabitatTerrainNativeChecks.RunChecks();
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "Habitat integration"); ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Object.Destroy(goatProfile); Object.Destroy(wolpertingerProfile); Object.Destroy(oniProfile);
        Check(SceneTransition.Load("GameScene"), "The habitat park uses the production scene-loading path");
        while (SceneTransition.IsLoading) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0; park.Visitors.Definition.maximumVisitors = 0;
        var restored = park.CapturePark(); park.ValidateSave(restored);
        Check(restored.hasHabitat && restored.habitat.biomes.Count == saved.habitat.biomes.Count
            && restored.habitat.water.Count == saved.habitat.water.Count, "Scene loading preserves all ground types and authored water levels");
        Check(restored.staff.Count == 1 && park.Personnel.Staff.Count == 1,
            "Scene loading retains the keeper that serviced the water trough");
        var restoredGrid = Field<SquareBuildGrid>(park, "grid");
        foreach (var water in saved.habitat.water)
            Check(Mathf.Abs(restoredGrid.Habitat.WaterLevel(water.cell) - (restoredGrid.Origin.y + water.level)) < .001f,
                "A saved pond surface keeps its exact shared elevation");
        Check(Mathf.Abs(restored.waterTroughs.Single().stock - saved.waterTroughs.Single().stock) < .001f
            && restored.waterTroughs.Single().expenses == saved.waterTroughs.Single().expenses,
            "Water-trough supply and expense state survive a real scene load");
        Check(Mathf.Abs(restored.creatures.Single(item => item.species == "Goat").thirst
            - saved.creatures.Single(item => item.species == "Goat").thirst) < .001f, "Individual creature thirst survives a real scene load");
        var restoredWorld = Field<CreatureWorld>(park, "creatureWorld");
        var restoredWolpertinger = restoredWorld.Agents.Single(item => !Field<bool>(item, "ExternallyControlled") && item.Definition.name == "Wolpertinger");
        var restoredOni = restoredWorld.Agents.Single(item => !Field<bool>(item, "ExternallyControlled") && item.Definition.name == "Oni");
        Check(restoredWolpertinger.InBurrow && restoredWolpertinger.Activity == CreatureActivity.Sleeping
            && restoredWorld.CaptureValidShelterHome(restoredWolpertinger) == saved.creatures.Single(item => item.species == "Wolpertinger").shelterHome,
            "The loaded Wolpertinger resumes inside its saved persistent burrow");
        Check(restoredOni.Activity == CreatureActivity.Sleeping && restoredWorld.IsHiddenInShelter(restoredOni)
            && restoredWorld.CaptureValidShelterHome(restoredOni) == caveCell, "The loaded Oni resumes shelter use in the saved cave");
    }
    private static void Cleanup()
    {
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Cleanup escaped the owned habitat test slot.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

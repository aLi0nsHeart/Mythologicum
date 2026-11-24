using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Disposable Play-mode validation only: real park construction, disk save and scene restoration.</summary>
[InitializeOnLoad]
public static class NorthAmericanExpansionValidation
{
    private const string Running = "NorthAmericanExpansionValidation.running";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] Ids = { "Wendigo", "Thunderbird", "Kalupelik" };
    private static readonly List<CreatureDefinition> profiles = new List<CreatureDefinition>();
    private static IEnumerator steps;
    private static double started;
    private static int checks;
    private static string slot;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "NorthAmericanExpansionValidationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Members).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Members).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] arguments)
        => owner.GetType().GetMethod(name, Members).Invoke(owner, arguments);
    private static float SavedFloat(SavedActor actor, string name) => Field<float>(actor, name);
    private static void Check(bool value, string why)
    { if (!value) throw new Exception(why); checks++; Debug.Log("NORTH AMERICA PASS: " + why); }
    private static Vector2Int C(int x, int y) => new Vector2Int(x, y);

    static NorthAmericanExpansionValidation()
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
        PlayerSettings.productName = "NorthAmericanExpansionValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (steps == null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 300 || SceneTransition.HasFailed)
                throw new Exception("North American production validation preparation failed or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup();
            File.WriteAllText(Result, "PASS: " + checks + " imported creature, flight, stalking, luring, completed cage/pool, breach, construction Undo, specimen/research and production disk/scene save checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            steps = null;
            try { Cleanup(); } catch (Exception cleanup) { Debug.LogException(cleanup); }
            File.WriteAllText(Result, "FAIL after " + checks + " checks: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private static List<Vector2Int> Box(int x, int y, int width, int depth)
    {
        var cells = new List<Vector2Int>();
        for (int z = y; z < y + depth; z++) for (int a = x; a < x + width; a++) cells.Add(C(a, z));
        return cells;
    }
    private static List<FenceEdge> Boundary(int x, int y, int width, int depth)
    {
        var result = new List<FenceEdge>();
        for (int a = x; a < x + width; a++)
        { result.Add(new FenceEdge(C(a, y), true)); result.Add(new FenceEdge(C(a, y + depth), true)); }
        for (int z = y; z < y + depth; z++)
        { result.Add(new FenceEdge(C(x, z), false)); result.Add(new FenceEdge(C(x + width, z), false)); }
        return result;
    }
    private static bool RoofVisible() => Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Any(mesh => mesh != null && mesh.name.StartsWith("Complete aviary roof", StringComparison.Ordinal)
            && mesh.sharedMesh != null && mesh.sharedMesh.vertexCount > 0);
    private static void Refresh(BuildModeController park)
    {
        Call(park, "RefreshSpecialEnclosures");
        Call(park, "RefreshTerrainAttachments"); Physics.SyncTransforms();
    }
    private static void Research(BuildModeController park, ParkBuildingRecord laboratory, string id)
    {
        if (park.Progression.Completed(id)) return;
        Check(park.Progression.StartResearch(laboratory, id), "ordinary technology research starts " + id);
        park.Progression.AdvanceResearch(laboratory, park.Progression.Technology(id).workSeconds + 1);
        Check(park.Progression.Completed(id), "ordinary staff research completes " + id);
    }
    private static CreatureDefinition Quiet(CreatureDefinition resource, bool sleeping)
    {
        var profile = Object.Instantiate(resource); profile.name = resource.name; profiles.Add(profile);
        // Keep imported locomotion/shelter identity; isolate a stable save state from unrelated needs and random combat.
        profile.initialHunger = profile.hungerPerSecond = 0; profile.needsFood = false;
        profile.Thirst.initialThirst = profile.Thirst.thirstPerSecond = 0;
        profile.initialEnergy = sleeping ? 0 : 100; profile.energyDrainPerSecond = profile.sleepRecoveryPerSecond = 0;
        profile.preyTags = profile.fearedTags = CreatureTags.None; profile.damage = profile.threat = profile.fearRange = 0;
        profile.minimumSpace = profile.minimumOpenGroundFraction = 0; profile.requiredTrees = profile.requiredGravestones = 0;
        profile.minimumCompanions = 0; profile.maximumCompanions = -1;
        profile.happinessThreshold = profile.happinessChangePerSecond = 0;
        profile.pauseMinSeconds = profile.pauseMaxSeconds = 600; profile.decisionInterval = .05f;
        profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
        return profile;
    }

    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 50, depth = 50, seed = 842913, relief = 0, forestDensity = 0, featureSize = 28 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        park.Economy.UnlimitedFunds = true; park.Visitors.Definition.maximumVisitors = 0;
        Call(park, "SelectTool", Enum.ToObject(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), 0));
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "NorthAmericanExpansionValidation"
            && ParkSaveStore.SavePath.Contains("NorthAmericanExpansionValidation"), "the fixture has an isolated save and settings identity");
        checks += NorthAmericanSpeciesNativeChecks.RunChecks();
        checks += SpecialEnclosureNativeChecks.RunChecks();
        checks += FlightNativeChecks.RunChecks();
        checks += StalkingLuringNativeChecks.RunChecks();
        foreach (string name in new[] { "Enrichment trees", "Natural rocks" })
        {
            var group = GameObject.Find(name);
            if (group != null) foreach (var target in group.GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        }
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var grid = Field<SquareBuildGrid>(park, "grid"); var world = Field<CreatureWorld>(park, "creatureWorld");
        var fences = Field<FenceConstruction>(park, "fences");
        var map = Field<SpecialEnclosureMap>(park, "specialEnclosureMap");
        var structures = Field<SpecialEnclosureConstruction>(park, "specialEnclosures");
        var buildings = Field<BuildingConstruction>(park, "buildingConstruction"); var paths = Field<PathConstruction>(park, "paths");
        var definitions = Field<BuildingDefinition[]>(park, "buildingCatalog");
        var catalog = Field<CreatureDefinition[]>(park, "creatureCatalog");
        Check(grid.Width == 50 && grid.Depth == 50 && catalog.Length == 25 && Ids.All(id => catalog.Any(profile => profile.name == id)),
            "the real park imports all twenty-four mythological species plus the goat");
        ParkBuildingRecord Place(BuildingKind kind, int x)
        {
            var definition = definitions.Single(item => item.kind == kind).Copy();
            definition.width = definition.depth = 1; definition.entranceColumn = 0;
            Check(buildings.TryPlace(new BuildingFootprint(definition, C(x, 3), 0)), "the real building system places " + kind);
            return buildings.Network.Records.Last();
        }
        Place(BuildingKind.ParkEntrance, 2); var expedition = Place(BuildingKind.ExpeditionCentre, 5); var lab = Place(BuildingKind.ResearchCentre, 8);
        for (int x = 1; x <= 10; x++) Check(paths.TryPlace(C(x, 2)), "ordinary public paths connect the fixture's workplaces");
        buildings.Network.Refresh();
        Check(expedition.IsOpen && lab.IsOpen, "both real workplaces have entrance-connected paths");
        Check(park.Personnel.TryHire(lab), "ordinary staff hiring supplies a researcher for the enclosure technologies");
        Check(!park.Progression.FenceUnlocked(FenceKind.Aviary) && !park.Progression.FenceUnlocked(FenceKind.Aquarium),
            "both new construction kinds begin behind technology research");
        Research(park, lab, "aviaries"); Research(park, lab, "aquariums");
        var palette = Field<ConstructionPalette>(park, "palette"); palette.Refresh(true);
        Check(Field<UnityEngine.UI.Button>(palette, "aviaryChoice").interactable
            && Field<UnityEngine.UI.Button>(palette, "aquariumChoice").interactable,
            "researched enclosure tools become available in the actual construction palette");

        var cageCells = Box(4, 15, 8, 8); var cage = Boundary(4, 15, 8, 8); var cageGap = cage[0];
        foreach (var edge in cage.Skip(1)) Check(fences.TryPlace(edge, FenceKind.Aviary), "the regular fence placer builds aviary framework");
        Refresh(park);
        Check(!map.IsAviary(C(7, 18)) && !RoofVisible(), "unfinished aviary walls create neither roof nor air containment");
        Check(fences.TryPlace(cageGap, FenceKind.Aviary), "the final regular fence segment closes the aviary"); Refresh(park);
        Check(map.IsAviary(C(7, 18)) && RoofVisible() && !map.CanFlyAcross(cageGap.CellA, cageGap.CellB),
            "closing the real aviary activates its large cage mesh and physical flight barrier");
        Check(fences.Layout.TryRemove(cageGap), "an aviary segment can be breached through the normal fence API"); Refresh(park);
        Check(!map.IsAviary(C(7, 18)) && !RoofVisible() && map.CanFlyAcross(cageGap.CellA, cageGap.CellB),
            "the breach immediately removes the roof and releases air movement");
        Check(fences.TryPlace(cageGap, FenceKind.Aviary), "the aviary can be repaired and closed again"); Refresh(park);

        var poolCells = Box(22, 15, 6, 6); var tank = Boundary(22, 15, 6, 6); var tankGap = tank[0];
        foreach (var edge in tank.Skip(1)) Check(fences.TryPlace(edge, FenceKind.Aquarium), "the regular fence placer builds aquarium walls");
        Refresh(park); var beforePoolHeights = grid.Terrain.Capture().heights;
        Check(!poolCells.Any(grid.Habitat.HasWaterSurface), "unfinished aquarium walls do not excavate or add water");
        Call(park, "BeginConstructionAction", "construction.aquarium_wall");
        Check(fences.TryPlace(tankGap, FenceKind.Aquarium), "the final aquarium wall closes the real pool boundary");
        Call(park, "CommitConstructionAction", 0f); Refresh(park);
        Check(map.IsAquarium(C(24, 18)) && poolCells.All(grid.Habitat.IsWater) && structures.HasCompletedPool(C(24, 18))
            && grid.Terrain.Capture().heights.Where((height, index) => height < beforePoolHeights[index] - .1f).Any(),
            "the final construction stroke excavates and fills an actual completed pool");
        Check(park.UndoCount > 0 && park.UndoLastConstruction(), "the completed aquarium's closing stroke can be undone through the player's real Undo action");
        Refresh(park);
        Check(!fences.Layout.HasFence(tankGap) && !poolCells.Any(grid.Habitat.HasWaterSurface)
            && grid.Terrain.Capture().heights.SequenceEqual(beforePoolHeights) && !structures.HasCompletedPool(C(24, 18)),
            "Undo restores original terrain, water, final fence and completion receipts together");
        Call(park, "BeginConstructionAction", "construction.aquarium_wall");
        Check(fences.TryPlace(tankGap, FenceKind.Aquarium), "the undone final wall can be placed again");
        Call(park, "CommitConstructionAction", 0f); Refresh(park);
        var poolHeights = grid.Terrain.Capture().heights;
        Check(fences.Layout.TryRemove(tankGap), "the aquarium wall can be breached"); Refresh(park);
        Check(!map.IsAquarium(C(24, 18)) && poolCells.All(grid.Habitat.IsWater)
            && grid.Terrain.Capture().heights.SequenceEqual(poolHeights), "a breach loses containment while retaining the entire excavated pool and water");
        var editCell = C(24, 18); float requestedWater = grid.Habitat.WaterLevel(editCell) + .15f;
        Check(grid.Habitat.TrySetWater(editCell, requestedWater), "the ordinary habitat API can edit an existing pool's water level");
        float authoredWater = grid.Habitat.WaterLevel(editCell);
        Check(Mathf.Approximately(authoredWater, requestedWater), "the pool stores the later authored water level");
        Check(fences.TryPlace(tankGap, FenceKind.Aquarium), "the aquarium can be repaired"); Refresh(park);
        Check(Mathf.Approximately(grid.Habitat.WaterLevel(editCell), authoredWater) && grid.Terrain.Capture().heights.SequenceEqual(poolHeights),
            "repairing the pool never excavates twice or refills over a later water edit");
        var wood = Boundary(38, 15, 5, 5);
        foreach (var edge in wood) Check(fences.TryPlace(edge, FenceKind.Wooden), "ordinary wooden barriers still place normally"); Refresh(park);
        Check(!map.IsAviary(C(40, 17)) && !map.IsAquarium(C(40, 17)) && !grid.Habitat.IsWater(C(40, 17)),
            "an ordinary closed fence creates no automatic cage or pool");
        Check(fences.TryPlaceGate(cage.Last()), "a staff gate preserves its underlying aviary kind"); Refresh(park);
        Check(map.IsAviary(C(7, 18)) && RoofVisible(), "a completed aviary remains contained with a staff gate installed");
        Check(fences.Layout.TryDamage(cage[2], 11) > 0 && fences.Layout.TryDamage(tank[2], 13) > 0,
            "the round trip includes genuinely damaged special fence segments"); Refresh(park);

        var progression = park.Progression;
        Check(progression.Region("north-america").species.SequenceEqual(new[] { "Chupacabra", "Wendigo", "Thunderbird", "Kalupelik" }),
            "North America retains Chupacabra and offers all three new species");
        Check(progression.HasAccess("north-america") || progression.BuyAccess(expedition, "north-america"), "the normal permit purchase unlocks North America");
        Check(progression.Teams > 0 || progression.BuyTeam(expedition), "the normal team purchase enables expeditions");
        foreach (string id in Ids)
        {
            var difficulty = progression.Rules.difficulties.Where(item => progression.Eligible("north-america", item.id).Contains(id))
                .OrderBy(item => progression.Eligible("north-america", item.id).Count).First();
            var eligible = progression.Eligible("north-america", difficulty.id); int draw = 0;
            Set(progression, "random", new Func<double>(() => draw++ % 2 == 0 ? 0 : (eligible.IndexOf(id) + .5d) / eligible.Count));
            Check(progression.Launch(expedition, "north-america", difficulty.id, 1), "a real eligible mission is dispatched for " + id);
            progression.Tick(progression.Missions.Last().remaining + .1f);
            Check(progression.Stock(id) >= 2, "a completed mission grants placeable and retained " + id + " specimens");
        }
        var trees = Field<TreeConstruction>(park, "trees");
        Check(trees.TryPlace(C(5, 16)), "the cage can contain a real tree roost");
        var actors = new Dictionary<string, CreatureAgent>();
        foreach (string id in Ids)
        {
            var profile = Quiet(catalog.Single(item => item.name == id), id == "Kalupelik");
            var cell = id == "Thunderbird" ? C(8, 19) : id == "Kalupelik"
                ? poolCells.OrderBy(item => ConstructionRouteGraph.CellDistance(item, editCell)).First(item => world.CanSpawn(profile, item)) : C(36, 27);
            int stock = progression.Stock(id);
            Check(progression.TryPlace(id, () => world.TrySpawn(profile, cell)) && progression.Stock(id) == stock - 1,
                "ordinary specimen placement consumes one acquired " + id);
            actors[id] = world.Agents.Last();
            Check(park.Personnel.Research.Study(catalog.Single(item => item.name == id), 35 + actors.Count * 15), "real creature observation records knowledge for " + id);
        }
        for (int i = 0; i < 600 && (world.FlightAltitude(actors["Thunderbird"]) <= 1.1f || actors["Kalupelik"].Activity != CreatureActivity.Sleeping); i++) world.Tick(.05f);
        var bird = actors["Thunderbird"]; var flight = Field<object>(bird, "FlightState");
        string flightDiagnostics = $"alive={bird.Alive}, activity={bird.Activity}, altitude={world.FlightAltitude(bird):R}, raw={(flight == null ? float.NaN : Field<float>(flight, "Altitude")):R}, "
            + $"cell={bird.Cell}, position={bird.transform.position}, actualCell={grid.WorldToCell(bird.transform.position)}, ceiling={map.FlightCeiling(bird.Cell):R}, "
            + $"external={Field<bool>(bird, "ExternallyControlled")}, sedated={bird.IsSedated}, carried={bird.CarriedBy != null}, captured={Field<CreatureAgent>(bird, "CapturedBy") != null}, "
            + $"poseControlled={Field<bool>(bird, "AttractionPoseControlled")}, wantsSleep={Field<bool>(bird, "WantsSleep")}, elevation={Field<float>(bird, "Elevation"):R}, "
            + $"energy={bird.Energy.Energy:R}, thirst={bird.Vitals.Thirst:R}, fear={bird.Vitals.Fear:R}, settings={JsonUtility.ToJson(bird.Definition.Flight)}";
        Check(world.FlightAltitude(bird) > 1 && bird.IsAirborne, "the saved Thunderbird is genuinely airborne inside the complete cage: " + flightDiagnostics);
        Check(actors["Kalupelik"].Activity == CreatureActivity.Sleeping && world.IsSwimming(actors["Kalupelik"])
            && world.CaptureValidShelterHome(actors["Kalupelik"]).HasValue, "the saved Kalupelik occupies a real sleeping water home in the generated pool");
        int index = 0;
        foreach (var actor in actors.Values)
        {
            actor.Vitals.Restore(17 + index * 4, 0, 24 + index * 3); actor.Health.Damage(index + 2);
            actor.Wellbeing.Restore(70 + index, .04f * index); index++;
        }
        world.RestoreFlightAltitude(actors["Thunderbird"], 2.75f);
        world.RestoreLureCooldown(actors["Kalupelik"], 12.5f);
        string activeTier = progression.Rules.difficulties.First(item => progression.Eligible("north-america", item.id).Contains("Wendigo")).id;
        Check(progression.Launch(expedition, "north-america", activeTier, 1), "the snapshot retains a real active North American mission");
        var saved = park.CapturePark(); park.ValidateSave(saved);
        var savedSpecial = Field<SavedSpecialEnclosures>(saved, "specialEnclosures");
        Check(saved.creatures.Count == 3 && Ids.All(id => saved.creatures.Count(actor => actor.species == id) == 1), "the ordinary park snapshot contains every new species exactly once");
        Check(savedSpecial != null && savedSpecial.completedPoolCells.Count >= poolCells.Count
            && savedSpecial.terrain.Count > 0 && savedSpecial.water.Count >= poolCells.Count,
            "the production snapshot retains sparse pool completion and original earthwork metadata");
        Check(Mathf.Approximately(SavedFloat(saved.creatures.Single(actor => actor.species == "Thunderbird"), "flightAltitude"), 2.75f)
            && Mathf.Approximately(SavedFloat(saved.creatures.Single(actor => actor.species == "Kalupelik"), "lureCooldown"), 12.5f),
            "airborne height and remaining lure cooldown are explicit durable actor state");
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "North American enclosure validation");
        ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Check(SceneTransition.Load("GameScene"), "the real disk slot reloads through production scene switching");
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0; park.Visitors.Definition.maximumVisitors = 0;
        grid = Field<SquareBuildGrid>(park, "grid"); world = Field<CreatureWorld>(park, "creatureWorld");
        fences = Field<FenceConstruction>(park, "fences"); map = Field<SpecialEnclosureMap>(park, "specialEnclosureMap");
        structures = Field<SpecialEnclosureConstruction>(park, "specialEnclosures");
        var restored = park.CapturePark(); park.ValidateSave(restored);
        Check(ParkSaveStore.Pending == null && GameSaveMenu.IsOpen && Time.timeScale == 0, "the restored park remains paused after consuming its disk-load request");
        Check(map.IsAviary(C(7, 18)) && RoofVisible() && map.IsAquarium(editCell)
            && structures.HasCompletedPool(editCell), "real special roofs and pool containment reconstruct from saved fences and receipts");
        Check(grid.Terrain.Capture().heights.SequenceEqual(saved.terrain.heights) && Mathf.Approximately(grid.Habitat.WaterLevel(editCell), authoredWater),
            "scene loading preserves the authored pool bed and edited water level without a second excavation");
        Check(fences.Layout.KindAt(cage.Last()) == FenceKind.Aviary && fences.Layout.IsGate(cage.Last())
            && Mathf.Approximately(fences.Layout.HealthAt(cage[2]), saved.fences.Single(edge => edge.cell == cage[2].Start && edge.alongX == cage[2].AlongX).health)
            && Mathf.Approximately(fences.Layout.HealthAt(tank[2]), saved.fences.Single(edge => edge.cell == tank[2].Start && edge.alongX == tank[2].AlongX).health),
            "special fence kinds, gate protection and actual partial health survive scene loading");
        foreach (string id in Ids)
        {
            var expected = saved.creatures.Single(actor => actor.species == id); var loaded = restored.creatures.Single(actor => actor.species == id);
            var actor = world.Agents.Single(item => item.Definition.name == id);
            Check(actor.Definition == Resources.Load<CreatureDefinition>("Creatures/" + id) && loaded.cell == expected.cell
                && Mathf.Approximately(loaded.hunger, expected.hunger) && Mathf.Approximately(loaded.thirst, expected.thirst)
                && Mathf.Approximately(loaded.energy, expected.energy) && Mathf.Approximately(loaded.health, expected.health)
                && Mathf.Approximately(loaded.happiness, expected.happiness) && Mathf.Approximately(loaded.agitation, expected.agitation),
                id + " restores its imported definition and exact individual needs and wellbeing");
            Check(park.Progression.Stock(id) == saved.progression.inventory.Single(item => item.species == id).count
                && Mathf.Approximately(park.Personnel.Research.Progress(actor.Definition), saved.research.Single(item => item.species == id).progress),
                id + " preserves acquired stock and independent research progress");
        }
        Check(Mathf.Approximately(world.FlightAltitude(world.Agents.Single(actor => actor.Definition.name == "Thunderbird")), 2.75f)
            && world.Agents.Single(actor => actor.Definition.name == "Thunderbird").IsAirborne,
            "the restored bird resumes its exact airborne height without snapping to the ground");
        Check(Mathf.Approximately(world.CaptureLureCooldown(world.Agents.Single(actor => actor.Definition.name == "Kalupelik")), 12.5f),
            "loading cannot bypass the Kalupelik's remaining lure cooldown");
        Check(park.Progression.HasAccess("north-america") && park.Progression.Completed("aviaries") && park.Progression.Completed("aquariums")
            && park.Progression.Missions.Count == 1 && park.Progression.Missions[0].region == "north-america"
            && Mathf.Approximately(park.Progression.Missions[0].remaining, saved.progression.missions.Single().remaining),
            "regional access, researched construction and ongoing expedition timing remain intact");
        Check(fences.Layout.TryRemove(tankGap), "a loaded aquarium can be breached again"); Refresh(park);
        Check(Mathf.Approximately(grid.Habitat.WaterLevel(editCell), authoredWater) && poolCells.All(grid.Habitat.IsWater),
            "a loaded aquarium also retains water when containment is broken");
    }

    private static void Cleanup()
    {
        foreach (var profile in profiles) if (profile != null) Object.Destroy(profile); profiles.Clear();
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Cleanup escaped the owned North American validation slot.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

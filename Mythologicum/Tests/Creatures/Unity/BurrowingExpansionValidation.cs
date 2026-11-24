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
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Production Main Menu -> configured park -> disk save -> production scene load.
/// Copy into the disposable validation project's Editor directory and invoke
/// -executeMethod BurrowingExpansionValidation.Start. Never run against player saves.
/// </summary>
[InitializeOnLoad]
public static class BurrowingExpansionValidation
{
    private const string Running = "BurrowingExpansionValidation.running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator steps;
    private static double started;
    private static int checks;
    private static string slot;
    private static readonly List<CreatureDefinition> profiles = new List<CreatureDefinition>();
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "BurrowingExpansionValidationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
    private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);
    private static void Property(object owner, string name, object value) => owner.GetType().GetProperty(name).SetValue(owner, value);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("BURROW EXPANSION PASS: " + message); }

    static BurrowingExpansionValidation()
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
        PlayerSettings.productName = "BurrowingExpansionValidation";
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
            if (EditorApplication.timeSinceStartup - started > 240 || SceneTransition.HasFailed)
                throw new Exception("Burrowing expansion park preparation failed or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup();
            File.WriteAllText(Result, "PASS: " + checks + " production creature assets, burrow sleep/reuse, Deep fence containment, research, expeditions and scene save/load checks.");
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

    private static void Advance(CreatureWorld world, float seconds)
    { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) world.Tick(.05f); }

    private static bool Until(CreatureWorld world, Func<bool> condition, float seconds = 12)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++)
        { if (condition()) return true; world.Tick(.05f); }
        return condition();
    }

    private static CreatureDefinition Profile(CreatureDefinition resource, bool sleepy)
    {
        var profile = Object.Instantiate(resource); profile.name = resource.name; profiles.Add(profile);
        // Clones preserve the imported species identity, shelter and movement abilities.
        // Unrelated needs, combat and random intentions are disabled for deterministic AI observation.
        profile.needsFood = false; profile.hungerPerSecond = 0; profile.initialHunger = 0;
        profile.Thirst.needsWater = false; profile.Thirst.thirstPerSecond = 0;
        profile.threat = profile.damage = 0; profile.fearRange = 0; profile.fearedTags = profile.preyTags = CreatureTags.None;
        profile.minimumSpace = profile.minimumOpenGroundFraction = 0; profile.requiredTrees = profile.requiredGravestones = 0;
        profile.minimumCompanions = 0; profile.maximumCompanions = -1;
        profile.energyDrainPerSecond = 0; profile.initialEnergy = sleepy ? 0 : 100;
        profile.sleepRecoveryPerSecond = 0; profile.sleepThreshold = 20; profile.wakeThreshold = 80;
        profile.happinessChangePerSecond = 0; profile.happinessThreshold = 0;
        profile.walkSpeed = profile.runSpeed = 8; profile.decisionInterval = .05f;
        profile.pauseMinSeconds = profile.pauseMaxSeconds = 600;
        profile.burrowSeconds = .3f; profile.burrowEnergyCost = 0;
        profile.climbRetrySeconds = 8; profile.escapeRetrySeconds = 30;
        profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
        return profile;
    }

    private static CreatureAgent Spawn(CreatureWorld world, CreatureDefinition profile, Vector2Int cell)
    {
        Check(world.TrySpawn(profile, cell), "The production world spawns " + profile.name + " at " + cell);
        var actor = world.Agents.Last(); actor.Wellbeing.Restore(100, 0); Set(actor, "DecisionTimer", 0f); return actor;
    }

    private static FenceEdge[] Cage(FenceConstruction construction, Vector2Int cell, FenceKind kind)
    {
        var edges = new[] { new FenceEdge(cell, true), new FenceEdge(cell, false),
            new FenceEdge(cell + Vector2Int.up, true), new FenceEdge(cell + Vector2Int.right, false) };
        foreach (var edge in edges) Check(construction.TryPlace(edge, kind), "Real " + kind + " construction closes " + edge.Start + "/" + edge.AlongX);
        return edges;
    }

    private static void Desire(CreatureWorld world, CreatureAgent actor)
    {
        Set(actor, "EscapeIntent", true); Set(actor, "EscapeUntil", world.SimulationTime + 30);
        Set(actor, "DecisionTimer", 0f);
    }

    private static void Research(BuildModeController park, ParkBuildingRecord lab, string id, HashSet<string> visited)
    {
        if (park.Progression.Completed(id)) return;
        Check(visited.Add(id), "Technology prerequisite traversal has no cycle at " + id);
        var technology = park.Progression.Technology(id);
        Check(technology != null, "Production progression defines technology " + id);
        foreach (string prerequisite in technology.prerequisites) Research(park, lab, prerequisite, visited);
        Check(park.Personnel.StartTechnology(lab, id), "A hired production researcher starts " + id);
        for (int i = 0; i < 20000 && !park.Progression.Completed(id); i++)
        { park.Personnel.Tick(.1f); park.Progression.Tick(.1f); }
        Check(park.Progression.Completed(id), "The connected laboratory completes " + id + " through staff work");
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
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "BurrowingExpansionValidation",
            "The test save identity is isolated from the player's saves");
        foreach (string name in new[] { "Enrichment trees", "Natural rocks" })
        {
            var group = GameObject.Find(name);
            if (group != null) foreach (var target in group.GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        }
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var grid = Field<SquareBuildGrid>(park, "grid"); var buildings = Field<BuildingConstruction>(park, "buildingConstruction");
        var paths = Field<PathConstruction>(park, "paths"); var world = Field<CreatureWorld>(park, "creatureWorld");
        var fences = Field<FenceConstruction>(park, "fences");
        Check(grid.Width == 50 && grid.Depth == 50, "Main-menu new game uses the configured 50 by 50 map");
        Check((int)FenceKind.Deep == 4, "Deep fences append the stable saved enum value four");
        var tatzel = Resources.Load<CreatureDefinition>("Creatures/Tatzelwurm");
        var allghoi = Resources.Load<CreatureDefinition>("Creatures/AllghoiKhorkhoi");
        foreach (var resource in new[] { tatzel, allghoi })
        {
            Check(resource != null, "The new creature Resources asset is imported");
            Check(resource.canBurrowFences && resource.requiresBurrowForSleep && resource.Shelter.mode == CreatureShelterMode.Burrow,
                resource.name + " uses the existing burrow movement and persistent sleep shelter");
            Check(resource.manuallyPlaceable && !string.IsNullOrWhiteSpace(resource.DisplayName), resource.name + " has a placeable translated species definition");
        }
        checks += BurrowingSpeciesNativeChecks.RunChecks();

        var definitions = Field<BuildingDefinition[]>(park, "buildingCatalog");
        ParkBuildingRecord Place(BuildingKind kind, int x)
        {
            var definition = definitions.Single(item => item.kind == kind).Copy();
            definition.width = definition.depth = 1; definition.entranceColumn = 0;
            Check(buildings.TryPlace(new BuildingFootprint(definition, new Vector2Int(x, 3), 0)), "Fixture places the production " + kind);
            return buildings.Network.Records.Last();
        }
        Place(BuildingKind.ParkEntrance, 2);
        var lab = Place(BuildingKind.ResearchCentre, 5); var expedition = Place(BuildingKind.ExpeditionCentre, 8);
        for (int x = 1; x < 12; x++) Check(paths.TryPlace(new Vector2Int(x, 2)), "Ordinary path connects laboratory and expedition centre");
        buildings.Network.Refresh();
        Check(buildings.Network.Entrance.IsOpen && lab.IsOpen && expedition.IsOpen, "Research and expeditions use ordinary connected production buildings");
        var progression = park.Progression;
        Check(progression.Region("western-europe").species.Contains(tatzel.name), "Western Europe includes Tatzelwurm in its production species pool");
        Check(progression.Region("east-asia") != null && progression.Region("east-asia").species.Contains(allghoi.name),
            "East Asia is a production expedition region with Allghoi Khorkhoi");
        foreach (var pair in new[] { new { region = "western-europe", species = tatzel.name }, new { region = "east-asia", species = allghoi.name } })
            Check(progression.Rules.difficulties.Any(difficulty => progression.Eligible(pair.region, difficulty.id).Contains(pair.species)),
                pair.species + " is obtainable at a configured expedition difficulty");
        Check(progression.HasAccess("east-asia") || progression.BuyAccess(expedition, "east-asia"), "Normal expedition purchase grants East Asia access");
        Check(progression.Teams > 0 || progression.BuyTeam(expedition), "Normal expedition purchase provides a team");
        string difficulty = progression.Rules.difficulties.First(item => progression.Eligible("east-asia", item.id).Contains(allghoi.name)).id;
        var eligibleSpecies = progression.Eligible("east-asia", difficulty);
        var originalRandom = Field<Func<double>>(progression, "random"); int missionDraw = 0;
        // East Asia also offers other high-rated species; choose the deathworm through the real eligibility picker.
        Set(progression, "random", new Func<double>(() => missionDraw++ % 2 == 0 ? 0 : (eligibleSpecies.IndexOf(allghoi.name) + .5d) / eligibleSpecies.Count));
        bool launched;
        try { launched = progression.Launch(expedition, "east-asia", difficulty, 1); }
        finally { Set(progression, "random", originalRandom); }
        Check(launched, "The region launches through normal expedition APIs");
        Check(progression.Missions.Last().region == "east-asia" && progression.Missions.Last().species == allghoi.name,
            "A real East Asia mission targets the imported Allghoi Khorkhoi identity");
        Check(!progression.FenceUnlocked(FenceKind.Deep), "Deep fences begin behind production technology research");
        var palette = Field<ConstructionPalette>(park, "palette"); palette.Refresh(true);
        Check(!Field<Button>(palette, "deepFenceChoice").interactable, "The real construction menu locks Deep fences before research");
        Check(progression.Technology("deep-fences") != null && progression.Technology("deep-fences").fences.Contains(FenceKind.Deep),
            "The deep-fences technology grants the actual Deep fence enum");
        Check(park.Personnel.TryHire(lab), "The connected laboratory hires a real researcher");
        Research(park, lab, "deep-fences", new HashSet<string>());
        Check(progression.FenceUnlocked(FenceKind.Deep), "Completed staff research unlocks Deep fence construction");
        palette.Refresh(true);
        Check(Field<Button>(palette, "deepFenceChoice").interactable, "Completed research enables the actual Deep fence construction button");
        yield return null;

        var sleepers = new List<CreatureAgent>();
        foreach (var resource in new[] { tatzel, allghoi })
        {
            var profile = Profile(resource, true);
            var actor = Spawn(world, profile, new Vector2Int(resource == tatzel ? 12 : 24, 16)); sleepers.Add(actor);
            Check(Until(world, () => actor.Activity == CreatureActivity.EnteringBurrow), resource.name + " visibly digs into natural ground for sleep");
            Check(Until(world, () => actor.InBurrow), resource.name + " completes actual underground sleep entry");
            var home = world.CaptureValidShelterHome(actor);
            Check(home.HasValue && world.ShelterRegistry.Contains(home.Value, ConstructedShelterKind.Burrow)
                && actor.Activity == CreatureActivity.Sleeping && world.IsHiddenInShelter(actor), resource.name + " creates a real persistent burrow home");
            int mounds = world.ShelterRegistry.Capture().Count;
            profile.sleepRecoveryPerSecond = 100;
            Check(Until(world, () => actor.Activity == CreatureActivity.LeavingBurrow), resource.name + " recovers energy and visibly emerges");
            Check(Until(world, () => !actor.InBurrow && actor.Activity == CreatureActivity.Resting), resource.name + " completes its return to the surface");
            Check(world.ShelterRegistry.Contains(home.Value, ConstructedShelterKind.Burrow), resource.name + " leaves its physical mound after waking");
            profile.sleepRecoveryPerSecond = 0;
            var away = home.Value + Vector2Int.up;
            Set(actor, "DecisionTimer", 10f);
            Check((bool)Call(world, "SetGoal", actor, away, true), resource.name + " can walk away from its sleeping mound");
            Property(actor, "Activity", CreatureActivity.Wandering);
            Check(Until(world, () => actor.Cell == away), resource.name + " physically walks to adjacent natural ground");
            actor.Energy.Spend(100); Set(actor, "DecisionTimer", 0f);
            Check(Until(world, () => actor.InBurrow), resource.name + " seeks shelter again after exhaustion");
            Check(actor.Cell == home.Value && world.CaptureValidShelterHome(actor) == home
                && world.ShelterRegistry.Capture().Count == mounds, resource.name + " reuses the existing home without creating another mound");
        }

        foreach (var resource in new[] { tatzel, allghoi })
        {
            int x = resource == tatzel ? 10 : 22;
            var start = new Vector2Int(x, 28); var wooden = Cage(fences, start, FenceKind.Wooden);
            var actor = Spawn(world, Profile(resource, false), start); Desire(world, actor);
            Check(Until(world, () => actor.Activity == CreatureActivity.BurrowingFence), resource.name + " begins its real escape tunnel under a wooden cage");
            Check(actor.Cell == start && actor.IsUnderground, resource.name + " keeps its logical cell until the crossing completes");
            Check(Until(world, () => actor.Cell != start), resource.name + " emerges outside the ordinary wooden fence");
            Check(!Field<bool>(actor, "EscapeIntent") && wooden.All(edge => Mathf.Approximately(fences.Layout.HealthAt(edge), fences.Layout.MaximumHealthAt(edge))),
                resource.name + " consumes one escape intention without damaging wooden fences");

            var deepStart = new Vector2Int(x, 36); var deep = Cage(fences, deepStart, FenceKind.Deep);
            var blocked = Spawn(world, Profile(resource, false), deepStart); Desire(world, blocked);
            Check(Until(world, () => blocked.Activity == CreatureActivity.FailedBurrow), resource.name + " performs its usual dig attempt against Deep foundations");
            Check(Until(world, () => !Field<bool>(blocked, "EscapeIntent")), resource.name + " finishes one rejected attempt");
            float retry = Field<float>(blocked, "EscapeAfter");
            Check(blocked.Cell == deepStart && retry > world.SimulationTime && blocked.Wellbeing.Agitation == 0,
                resource.name + " remains on the same cell with an actual retry cooldown");
            Advance(world, 2);
            Check(blocked.Cell == deepStart && !Field<bool>(blocked, "EscapeIntent") && Field<float>(blocked, "EscapeAfter") == retry
                && deep.All(edge => Mathf.Approximately(fences.Layout.HealthAt(edge), fences.Layout.MaximumHealthAt(edge))),
                resource.name + " does not repeat failed digging or damage Deep fences during cooldown");
        }

        var gateStart = new Vector2Int(35, 29); var gates = Cage(fences, gateStart, FenceKind.Deep); var gate = gates.Last();
        Check(fences.TryPlaceGate(gate), "A Deep segment can become an ordinary staff gate");
        var normalNavigation = new CreatureNavigation(grid, fences.Layout, world.IsClear);
        var burrowNavigation = new CreatureNavigation(grid, fences.Layout, world.IsClear, canBurrowFences: true);
        var staffNavigation = new StaffNavigation(grid, fences.Layout, world.IsClear);
        Check(staffNavigation.CanCross(gate.CellA, gate.CellB) && !normalNavigation.CanCross(gate.CellA, gate.CellB)
            && !burrowNavigation.CanCross(gate.CellA, gate.CellB), "A Deep staff gate admits staff while containing ordinary animals and burrowers");
        var goat = Spawn(world, Profile(Resources.Load<CreatureDefinition>("Creatures/Goat"), false), gateStart); Desire(world, goat);
        Advance(world, 1);
        Check(goat.Cell == gateStart && goat.Activity != CreatureActivity.BurrowingFence && !goat.InBurrow,
            "A non-burrowing control creature gains no digging ability near a Deep gate");

        var breachStart = new Vector2Int(35, 36); var breachEdges = Cage(fences, breachStart, FenceKind.Deep); var breach = breachEdges.Last();
        float maximum = fences.Layout.MaximumHealthAt(breach);
        Check(fences.Layout.TryDamage(breach, maximum / 2) > 0 && fences.Layout.HasFence(breach)
            && !normalNavigation.CanCross(breach.CellA, breach.CellB) && !burrowNavigation.CanCross(breach.CellA, breach.CellB),
            "Partially damaged Deep foundations retain ordinary and underground containment");
        Check(fences.Layout.TryDamage(breach, maximum) > 0 && !fences.Layout.HasFence(breach), "Creature damage can breach the Deep barrier through its normal fence health API");
        fences.Refresh(); Physics.SyncTransforms();
        Check(normalNavigation.CanCross(breach.CellA, breach.CellB), "A destroyed Deep segment releases normal movement");
        var walker = Spawn(world, Profile(tatzel, false), breachStart);
        var exit = breach.CellA == breachStart ? breach.CellB : breach.CellA;
        Check((bool)Call(world, "SetGoal", walker, exit, true), "Production creature routing accepts the breached Deep opening");
        Set(walker, "DecisionTimer", 10f); Property(walker, "Activity", CreatureActivity.Wandering);
        Check(Until(world, () => walker.Cell == exit) && !walker.IsUnderground,
            "The creature actually walks through the breached opening without a tunnel");
        // Retain a damaged segment as well as the Deep staff gate for the scene round trip.
        var damaged = breachEdges.First(); fences.Layout.TryDamage(damaged, fences.Layout.MaximumHealthAt(damaged) / 3);
        fences.Refresh();

        var saved = park.CapturePark(); park.ValidateSave(saved);
        foreach (var sleeper in sleepers)
        {
            var record = saved.creatures.Single(item => item.species == sleeper.Definition.name && item.hasShelterHome);
            Check(record.shelterSleeping && record.shelterHome == sleeper.Cell
                && saved.shelters.Any(item => item.cell == record.shelterHome && item.kind == ConstructedShelterKind.Burrow),
                sleeper.Definition.name + " serializes its real sleeping burrow association");
        }
        Check(saved.fences.Any(item => item.kind == FenceKind.Deep && item.gate)
            && saved.fences.Any(item => item.kind == FenceKind.Deep && item.health < fences.Layout.MaximumHealthAt(new FenceEdge(item.cell, item.alongX))),
            "The park captures Deep gate identity and partial fence damage");
        Check(saved.progression.technologies.Contains("deep-fences") && saved.progression.regions.Contains("east-asia"),
            "The park captures researched Deep fences and purchased East Asia access");
        var json = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved)); park.ValidateSave(json);
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(json, slot, "Burrowing expansion validation");
        ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Check(SceneTransition.Load("GameScene"), "Burrowing expansion reloads through the production scene transition");
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0; park.Visitors.Definition.maximumVisitors = 0;
        var restored = park.CapturePark(); park.ValidateSave(restored);
        world = Field<CreatureWorld>(park, "creatureWorld");
        Check(ParkSaveStore.Pending == null && GameSaveMenu.IsOpen && Time.timeScale == 0, "The production load consumes its request and keeps the restored park paused");
        foreach (string name in new[] { tatzel.name, allghoi.name })
        {
            var expected = saved.creatures.Single(item => item.species == name && item.hasShelterHome);
            var loaded = world.Agents.Single(item => !Field<bool>(item, "ExternallyControlled") && item.Definition.name == name && world.CaptureValidShelterHome(item).HasValue);
            Check(loaded.InBurrow && loaded.Activity == CreatureActivity.Sleeping && world.CaptureValidShelterHome(loaded) == expected.shelterHome
                && loaded.Cell == expected.cell, name + " resumes asleep in the same physical burrow after a real scene load");
        }
        var expectedDeep = saved.fences.Where(item => item.kind == FenceKind.Deep).ToArray();
        var restoredDeep = restored.fences.Where(item => item.kind == FenceKind.Deep).ToArray();
        Check(expectedDeep.Length == restoredDeep.Length && expectedDeep.All(expected => restoredDeep.Any(item =>
            item.cell == expected.cell && item.alongX == expected.alongX && item.gate == expected.gate && Mathf.Approximately(item.health, expected.health))),
            "Every Deep fence, staff gate and damaged health value survives JSON, disk and scene loading");
        Check(park.Progression.Completed("deep-fences") && park.Progression.FenceUnlocked(FenceKind.Deep) && park.Progression.HasAccess("east-asia"),
            "Loaded progression preserves Deep construction access and East Asia expeditions");
    }

    private static void Cleanup()
    {
        foreach (var profile in profiles) if (profile != null) Object.Destroy(profile);
        profiles.Clear();
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Cleanup escaped the owned burrowing expansion test slot.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

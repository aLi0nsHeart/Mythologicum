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

/// <summary>
/// Disposable Editor validation: production menu -> configured park -> real disk slot -> production scene load.
/// Copy this and AfricanSpeciesNativeChecks into the validation project's Editor directory;
/// invoke -executeMethod AfricanExpansionValidation.Start. Never run against player saves.
/// </summary>
[InitializeOnLoad]
public static class AfricanExpansionValidation
{
    private const string Running = "AfricanExpansionValidation.running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator steps;
    private static double started;
    private static int checks;
    private static string slot;
    private static readonly List<CreatureDefinition> profiles = new List<CreatureDefinition>();
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "AfricanExpansionValidationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static object Call(object owner, string name, params object[] arguments) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Hidden).SetValue(owner, value);
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); checks++; Debug.Log("AFRICAN EXPANSION PASS: " + reason); }

    static AfricanExpansionValidation()
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
        PlayerSettings.productName = "AfricanExpansionValidation";
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
                throw new Exception("African expansion production scene preparation failed or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup();
            File.WriteAllText(Result, "PASS: " + checks + " imported species, bodies, shoreline grazing, water hunting, cave sleep, fence abilities, real specimen acquisition, regional access, research, needs and disk/scene round-trip checks.");
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

    private static CreatureDefinition QuietProfile(CreatureDefinition resource)
    {
        var profile = Object.Instantiate(resource); profile.name = resource.name; profiles.Add(profile);
        // Shelter and species identity stay imported; isolate each sleep journey from
        // fighting, random escape, and unrelated need changes during observation.
        profile.initialHunger = profile.hungerPerSecond = 0; profile.Thirst.initialThirst = profile.Thirst.thirstPerSecond = 0;
        profile.initialEnergy = profile.energyDrainPerSecond = profile.sleepRecoveryPerSecond = 0;
        profile.threat = profile.damage = profile.fearRange = 0; profile.preyTags = profile.fearedTags = CreatureTags.None;
        profile.minimumSpace = profile.minimumOpenGroundFraction = 0; profile.requiredTrees = profile.requiredGravestones = 0;
        profile.minimumCompanions = 0; profile.maximumCompanions = -1;
        profile.happinessChangePerSecond = profile.happinessThreshold = 0;
        profile.walkSpeed = profile.runSpeed = profile.Aquatic.swimSpeed = 8; profile.decisionInterval = .05f;
        profile.pauseMinSeconds = profile.pauseMaxSeconds = 600;
        profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
        return profile;
    }

    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 50, depth = 50, seed = 843172, relief = 0, forestDensity = 0, featureSize = 28 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        park.Economy.UnlimitedFunds = true; park.Visitors.Definition.maximumVisitors = 0;
        Call(park, "SelectTool", Enum.ToObject(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), 0));
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "AfricanExpansionValidation",
            "validation has its own application identity and cannot access the player's save directory");
        checks += AfricanSpeciesNativeChecks.RunChecks();
        foreach (string name in new[] { "Enrichment trees", "Natural rocks" })
        {
            var group = GameObject.Find(name);
            if (group != null) foreach (var target in group.GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        }
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var grid = Field<SquareBuildGrid>(park, "grid"); var world = Field<CreatureWorld>(park, "creatureWorld");
        var buildings = Field<BuildingConstruction>(park, "buildingConstruction"); var paths = Field<PathConstruction>(park, "paths");
        var catalog = Field<CreatureDefinition[]>(park, "creatureCatalog");
        Check(grid.Width == 50 && grid.Depth == 50 && catalog.Length == 25,
            "the configured production park imports the complete 25-species catalog");
        var definitions = Field<BuildingDefinition[]>(park, "buildingCatalog");
        ParkBuildingRecord Place(BuildingKind kind, int x)
        {
            var definition = definitions.Single(item => item.kind == kind).Copy();
            definition.width = definition.depth = 1; definition.entranceColumn = 0;
            Check(buildings.TryPlace(new BuildingFootprint(definition, new Vector2Int(x, 3), 0)), "fixture places a real " + kind);
            return buildings.Network.Records.Last();
        }
        Place(BuildingKind.ParkEntrance, 2); var expedition = Place(BuildingKind.ExpeditionCentre, 5);
        for (int x = 1; x < 9; x++) Check(paths.TryPlace(new Vector2Int(x, 2)), "ordinary paths connect production expedition access");
        buildings.Network.Refresh();
        Check(buildings.Network.Entrance.IsOpen && expedition.IsOpen, "the expedition centre uses the ordinary park entrance and connected path network");
        var progression = park.Progression;
        string[] regions = { "west-africa", "central-africa", "east-africa", "south-africa", "west-asia" };
        string[][] pools = { new[] { "NinkiNanka" }, new[] { "MokeleMbembe", "Abada" }, new[] { "NandiBear", "Dingonek", "Marozi" },
            new[] { "Grootslang", "Inkanyamba", "Aigamuxa" }, new[] { "Manticore" } };
        for (int i = 0; i < regions.Length; i++)
        {
            Check(progression.Region(regions[i]) != null && progression.Region(regions[i]).species.SequenceEqual(pools[i]),
                regions[i] + " exposes the intended imported species through production progression");
            Check(progression.HasAccess(regions[i]) || progression.BuyAccess(expedition, regions[i]),
                "ordinary expedition purchase grants " + regions[i] + " access");
        }
        Check(Loc.English(progression.Region("west-africa").name) == "West Africa"
            && Loc.English(progression.Region("central-africa").name) == "Central Africa",
            "West Africa and Central Africa display separate canonical region names");
        Check(progression.Teams > 0 || progression.BuyTeam(expedition), "ordinary purchases provide a real expedition team");
        // Supply a deterministic draw stream while preserving the real launch, completion,
        // reward and placement APIs. No specimen stock or saved progression is injected.
        for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
            foreach (string id in pools[regionIndex])
            {
                string region = regions[regionIndex];
                var difficulty = progression.Rules.difficulties.Where(item => progression.Eligible(region, item.id).Contains(id))
                    .OrderBy(item => progression.Eligible(region, item.id).Count).ThenByDescending(item => item.successChance).First();
                var eligible = progression.Eligible(region, difficulty.id); int draw = 0;
                Set(progression, "random", new Func<double>(() => draw++ % 2 == 0 ? 0 : (eligible.IndexOf(id) + .5d) / eligible.Count));
                for (int attempts = 0; attempts < 2 && progression.Stock(id) < 2; attempts++)
                {
                    Check(progression.Launch(expedition, region, difficulty.id, 1), "a production " + region + " mission launches for the " + id + " tier");
                    progression.Tick(progression.Missions.Last().remaining + .1f);
                }
                Check(progression.Stock(id) >= 2, "completed real expeditions supply placeable and retained " + id + " specimens");
            }
        Check(grid.Habitat.AddWater(new Vector2(12.5f, 24.5f), 6, grid.Origin.y, 2, null, out _),
            "the normal terraforming operation excavates a persistent pond for all four new swimming species");
        Call(park, "RefreshTerrainAttachments");
        var shelters = Field<ShelterConstruction>(park, "shelters"); var trees = Field<TreeConstruction>(park, "trees");
        var caves = new[] { new Vector2Int(25, 15), new Vector2Int(31, 15), new Vector2Int(37, 15) };
        foreach (var cell in caves) Check(shelters.TryPlaceCave(cell), "normal shelter construction places an actual saveable cave");
        Check(trees.TryPlace(new Vector2Int(25, 26)) && trees.TryPlace(new Vector2Int(31, 26)), "normal tree construction provides two real shaded sleeping sites");
        Physics.SyncTransforms();
        var wet = grid.Habitat.WaterCells.Where(cell => Resources.Load<CreatureDefinition>("Creatures/MokeleMbembe").Aquatic.IsSwimmable(grid, cell))
            .OrderBy(cell => ConstructionRouteGraph.CellDistance(cell, new Vector2Int(12, 24))).ToArray();
        Check(wet.Length >= 12, "the production pond has ample swimmable interior cells");
        var actors = new List<CreatureAgent>(); int waterIndex = 0, caveIndex = 0, shadeIndex = 0;
        foreach (string id in AfricanSpeciesNativeChecks.Ids)
        {
            var resource = catalog.Single(item => item.name == id); var profile = QuietProfile(resource);
            Vector2Int cell;
            if (profile.Aquatic.canSwim)
            {
                // Select a real separated wet cell using the same placement suitability API.
                cell = wet.First(candidate => world.CanSpawn(profile, candidate)); waterIndex++;
            }
            else if (profile.Shelter.mode == CreatureShelterMode.Cave) cell = caves[caveIndex++] + Vector2Int.left;
            else if (profile.Shelter.mode == CreatureShelterMode.TreeShade) cell = new Vector2Int(24 + 6 * shadeIndex++, 26);
            else cell = new Vector2Int(37, 27);
            int stockBefore = progression.Stock(id);
            Check(progression.TryPlace(id, () => world.TrySpawn(profile, cell)) && progression.Stock(id) == stockBefore - 1,
                "normal specimen placement consumes one acquired " + id + " and creates its real actor");
            var actor = world.Agents.Last(); actors.Add(actor); Set(actor, "DecisionTimer", 0f);
            Check(park.Personnel.Research.Study(resource, 31 + actors.Count * 5), "production species research records partial knowledge of " + id);
        }
        Check(waterIndex == 4 && caveIndex == 3 && shadeIndex == 2 && actors.Count == 10, "all ten species use the expected mix of actual shelter journeys");
        for (int i = 0; i < 1200 && actors.Any(actor => actor.Activity != CreatureActivity.Sleeping); i++) world.Tick(.05f);
        Check(actors.All(actor => actor.Activity == CreatureActivity.Sleeping), "all ten imported species naturally settle into their configured sleeping behavior");
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i]; actor.Vitals.Restore(17 + 3 * i, 0, 19 + 2 * i); actor.Health.Damage(i + 1);
            actor.Wellbeing.Restore(63 + i, .04f + .04f * i);
            if (actor.Definition.Shelter.mode == CreatureShelterMode.Water)
                Check(grid.Habitat.IsWater(actor.Cell) && world.CaptureValidShelterHome(actor) == actor.Cell,
                    actor.Definition.name + " sleeps at a real persistent water home");
            if (actor.Definition.Shelter.mode == CreatureShelterMode.Cave)
                Check(world.IsHiddenInShelter(actor) && shelters.Registry.Contains(actor.Cell, ConstructedShelterKind.Cave),
                    actor.Definition.name + " sleeps inside an actual constructed cave");
        }
        string activeDifficulty = progression.Rules.difficulties.First(item => progression.Eligible("west-asia", item.id).Contains("Manticore")).id;
        Check(progression.Launch(expedition, "west-asia", activeDifficulty, 1), "a real ongoing Manticore expedition is retained for the round trip");
        var saved = park.CapturePark(); park.ValidateSave(saved);
        Check(saved.creatures.Count == 10 && AfricanSpeciesNativeChecks.Ids.All(id => saved.creatures.Count(item => item.species == id) == 1),
            "the production snapshot captures every new stable species exactly once");
        Check(saved.creatures.Count(item => item.hasShelterHome && item.shelterSleeping) == 9
            && saved.shelters.Count(item => item.kind == ConstructedShelterKind.Cave) == 3,
            "water, cave and shade sleepers retain nine real homes plus all constructed caves");
        Check(regions.All(saved.progression.regions.Contains)
            && AfricanSpeciesNativeChecks.Ids.All(id => saved.progression.inventory.Single(item => item.species == id).count > 0)
            && saved.progression.missions.Any(item => item.region == "west-asia" && item.species == "Manticore"),
            "the ordinary snapshot captures five regional purchases, acquired specimen stock and an active new-species expedition");
        slot = ParkSaveStore.NewManualSlotId();
        ParkSaveStore.WriteSlot(saved, slot, "African expansion validation");
        ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Check(SceneTransition.Load("GameScene"), "the disk slot reloads through the real production scene transition");
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0; park.Visitors.Definition.maximumVisitors = 0;
        world = Field<CreatureWorld>(park, "creatureWorld"); grid = Field<SquareBuildGrid>(park, "grid");
        var restored = park.CapturePark(); park.ValidateSave(restored);
        Check(ParkSaveStore.Pending == null && GameSaveMenu.IsOpen && Time.timeScale == 0, "production loading consumes the disk request and keeps the restored park paused");
        foreach (string id in AfricanSpeciesNativeChecks.Ids)
        {
            var expected = saved.creatures.Single(item => item.species == id); var loaded = restored.creatures.Single(item => item.species == id);
            var actor = world.Agents.Single(item => item.Definition.name == id);
            Check(actor.Definition == Resources.Load<CreatureDefinition>("Creatures/" + id)
                && loaded.cell == expected.cell && Mathf.Approximately(loaded.hunger, expected.hunger)
                && Mathf.Approximately(loaded.thirst, expected.thirst) && Mathf.Approximately(loaded.energy, expected.energy)
                && Mathf.Approximately(loaded.health, expected.health) && Mathf.Approximately(loaded.happiness, expected.happiness)
                && Mathf.Approximately(loaded.agitation, expected.agitation), id + " restores the real imported definition and exact individual needs, health and wellbeing");
            Check(loaded.hasShelterHome == expected.hasShelterHome && loaded.shelterSleeping == expected.shelterSleeping
                && (!expected.hasShelterHome || loaded.shelterHome == expected.shelterHome && actor.Activity == CreatureActivity.Sleeping),
                id + " restores its same physical shelter home and sleeping state");
            Check(park.Progression.Stock(id) == saved.progression.inventory.Single(item => item.species == id).count
                && Mathf.Approximately(park.Personnel.Research.Progress(actor.Definition), saved.research.Single(item => item.species == id).progress),
                id + " preserves acquired specimen inventory and independent species research progress");
            if (actor.Definition.Shelter.mode == CreatureShelterMode.Water)
                Check(world.IsSwimming(actor) && Mathf.Abs(actor.transform.position.y - (grid.Habitat.WaterLevel(actor.Cell)
                    + actor.Definition.bodyScale.y + actor.Definition.Aquatic.restingBodyOffset)) < .01f,
                    id + " immediately resumes its actual sleeping water-surface pose");
            if (actor.Definition.Shelter.mode == CreatureShelterMode.Cave)
                Check(world.IsHiddenInShelter(actor), id + " immediately resumes hidden cave sleep after scene loading");
        }
        Check(regions.All(park.Progression.HasAccess) && park.Progression.Missions.Count == 1
            && park.Progression.Missions[0].species == "Manticore"
            && Mathf.Approximately(park.Progression.Missions[0].remaining, saved.progression.missions.Single().remaining),
            "all purchased new regional access and active Manticore mission timing survive the real disk and scene load");
    }

    private static void Cleanup()
    {
        foreach (var profile in profiles) if (profile != null) Object.Destroy(profile); profiles.Clear();
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Cleanup escaped the owned African expansion test slot.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

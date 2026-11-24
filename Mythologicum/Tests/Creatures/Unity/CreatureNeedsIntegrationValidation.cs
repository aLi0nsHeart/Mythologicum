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

/// <summary>Run only in the ignored disposable project. Uses production simulation, inspection and disk/scene loading.</summary>
[InitializeOnLoad]
public static class CreatureNeedsIntegrationValidation
{
    private const string Running = "CreatureNeedsIntegrationValidation.running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static IEnumerator steps;
    private static double started;
    private static int checks;
    private static string slot;
    private static readonly List<CreatureDefinition> clones = new List<CreatureDefinition>();
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "CreatureNeedsIntegrationValidationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name, Hidden).SetValue(owner, value);
    }
    private static object Call(object owner, string name, params object[] values) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, values);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("CREATURE NEEDS PASS: " + message); }
    private static Vector2Int C(int x, int z) => new Vector2Int(x, z);
    private static bool Has(CreatureAgent actor, CreatureUnmetNeeds need) => (actor.Wellbeing.Scores.UnmetNeeds & need) != 0;
    static CreatureNeedsIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); started = EditorApplication.timeSinceStartup; checks = 0; steps = Run();
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "CreatureNeedsIntegrationValidation";
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
            if (SceneTransition.HasFailed || EditorApplication.timeSinceStartup - started > 240) throw new Exception("Needs validation failed scene preparation or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup();
            File.WriteAllText(Result, "PASS: " + checks + " imported tuning, screenshot needs, compound distress, urgent priorities, controlled-state care, localized inspection, recovery and production disk/scene continuity checks.");
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
    private static CreatureDefinition Clone(string id)
    {
        var resource = Resources.Load<CreatureDefinition>("Creatures/" + id);
        var profile = Object.Instantiate(resource); profile.name = id; clones.Add(profile);
        return profile;
    }
    private static void Freeze(CreatureAgent actor)
    {
        Set(actor, "DecisionTimer", 100000f); Set(actor, "WanderTimer", 100000f);
        Set(actor, "StateTimer", 100000f); Set(actor, "EscapeAfter", 100000f);
        Field<List<Vector2Int>>(actor, "Route").Clear(); Set(actor, "Goal", null);
    }
    private static void Advance(CreatureWorld world, float seconds)
    { for (int i = 0; i < Mathf.CeilToInt(seconds / .1f); i++) world.Tick(.1f); }

    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 50, depth = 50, seed = 246810, relief = 0, forestDensity = 0, featureSize = 28 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        park.Economy.UnlimitedFunds = true; park.Visitors.Definition.maximumVisitors = 0;
        Call(park, "SelectTool", Enum.ToObject(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), 0));
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "CreatureNeedsIntegrationValidation"
            && ParkSaveStore.SavePath.Contains("CreatureNeedsIntegrationValidation"), "the fixture owns isolated settings and saves");
        foreach (string id in new[] { "Waldschrat", "Thunderbird" })
        {
            var data = Resources.Load<CreatureDefinition>("Creatures/" + id).Behaviour;
            Check(Mathf.Approximately(data.energyWeight, .2f) && Mathf.Approximately(data.criticalNeedThreshold, 40)
                && Mathf.Approximately(data.criticalHappinessCap, 20) && Mathf.Approximately(data.criticalAgitation, .85f)
                && Mathf.Approximately(data.agitationBuildPerSecond, .2f) && Mathf.Approximately(data.agitationRecoveryPerSecond, .08f)
                && Mathf.Approximately(data.sleepSearchRetrySeconds, 5),
                "new designer controls import into " + id + "'s existing " + (id == "Waldschrat" ? "implicit" : "explicit") + " behaviour data");
        }
        checks += CreatureNeedsPriorityNativeChecks.RunChecks();
        foreach (string groupName in new[] { "Enrichment trees", "Natural rocks" })
        {
            var group = GameObject.Find(groupName);
            if (group != null) foreach (var item in group.GetComponentsInChildren<DemolitionTarget>()) item.Demolish();
        }
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var world = Field<CreatureWorld>(park, "creatureWorld"); var grid = Field<SquareBuildGrid>(park, "grid");
        var trees = Field<TreeConstruction>(park, "trees");
        Check(trees.TryPlace(C(20, 20)) && trees.TryPlace(C(22, 20)), "the screenshot fixture has two real reachable trees");
        var source = Resources.Load<CreatureDefinition>("Creatures/Waldschrat"); string sourceBefore = JsonUtility.ToJson(source);
        var profile = Clone("Waldschrat");
        profile.requiredTrees = 2; profile.environmentRadius = 14; profile.minimumSpace = 20;
        profile.initialHunger = 100; profile.hungerPerSecond = 0; profile.initialEnergy = 0;
        profile.energyDrainPerSecond = profile.sleepRecoveryPerSecond = 0;
        profile.Thirst.initialThirst = 54; profile.Thirst.thirstPerSecond = 0;
        profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
        Check(world.TrySpawn(profile, C(21, 22)), "the real placement system creates the screenshot's herbivore");
        var actor = world.Agents.Last();
        Check(actor.Wellbeing.Scores.Distress > .95f && Has(actor, CreatureUnmetNeeds.Hunger)
            && Has(actor, CreatureUnmetNeeds.Exhaustion) && actor.Wellbeing.Agitation == 0
            && Mathf.Approximately(actor.Wellbeing.Happiness, profile.Behaviour.initialHappiness),
            "newly placed paused creatures immediately expose real unmet needs without advancing their emotions");
        Freeze(actor); Set(actor, "Activity", CreatureActivity.SeekingSleep);
        actor.Wellbeing.Restore(61, 0); world.RefreshWellbeing();
        Check(actor.Vitals.Hunger == 100 && actor.Energy.Exhaustion == 100 && Mathf.Approximately(actor.Vitals.Thirst, 54),
            "screenshot needs are reproduced without changing a resource asset");
        Check(Mathf.Approximately(actor.BiomeSatisfaction, 50) && actor.NearbyTrees >= 2 && actor.ShelterSatisfaction == 100,
            "real grass samples produce half-suitable ground despite sufficient trees and shelter");
        Check(actor.Wellbeing.Scores.Happiness <= 20 && actor.Wellbeing.Scores.Distress > .95f && actor.Wellbeing.Agitation == 0,
            "paused inspection refresh computes the severe target without advancing saved emotions");
        Advance(world, 1);
        Check(actor.Wellbeing.Happiness > 50 && actor.Wellbeing.Agitation > .15f,
            "direct need pressure becomes visible before old happiness would have crossed fifty");
        Advance(world, 5);
        Check(actor.Wellbeing.Agitation > .9f && actor.Wellbeing.Happiness < 50,
            "persistent starvation and exhaustion make the actual creature strongly agitated");
        foreach (var need in new[] { CreatureUnmetNeeds.Hunger, CreatureUnmetNeeds.Exhaustion, CreatureUnmetNeeds.Thirst, CreatureUnmetNeeds.Ground })
            Check(Has(actor, need), "real sampled welfare diagnoses " + need);
        var inspection = Field<CreatureInspection>(park, "creatureInspection"); inspection.Select(actor); inspection.Refresh();
        string info = Field<Text>(inspection, "text").text;
        Check(info.Contains(Loc.Text("creature.info.agitated").Trim()) && info.Contains(Loc.Text("creature.needs.hunger"))
            && info.Contains(Loc.Text("creature.needs.exhaustion")) && info.Contains(Loc.Text("creature.needs.thirst"))
            && info.Contains(Loc.Text("creature.needs.ground")), "the real inspection panel explains agitation and each unmet need");
        Check(!info.Contains("creature.info.wellbeing") && !info.Contains("creature.needs."),
            "the added wellbeing information resolves localization IDs rather than exposing them");
        var saved = park.CapturePark(); park.ValidateSave(saved);
        var expected = saved.creatures.Single(item => item.species == "Waldschrat");
        Check(Mathf.Approximately(expected.agitation, actor.Wellbeing.Agitation) && expected.hunger == 100 && expected.energy == 0,
            "ordinary snapshots persist new emotional response using the existing save fields");
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "Creature needs validation");

        // Recover in the same physical park; emotional recovery must be gradual.
        actor.Vitals.Restore(0, 0, 0); Set(actor, "Energy", new CreatureEnergy(100)); Freeze(actor);
        for (int z = 14; z <= 29; z++) for (int x = 14; x <= 29; x++)
            if ((x + z) % 2 == 0) grid.Habitat.PaintBiome(new Vector2(x + .5f, z + .5f), .6f, HabitatBiome.Thicket, _ => true, out _);
        world.RefreshWellbeing();
        // Habitat edits invalidate routes and reset decision timers. Freeze only after
        // that refresh so this emotional-recovery check stays inside the cared-for area.
        Freeze(actor); float before = actor.Wellbeing.Agitation;
        Check(actor.Wellbeing.Scores.Happiness > 95 && actor.Wellbeing.Scores.Distress == 0,
            "food, rest, hydration and an appropriate physical biome resolve the care deficit");
        Advance(world, 1);
        Check(actor.Wellbeing.Agitation < before && actor.Wellbeing.Agitation > 0, "good care reduces agitation gradually");
        Advance(world, 30);
        Check(actor.Wellbeing.Happiness > 95 && actor.Wellbeing.Agitation == 0,
            $"sustained proper care restores calm and happiness; happiness={actor.Wellbeing.Happiness:R}, target={actor.Wellbeing.Scores.Happiness:R}, agitation={actor.Wellbeing.Agitation:R}, distress={actor.Wellbeing.Scores.Distress:R}, "
            + $"unmet={actor.Wellbeing.Scores.UnmetNeeds}, hunger={actor.Vitals.Hunger:R}, energy={actor.Energy.Energy:R}, thirst={actor.Vitals.Thirst:R}, cell={actor.Cell}, position={actor.transform.position}, activity={actor.Activity}, "
            + $"environment={actor.Wellbeing.Scores.Environment:R}, ground={actor.BiomeSatisfaction:R}, shelter={actor.ShelterSatisfaction:R}, waterHabitat={actor.WaterSatisfaction:R}, trees={actor.NearbyTrees}, space={actor.Wellbeing.Scores.Space:R}, herd={actor.Wellbeing.Scores.Herd:R}, "
            + $"happinessRate={actor.Definition.happinessChangePerSecond:R}, tuning={JsonUtility.ToJson(actor.Definition.Behaviour)}");
        Check(world.Sedate(actor, 20), "the keeper system can sedate the fixture creature");
        actor.Vitals.Restore(100, 0, 54); Set(actor, "Energy", new CreatureEnergy(0)); Advance(world, 1);
        Check(actor.IsSedated && actor.Wellbeing.Scores.Distress > .95f && actor.Wellbeing.Agitation > 0,
            "intrinsic needs and wellbeing continue updating under sedation");
        var spiritProfile = Clone("Yurei"); spiritProfile.initialHunger = 100; spiritProfile.hungerPerSecond = 0;
        spiritProfile.initialEnergy = 100; spiritProfile.energyDrainPerSecond = 0;
        Check(world.TrySpawn(spiritProfile, C(32, 32)), "a real food-free species participates in the same simulation");
        var spirit = world.Agents.Last(); Freeze(spirit); Advance(world, .2f); world.RefreshWellbeing();
        Check(spirit.Vitals.Hunger == 0 && !Has(spirit, CreatureUnmetNeeds.Hunger), "Yurei retains its explicit no-food exception without false starvation distress");
        Check(JsonUtility.ToJson(source) == sourceBefore, "temporary balancing profiles and biome tests leave the designer's resource unchanged");

        ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Check(SceneTransition.Load("GameScene"), "the saved crisis park reloads through ordinary production scene loading"); yield return null;
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        world = Field<CreatureWorld>(park, "creatureWorld"); actor = world.Agents.Single(item => item.Definition.name == "Waldschrat");
        Check(ParkSaveStore.Pending == null && GameSaveMenu.IsOpen, "loading consumes the isolated slot and leaves the park paused");
        Check(Mathf.Approximately(actor.Wellbeing.Happiness, expected.happiness) && Mathf.Approximately(actor.Wellbeing.Agitation, expected.agitation)
            && actor.Vitals.Hunger == 100 && actor.Energy.Energy == 0 && Mathf.Approximately(actor.Vitals.Thirst, 54),
            "actual scene loading preserves hunger, exhaustion, hydration, happiness and agitation");
        Check(actor.Wellbeing.Scores.Happiness <= 20 && actor.Wellbeing.Scores.Distress > .95f
            && Has(actor, CreatureUnmetNeeds.Hunger) && Has(actor, CreatureUnmetNeeds.Exhaustion),
            "the paused loaded creature already has authoritative need scores instead of zero-filled inspection data");
        inspection = Field<CreatureInspection>(park, "creatureInspection"); inspection.Select(actor); inspection.Refresh();
        Check(Field<Text>(inspection, "text").text.Contains(Loc.Text("creature.needs.exhaustion")),
            "the loaded object's real inspector immediately displays the persisted crisis");
    }

    private static void Cleanup()
    {
        foreach (var profile in clones) if (profile != null) Object.Destroy(profile); clones.Clear();
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Needs validation cleanup escaped its owned manual slot.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

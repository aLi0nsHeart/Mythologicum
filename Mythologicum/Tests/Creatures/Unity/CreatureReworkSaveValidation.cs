using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Actual park save round-trip for feeding enrichment and consumed escape budgets.</summary>
[InitializeOnLoad]
public static class CreatureReworkSaveValidation
{
    private const string Running = "CreatureReworkSaveValidation.running";
    private static IEnumerator run;
    private static int checks;
    private static double began;
    private static string slot;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "CreatureReworkSaveResults.txt");

    static CreatureReworkSaveValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); began = EditorApplication.timeSinceStartup; checks = 0; run = Run();
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "CreatureReworkSaveValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (run == null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 240) throw new Exception("Save integration timed out.");
            if (SceneTransition.HasFailed) throw new Exception("Park preparation failed.");
            if (run.MoveNext()) return;
            run = null; Cleanup(); File.WriteAllText(Result, "PASS: " + checks + " scarecrow, undead hunger, escape cooldown and legacy-save checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            run = null; Cleanup(); Debug.LogException(error); File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1);
        }
    }

    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 32, depth = 32, seed = 89231, relief = 0, featureSize = 28, forestDensity = 0 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>();
        park.enabled = false; Time.timeScale = 0;
        var grid = Field<SquareBuildGrid>(park, "grid");
        var construction = Field<BuildingConstruction>(park, "buildingConstruction");
        var world = Field<CreatureWorld>(park, "creatureWorld");
        var dummies = Field<ScarecrowConstruction>(park, "scarecrows");
        var entrance = BuildingCatalogDefinition.Current.buildings.First(item => item.kind == BuildingKind.ParkEntrance).Copy();
        BuildingFootprint? footprint = null;
        for (int y = 1; y < grid.Depth - entrance.depth && !footprint.HasValue; y++)
        for (int x = 1; x < grid.Width - entrance.width && !footprint.HasValue; x++)
        {
            var candidate = new BuildingFootprint(entrance, new Vector2Int(x, y), 0);
            if (construction.CanPlace(candidate)) footprint = candidate;
        }
        Check(footprint.HasValue && construction.TryPlace(footprint.Value), "Actual entrance can be placed");
        checks += ScarecrowConstructionNativeChecks.Run(park);
        Vector2Int? cell = null;
        for (int y = 1; y < grid.Depth - 1 && !cell.HasValue; y++)
        for (int x = 1; x < grid.Width - 1 && !cell.HasValue; x++)
        {
            var candidate = new Vector2Int(x, y);
            if (candidate != footprint.Value.AccessCell && dummies.CanPlace(candidate)) cell = candidate;
        }
        Check(cell.HasValue && dummies.TryPlace(cell.Value), "Actual procedural scarecrow can be placed");
        var yurei = Resources.Load<CreatureDefinition>("Creatures/Yurei");
        Check(yurei != null && !yurei.needsFood && yurei.StartingHunger == 0, "Shipped Yurei profile has no food requirement");
        // Enrichment is walkable, so the ghost can occupy the same tile.
        Check(world.TrySpawn(yurei, cell.Value), "A scarecrow remains walkable creature enrichment");
        var ghost = world.Agents.Last();
        ghost.Vitals.Restore(92, 17);
        Set(ghost, "EscapeIntent", true);
        Set(ghost, "EscapeAfter", world.SimulationTime + 4f);
        Set(ghost, "AttemptAfter", world.SimulationTime + 9f);
        Set(ghost, "ScareAfter", world.SimulationTime + 7f);
        var saved = park.CapturePark(); park.ValidateSave(saved);
        Check(saved.scarecrows.SequenceEqual(new[] { cell.Value }), "Save captures scarecrow locations");
        Check(saved.creatures.Single().escapeCooldown >= yurei.escapeRetrySeconds, "An in-flight intention is consumed on saving");
        Check(Mathf.Approximately(saved.creatures.Single().fenceRetryCooldown, 9), "Fence retry remaining time is captured");
        Check(Mathf.Approximately(saved.creatures.Single().scareCooldown, 7), "Scare retry remaining time is captured");
        var duplicate = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        duplicate.scarecrows.Add(cell.Value);
        bool rejected = false;
        try { park.ValidateSave(duplicate); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Duplicate scarecrow locations cannot enter a save");
        duplicate = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        duplicate.scarecrows[0] = footprint.Value.AccessCell; rejected = false;
        try { park.ValidateSave(duplicate); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "A saved scarecrow cannot obstruct a building entrance");
        duplicate = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        duplicate.scarecrows = null; park.ValidateSave(duplicate);
        Check(true, "Older save data without scarecrows remains valid");
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "Creature behaviour save validation");
        var read = ParkSaveStore.ReadSlot(slot);
        Check(read.scarecrows.Count == 1 && read.creatures.Single().escapeCooldown >= yurei.escapeRetrySeconds,
            "JSON save round-trip preserves enrichment and cooldown state");
        ParkSaveStore.Pending = read;
        Check(SceneTransition.Load("GameScene"), "Actual saved park begins restoration");
        while (SceneTransition.IsLoading) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        world = Field<CreatureWorld>(park, "creatureWorld"); ghost = world.Agents.Single(actor => !Field<bool>(actor, "ExternallyControlled"));
        Check(park.Environment.ScarecrowsAt(cell.Value) == 1, "Scarecrow exists in the restored park environment");
        Check(ghost.Vitals.Hunger == 0 && Mathf.Approximately(ghost.Vitals.Fear, 17), "Loading older hungry Yurei normalizes hunger while preserving fear");
        Check(!Field<bool>(ghost, "EscapeIntent") && Field<float>(ghost, "EscapeAfter") - world.SimulationTime > 0,
            "Loading cannot grant a free replacement escape attempt");
        Check(Field<float>(ghost, "AttemptAfter") - world.SimulationTime > 0
            && Field<float>(ghost, "ScareAfter") - world.SimulationTime > 0, "Remaining fence and scare cooldowns survive loading");
        world.Tick(2f);
        Check(ghost.Vitals.Hunger == 0, "Undead hunger remains full during subsequent simulation");
    }

    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Set(object owner, string name, object value)
        => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; Debug.Log("CREATURE SAVE PASS: " + message); }
    private static void Cleanup()
    {
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Save test cleanup escaped its owned slot.");
        foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

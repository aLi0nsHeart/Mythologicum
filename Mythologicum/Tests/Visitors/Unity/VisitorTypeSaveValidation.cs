using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual scene save/restore for visitor identities, needs and discovery history.</summary>
[InitializeOnLoad]
public static class VisitorTypeSaveValidation
{
    private const string Running = "VisitorTypeSaveValidation.running";
    private static IEnumerator run;
    private static int checks;
    private static double began;
    private static string slot;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "VisitorTypeSaveResults.txt");
    static VisitorTypeSaveValidation()
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
        PlayerSettings.productName = "VisitorTypeSaveValidation";
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
            if (EditorApplication.timeSinceStartup - began > 240 || SceneTransition.HasFailed)
                throw new Exception("Visitor save preparation failed or timed out.");
            if (run.MoveNext()) return;
            run = null; Cleanup(); File.WriteAllText(Result, "PASS: " + checks + " typed visitor, discovery history, legacy and actual park save checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        { run = null; Cleanup(); Debug.LogException(error); File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 32, depth = 32, seed = 89231, relief = 0, featureSize = 28, forestDensity = 0 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        var grid = Field<SquareBuildGrid>(park, "grid");
        var creatures = Field<CreatureWorld>(park, "creatureWorld");
        var visitors = park.Visitors;
        var restore = typeof(VisitorWorld).GetMethod("RestoreGuestWithType", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var type in new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children })
        {
            Vector2Int? cell = null;
            for (int y = 1; y < grid.Depth - 1 && !cell.HasValue; y++)
            for (int x = 1; x < grid.Width - 1 && !cell.HasValue; x++)
            {
                var candidate = new Vector2Int(x, y);
                if (grid.IsAvailable(candidate) && creatures.CanSpawn(candidate)) cell = candidate;
            }
            Check(cell.HasValue && grid.TryOccupy(cell.Value, BuildCellContent.Path), "Visitor fixture has clear path ground: " + type);
            var guest = (VisitorAgent)restore.Invoke(visitors, new object[] { cell.Value, type });
            Check(guest != null && guest.Type == type, "Typed visitor enters the actual park: " + type);
            guest.Actor.Vitals.Restore(34, 11);
            guest.Mood.Restore(77, 12, .18f, false);
            guest.RestoreInterests(new[] { "Unicorn", "Wolpertinger" }, new[] { "creature.origin.central_europe" });
        }
        var saved = park.CapturePark(); park.ValidateSave(saved);
        Check(saved.visitors.Count == 4 && saved.visitors.Select(item => item.visitorType).Distinct().Count() == 4,
            "The saved park captures every requested visitor type");
        Check(saved.visitors.All(item => item.observedSpecies.Count == 2 && item.observedOrigins.Count == 1),
            "Discovery history is captured before leaving the scene");
        var invalid = Clone(saved); invalid.visitors[0].visitorType = (VisitorType)999;
        Reject(() => park.ValidateSave(invalid), "Unknown visitor types are rejected before partial restoration");
        invalid = Clone(saved); invalid.visitors[0].observedSpecies.Add(invalid.visitors[0].observedSpecies[0]);
        Reject(() => park.ValidateSave(invalid), "Duplicate discovery history is rejected");
        invalid = Clone(saved); invalid.visitors[0].observedOrigins[0] = "";
        Reject(() => park.ValidateSave(invalid), "Empty discovery identifiers are rejected");
        var legacy = Clone(saved);
        foreach (var visitor in legacy.visitors)
        { visitor.visitorType = VisitorType.Legacy; visitor.observedSpecies = visitor.observedOrigins = null; }
        park.ValidateSave(legacy);
        Check(true, "Older general visitors without type or history remain valid");
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "Visitor types save validation");
        var read = ParkSaveStore.ReadSlot(slot);
        Check(read.visitors.Select(item => item.visitorType).SequenceEqual(saved.visitors.Select(item => item.visitorType)),
            "JSON round-trip preserves stable visitor type values");
        ParkSaveStore.Pending = read;
        Check(SceneTransition.Load("GameScene"), "Typed park uses the production scene-loading path");
        while (SceneTransition.IsLoading) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        Check(park.Visitors.Population == 4, "Loading retains every visitor without duplication");
        foreach (var type in new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children })
        {
            var guest = park.Visitors.Visitors.Single(item => item.Type == type);
            Check(guest.ObservedSpecies.SetEquals(new[] { "Unicorn", "Wolpertinger" })
                && guest.ObservedOrigins.SetEquals(new[] { "creature.origin.central_europe" }), "Loading preserves discovery history: " + type);
            Check(Mathf.Approximately(guest.Actor.Vitals.Hunger, 34) && Mathf.Approximately(guest.Actor.Vitals.Fear, 11)
                && Mathf.Approximately(guest.Mood.Satisfaction, 77) && Mathf.Approximately(guest.Mood.VisitProgress, .18f),
                "Loading preserves needs, satisfaction and patience: " + type);
            Check(guest.TypeSettings != null && guest.TypeSettings.type == type, "Loading restores the matching behavioral settings: " + type);
        }
    }
    private static ParkSaveData Clone(ParkSaveData value) => JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(value));
    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); checks++; Debug.Log("VISITOR SAVE PASS: " + reason); }
    private static void Reject(Action action, string reason)
    { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, reason); }
    private static void Cleanup()
    {
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Visitor save cleanup escaped its owned slot.");
        foreach (var suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

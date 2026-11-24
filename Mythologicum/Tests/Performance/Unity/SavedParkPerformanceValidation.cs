using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>CPU replay of a copied save. Run only inside the disposable validation project.</summary>
[InitializeOnLoad]
public static class SavedParkPerformanceValidation
{
    private const string Running = "SavedParkPerformanceValidation.running";
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const int MeasuredTicks = 1800;
    private const float StepSeconds = 1f / 30f;
    private static IEnumerator steps;
    private static double started;
    private static Report report;
    private static string output;
    private static readonly List<Sample> samples = new List<Sample>(MeasuredTicks);
    private static readonly List<SlowTick> slowest = new List<SlowTick>();

    [Serializable] public sealed class Timing
    {
        public string name;
        public int samples, over16Ms, over50Ms;
        public double medianMs, p95Ms, p99Ms, worstMs, totalMs;
    }
    [Serializable] public sealed class Sample
    {
        public int tick, gridRevision, fenceRevision, environmentRevision;
        public double totalMs, creaturesMs, staffMs, visitorsMs, otherMs;
    }
    [Serializable] public sealed class SlowTick
    {
        public Sample timing;
        public string[] creatures, staff;
    }
    [Serializable] public sealed class Report
    {
        public string fixture, tag, utcTime, unityVersion, status;
        public string scope = "Copied real save, production scene load and imported models; 1800 manual 1/30-second simulation ticks. CPU simulation and ordinary reports measured; excludes rendering, GPU, frame scheduling, input and autosave. No FPS claim.";
        public int width, depth, initialCreatures, initialVisitors, initialStaff, finalCreatures, finalVisitors, finalStaff, ticks;
        public float simulatedSeconds;
        public List<Timing> timings;
        public List<SlowTick> slowestTicks;
    }

    static SavedParkPerformanceValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); started = EditorApplication.timeSinceStartup; steps = Run();
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "SavedParkPerformanceValidation";
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
                throw new Exception("Saved park preparation or replay timed out/failed.");
            if (steps.MoveNext()) return;
            steps = null; WriteReport("PASS"); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            steps = null; WriteReport("FAIL: " + error); UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Members).GetValue(owner);
    private static Action Bind(object owner, string method) => owner == null ? (Action)(() => { })
        : (Action)Delegate.CreateDelegate(typeof(Action), owner, owner.GetType().GetMethod(method, Members, null, Type.EmptyTypes, null));
    private static Action<float> BindTick(object owner, string method) => (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), owner,
        owner.GetType().GetMethod(method, Members, null, new[] { typeof(float) }, null));
    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * (1000.0 / Stopwatch.Frequency);

    private static IEnumerator Run()
    {
        string fixture = Environment.GetEnvironmentVariable("MYTHOLOGICUM_PERF_FIXTURE") ?? "LatestPark";
        string tag = Environment.GetEnvironmentVariable("MYTHOLOGICUM_PERF_TAG") ?? "Baseline";
        if ((fixture != "LatestPark" && fixture != "HabitatPark") || tag.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-' && ch != '_'))
            throw new ArgumentException("Unknown fixture or unsafe report tag.");
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "PerformanceInvestigation");
        output = Path.Combine(directory, fixture + "-" + tag);
        samples.Clear(); slowest.Clear();
        report = new Report { fixture = fixture, tag = tag, utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        var save = JsonUtility.FromJson<ParkSaveData>(File.ReadAllText(Path.Combine(directory, fixture + ".json")));
        if (save == null || save.creatures == null || save.staff == null || save.visitors == null) throw new Exception("Fixture is not a complete park save.");
        UnityEngine.Random.InitState(712703); ParkSaveStore.Pending = save;
        if (!SceneTransition.Load("GameScene")) throw new Exception("Production scene loading refused the fixture.");
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        var creatures = Field<CreatureWorld>(park, "creatureWorld");
        var buildings = Field<BuildingConstruction>(park, "buildingConstruction");
        var fences = Field<FenceConstruction>(park, "fences");
        var grid = Field<SquareBuildGrid>(park, "grid"); var view = Field<Camera>(park, "view");
        var refreshTerrain = Bind(park, "RefreshTerrainAttachments");
        var refreshVegetation = Bind(Field<object>(park, "groundVegetation"), "Refresh");
        var refreshWater = Bind(Field<object>(park, "waterTroughs"), "Refresh");
        var windowEconomy = BindTick(park, "TickWindowEconomy");
        var reportRefreshes = new[] { "parkStatus", "creatureInspection", "buildingInspection", "visitorPresentation",
            "enclosurePresentation", "overlays", "management", "parkManagement", "notifications", "encyclopedia" }
            .Select(name => Bind(Field<object>(park, name), "Refresh")).ToArray();
        var history = Bind(park, "UpdateConstructionHistoryUI");
        var alerts = BindTick(Field<object>(park, "alerts"), "Tick");
        var palette = Field<ConstructionPalette>(park, "palette");
        var enclosures = Field<EnclosureMap>(park, "enclosureMap");
        report.width = grid.Width; report.depth = grid.Depth;
        report.initialCreatures = creatures.Agents.Count(actor => actor.Alive && (actor.Definition.tags & (CreatureTags.Visitor | CreatureTags.Staff)) == 0);
        report.initialVisitors = park.Visitors.Visitors.Count; report.initialStaff = park.Personnel.Staff.Count;
        if (report.initialCreatures != save.creatures.Count || report.initialVisitors != save.visitors.Count || report.initialStaff != save.staff.Count)
            throw new Exception("The replay did not restore the fixture actor counts exactly.");
        UnityEngine.Random.InitState(712703);
        for (int tick = 0; tick < MeasuredTicks; tick++)
        {
            if (EditorApplication.timeSinceStartup - started > 300) throw new Exception("Saved replay exceeded its execution limit.");
            long totalStart = Stopwatch.GetTimestamp(), segment = totalStart;
            refreshTerrain(); park.Economy.Advance(StepSeconds); refreshVegetation(); buildings.Refresh(view);
            double beforeMs = Elapsed(segment); segment = Stopwatch.GetTimestamp();
            creatures.Tick(StepSeconds); double creatureMs = Elapsed(segment); segment = Stopwatch.GetTimestamp();
            park.Personnel.Tick(StepSeconds); double staffMs = Elapsed(segment); segment = Stopwatch.GetTimestamp();
            refreshWater(); park.Progression.Tick(StepSeconds); fences.Refresh(view);
            double middleMs = Elapsed(segment); segment = Stopwatch.GetTimestamp();
            park.Visitors.Tick(StepSeconds); double visitorMs = Elapsed(segment); segment = Stopwatch.GetTimestamp();
            BuildingEconomy.Tick(buildings.Network, park.Economy, StepSeconds); windowEconomy(StepSeconds);
            foreach (var refresh in reportRefreshes.Take(4)) refresh();
            palette.Refresh(buildings.Network.HasEntrance); enclosures.Refresh(buildings.Network.IsPathConnected);
            reportRefreshes[4](); alerts(StepSeconds);
            for (int i = 5; i < reportRefreshes.Length; i++) reportRefreshes[i]();
            history(); Physics.SyncTransforms();
            double afterMs = Elapsed(segment);
            var sample = new Sample { tick = tick, totalMs = Elapsed(totalStart), creaturesMs = creatureMs, staffMs = staffMs, visitorsMs = visitorMs,
                otherMs = beforeMs + middleMs + afterMs, gridRevision = grid.Revision, fenceRevision = fences.Layout.Revision, environmentRevision = park.Environment.Revision };
            samples.Add(sample);
            if (slowest.Count < 24 || sample.totalMs > slowest[slowest.Count - 1].timing.totalMs)
            {
                slowest.Add(new SlowTick { timing = sample,
                    creatures = creatures.Agents.Where(actor => actor.Alive && (actor.Definition.tags & (CreatureTags.Visitor | CreatureTags.Staff)) == 0)
                        .Select(actor => actor.Definition.name + ":" + actor.Activity).ToArray(),
                    staff = park.Personnel.Staff.Select(worker => worker.Role + ":" + worker.Task).ToArray() });
                slowest.Sort((a,b) => b.timing.totalMs.CompareTo(a.timing.totalMs));
                if (slowest.Count > 24) slowest.RemoveAt(24);
            }
            if ((tick + 1) % 180 == 0) UnityEngine.Debug.Log("SAVED PARK REPLAY: " + fixture + " " + tag + " ticks=" + (tick + 1));
            if ((tick + 1) % 8 == 0) yield return null;
        }
        report.finalCreatures = creatures.Agents.Count(actor => actor.Alive && (actor.Definition.tags & (CreatureTags.Visitor | CreatureTags.Staff)) == 0);
        report.finalVisitors = park.Visitors.Visitors.Count; report.finalStaff = park.Personnel.Staff.Count;
    }

    private static Timing Describe(string name, Func<Sample, double> select)
    {
        var values = samples.Select(select).OrderBy(value => value).ToArray();
        return new Timing { name = name, samples = values.Length, medianMs = Percentile(values, .5), p95Ms = Percentile(values, .95),
            p99Ms = Percentile(values, .99), worstMs = values.Length == 0 ? 0 : values[values.Length - 1], totalMs = values.Sum(),
            over16Ms = values.Count(value => value > 16.7), over50Ms = values.Count(value => value > 50) };
    }
    private static double Percentile(double[] values, double fraction) => values.Length == 0 ? 0 : values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * fraction) - 1)];
    private static void WriteReport(string status)
    {
        if (report == null || output == null) { File.WriteAllText("SavedParkPerformanceResults.txt", status); return; }
        report.status = status; report.ticks = samples.Count; report.simulatedSeconds = samples.Count * StepSeconds;
        report.timings = new List<Timing> { Describe("Total", item => item.totalMs), Describe("Creatures", item => item.creaturesMs),
            Describe("Staff", item => item.staffMs), Describe("Visitors", item => item.visitorsMs), Describe("Other", item => item.otherMs) };
        report.slowestTicks = slowest;
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output + ".json", JsonUtility.ToJson(report, true));
        using (var writer = new StreamWriter(output + ".csv"))
        {
            writer.WriteLine("tick,totalMs,creaturesMs,staffMs,visitorsMs,otherMs,gridRevision,fenceRevision,environmentRevision");
            foreach (var sample in samples) writer.WriteLine(string.Join(",", new[] { sample.tick.ToString(), sample.totalMs.ToString("F6", CultureInfo.InvariantCulture),
                sample.creaturesMs.ToString("F6", CultureInfo.InvariantCulture), sample.staffMs.ToString("F6", CultureInfo.InvariantCulture), sample.visitorsMs.ToString("F6", CultureInfo.InvariantCulture),
                sample.otherMs.ToString("F6", CultureInfo.InvariantCulture), sample.gridRevision.ToString(), sample.fenceRevision.ToString(), sample.environmentRevision.ToString() }));
        }
        string summary = status + ": " + report.fixture + "/" + report.tag + " " + report.ticks + " ticks\n" + string.Join("\n", report.timings.Select(item =>
            $"{item.name}: median {item.medianMs:F3} ms; p95 {item.p95Ms:F3}; p99 {item.p99Ms:F3}; worst {item.worstMs:F3}; >16.7ms {item.over16Ms}; >50ms {item.over50Ms}"));
        File.WriteAllText(output + ".txt", summary); File.WriteAllText("SavedParkPerformanceResults.txt", summary); UnityEngine.Debug.Log(summary);
    }
}

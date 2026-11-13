using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Mythologicum.Editor
{
    [Serializable]
    public sealed class ParkBenchmarkProfile
    {
        public string name;
        public int creatures, visitors, staff;
        public ParkBenchmarkProfile(string name, int creatures, int visitors, int staff)
        { this.name = name; this.creatures = creatures; this.visitors = visitors; this.staff = staff; }
    }
    [Serializable]
    public sealed class ParkBenchmarkTiming
    {
        public int samples;
        public double meanMs, p50Ms, p95Ms, maximumMs;
        public static ParkBenchmarkTiming From(List<double> values)
        {
            var sorted = values.OrderBy(value => value).ToArray();
            return new ParkBenchmarkTiming { samples = sorted.Length, meanMs = sorted.Average(),
                p50Ms = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1],
                p95Ms = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], maximumMs = sorted[sorted.Length - 1] };
        }
    }
    [Serializable]
    public sealed class ParkBenchmarkAllocation
    {
        public int samples;
        public double meanBytes, p50Bytes, p95Bytes, maximumBytes;
        public static ParkBenchmarkAllocation From(List<double> values)
        {
            var sorted = values.OrderBy(value => value).ToArray();
            return new ParkBenchmarkAllocation { samples = sorted.Length, meanBytes = sorted.Average(),
                p50Bytes = sorted[(int)Math.Ceiling(sorted.Length * .5) - 1],
                p95Bytes = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1], maximumBytes = sorted[sorted.Length - 1] };
        }
    }
    [Serializable]
    public sealed class ParkBenchmarkResult
    {
        public string name;
        public int creatures, visitors, staff, totalActors, connectedPathTiles, mapSize;
        public int endingCreatures, endingVisitors, endingStaff;
        public ParkBenchmarkTiming creatureTick, visitorTick, staffTick, totalTick;
        public ParkBenchmarkAllocation creatureAllocatedBytes, visitorAllocatedBytes, staffAllocatedBytes, totalAllocatedBytes;
    }
    [Serializable]
    public sealed class ParkBenchmarkReport
    {
        public string utcTime, unityVersion, operatingSystem, processor, graphicsDevice;
        public string workload = "CPU simulation fixture with flat ground, connected paths and placeholder actors. Neutral species and held weapon cooldowns prevent deaths, departures and shot effects; original game assets and scenes are untouched. Rendering, UI, imported art, combat effects and mass construction invalidation are excluded. Timings measure this editor/hardware/run and are not a game FPS claim.";
        public int seed, warmupTicks, measuredTicks;
        public float simulationStep;
        public bool allocationCounterAvailable;
        public List<ParkBenchmarkResult> profiles = new List<ParkBenchmarkResult>();
        private static bool HasAllocationCounter()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[8192];
            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);
            return after - before >= probe.Length;
        }
        public ParkBenchmarkReport() { allocationCounterAvailable = HasAllocationCounter(); }
    }

    /// <summary>Repeatable real-world simulation timing, kept outside all authored park scenes.</summary>
    public sealed class LargeParkBenchmark : EditorWindow
    {
        [SerializeField] private int seed = 2471, mapSize = 32, warmupTicks = 10, measuredTicks = 60;
        [SerializeField] private float simulationStep = .1f;
        [SerializeField] private int smallCreatures = 20, smallVisitors = 25, smallStaff = 6;
        [SerializeField] private int mediumCreatures = 50, mediumVisitors = 80, mediumStaff = 12;
        [SerializeField] private int largeCreatures = 100, largeVisitors = 150, largeStaff = 24;
        private Run run;
        private string resultMessage;
        private Vector2 scroll;

        [MenuItem("Mythologicum/Gameplay/Run large park benchmark")]
        public static void Open()
        {
            var window = GetWindow<LargeParkBenchmark>("Park performance");
            window.minSize = new Vector2(480, 450); window.Show();
        }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Measure the real creature, visitor and staff simulations with repeatable populations. The benchmark creates and removes a temporary additive scene. Run outside Play mode. Results are written to LocalCache/Performance.", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUI.DisabledScope(run != null))
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                mapSize = EditorGUILayout.IntSlider("Map width/depth", mapSize, 24, 64);
                warmupTicks = EditorGUILayout.IntSlider("Warm-up ticks", warmupTicks, 1, 100);
                measuredTicks = EditorGUILayout.IntSlider("Measured ticks", measuredTicks, 10, 600);
                simulationStep = EditorGUILayout.Slider("Simulation seconds per tick", simulationStep, .02f, .25f);
                Counts("Small", ref smallCreatures, ref smallVisitors, ref smallStaff);
                Counts("Medium", ref mediumCreatures, ref mediumVisitors, ref mediumStaff);
                Counts("Large", ref largeCreatures, ref largeVisitors, ref largeStaff);
                if (GUILayout.Button("Run benchmark"))
                {
                    try
                    {
                        run = new Run(new[] { new ParkBenchmarkProfile("Small", smallCreatures, smallVisitors, smallStaff),
                            new ParkBenchmarkProfile("Medium", mediumCreatures, mediumVisitors, mediumStaff),
                            new ParkBenchmarkProfile("Large", largeCreatures, largeVisitors, largeStaff) },
                            mapSize, seed, warmupTicks, measuredTicks, simulationStep);
                        resultMessage = "Running…"; EditorApplication.update += Advance;
                    }
                    catch (Exception error) { resultMessage = error.Message; run?.Dispose(); run = null; }
                }
            }
            if (run != null && GUILayout.Button("Cancel benchmark")) Stop("Cancelled. Temporary objects removed.");
            if (!string.IsNullOrEmpty(resultMessage)) EditorGUILayout.HelpBox(resultMessage, MessageType.None);
            EditorGUILayout.EndScrollView();
        }
        private static void Counts(string title, ref int creatures, ref int visitors, ref int staff)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField(title + " population", EditorStyles.boldLabel);
            creatures = Mathf.Max(1, EditorGUILayout.IntField("Creatures", creatures));
            visitors = Mathf.Max(1, EditorGUILayout.IntField("Visitors", visitors));
            staff = Mathf.Max(3, EditorGUILayout.IntField("Staff", staff));
        }
        private void Advance()
        {
            try
            {
                var budget = Stopwatch.StartNew();
                for (int i = 0; i < 20 && budget.ElapsedMilliseconds < 35; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Large park benchmark", run.Status, run.Progress))
                    { Stop("Cancelled. Temporary objects removed."); return; }
                    if (!run.Step()) { string path = WriteReport(run.Report); Stop("Finished. Report: " + path); return; }
                }
                Repaint();
            }
            catch (Exception error) { Debug.LogException(error); Stop("Benchmark failed: " + error.Message); }
        }
        private void Stop(string message)
        {
            EditorApplication.update -= Advance; EditorUtility.ClearProgressBar();
            run?.Dispose(); run = null; resultMessage = message; Repaint();
        }
        private void OnDisable() => Stop(resultMessage);

        /// <summary>Batch entry point for an isolated Unity project with no Play mode or UI test fixture active.</summary>
        public static void RunBlocking()
        {
            try
            {
                var report = RunValidationBlocking();
                string path = WriteReport(report);
                Debug.Log("PARK BENCHMARK PASS: " + path);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Directory.CreateDirectory(ReportDirectory);
                File.WriteAllText(Path.Combine(ReportDirectory, "Benchmark-Failure.txt"), error.ToString());
                Debug.LogException(error); EditorApplication.Exit(1);
            }
        }
        public static ParkBenchmarkReport RunValidationBlocking()
        {
            using (var run = new Run(new[] { new ParkBenchmarkProfile("Small", 20, 25, 6),
                new ParkBenchmarkProfile("Medium", 50, 80, 12), new ParkBenchmarkProfile("Large", 100, 150, 24) }, 32, 2471, 10, 60, .1f))
            {
                while (run.Step()) { }
                return run.Report;
            }
        }
        private static string ReportDirectory => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "Performance");
        public static string WriteReport(ParkBenchmarkReport report)
        {
            Directory.CreateDirectory(ReportDirectory);
            string stem = "ParkBenchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string json = Path.Combine(ReportDirectory, stem + ".json");
            File.WriteAllText(json, JsonUtility.ToJson(report, true));
            var lines = new List<string> { "profile,creatures,visitors,staff,totalActors,pathTiles,subsystem,samples,meanMs,p50Ms,p95Ms,maxMs,meanAllocatedBytes,p95AllocatedBytes" };
            foreach (var profile in report.profiles)
                foreach (var entry in new[] { ("Creatures", profile.creatureTick), ("Visitors", profile.visitorTick),
                    ("Staff", profile.staffTick), ("Total", profile.totalTick) })
                {
                    var allocation = entry.Item1 == "Creatures" ? profile.creatureAllocatedBytes
                        : entry.Item1 == "Visitors" ? profile.visitorAllocatedBytes
                        : entry.Item1 == "Staff" ? profile.staffAllocatedBytes : profile.totalAllocatedBytes;
                    lines.Add(string.Join(",", profile.name, profile.creatures, profile.visitors, profile.staff, profile.totalActors,
                        profile.connectedPathTiles, entry.Item1, entry.Item2.samples,
                        entry.Item2.meanMs.ToString("0.000", CultureInfo.InvariantCulture), entry.Item2.p50Ms.ToString("0.000", CultureInfo.InvariantCulture),
                        entry.Item2.p95Ms.ToString("0.000", CultureInfo.InvariantCulture), entry.Item2.maximumMs.ToString("0.000", CultureInfo.InvariantCulture),
                        allocation != null ? allocation.meanBytes.ToString("0", CultureInfo.InvariantCulture) : "",
                        allocation != null ? allocation.p95Bytes.ToString("0", CultureInfo.InvariantCulture) : ""));
                }
            File.WriteAllLines(Path.ChangeExtension(json, ".csv"), lines);
            File.WriteAllText(Path.Combine(ReportDirectory, "LatestBenchmark.txt"), json);
            return json;
        }

        private sealed class Run : IDisposable
        {
            private readonly ParkBenchmarkProfile[] profiles;
            private readonly int size, warmup, measured;
            private readonly float dt;
            private int index, tick;
            private Fixture fixture;
            private readonly List<double> creatureTimes = new List<double>(), visitorTimes = new List<double>(),
                staffTimes = new List<double>(), totalTimes = new List<double>();
            private readonly List<double>[] allocations = { new List<double>(), new List<double>(), new List<double>(), new List<double>() };
            public ParkBenchmarkReport Report { get; }
            public string Status => index >= profiles.Length ? "Complete" : profiles[index].name + ": tick " + tick + " / " + (warmup + measured);
            public float Progress => (index + (float)tick / (warmup + measured)) / profiles.Length;
            public Run(ParkBenchmarkProfile[] profiles, int size, int seed, int warmup, int measured, float dt)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before running the benchmark.");
                for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(sceneIndex).path))
                        throw new InvalidOperationException("Save any untitled scenes before running the benchmark. The benchmark leaves your open scenes unchanged.");
                if (profiles == null || profiles.Length == 0 || size < 24 || warmup < 0 || measured < 1 || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt))
                    throw new ArgumentException("Choose valid benchmark dimensions, populations and tick settings.");
                foreach (var profile in profiles)
                    if (profile == null || profile.creatures < 1 || profile.visitors < 1 || profile.staff < 3
                        || (long)profile.creatures + profile.visitors + profile.staff > (long)size * size)
                        throw new ArgumentException("Each benchmark profile needs positive populations that fit inside its map.");
                this.profiles = profiles; this.size = size; this.warmup = warmup; this.measured = measured; this.dt = dt;
                Report = new ParkBenchmarkReport { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                    operatingSystem = SystemInfo.operatingSystem, processor = SystemInfo.processorType, graphicsDevice = SystemInfo.graphicsDeviceName,
                    seed = seed, warmupTicks = warmup, measuredTicks = measured, simulationStep = dt };
            }
            public bool Step()
            {
                if (index >= profiles.Length) return false;
                if (fixture == null)
                {
                    fixture = new Fixture(profiles[index], size, Report.seed);
                    creatureTimes.Clear(); visitorTimes.Clear(); staffTimes.Clear(); totalTimes.Clear(); tick = 0;
                    foreach (var samples in allocations) samples.Clear();
                    return true;
                }
                fixture.Tick(dt, tick >= warmup ? creatureTimes : null, visitorTimes, staffTimes, totalTimes, allocations);
                tick++;
                if (tick < warmup + measured) return true;
                fixture.ValidateCounts();
                var profile = profiles[index];
                Report.profiles.Add(new ParkBenchmarkResult { name = profile.name, creatures = profile.creatures, visitors = profile.visitors,
                    staff = profile.staff, totalActors = fixture.Creatures.Population, connectedPathTiles = fixture.PathTiles, mapSize = size,
                    endingCreatures = fixture.Creatures.Agents.Count(actor => actor.Alive && (actor.Definition.tags & (CreatureTags.Visitor | CreatureTags.Staff)) == 0),
                    endingVisitors = fixture.Visitors.Population, endingStaff = fixture.Staff.Staff.Count,
                    creatureTick = ParkBenchmarkTiming.From(creatureTimes), visitorTick = ParkBenchmarkTiming.From(visitorTimes),
                    staffTick = ParkBenchmarkTiming.From(staffTimes), totalTick = ParkBenchmarkTiming.From(totalTimes),
                    creatureAllocatedBytes = Report.allocationCounterAvailable ? ParkBenchmarkAllocation.From(allocations[0]) : null,
                    visitorAllocatedBytes = Report.allocationCounterAvailable ? ParkBenchmarkAllocation.From(allocations[1]) : null,
                    staffAllocatedBytes = Report.allocationCounterAvailable ? ParkBenchmarkAllocation.From(allocations[2]) : null,
                    totalAllocatedBytes = Report.allocationCounterAvailable ? ParkBenchmarkAllocation.From(allocations[3]) : null });
                fixture.Dispose(); fixture = null; index++; return index < profiles.Length;
            }
            public void Dispose() { fixture?.Dispose(); fixture = null; }
        }

        private sealed class Fixture : IDisposable
        {
            private Scene scene;
            private readonly List<Object> owned = new List<Object>();
            private readonly ParkBenchmarkProfile requested;
            private Random.State randomState;
            private readonly Stopwatch timer = new Stopwatch();
            public CreatureWorld Creatures { get; private set; }
            public VisitorWorld Visitors { get; private set; }
            public StaffWorld Staff { get; private set; }
            public int PathTiles { get; private set; }
            public Fixture(ParkBenchmarkProfile profile, int size, int seed)
            {
                requested = profile;
                Scene oldScene = SceneManager.GetActiveScene(); var oldRandom = Random.state;
                try
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    scene.name = "Temporary park benchmark"; SceneManager.SetActiveScene(scene); Random.InitState(seed);
                    var grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2f, size, size);
                    var fences = new FenceLayout(grid);
                    var buildings = new ParkBuildingNetwork(grid, fences);
                    var entrance = Place(buildings, BuildingKind.ParkEntrance, 1);
                    var research = Place(buildings, BuildingKind.ResearchCentre, 4);
                    var mercenary = Place(buildings, BuildingKind.MercenaryHQ, 8);
                    var keeper = Place(buildings, BuildingKind.ZookeeperHut, 12);
                    Place(buildings, BuildingKind.Kiosk, 16); Place(buildings, BuildingKind.RestArea, 20);
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                        if ((y % 4 == 0 || x % 4 == 0) && grid.IsAvailable(new Vector2Int(x, y))) grid.TryOccupy(new Vector2Int(x, y), BuildCellContent.Path);
                    buildings.Refresh();
                    var pathCells = new List<Vector2Int>(); var groundCells = new List<Vector2Int>();
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        if (buildings.IsPathConnected(cell)) pathCells.Add(cell);
                        else if (grid.IsAvailable(cell)) groundCells.Add(cell);
                    }
                    PathTiles = pathCells.Count;
                    if (!entrance.IsOpen || !research.IsOpen || !mercenary.IsOpen || !keeper.IsOpen
                        || groundCells.Count < profile.creatures || pathCells.Count < profile.visitors + profile.staff)
                        throw new InvalidOperationException("Choose a larger map for the requested populations.");
                    var source = Resources.Load<CreatureDefinition>("Creatures/Goat");
                    if (source == null) throw new InvalidOperationException("The goat profile is missing.");
                    var species = Own(Object.Instantiate(source)); species.name = "Benchmark animal"; species.displayName = "Benchmark animal";
                    species.visualPrefab = null; species.diet = CreatureDiet.None; species.tags = CreatureTags.Goat;
                    species.damage = 0; species.threat = 1000; species.initialHunger = 0; species.hungerPerSecond = 0;
                    species.energyDrainPerSecond = 0; species.fearRange = 0; species.minimumSpace = 0;
                    species.randomEscapeChance = species.hungryEscapeChance = species.agitatedEscapeChance = species.crowdedEscapeChance = 0;
                    var settings = Own(ScriptableObject.CreateInstance<CreatureSimulationDefinition>());
                    settings.populationLimit = profile.creatures + profile.visitors + profile.staff;
                    settings.placementSpacingTiles = 0;
                    var material = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")));
                    Creatures = new CreatureWorld(grid, fences, buildings, null, material, new[] { species }, null, settings);
                    for (int i = 0; i < profile.creatures; i++)
                        if (!Creatures.TrySpawn(species, groundCells[i])) throw new InvalidOperationException("Creature fixture population was capped or blocked.");
                    var visitorSettings = Own(ScriptableObject.CreateInstance<VisitorDefinition>());
                    visitorSettings.people = null; visitorSettings.maximumVisitors = profile.visitors; visitorSettings.spawnInterval = 100000;
                    visitorSettings.initialHunger = visitorSettings.hungerPerSecond = visitorSettings.energyDrainPerSecond = 0;
                    visitorSettings.initialEnergy = 100; visitorSettings.threat = 2000; visitorSettings.minimumStaySeconds = 100000;
                    visitorSettings.maximumStaySeconds = 100000; visitorSettings.mood.initialSatisfaction = 100;
                    visitorSettings.mood.patienceSeconds = 100000;
                    var economy = new ParkEconomy(1000000); economy.UnlimitedFunds = true;
                    Visitors = new VisitorWorld(grid, fences, buildings, Creatures, visitorSettings, economy);
                    var restoreGuest = typeof(VisitorWorld).GetMethod("RestoreGuest", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int i = 0; i < profile.visitors; i++)
                        if (restoreGuest.Invoke(Visitors, new object[] { pathCells[i] }) == null) throw new InvalidOperationException("Visitor fixture population was capped or blocked.");
                    var staffSettings = Own(ScriptableObject.CreateInstance<StaffDefinition>());
                    staffSettings.mercenaryDamage = 0; staffSettings.mercenaryStrength = staffSettings.civilianThreat = 2000;
                    staffSettings.researchPerSecond = 0;
                    Staff = new StaffWorld(grid, fences, buildings, Creatures, economy, null, new[] { species }, staffSettings);
                    var restoreStaff = typeof(StaffWorld).GetMethod("RestoreEmployee", BindingFlags.Instance | BindingFlags.NonPublic);
                    var homes = new[] { research, mercenary, keeper };
                    for (int i = 0; i < profile.staff; i++)
                        if (restoreStaff.Invoke(Staff, new object[] { homes[i % homes.Length], pathCells[profile.visitors + i] }) == null)
                            throw new InvalidOperationException("Staff fixture population was capped or blocked.");
                    // Keep actual approach/group/navigation work while omitting editor-unsafe delayed shot-effect destruction.
                    var shotTimer = typeof(StaffAgent).GetField("ShotTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var worker in Staff.Staff) shotTimer.SetValue(worker, 100000f);
                    foreach (var renderer in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Renderer>(true))) renderer.enabled = false;
                    Physics.SyncTransforms(); randomState = Random.state; ValidateCounts();
                }
                catch { Dispose(); throw; }
                finally { if (oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene); Random.state = oldRandom; }
            }
            private T Own<T>(T value) where T : Object { value.hideFlags = HideFlags.HideAndDontSave; owned.Add(value); return value; }
            private static ParkBuildingRecord Place(ParkBuildingNetwork buildings, BuildingKind kind, int x)
            {
                var definition = new BuildingDefinition { kind = kind, width = 1, depth = 1, entranceColumn = 0, staffCapacity = 10000 };
                var record = buildings.TryPlace(new BuildingFootprint(definition, new Vector2Int(x, 1), 0));
                if (record == null) throw new InvalidOperationException("Could not place benchmark workplace: " + kind);
                return record;
            }
            public void Tick(float dt, List<double> creatureTimes, List<double> visitorTimes, List<double> staffTimes, List<double> totalTimes, List<double>[] allocations)
            {
                var oldScene = SceneManager.GetActiveScene(); var oldRandom = Random.state;
                try
                {
                    SceneManager.SetActiveScene(scene); Random.state = randomState;
                    long totalStart = Stopwatch.GetTimestamp();
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    timer.Restart(); Creatures.Tick(dt); timer.Stop(); double creature = timer.Elapsed.TotalMilliseconds;
                    long allocatedCreatures = GC.GetAllocatedBytesForCurrentThread();
                    timer.Restart(); Staff.Tick(dt); timer.Stop(); double staff = timer.Elapsed.TotalMilliseconds;
                    long allocatedStaff = GC.GetAllocatedBytesForCurrentThread();
                    timer.Restart(); Visitors.Tick(dt); timer.Stop(); double visitor = timer.Elapsed.TotalMilliseconds;
                    long allocatedVisitors = GC.GetAllocatedBytesForCurrentThread();
                    if (creatureTimes != null)
                    {
                        creatureTimes.Add(creature); visitorTimes.Add(visitor); staffTimes.Add(staff);
                        totalTimes.Add((Stopwatch.GetTimestamp() - totalStart) * 1000d / Stopwatch.Frequency);
                        allocations[0].Add(allocatedCreatures - allocatedBefore); allocations[1].Add(allocatedVisitors - allocatedStaff);
                        allocations[2].Add(allocatedStaff - allocatedCreatures); allocations[3].Add(allocatedVisitors - allocatedBefore);
                    }
                    randomState = Random.state;
                }
                finally { if (oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene); Random.state = oldRandom; }
            }
            public void ValidateCounts()
            {
                if (Creatures == null || Visitors == null || Staff == null || Creatures.Population != requested.creatures + requested.visitors + requested.staff
                    || Visitors.Population != requested.visitors || Staff.Staff.Count != requested.staff)
                    throw new InvalidOperationException("Benchmark actor counts changed or were silently capped.");
            }
            public void Dispose()
            {
                // The runtime Dispose methods use delayed Destroy; editor fixtures clean their owned assets immediately instead.
                if (Visitors != null) DestroyPrivate(Visitors, "actorProfile");
                if (Staff != null)
                {
                    var profiles = (IDictionary)typeof(StaffWorld).GetField("profiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Staff);
                    foreach (Object profile in profiles.Values) if (profile != null) Object.DestroyImmediate(profile);
                    DestroyPrivate(Staff, "shotMaterial");
                }
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value);
                owned.Clear(); Creatures = null; Visitors = null; Staff = null;
            }
            private static void DestroyPrivate(object owner, string field)
            {
                var value = typeof(StaffWorld) == owner.GetType() ? typeof(StaffWorld).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner)
                    : typeof(VisitorWorld).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
                if (value is Object target && target != null) Object.DestroyImmediate(target);
            }
        }
    }
}

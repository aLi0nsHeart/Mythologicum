using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Arrival CPU timings and invariants; copy only into the disposable Unity validation project.</summary>
public static class VisitorArrivalPerformanceValidation
{
    [Serializable] public sealed class Timing
    {
        public string name;
        public int samples;
        public double coldMs, medianMs, worstMs;
    }
    [Serializable] public sealed class Report
    {
        public string utcTime, unityVersion;
        public string scope = "128x128 real flat GridHeightField, imported visitor models. Edit-mode CPU only: no rendering, skinning, GPU uploads, or frame-rate claim.";
        public int mapSize = 128, connectedPathTiles, exhibits = 48, checks;
        public List<Timing> timings = new List<Timing>();
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static object Call(object owner, string name, params object[] values)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, values);
    private static void Check(bool condition, string reason)
    { if (!condition) throw new Exception("Visitor arrivals: " + reason); checks++; }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            var report = RunChecks();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "Performance", "VisitorArrivals");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "Latest.json"), JsonUtility.ToJson(report, true));
            string text = string.Join("\n", report.timings.Select(item =>
                $"{item.name}: first {item.coldMs:F3} ms, median {item.medianMs:F3} ms, worst {item.worstMs:F3} ms ({item.samples} samples)"));
            File.WriteAllText(Path.Combine(folder, "Latest.txt"), report.scope + "\n" + text);
            File.WriteAllText("VisitorArrivalPerformanceResults.txt", "PASS: " + report.checks + " visitor arrival, profiles, admission and fixture checks.\n" + text);
            UnityEngine.Debug.Log("VISITOR ARRIVAL PERFORMANCE PASS: " + text); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorArrivalPerformanceResults.txt", "FAIL: " + error);
            UnityEngine.Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static Report RunChecks()
    {
        checks = 0;
        var report = new Report { utcTime = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion };
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        int actorCount = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var random = UnityEngine.Random.state;
        var owned = new HashSet<Object>();
        var worlds = new List<VisitorWorld>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var terrain = new GridHeightField(report.mapSize, report.mapSize, 2);
            var grid = new SquareBuildGrid(new Vector3(30000, 0, 30000), 2, report.mapSize, report.mapSize, terrain);
            var fences = new FenceLayout(grid);
            var buildings = new ParkBuildingNetwork(grid, fences);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1, admissionPrice = 1 }, new Vector2Int(3, 3), 0));
            for (int z = 0; z < grid.Depth; z++)
            for (int x = 0; x < grid.Width; x++)
                if ((x % 6 == 2 || z % 6 == 2) && grid.TryOccupy(new Vector2Int(x, z), BuildCellContent.Path)) report.connectedPathTiles++;
            buildings.Refresh();
            Check(entrance != null && entrance.IsOpen && report.connectedPathTiles > 4000,
                "the fixture has a real paid entrance and large connected public path network");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.populationLimit = 512; simulation.placementSpacingTiles = 0;
            var profiles = new CreatureDefinition[4];
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(profile); profiles[i] = profile;
                profile.name = "Arrival exhibit " + i; profile.displayName = "Arrival exhibit";
                profile.minimumSpace = 0; profile.diet = CreatureDiet.None; profile.tags = CreatureTags.Goat; profile.threat = 0;
                profile.rating = 3; profile.origin = "Arrival origin " + i;
            }
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, profiles, null, simulation);
            for (int i = 0; i < report.exhibits; i++)
                Check(creatures.TrySpawn(profiles[i % profiles.Length], new Vector2Int(7 + i % 8 * 12, 7 + i / 8 * 18)), "an exhibit actor is placed");
            var definition = Object.Instantiate(Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors")); owned.Add(definition);
            var people = Object.Instantiate(Resources.Load<PeopleDefinition>("People/People")); owned.Add(people);
            var adults = people.VisitorModels(false); var children = people.VisitorModels(true);
            Check(adults.Length > 0 && children.Length > 0, "real retained adult and child pools are available");
            definition.people = people; definition.maximumVisitors = 256; definition.spawnInterval = 100000;
            definition.minimumStaySeconds = 10000; definition.maximumStaySeconds = 20000;
            var economy = new ParkEconomy(10000);
            var visitors = new VisitorWorld(grid, fences, buildings, creatures, definition, economy); worlds.Add(visitors);
            var access = entrance.Footprint.AccessCell;
            var types = new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children };
            VisitorAgent current = null;
            void RemoveGuest()
            {
                if (current == null) return;
                Field<List<VisitorAgent>>(visitors, "visitors").Remove(current);
                Field<List<CreatureAgent>>(creatures, "agents").Remove(current.Actor);
                Object.DestroyImmediate(current.gameObject); current = null;
            }
            void SelectModel(int index)
            {
                people.adultVisitors = new[] { adults[index % adults.Length] };
                people.childVisitors = new[] { children[index % children.Length] };
            }
            void Measure(string name, Action<int> prepare, Action<int> operation, Action cleanup = null, int count = 16)
            {
                var values = new double[count];
                for (int i = 0; i < count; i++)
                {
                    prepare?.Invoke(i);
                    long started = Stopwatch.GetTimestamp(); operation(i);
                    values[i] = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                    cleanup?.Invoke();
                }
                var sorted = values.OrderBy(value => value).ToArray();
                report.timings.Add(new Timing { name = name, samples = count, coldMs = values[0], medianMs = sorted[count / 2], worstMs = sorted[count - 1] });
            }
            Measure("RefreshReputation", null, i => visitors.RefreshReputation());
            var warmModels = typeof(VisitorWorld).GetMethod("PrepareModels", BindingFlags.Instance | BindingFlags.Public);
            if (warmModels != null)
                Measure("PrepareModels-loading-total", null, i =>
                {
                    var preparation = (IEnumerator)warmModels.Invoke(visitors, new object[] { null });
                    try { while (preparation.MoveNext()) { } }
                    finally { (preparation as IDisposable)?.Dispose(); }
                }, count: 1);
            float incomeBefore = economy.Income;
            Measure("TrySpawn-real-model", i =>
            {
                SelectModel(i);
                foreach (var settings in definition.typeSettings) settings.arrivalWeight = settings.type == types[i % types.Length] ? 1 : 0;
            }, i => current = visitors.TrySpawn(), () =>
            {
                Check(current != null, "a paid arrival creates a real visitor");
                Check(Field<bool>(current.Actor, "HasVisualModel"), "a real-model arrival keeps its retained visual");
                RemoveGuest();
            });
            Check(Mathf.Approximately(economy.Income - incomeBefore, 16f), "each admitted visitor is charged exactly once");
            Measure("RestoreTyped-real-model", SelectModel,
                i => current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, types[i % types.Length]), RemoveGuest);
            var bodyProfiles = new Dictionary<VisitorType, CreatureDefinition>();
            Measure("FirstThink-real-model-cold-viewing", i =>
            {
                SelectModel(i); current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, types[i % types.Length]);
                Check(current != null, "a fresh typed guest can be created at the entrance");
                if (bodyProfiles.TryGetValue(current.Type, out var previous)) Check(previous == current.Actor.Definition, "typed actor profiles are reused");
                else bodyProfiles.Add(current.Type, current.Actor.Definition);
                Call(Field<object>(visitors, "viewing"), "BeginTick", definition.viewRange);
            }, i => Call(visitors, "Think", current), RemoveGuest);
            Call(Field<object>(visitors, "viewing"), "BeginTick", definition.viewRange);
            Measure("FirstThink-real-model-shared-viewing", i =>
                current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, types[i % types.Length]),
                i => Call(visitors, "Think", current), RemoveGuest);
            definition.people = null;
            Measure("RestoreTyped-placeholder", null,
                i => current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, types[i % types.Length]), RemoveGuest);
            Measure("FirstThink-placeholder-cold-viewing", i =>
            {
                current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, types[i % types.Length]);
                Call(Field<object>(visitors, "viewing"), "BeginTick", definition.viewRange);
            }, i => Call(visitors, "Think", current), RemoveGuest);
            var arrivalField = typeof(VisitorWorld).GetField("arrivalPaths", BindingFlags.Instance | BindingFlags.NonPublic);
            if (arrivalField != null)
            {
                current = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, VisitorType.ThrillSeekers);
                Check(current != null, "the runtime arrival sample creates one fresh entrance visitor");
                grid.NotifyPhysicalChange();
                var arrivalPaths = arrivalField.GetValue(visitors);
                var tickTimes = new List<double>();
                bool ready = false;
                while (!ready)
                {
                    Check(tickTimes.Count < grid.Width * grid.Depth, "the frame-budgeted runtime entrance search completes");
                    long started = Stopwatch.GetTimestamp(); visitors.Tick(.001f);
                    tickTimes.Add((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency);
                    var args = new object[] { access, null };
                    ready = (bool)Call(arrivalPaths, "TryGet", args);
                }
                var sorted = tickTimes.OrderBy(value => value).ToArray();
                report.timings.Add(new Timing { name = "Budgeted-arrival-Tick", samples = tickTimes.Count,
                    coldMs = tickTimes[0], medianMs = sorted[sorted.Length / 2], worstMs = sorted[sorted.Length - 1] });
                Check(tickTimes.Count > 1, "the actual visitor Tick spreads entrance preparation over multiple frames");
                Check(Field<CreatureAgent>(current, "Watching") != null
                    && (Field<List<Vector2Int>>(current.Actor, "Route").Count > 0 || current.Activity == VisitorActivity.Watching),
                    "a prepared runtime arrival starts its exhibit route or watches the selected creature");
                RemoveGuest();
            }
            definition.maximumVisitors = 0;
            float rejectedIncome = economy.Income;
            Check(visitors.TrySpawn() == null && economy.Income == rejectedIncome, "capacity prevents arrivals without charging admission");
            Check(visitors.Population == 0 && creatures.Population == report.exhibits, "timed guests are removed without losing exhibits");
        }
        finally
        {
            // Runtime Dispose uses delayed destruction; this edit-mode fixture owns and immediately removes its scene instead.
            foreach (var world in worlds)
                foreach (var field in world.GetType().GetFields(Private))
                {
                    var value = field.GetValue(world);
                    if (value is Object asset && !AssetDatabase.Contains(asset)) owned.Add(asset);
                    if (value is IDictionary map)
                        foreach (var item in map.Values) if (item is Object profile && !AssetDatabase.Contains(profile)) owned.Add(profile);
                }
            EditorSceneManager.CloseScene(scene, true);
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
        }
        Check(original.isLoaded && original.isDirty == dirty && SceneManager.GetActiveScene() == original,
            "the original scene and dirty flag are preserved");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == actorCount,
            "all fixture actors are cleaned up");
        report.checks = checks; return report;
    }
}

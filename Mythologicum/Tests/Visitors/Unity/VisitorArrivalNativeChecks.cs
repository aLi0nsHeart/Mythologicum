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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Arrival preparation, loading-only model warm-up and exact entrance navigation. Disposable project only.</summary>
public static class VisitorArrivalNativeChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static object Call(object owner, string name, params object[] values)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, values);
    private static void Set(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(owner, value); else owner.GetType().GetField(name, Private).SetValue(owner, value);
    }
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception("Visitor arrival: " + reason); checks++; }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            int count = RunChecks();
            File.WriteAllText("VisitorArrivalNativeResults.txt", "PASS: " + count + " visitor model preparation, bounded arrival, exact route and invalidation checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorArrivalNativeResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        Check(!Application.isPlaying, "this fixture uses immediate edit-mode model cleanup rather than rendered Play-mode frames");
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        int beforeActors = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var random = UnityEngine.Random.state;
        var owned = new HashSet<Object>(); var worlds = new List<VisitorWorld>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var terrain = new GridHeightField(24, 16, 2);
            var grid = new SquareBuildGrid(new Vector3(40000, 0, 40000), 2, 24, 16, terrain);
            var fences = new FenceLayout(grid); var buildings = new ParkBuildingNetwork(grid, fences);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1, admissionPrice = 1 }, new Vector2Int(1, 3), 0));
            for (int x = 0; x < grid.Width; x++) grid.TryOccupy(new Vector2Int(x, 2), BuildCellContent.Path);
            for (int z = 3; z <= 8; z++) { grid.TryOccupy(new Vector2Int(6, z), BuildCellContent.Path); grid.TryOccupy(new Vector2Int(18, z), BuildCellContent.Path); }
            buildings.Refresh(); Check(entrance != null && entrance.IsOpen, "a paid entrance has a connected public path with branches");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.populationLimit = 100; simulation.placementSpacingTiles = 0;
            var preferred = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(preferred);
            preferred.name = "Arrival preferred exhibit"; preferred.minimumSpace = 0; preferred.diet = CreatureDiet.None;
            preferred.tags = CreatureTags.Goat; preferred.threat = 0; preferred.visitorAppeal.thrill = 1;
            var other = Object.Instantiate(preferred); owned.Add(other); other.name = "Arrival alternative exhibit"; other.visitorAppeal.thrill = 0;
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, new[] { preferred, other }, null, simulation);
            Check(creatures.TrySpawn(preferred, new Vector2Int(8, 4)) && creatures.TrySpawn(other, new Vector2Int(8, 4)), "equally located exhibits isolate the visitor's preference");
            var definition = Object.Instantiate(Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors")); owned.Add(definition);
            definition.maximumVisitors = 1; definition.spawnInterval = 100000; definition.minimumStaySeconds = 10000; definition.maximumStaySeconds = 20000;
            typeof(VisitorDefinition).GetField("arrivalPathCellsPerFrame").SetValue(definition, 1);
            var economy = new ParkEconomy(1000);
            var visitors = new VisitorWorld(grid, fences, buildings, creatures, definition, economy); worlds.Add(visitors);
            var access = entrance.Footprint.AccessCell;
            int uniqueModels = definition.people.civilians.Concat(definition.people.VisitorModels(false)).Concat(definition.people.VisitorModels(true))
                .Where(prefab => prefab != null).Distinct().Count();
            int originalAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int yields = 0;
            var preparation = (IEnumerator)Call(visitors, "PrepareModels", new object[] { null });
            try
            {
                while (preparation.MoveNext())
                {
                    yields++;
                    Check(visitors.Population == 0 && creatures.Population == 2 && economy.Income == 0,
                        "loading preparation neither registers visitors nor charges tickets");
                    Check(yields <= uniqueModels + 2, "preparation visits each unique retained model at most once");
                }
            }
            finally { (preparation as IDisposable)?.Dispose(); }
            Check(yields >= uniqueModels && uniqueModels > 0, "each retained adult/child model receives its own preparation step");
            Check(Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == originalAnimators,
                "completed preparation leaves no temporary animated model behind");
            preparation = (IEnumerator)Call(visitors, "PrepareModels", new object[] { null });
            try { Check(!preparation.MoveNext(), "model preparation is idempotent for a prepared world"); }
            finally { (preparation as IDisposable)?.Dispose(); }

            var navigation = new VisitorNavigation(grid, fences, creatures.IsClear);
            var reference = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, VisitorType.ThrillSeekers);
            Call(Field<object>(visitors, "viewing"), "BeginTick", definition.viewRange);
            Check((bool)Call(visitors, "TryViewingSpot", reference, navigation.Reachable(access)), "the exact reference search finds an exhibit");
            var expectedExhibit = Field<CreatureAgent>(reference, "Watching"); var expectedGoal = Field<Vector2Int?>(reference.Actor, "Goal");
            Check(expectedExhibit.Definition == preferred, "the reference visitor selects its preferred exhibit");
            Field<List<VisitorAgent>>(visitors, "visitors").Remove(reference); Field<List<CreatureAgent>>(creatures, "agents").Remove(reference.Actor); Object.DestroyImmediate(reference.gameObject);
            var guest = (VisitorAgent)Call(visitors, "RestoreGuestWithType", access, VisitorType.ThrillSeekers);
            Check(visitors.Population == definition.maximumVisitors, "the arrival fixture is already at its visitor limit");
            var cache = Field<object>(visitors, "arrivalPaths");
            bool Cached(out List<Vector2Int> result)
            {
                var args = new object[] { access, null }; bool ready = (bool)Call(cache, "TryGet", args); result = (List<Vector2Int>)args[1]; return ready;
            }
            int steps = 0;
            while (!Cached(out _))
            {
                Check(steps++ < grid.Width * grid.Depth, "the bounded entrance search completes without starvation");
                int previous = Field<List<Vector2Int>>(cache, "paths").Count;
                Set(guest, "DecisionTimer", 0f); visitors.Tick(.01f);
                int current = Field<List<Vector2Int>>(cache, "paths").Count;
                Check(current - previous <= 1, "one simulation update honors the configured one-cell search budget");
                if (!Cached(out _)) Check(Field<CreatureAgent>(guest, "Watching") == null && Field<List<Vector2Int>>(guest.Actor, "Route").Count == 0,
                    "a fresh arrival waits without choosing an exhibit from an incomplete path search");
            }
            Check(steps > 1 && Cached(out var paths) && paths.SequenceEqual(navigation.Reachable(access)), "the completed cache has the exact reference BFS order");
            Check(Field<CreatureAgent>(guest, "Watching") == expectedExhibit && Field<Vector2Int?>(guest.Actor, "Goal") == expectedGoal,
                "the first completed decision preserves exhibit preference and route target");
            void Rebuild(string reason)
            {
                Check(!Cached(out _), reason + " invalidates the cached entrance list immediately");
                var rebuilt = (List<Vector2Int>)Call(cache, "GetOrBuild", access);
                Check(rebuilt.SequenceEqual(navigation.Reachable(access)), reason + " rebuilds the exact accessible paths and tie order");
            }
            var edge = new FenceEdge(new Vector2Int(11, 2), false);
            Check(fences.TryAdd(edge, FenceKind.Heavy), "a physical fence can split the public path"); Rebuild("A fence revision");
            Check(fences.TrySetGate(edge), "an authorized gate reconnects the visitor network"); Rebuild("A gate revision");
            var blocker = new GameObject("Arrival physical blocker", typeof(BoxCollider));
            blocker.transform.position = grid.CellCenter(new Vector2Int(14, 2)) + Vector3.up * .6f;
            blocker.GetComponent<BoxCollider>().size = new Vector3(1.8f, 2, 1.8f); grid.NotifyPhysicalChange(); Physics.SyncTransforms(); Rebuild("A notified physical obstacle");
            Check(!((List<Vector2Int>)Call(cache, "GetOrBuild", access)).Contains(new Vector2Int(14, 2)), "new physical obstruction is not retained as walkable cached ground");
            Object.DestroyImmediate(blocker); grid.NotifyPhysicalChange(); Rebuild("Removing the physical obstacle");
            Check(grid.TryRemove(new Vector2Int(10, 2), BuildCellContent.Path), "a path tile can be removed"); Rebuild("A path revision");
            Check(visitors.Population == 1 && creatures.Population == 3 && economy.Income == 0, "preparation and restored first decisions never duplicate actors or admission");
            var danger = Object.Instantiate(preferred); owned.Add(danger); danger.threat = 100;
            Check(creatures.TrySpawn(danger, access + Vector2Int.right), "an accessible dangerous creature can approach the waiting arrival");
            Set(guest.Actor, "Cell", access); guest.Actor.transform.position = grid.ActorPosition(access, guest.Actor.Definition.bodyScale.y);
            Set(guest, "Activity", VisitorActivity.Arriving); Set(guest, "DecisionTimer", 0f); Set(guest, "LeavingPark", false);
            Field<List<Vector2Int>>(guest.Actor, "Route").Clear(); Set(guest.Actor, "Goal", null);
            grid.NotifyPhysicalChange(); visitors.Tick(.01f);
            Check(!Cached(out _) && guest.Activity == VisitorActivity.Fleeing && Field<bool>(guest, "LeavingPark"),
                "actual danger interrupts waiting immediately even while the bounded arrival search is incomplete");
        }
        finally
        {
            foreach (var world in worlds)
                foreach (var field in world.GetType().GetFields(Private))
                {
                    var value = field.GetValue(world);
                    if (value is Object asset && !AssetDatabase.Contains(asset)) owned.Add(asset);
                    if (value is IDictionary map) foreach (var item in map.Values) if (item is Object assetValue && !AssetDatabase.Contains(assetValue)) owned.Add(assetValue);
                }
            EditorSceneManager.CloseScene(scene, true);
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
        }
        Check(original.isLoaded && original.isDirty == dirty && SceneManager.GetActiveScene() == original,
            "the original scene remains loaded and unmodified");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == beforeActors,
            "all temporary arrival and exhibit actors are removed");
        return checks;
    }
}

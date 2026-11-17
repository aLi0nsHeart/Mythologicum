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
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>Exact old/new staff approaches and danger membership. Disposable native editor fixture only.</summary>
public static class StaffPerformanceNativeChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static double referenceMs, optimizedMs;
    private static double referenceDangerMs, optimizedDangerMs;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static object Call(object owner, string name, params object[] values)
        => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, values);
    private static void Set(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(owner, value); else owner.GetType().GetField(name, Private).SetValue(owner, value);
    }
    private static void Check(bool condition, string reason)
    { if (!condition) throw new Exception("Staff performance: " + reason); checks++; }
    private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    private static Vector2Int? ReferencePosition(StaffNavigation navigation, SquareBuildGrid grid,
        StaffDefinition tuning, StaffAgent worker, CreatureAgent target, float range, bool clearShot)
    {
        Vector2Int? best = null; float score = float.MaxValue;
        foreach (var cell in navigation.Reachable(worker.Actor.Cell))
        {
            if (Distance(grid.CellCenter(cell), target.transform.position) > range || !navigation.HasLineOfSight(cell, target.Cell)) continue;
            if (clearShot && !navigation.HasClearShot(cell, target.Cell)) continue;
            float distance = ConstructionRouteGraph.CellDistance(worker.Actor.Cell, cell)
                + (grid.ContentAt(cell) == BuildCellContent.Path ? 0f : tuning.offPathApproachPenalty);
            if (distance < score) { score = distance; best = cell; }
        }
        return best;
    }
    private static CreatureAgent ReferenceDanger(CreatureWorld creatures, CreatureNavigation navigation,
        SquareBuildGrid grid, StaffDefinition tuning, StaffAgent worker)
    {
        CreatureAgent best = null; float distance = float.MaxValue;
        foreach (var target in creatures.Agents)
        {
            if (target == null || !target.Alive || Field<bool>(target, "ExternallyControlled") || target.IsSedated
                || target.Definition.threat <= worker.Actor.Definition.threat
                || !creatures.CanSee(worker.Actor, target, tuning.threatFleeRange)) continue;
            var region = navigation.Reachable(worker.Actor.Cell, grid.Width * grid.Depth);
            if (!region.Contains(target.Cell)) continue;
            float current = Distance(worker.Actor.transform.position, target.transform.position);
            if (current < distance) { distance = current; best = target; }
        }
        return best;
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            int count = RunChecks();
            string result = $"PASS: {count} staff approach, tie order, containment, danger and cleanup checks.\n"
                + $"Informational ranged-query totals: reference {referenceMs:F3} ms, optimized {optimizedMs:F3} ms.\n"
                + $"Informational contained-danger totals: reference {referenceDangerMs:F3} ms, optimized {optimizedDangerMs:F3} ms; no timing thresholds.";
            File.WriteAllText("StaffPerformanceNativeResults.txt", result);
            Debug.Log(result); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("StaffPerformanceNativeResults.txt", "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    public static int RunChecks()
    {
        checks = 0; referenceMs = optimizedMs = referenceDangerMs = optimizedDangerMs = 0;
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        int beforeActors = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var oldRandom = UnityEngine.Random.state;
        var owned = new HashSet<Object>(); StaffWorld staff = null;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var terrain = new GridHeightField(24, 18, 2);
            var grid = new SquareBuildGrid(new Vector3(50000, 0, 50000), 2, 24, 18, terrain);
            var fences = new FenceLayout(grid); var buildings = new ParkBuildingNetwork(grid, fences);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 3), 0));
            var home = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ResearchCentre, width = 1, depth = 1, staffCapacity = 5 }, new Vector2Int(4, 3), 0));
            for (int y = 0; y < grid.Depth; y++) for (int x = 0; x < grid.Width; x++)
                if ((x % 3 == 0 || y % 3 == 2) && grid.IsAvailable(new Vector2Int(x, y))) grid.TryOccupy(new Vector2Int(x, y), BuildCellContent.Path);
            for (int y = 4; y < 12; y++)
            {
                var cell = new Vector2Int(8, y); grid.TryRemove(cell, BuildCellContent.Path); grid.TryOccupy(cell, BuildCellContent.Building);
            }
            for (int y = 0; y < grid.Depth; y++) fences.TryAdd(new FenceEdge(new Vector2Int(12, y), false), FenceKind.Wooden);
            fences.TrySetGate(new FenceEdge(new Vector2Int(12, 2), false));
            fences.TrySetGate(new FenceEdge(new Vector2Int(12, 11), false));
            for (int x = 1; x < 23; x++) fences.TryAdd(new FenceEdge(new Vector2Int(x, 13), true), FenceKind.Heavy);
            fences.TrySetGate(new FenceEdge(new Vector2Int(18, 13), true));
            buildings.Refresh(); Check(entrance != null && home != null && home.IsOpen, "the fixture has an operating employee home and connected path network");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.populationLimit = 100; simulation.placementSpacingTiles = 0;
            var species = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(species);
            species.name = "Staff test creature"; species.displayName = "Staff test creature"; species.tags = CreatureTags.Goat;
            species.minimumSpace = 0; species.threat = 100; species.damage = 0; species.diet = CreatureDiet.None;
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, new[] { species }, null, simulation);
            Check(creatures.TrySpawn(species, new Vector2Int(6, 6)), "the fixture contains a healthy visible creature");
            var target = creatures.Agents.Last();
            var tuning = ScriptableObject.CreateInstance<StaffDefinition>(); owned.Add(tuning); tuning.threatFleeRange = 16;
            tuning.researchPerSecond = 0; tuning.mercenaryDamage = 0;
            staff = new StaffWorld(grid, fences, buildings, creatures, new ParkEconomy(10000), null, new[] { species }, tuning);
            var worker = (StaffAgent)Call(staff, "RestoreEmployee", home, new Vector2Int(6, 5));
            Check(worker != null && worker.Role == StaffRole.Researcher, "a real civilian employee can be restored without paid hiring");
            var navigation = new StaffNavigation(grid, fences, creatures.IsClear);
            var strictGround = new CreatureNavigation(grid, fences, creatures.IsClear);
            var cells = new List<Vector2Int>();
            for (int y = 0; y < grid.Depth; y++) for (int x = 0; x < grid.Width; x++)
                if (navigation.IsWalkable(new Vector2Int(x, y))) cells.Add(new Vector2Int(x, y));
            void Position(CreatureAgent actor, Vector2Int cell, float x = 0, float z = 0)
            { Set(actor, "Cell", cell); actor.transform.position = grid.ActorPosition(cell, actor.Definition.bodyScale.y) + new Vector3(x, 0, z); }
            var random = new System.Random(91365);
            float[] ranges = { 0, 1, 3, 8, 16, -1, float.PositiveInfinity, float.NaN };
            float[] penalties = { 0, .5f, 2, 5, -.5f, float.PositiveInfinity, float.NaN };
            for (int query = 0; query < 512; query++)
            {
                Position(worker.Actor, cells[random.Next(cells.Count)]);
                Position(target, cells[random.Next(cells.Count)], (float)(random.NextDouble() - .5) * 1.4f, (float)(random.NextDouble() - .5) * 1.4f);
                tuning.offPathApproachPenalty = penalties[query % penalties.Length];
                float range = ranges[query % ranges.Length]; bool clearShot = query % 2 == 0;
                long began = Stopwatch.GetTimestamp();
                var expected = ReferencePosition(navigation, grid, tuning, worker, target, range, clearShot);
                double oldTime = (Stopwatch.GetTimestamp() - began) * 1000d / Stopwatch.Frequency;
                began = Stopwatch.GetTimestamp();
                var actual = (Vector2Int?)Call(staff, "FindRangedPosition", worker, target, range, clearShot);
                double newTime = (Stopwatch.GetTimestamp() - began) * 1000d / Stopwatch.Frequency;
                if (query >= 8) { referenceMs += oldTime; optimizedMs += newTime; }
                Check(actual == expected, "ranged goal and BFS ties match the original for varied positions, ranges, path bias and clear shots (query " + query + ")");
            }
            tuning.offPathApproachPenalty = .5f;
            foreach (var edge in fences.Edges.ToArray()) fences.TryRemove(edge);
            Position(worker.Actor, new Vector2Int(6, 5)); Position(target, new Vector2Int(6, 6));
            var ring = new[] { new FenceEdge(new Vector2Int(6, 6), true), new FenceEdge(new Vector2Int(6, 7), true),
                new FenceEdge(new Vector2Int(6, 6), false), new FenceEdge(new Vector2Int(7, 6), false) };
            foreach (var edge in ring) Check(fences.TryAdd(edge, FenceKind.Heavy), "the fear fixture installs a real containment edge");
            void CheckDanger(string reason, CreatureAgent expected)
            {
                Call(staff, "RefreshRoutes");
                var reference = ReferenceDanger(creatures, strictGround, grid, tuning, worker);
                Check(reference == expected, "the reference danger scenario is valid: " + reason);
                Check((CreatureAgent)Call(staff, "WorkerDanger", worker) == reference, "cached staff danger equals the original strict-ground flood: " + reason);
                Check((CreatureAgent)Call(staff, "WorkerDanger", worker) == reference, "repeated danger checks retain current containment: " + reason);
            }
            Check(creatures.CanSee(worker.Actor, target, tuning.threatFleeRange), "the civilian can observe a dangerous contained creature through fencing");
            CheckDanger("intact containment", null);
            Check(fences.TrySetGate(ring[0]), "the containing fence can become an authorized staff gate");
            Check(navigation.FindPath(worker.Actor.Cell, target.Cell).Count > 0, "staff can enter an enclosure through its gate");
            CheckDanger("a closed staff gate still contains danger", null);
            for (int query = 0; query < 136; query++)
            {
                long began = Stopwatch.GetTimestamp();
                var expected = ReferenceDanger(creatures, strictGround, grid, tuning, worker);
                double oldTime = (Stopwatch.GetTimestamp() - began) * 1000d / Stopwatch.Frequency;
                began = Stopwatch.GetTimestamp();
                var actual = (CreatureAgent)Call(staff, "WorkerDanger", worker);
                double newTime = (Stopwatch.GetTimestamp() - began) * 1000d / Stopwatch.Frequency;
                if (query >= 8) { referenceDangerMs += oldTime; optimizedDangerMs += newTime; }
                Check(expected == null && actual == expected, "repeated contained-danger checks match the original without opening the staff gate");
            }
            Check(fences.TryRemove(ring[0]), "opening a real breach changes containment");
            CheckDanger("a breached enclosure exposes the civilian", target);
            staff.Tick(.01f);
            Check(worker.Task == StaffTask.Fleeing && worker.Actor.Vitals.Fear > 0, "actual civilian updates respond immediately to exposed danger");
            Position(worker.Actor, new Vector2Int(6, 5));
            var blocker = new GameObject("Staff danger obstruction", typeof(BoxCollider));
            blocker.transform.position = grid.CellCenter(target.Cell) + Vector3.up * .6f;
            blocker.GetComponent<BoxCollider>().size = new Vector3(1.6f, 2, 1.6f);
            grid.NotifyPhysicalChange(); Physics.SyncTransforms(); CheckDanger("a notified physical obstruction", null);
            Object.DestroyImmediate(blocker); grid.NotifyPhysicalChange(); CheckDanger("removing the physical obstruction", target);
            Set(target, "IsSedated", true); CheckDanger("sedation immediately removes active danger", null); Set(target, "IsSedated", false);
            Position(target, new Vector2Int(4, 5)); CheckDanger("a target moves within the cached strict-ground region", target);
            Position(target, new Vector2Int(22, 17)); CheckDanger("a target leaves perception without a topology change", null);
        }
        finally
        {
            if (staff != null)
                foreach (var field in staff.GetType().GetFields(Private))
                {
                    if (field.GetValue(staff) is Object asset && !AssetDatabase.Contains(asset)) owned.Add(asset);
                    if (field.GetValue(staff) is IDictionary map)
                        foreach (var item in map.Values) if (item is Object assetValue && !AssetDatabase.Contains(assetValue)) owned.Add(assetValue);
                }
            EditorSceneManager.CloseScene(scene, true);
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = oldRandom;
        }
        Check(original.isLoaded && original.isDirty == dirty && SceneManager.GetActiveScene() == original, "the original scene and dirty flag are unchanged");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == beforeActors, "all temporary actors are removed");
        return checks;
    }
}

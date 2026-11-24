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
            CheckApproachReuse(staff, grid, fences, navigation, tuning, worker, target, species);
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

    private static void CheckApproachReuse(StaffWorld staff, SquareBuildGrid grid, FenceLayout fences,
        StaffNavigation navigation, StaffDefinition tuning, StaffAgent worker, CreatureAgent target, CreatureDefinition species)
    {
        Vector2Int V(int x, int y) => new Vector2Int(x, y);
        void Position(CreatureAgent actor, Vector2Int cell)
        { Set(actor, "Cell", cell); actor.transform.position = grid.ActorPosition(cell, actor.Definition.bodyScale.y); }
        void AdvanceRetry() => Set(staff, "workQueryTime", Field<float>(staff, "workQueryTime") + tuning.decisionInterval + .01f);
        int RangedSearches() => Field<int>(staff, "rangedSearchCount");
        Vector2Int? Goal() => Field<Vector2Int?>(worker.Actor, "Goal");
        var route = Field<List<Vector2Int>>(worker.Actor, "Route");
        float originalStudyRange = tuning.studyRange;
        var changedGround = new List<Vector2Int>();
        try
        {
            tuning.studyRange = 3;
            Position(worker.Actor, V(1, 5)); Position(target, V(6, 6));
            Check(staff.SetResearchTarget(worker.Home, species), "an actual research assignment can select its specimen");
            var expected = ReferencePosition(navigation, grid, tuning, worker, target, tuning.studyRange, false);
            int initial = RangedSearches();
            Call(staff, "Assign", worker);
            Check(worker.Target == target && worker.Task == StaffTask.Researching && RangedSearches() == initial + 1,
                "assignment makes one exact ranged search and retains its result");
            Call(staff, "Work", worker, .01f);
            Check(Goal() == expected && route.Count > 0 && RangedSearches() == initial + 1,
                "the first working frame reuses the assignment search and its original BFS winner");
            for (int frame = 0; frame < 120; frame++) Call(staff, "Work", worker, .01f);
            Check(RangedSearches() == initial + 1 && Goal() == expected,
                "120 unchanged travelling frames do not flood the map again");
            target.transform.position = Vector3.MoveTowards(target.transform.position, grid.CellCenter(expected.Value), .1f);
            for (int frame = 0; frame < 60; frame++) Call(staff, "Work", worker, .01f);
            Check(RangedSearches() == initial + 1, "a moving target keeps its still-valid ranged approach");
            bool observed = false;
            for (int step = 0; step < 300 && !observed; step++)
            {
                observed = (bool)Call(staff, "ApproachRanged", worker, target, tuning.studyRange, false);
                if (!observed) Call(staff, "Move", worker, .05f);
            }
            Check(observed && navigation.HasLineOfSight(worker.Actor.Cell, target.Cell) && RangedSearches() == initial + 1,
                "the retained approach still physically reaches a valid observation position");

            Call(staff, "ClearJob", worker); Position(worker.Actor, V(1, 5)); Position(target, V(6, 6));
            Check(!(bool)Call(staff, "ApproachRanged", worker, target, 3f, false), "a new distant target first requires travel");
            var previousGoal = Goal();
            Position(target, V(15, 7)); AdvanceRetry(); initial = RangedSearches();
            Call(staff, "ApproachRanged", worker, target, 3f, false);
            Check(RangedSearches() == initial + 1 && Goal().HasValue && Goal() != previousGoal
                && Distance(grid.CellCenter(Goal().Value), target.transform.position) <= 3,
                "a target moving outside the retained goal's range causes a fresh valid approach");

            for (int y = 0; y < grid.Depth; y++) Check(fences.TryAdd(new FenceEdge(V(12, y), false)), "the retry fixture closes a physical boundary");
            initial = RangedSearches();
            Check(!(bool)Call(staff, "ApproachRanged", worker, target, 3f, false)
                && RangedSearches() == initial + 1 && route.Count == 0,
                "a new closed fence invalidates the retained route immediately even within the retry interval");
            initial = RangedSearches();
            for (int frame = 0; frame < 180; frame++)
            {
                Set(staff, "workQueryTime", Field<float>(staff, "workQueryTime") + 1f / 60);
                Call(staff, "ApproachRanged", worker, target, 3f, false);
            }
            Check(RangedSearches() > initial && RangedSearches() - initial <= Mathf.CeilToInt(3 / tuning.decisionInterval) + 1,
                "180 blocked frames perform only the retries allowed by the staff decision interval");
            initial = RangedSearches();
            AdvanceRetry(); Call(staff, "ApproachRanged", worker, target, 3f, false);
            Check(RangedSearches() == initial + 1, "a blocked approach retries at the existing staff decision interval");
            var gate = new FenceEdge(V(12, 7), false);
            Check(fences.TrySetGate(gate), "the boundary can receive an actual staff gate");
            initial = RangedSearches(); Call(staff, "ApproachRanged", worker, target, 3f, true);
            Check(RangedSearches() == initial + 1 && route.Contains(V(12, 7))
                && !navigation.HasClearShot(worker.Actor.Cell, target.Cell),
                "a newly available gate immediately replans entry instead of permitting shots through the fence");
            var occupiedGoal = Goal().Value;
            Check(grid.TryOccupy(occupiedGoal, BuildCellContent.Building), "a construction edit can occupy the retained destination");
            changedGround.Add(occupiedGoal); initial = RangedSearches();
            Call(staff, "ApproachRanged", worker, target, 3f, true);
            Check(RangedSearches() == initial + 1 && !route.Contains(occupiedGoal),
                "grid edits immediately invalidate an obstructed cached approach");
            grid.TryRemove(occupiedGoal, BuildCellContent.Building); changedGround.Remove(occupiedGoal);

            Call(staff, "ClearJob", worker); Position(worker.Actor, V(11, 7)); Position(target, V(12, 7));
            Check(creaturesVisible() && !navigation.HasClearShot(worker.Actor.Cell, target.Cell),
                "the close-range specimen is visible through a closed staff gate but cannot be shot through it");
            Check(!(bool)Call(staff, "ApproachRanged", worker, target, 5f, true) && route.Contains(V(12, 7)),
                "a mercenary must still physically enter despite being within shooting range");
            bool clearShot = false;
            for (int step = 0; step < 100 && !clearShot; step++)
            {
                clearShot = (bool)Call(staff, "ApproachRanged", worker, target, 5f, true);
                if (!clearShot) Call(staff, "Move", worker, .05f);
            }
            Check(clearShot && worker.Actor.Cell.x >= 12 && navigation.HasClearShot(worker.Actor.Cell, target.Cell),
                "the retained shooting approach only succeeds after crossing the gate and acquiring a clear shot");

            Call(staff, "ClearJob", worker); Position(worker.Actor, V(10, 7));
            int repairs = Field<int>(staff, "fenceApproachSearchCount");
            var repairGoal = (Vector2Int?)Call(staff, "FenceApproach", worker, gate);
            for (int frame = 0; frame < 120; frame++) Call(staff, "FenceApproach", worker, gate);
            Check(repairGoal.HasValue && Field<int>(staff, "fenceApproachSearchCount") == repairs + 1,
                "repeated repair work retains its reachable fence side instead of doing two A-star searches per frame");
            Check(grid.TryOccupy(repairGoal.Value, BuildCellContent.Building), "the cached repair approach can be obstructed");
            changedGround.Add(repairGoal.Value); Call(staff, "FenceApproach", worker, gate);
            Check(Field<int>(staff, "fenceApproachSearchCount") == repairs + 2,
                "a physical grid edit immediately invalidates a cached repair approach");

            bool creaturesVisible() => Field<CreatureWorld>(staff, "creatures").CanSee(worker.Actor, target, 5f);
        }
        finally
        {
            foreach (var cell in changedGround) grid.TryRemove(cell, BuildCellContent.Building);
            foreach (var edge in fences.Edges.ToArray()) fences.TryRemove(edge);
            Call(staff, "ClearJob", worker); tuning.studyRange = originalStudyRange;
        }
    }
}

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

/// <summary>Reference-equivalence checks for optimized visitor viewing; disposable validation project only.</summary>
public static class VisitorPerformanceNativeChecks
{
    private static int checks;
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            int count = RunChecks();
            File.WriteAllText("VisitorPerformanceValidationResults.txt", "PASS: " + count + " native visitor choice-equivalence and cleanup checks.");
            Debug.Log("VISITOR PERFORMANCE PASS: " + count); EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorPerformanceValidationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        var original = SceneManager.GetActiveScene();
        bool originalDirty = original.isDirty;
        int actorsBefore = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var oldRandom = UnityEngine.Random.state;
        var owned = new List<Object>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var grid = new SquareBuildGrid(Vector3.zero, 2, 17, 15);
            var fences = new FenceLayout(grid);
            var buildings = new ParkBuildingNetwork(grid, fences);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, V(1, 1), 0));
            for (int z = 0; z < grid.Depth; z++)
            for (int x = 0; x < grid.Width; x++)
                if ((x % 3 == 0 || z % 3 == 0) && grid.IsAvailable(V(x, z))) grid.TryOccupy(V(x, z), BuildCellContent.Path);
            buildings.Refresh();
            var species = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(species);
            species.name = "Viewing-reference species"; species.displayName = "Viewing reference";
            species.diet = CreatureDiet.None; species.tags = CreatureTags.Goat; species.minimumSpace = 0;
            var settings = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(settings);
            settings.populationLimit = 100; settings.placementSpacingTiles = 0;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, new[] { species }, null, settings);
            foreach (var cell in new[] { V(4, 4), V(4, 4), V(11, 7), V(14, 11) })
                Check(creatures.TrySpawn(species, cell), "Reference exhibit actor can be placed");
            var definition = ScriptableObject.CreateInstance<VisitorDefinition>(); owned.Add(definition);
            definition.people = null;
            var visitors = new VisitorWorld(grid, fences, buildings, creatures, definition, new ParkEconomy(10000));
            owned.Add(Field<Object>(visitors, "actorProfile"));
            var restore = typeof(VisitorWorld).GetMethod("RestoreGuest", BindingFlags.Instance | BindingFlags.NonPublic);
            var guest = (VisitorAgent)restore.Invoke(visitors, new object[] { V(0, 0) });
            Check(guest != null, "Reference visitor can be restored on the path network");
            var navigation = new VisitorNavigation(grid, fences, creatures.IsClear);
            var sight = new CreatureNavigation(grid, fences, creatures.IsClear);
            var choose = typeof(VisitorWorld).GetMethod("TryViewingSpot", BindingFlags.Instance | BindingFlags.NonPublic);
            var viewing = Field<object>(visitors, "viewing");
            var beginTick = viewing.GetType().GetMethod("BeginTick");
            var cellProperty = typeof(CreatureAgent).GetProperty("Cell");

            void Compare(Vector2Int origin)
            {
                cellProperty.SetValue(guest.Actor, origin);
                guest.Actor.transform.position = grid.ActorPosition(origin, guest.Actor.Definition.bodyScale.y);
                Field<List<Vector2Int>>(guest.Actor, "Route").Clear();
                typeof(CreatureAgent).GetField("Goal", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(guest.Actor, null);
                var paths = navigation.Reachable(origin);
                CreatureAgent expectedAnimal = null;
                Vector2Int expectedPoint = origin;
                float best = float.MinValue;
                var viewed = Field<Dictionary<CreatureAgent, float>>(guest, "ViewedAt");
                foreach (var animal in creatures.Agents)
                {
                    if (!animal.Alive || Field<bool>(animal, "ExternallyControlled")
                        || animal.Definition.requiresGravestoneForSleep && animal.Activity == CreatureActivity.Sleeping) continue;
                    if (viewed.TryGetValue(animal, out float last) && -last < definition.exhibitRevisitSeconds) continue;
                    foreach (var path in paths)
                    {
                        var point = grid.CellCenter(path); var position = animal.transform.position;
                        float distance = new Vector2(point.x - position.x, point.z - position.z).magnitude;
                        if (distance > definition.viewRange || !sight.HasLineOfSight(path, animal.Cell)) continue;
                        float score = -ConstructionRouteGraph.CellDistance(origin, path) * definition.viewingTravelPenalty
                            - Mathf.Abs(distance - definition.preferredViewingDistance) * definition.viewingDistancePenalty;
                        if (score > best) { best = score; expectedAnimal = animal; expectedPoint = path; }
                    }
                }
                bool expected = expectedAnimal != null && navigation.FindPath(origin, expectedPoint).Count > 0;
                bool actual = (bool)choose.Invoke(visitors, new object[] { guest, paths });
                Check(actual == expected, "Optimized and exhaustive viewing searches agree on whether an exhibit is reachable");
                if (actual)
                {
                    Check(Field<CreatureAgent>(guest, "Watching") == expectedAnimal,
                        "The optimized search preserves creature order, including equally scored overlapping creatures");
                    Check(Field<Vector2Int?>(guest.Actor, "Goal") == expectedPoint,
                        "The optimized search preserves this guest's original BFS path tie order");
                }
            }

            for (int phase = 0; phase < 4; phase++)
            {
                if (phase == 1)
                {
                    for (int z = 0; z < grid.Depth; z++)
                        if (grid.IsAvailable(V(8, z))) grid.TryOccupy(V(8, z), BuildCellContent.Building);
                    // Removing this full path column creates genuinely disconnected public networks.
                    for (int z = 0; z < grid.Depth; z++) grid.TryRemove(V(8, z), BuildCellContent.Path);
                    for (int z = 0; z < grid.Depth; z++)
                        if (grid.IsAvailable(V(8, z))) grid.TryOccupy(V(8, z), BuildCellContent.Building);
                }
                if (phase == 2)
                {
                    // The position can lie between tile centers while Cell still identifies the previous tile.
                    creatures.Agents[0].transform.position += new Vector3(.98f, 0, -.98f);
                    Field<Dictionary<CreatureAgent, float>>(guest, "ViewedAt")[creatures.Agents[1]] = 0;
                }
                if (phase == 3)
                {
                    Field<Dictionary<CreatureAgent, float>>(guest, "ViewedAt").Clear();
                    definition.preferredViewingDistance = 0;
                }
                foreach (float range in new[] { .1f, 4f, 12f, 100f })
                foreach (float travel in new[] { 0f, 1f, 3f })
                foreach (float distance in new[] { 0f, 2f })
                {
                    definition.viewRange = range; definition.viewingTravelPenalty = travel; definition.viewingDistancePenalty = distance;
                    beginTick.Invoke(viewing, new object[] { range });
                    foreach (var origin in new[] { V(0, 0), V(3, 6), V(12, 12), V(16, 0) }) Compare(origin);
                }
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            UnityEngine.Random.state = oldRandom;
        }
        Check(SceneManager.GetActiveScene() == original && original.isDirty == originalDirty,
            "Visitor equivalence checks restore the original scene and dirty flag");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == actorsBefore,
            "Visitor equivalence checks remove their temporary actors");
        return checks;
    }
}

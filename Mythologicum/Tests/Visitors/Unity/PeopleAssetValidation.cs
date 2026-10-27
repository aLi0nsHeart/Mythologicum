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

[InitializeOnLoad]
public static class PeopleAssetValidation
{
    private const string Running = "PeopleAssetValidation.running";
    private static int checks;
    static PeopleAssetValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); Run(); }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/PeopleValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("PEOPLE CHECK PASS: " + message);
    }
    private static void Run()
    {
        try
        {
            var catalog = Resources.Load<PeopleDefinition>("People/People");
            var definition = UnityEngine.Object.Instantiate(Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors"));
            Check(catalog != null && catalog.civilians.Length == 7, "Seven civilians assigned");
            Check(definition.people == catalog, "Default visitor settings reference people catalog");
            Check(catalog.police != null && catalog.doctor != null && catalog.worker != null, "All three staff models retained");
            var staff = new[] { catalog.police, catalog.doctor, catalog.worker };
            Check(staff.Distinct().Count() == 3 && !catalog.civilians.Intersect(staff).Any(), "Staff excluded from civilian pool");
            foreach (var prefab in catalog.civilians.Concat(staff))
            {
                string path = AssetDatabase.GetAssetPath(prefab);
                Check(path.StartsWith("Assets/Mythologicum/Prefabs/People/"), "Organized prefab: " + prefab.name);
                Check(!AssetDatabase.GetDependencies(path, true).Any(p => p.StartsWith("Assets/DenysAlmaral/")), "All prefab dependencies migrated");
                Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "No demo behaviour or missing scripts: " + prefab.name);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    Check(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit"), "URP materials resolve");
            }
            var grid = new SquareBuildGrid(Vector3.zero, 2f, 60, 60);
            var fences = new FenceLayout(grid);
            var network = new ParkBuildingNetwork(grid, fences);
            network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 2), 0));
            for (int x = 1; x < 12; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
            network.Refresh();
            var camera = new GameObject("Camera").AddComponent<Camera>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var creatures = new CreatureWorld(grid, fences, network, camera, material, new CreatureDefinition[0]);
            var economy = new ParkEconomy(5000f);
            definition.maximumVisitors = 40;
            var visitors = new VisitorWorld(grid, fences, network, creatures, definition, economy);
            var names = new HashSet<string>();
            var modelInstances = new HashSet<GameObject>();
            var allowed = new HashSet<string>(catalog.civilians.Select(p => p.name));
            var reserved = new HashSet<string>(staff.Select(p => p.name));
            var routeField = typeof(CreatureAgent).GetField("Route", BindingFlags.NonPublic | BindingFlags.Instance);
            var activity = typeof(CreatureAgent).GetProperty("Activity");
            var alive = typeof(CreatureAgent).GetProperty("Alive");
            for (int i = 0; i < 30; i++)
            {
                var guest = visitors.TrySpawn();
                Check(guest != null, "Visitor spawns via normal entrance");
                var actor = guest.Actor;
                var visual = actor.transform.GetChild(0).gameObject;
                string modelName = visual.name.Replace("(Clone)", "");
                Check(allowed.Contains(modelName) && !reserved.Contains(modelName), "Random selection uses civilian only: " + modelName);
                names.Add(modelName);
                Check(modelInstances.Add(visual), "Each guest owns its own model instance");
                Check(actor.Definition.visualPrefab == null, "Shared gameplay profile is not mutated by appearance choice");
                actor.transform.position = new Vector3(20 + i * 2, 0.85f, 30);
                var animator = visual.GetComponentInChildren<Animator>();
                Check(animator != null && animator.avatar != null && animator.avatar.isValid, "Character rig resolves");
                Check(!animator.applyRootMotion && actor.GetComponent<Collider>().isTrigger, "Simulation owns movement and collisions");
                Check(visual.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Visual model has no blocking colliders");
                foreach (string state in new[] { "idle", "walk_forward", "run_forward" })
                {
                    Check(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "Animation state exists: " + state);
                    animator.Play("Base Layer." + state, 0, 0);
                    animator.Update(0.1f);
                    Check(animator.GetCurrentAnimatorClipInfo(0).Length > 0, "Animation clip resolves: " + state);
                }
                var route = (IList)routeField.GetValue(actor);
                route.Add(new Vector2Int(2, 1));
                foreach (var pair in new[] { (CreatureActivity.Wandering, "walk_forward"), (CreatureActivity.Fleeing, "run_forward") })
                {
                    activity.SetValue(actor, pair.Item1);
                    creatures.RefreshExternal(actor);
                    animator.Update(0.2f);
                    Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + pair.Item2), "Visitor activity drives animation");
                }
                route.Clear();
                activity.SetValue(actor, CreatureActivity.Resting);
                creatures.RefreshExternal(actor);
                animator.Update(0.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.idle"), "Stationary visitor returns to idle");
                activity.SetValue(actor, CreatureActivity.Feeding);
                creatures.RefreshExternal(actor);
                animator.Update(0.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.idle"), "Missing eating clip safely uses idle");
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                Check(renderers.Length > 0 && renderers.All(r => r.sharedMesh != null), "Character meshes resolve");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Debug.Log("PEOPLE BOUNDS " + modelName + ": " + bounds);
                Check(bounds.size.y > 0.5f && bounds.size.y < 2.6f, "Character scale fits park");
                Check(Mathf.Abs(bounds.min.y) < 0.2f, "Character feet align to ground");
            }
            Check(names.Count > 1, "Normal arrivals choose varied civilian models");
            var first = visitors.Visitors[0].Actor;
            alive.SetValue(first, false);
            creatures.RefreshExternal(first);
            Check(!first.GetComponentInChildren<Animator>().enabled, "Dead visitors do not keep walking");
            Check(visitors.Visitors.Skip(1).All(v => v.Actor.GetComponentInChildren<Animator>().enabled), "Other visitors remain animated independently");
            var placeholderDefinition = ScriptableObject.CreateInstance<CreatureDefinition>();
            Check(creatures.TrySpawn(placeholderDefinition, new Vector2Int(40, 40)), "Unconfigured species keep placeholder fallback");
            File.WriteAllText("PeopleValidationResult.txt", "PASS: " + checks + " checks; " + names.Count + " civilian variants sampled");
            Debug.Log("PEOPLE VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("PeopleValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
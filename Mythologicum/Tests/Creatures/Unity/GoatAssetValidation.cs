using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GoatAssetValidation
{
    private const string Running = "GoatAssetValidation.running";
    private static int checks;
    static GoatAssetValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Running, false);
                Run();
            }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Validation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("GOAT CHECK PASS: " + message);
    }
    private static void Run()
    {
        try
        {
            var profile = Resources.Load<CreatureDefinition>("Creatures/Goat");
            Check(profile != null && profile.visualPrefab != null, "Goat profile resolves visual prefab");
            string path = AssetDatabase.GetAssetPath(profile.visualPrefab);
            Check(path == "Assets/Mythologicum/Prefabs/Creatures/Goat/Goat.prefab", "Prefab uses project folder structure");
            foreach (var dep in AssetDatabase.GetDependencies(path, true))
            {
                Check(!dep.StartsWith("Assets/UrsaAnimation"), "Retained dependency: " + dep);
                Check(!dep.StartsWith("Assets/") || AssetDatabase.LoadMainAssetAtPath(dep) != null, "Dependency imports: " + dep);
            }
            var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 12);
            var fences = new FenceLayout(grid);
            var network = new ParkBuildingNetwork(grid, fences);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var world = new CreatureWorld(grid, fences, network, camera, material, new[] { profile });
            Check(world.TrySpawn(profile, new Vector2Int(4, 4)), "Real goat spawns through creature world");
            var agent = world.Agents[0];
            var animator = agent.GetComponentInChildren<Animator>();
            Check(animator != null && !animator.applyRootMotion, "AI owns movement; root motion disabled");
            Check(agent.GetComponent<Collider>() != null, "Simulation collider preserved");
            Check(agent.GetComponentsInChildren<MonoBehaviour>().Length == 1, "No demo keyboard behaviour or missing scripts");
            var refresh = typeof(CreatureWorld).GetMethod("Animate", BindingFlags.NonPublic | BindingFlags.Instance);
            var activity = typeof(CreatureAgent).GetProperty("Activity");
            var alive = typeof(CreatureAgent).GetProperty("Alive");
            foreach (string state in new[] { "idle", "walk_forward", "run_forward", "stand_to_sit", "eating", "death", "attack" })
            {
                Check(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "Animator state exists: " + state);
                animator.Play("Base Layer." + state, 0, 0);
                animator.Update(0);
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                Check(clips.Length > 0 && clips[0].clip != null, "Animation clip resolves: " + state);
                animator.Update(0.25f);
            }
            animator.Play("Base Layer.idle", 0, 0);
            animator.Update(0);
            var renderers = agent.GetComponentsInChildren<SkinnedMeshRenderer>();
            Check(renderers.Length > 0, "Skinned goat meshes present");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                Check(renderer.sharedMesh != null && renderer.sharedMaterials.All(m => m != null && m.shader != null), "Mesh and materials resolve: " + renderer.name);
                bounds.Encapsulate(renderer.bounds);
            }
            Debug.Log("GOAT MODEL BOUNDS: " + bounds);
            Check(bounds.size.y > 0.3f && bounds.size.y < 3f, "Model uses sensible metre scale");
            Check(Mathf.Abs(bounds.min.y) < 0.15f, "Goat feet meet ground plane");
            foreach (var pair in new[] {
                (CreatureActivity.Resting, "idle"), (CreatureActivity.Grazing, "eating"),
                (CreatureActivity.Sleeping, "stand_to_sit"), (CreatureActivity.Attacking, "attack") })
            {
                activity.SetValue(agent, pair.Item1);
                refresh.Invoke(world, new object[] { agent });
                animator.Update(0.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + pair.Item2), "AI selects animation: " + pair.Item1);
            }
            var route = (System.Collections.IList)typeof(CreatureAgent).GetField("Route", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(agent);
            route.Add(new Vector2Int(5, 4));
            foreach (var pair in new[] { (CreatureActivity.Wandering, "walk_forward"), (CreatureActivity.Fleeing, "run_forward") })
            {
                activity.SetValue(agent, pair.Item1);
                refresh.Invoke(world, new object[] { agent });
                animator.Update(0.2f);
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + pair.Item2), "Movement selects animation: " + pair.Item1);
            }
            route.Clear();
            alive.SetValue(agent, false);
            refresh.Invoke(world, new object[] { agent });
            animator.Update(0.2f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.death"), "Dead goats play death animation");
            var fallback = ScriptableObject.CreateInstance<CreatureDefinition>();
            Check(world.TrySpawn(fallback, new Vector2Int(8, 8)), "Species without a model still spawn placeholders");
            Debug.Log("GOAT VALIDATION PASSED: " + checks + " checks");
            File.WriteAllText("GoatValidationResult.txt", "PASS: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("GoatValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
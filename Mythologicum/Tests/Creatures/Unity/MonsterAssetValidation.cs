using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MonsterAssetValidation
{
    private const string Running = "MonsterAssetValidation.running";
    private static int checks;
    static MonsterAssetValidation()
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
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/MonsterValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("MONSTER CHECK PASS: " + message);
    }
    private static void Run()
    {
        try
        {
            var profile = Resources.Load<CreatureDefinition>("Creatures/Chupacabra");
            Check(profile.visualPrefab != null && AssetDatabase.GetAssetPath(profile.visualPrefab) == "Assets/Mythologicum/Prefabs/Creatures/Hitotume/Hitotume.prefab", "Chupacabra uses Hitotume visual");
            Check(profile.displayName == "Chupacabra" && profile.diet == CreatureDiet.Carnivore && profile.preyTags == (CreatureTags.Goat | CreatureTags.Visitor), "Species identity and predation rules preserved");
            Check(profile.requiresTreeForSleep && profile.canClimbWoodenFences && profile.walkSpeed == 1.8f && profile.runSpeed == 5.5f, "Existing movement, climbing and sleep rules preserved");
            Check(Resources.LoadAll<CreatureDefinition>("Creatures").Length == 2, "Reserved monsters are not added to gameplay catalog");
            foreach (string name in new[] { "Hitotume", "Oni", "Kuchisakeonna" })
            {
                string path = "Assets/Mythologicum/Prefabs/Creatures/" + name + "/" + name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, "Monster retained: " + name);
                foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                    Check(!dependency.StartsWith("Assets/tt-3d/"), "Dependency organized: " + dependency);
                Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "No demo behaviours or missing scripts");
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    Check(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit"
                        && m.GetTexture("_BaseMap") != null), "URP materials retain original textures");
                var instance = UnityEngine.Object.Instantiate(prefab);
                var animator = instance.GetComponentInChildren<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Check(animator != null && animator.avatar.isValid && animator.avatar.isHuman && !animator.applyRootMotion, "Valid humanoid rig with root motion disabled");
                foreach (string state in new[] { "idle", "walk_forward", "run_forward" })
                {
                    Check(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "Locomotion state resolves: " + state);
                    animator.Play("Base Layer." + state, 0, 0);
                    animator.Update(0.1f);
                    Check(animator.GetCurrentAnimatorClipInfo(0).Length > 0, "Humanoid clip resolves");
                }
                var bones = instance.GetComponentsInChildren<Transform>();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                animator.Update(0.2f);
                Check(bones.Where((t, i) => Quaternion.Angle(t.localRotation, rotations[i]) > 0.1f).Any(), "Retargeted animation moves monster bones");
                UnityEngine.Object.DestroyImmediate(instance);
            }
            var grid = new SquareBuildGrid(Vector3.zero, 2f, 12, 12);
            var fences = new FenceLayout(grid);
            var network = new ParkBuildingNetwork(grid, fences);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var world = new CreatureWorld(grid, fences, network, camera, material, new[] { profile });
            Check(world.TrySpawn(profile, new Vector2Int(4, 4)), "Chupacabra spawns through normal creature system");
            var actor = world.Agents[0];
            var visual = actor.transform.GetChild(0);
            var bodyAnimator = visual.GetComponentInChildren<Animator>();
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Check(visual.name.StartsWith(profile.visualPrefab.name), "Spawned visual is Hitotume");
            Check(actor.GetComponent<Collider>().isTrigger && visual.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Simulation selection and navigation colliders preserved");
            var animate = typeof(CreatureWorld).GetMethod("Animate", BindingFlags.NonPublic | BindingFlags.Instance);
            var route = (IList)typeof(CreatureAgent).GetField("Route", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(actor);
            var activity = typeof(CreatureAgent).GetProperty("Activity");
            route.Add(new Vector2Int(5, 4));
            foreach (var pair in new[] { (CreatureActivity.Wandering, "walk_forward"), (CreatureActivity.Hunting, "run_forward"), (CreatureActivity.Fleeing, "run_forward") })
            {
                activity.SetValue(actor, pair.Item1);
                animate.Invoke(world, new object[] { actor });
                bodyAnimator.Update(0.2f);
                Check(bodyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + pair.Item2), "AI activity selects locomotion: " + pair.Item1);
            }
            route.Clear();
            foreach (var state in new[] { CreatureActivity.Resting, CreatureActivity.Attacking, CreatureActivity.Feeding, CreatureActivity.Sleeping })
            {
                activity.SetValue(actor, state);
                animate.Invoke(world, new object[] { actor });
                bodyAnimator.Update(0.2f);
                Check(bodyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.idle"), "Unsupported action safely uses idle: " + state);
            }
            bodyAnimator.Play("Base Layer.idle", 0, 0);
            bodyAnimator.Update(0);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Debug.Log("MONSTER MODEL BOUNDS: " + bounds);
            Check(bounds.size.y > 1.2f && bounds.size.y < 2.2f, "Hitotume uses appropriate Chupacabra scale");
            Check(Mathf.Abs(bounds.min.y) < 0.2f, "Hitotume feet align to ground");
            typeof(CreatureAgent).GetProperty("Alive").SetValue(actor, false);
            animate.Invoke(world, new object[] { actor });
            Check(!bodyAnimator.enabled, "Monster without death clip stops animating when dead");
            File.WriteAllText("MonsterValidationResult.txt", "PASS: " + checks + " checks");
            Debug.Log("MONSTER VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("MonsterValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
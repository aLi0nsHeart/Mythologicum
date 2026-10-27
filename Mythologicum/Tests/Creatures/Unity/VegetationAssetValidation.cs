using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class VegetationAssetValidation
{
    private const string Running = "VegetationAssetValidation.running";
    private static int checks;
    private static double runAfter;
    static VegetationAssetValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Running, false);
                runAfter = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Wait;
            }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void Wait()
    {
        if (EditorApplication.timeSinceStartup < runAfter) return;
        EditorApplication.update -= Wait;
        Run();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("VEGETATION CHECK PASS: " + message);
    }
    private static GameObject Root(GroundVegetation cover) => (GameObject)typeof(GroundVegetation)
        .GetField("root", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cover);
    private static Bounds BoundsOf(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void Run()
    {
        try
        {
            var definition = Resources.Load<VegetationDefinition>("Environment/Vegetation");
            Check(definition != null, "Vegetation catalog loads");
            Check(definition.trees.Length == 10 && definition.grasses.Length == 5
                && definition.herbs.Length == 6 && definition.flowers.Length == 7, "All intended variants are assigned");
            var ground = GameObject.Find("Prototype Ground - 100 x 100 metres");
            var color = ground.GetComponent<Renderer>().sharedMaterial.color;
            Check(color.g > color.r && color.g > color.b, "Actual game scene uses green ground");
            Check(GameObject.Find("Ground vegetation").transform.childCount == 2500, "Game scatters ground cover over all 100 x 100 metres");
            Check(UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
                .Any(t => t.text.Contains("LUX ART STUDIOS")), "Publisher credit is visible in the game UI");
            foreach (var prefab in definition.trees.Concat(definition.grasses).Concat(definition.herbs).Concat(definition.flowers))
            {
                Check(prefab != null && prefab.GetComponentsInChildren<Renderer>().Length > 0, "Prefab imports: " + prefab.name);
                foreach (var dep in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(prefab), true))
                    Check(!dep.StartsWith("Assets/Vegetation_Stylized"), "No dependency on removed pack folder");
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                    Check(renderer.sharedMaterials.All(m => m != null && m.shader != null
                        && m.shader.name == "Universal Render Pipeline/Lit"), "URP materials: " + renderer.name);
            }
            var grid = new SquareBuildGrid(new Vector3(200f, 3f, 200f), 2f, 12, 12);
            var environment = new EnvironmentGrid(grid);
            var oldRandom = UnityEngine.Random.state;
            var cover = new GroundVegetation(grid, environment, definition);
            Check(oldRandom.Equals(UnityEngine.Random.state), "Scattering does not alter gameplay random state");
            GameObject coverRoot = Root(cover);
            Check(coverRoot.transform.childCount == 144, "Every grid cell receives ground cover");
            Check(coverRoot.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Ground cover cannot obstruct physics or selection");
            foreach (Transform cell in coverRoot.transform)
            {
                Check(cell.childCount == definition.patchesPerCell, "Configured density applies to cell");
                foreach (Transform plant in cell)
                {
                    Bounds bounds = BoundsOf(plant.gameObject);
                    Check(bounds.size.y >= 0.21f && bounds.size.y <= 0.49f, "Ground cover uses appropriate scale");
                    Check(Mathf.Abs(bounds.min.y - 3.005f) < 0.02f, "Plant feet align to elevated map plane");
                    Check(plant.GetComponentsInChildren<LODGroup>().All(g => !g.enabled), "Ground cover does not disappear at park camera height");
                }
            }
            var cellToBuild = new Vector2Int(0, 0);
            grid.TryOccupy(cellToBuild, BuildCellContent.Path);
            cover.Refresh();
            Check(!coverRoot.transform.GetChild(0).gameObject.activeSelf, "Paths clear ground cover");
            grid.TryRemove(cellToBuild, BuildCellContent.Path);
            cover.Refresh();
            Check(coverRoot.transform.GetChild(0).gameObject.activeSelf, "Demolished paths restore original ground cover");
            grid.TryOccupy(cellToBuild, BuildCellContent.Building);
            cover.Refresh();
            Check(!coverRoot.transform.GetChild(0).gameObject.activeSelf, "Buildings clear ground cover");
            grid.TryRemove(cellToBuild, BuildCellContent.Building);
            var parent = new GameObject("Tree validation root");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var trees = new TreeConstruction(grid, environment, parent.transform, material, definition);
            var variants = new HashSet<string>();
            for (int i = 0; i < 30; i++)
            {
                var cell = new Vector2Int(i % 12, i / 12);
                Check(trees.TryPlace(cell), "Imported trees remain placeable");
                GameObject tree = parent.transform.GetChild(i).gameObject;
                var visual = tree.transform.GetChild(0).gameObject;
                variants.Add(visual.name);
                Bounds bounds = BoundsOf(visual);
                Check(Mathf.Abs(bounds.min.y - 3f) < 0.02f, "Tree feet align to ground");
                Check(bounds.size.y >= 3.05f && bounds.size.y <= 3.75f, "Trees fit gameplay scale");
                Check(tree.GetComponent<Collider>().isTrigger, "Tree retains selection trigger");
                Check(visual.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Imported tree colliders do not block navigation");
                Check(visual.GetComponentsInChildren<LODGroup>().All(g => g.GetLODs().Last().screenRelativeTransitionHeight <= 0.0011f), "Distant tree LOD stays visible");
            }
            Check(variants.Count > 1, "Placement chooses varied tree models");
            cover.Refresh();
            Check(!coverRoot.transform.GetChild(0).gameObject.activeSelf, "Placed tree clears underlying plants");
            parent.transform.GetChild(0).GetComponent<DemolitionTarget>().Demolish();
            cover.Refresh();
            Check(environment.TreesAt(cellToBuild) == 0 && coverRoot.transform.GetChild(0).gameObject.activeSelf,
                "Tree demolition restores environment and ground cover");
            trees.Dispose();
            cover.Dispose();
            File.WriteAllText("VegetationValidationResult.txt", "PASS: " + checks + " checks");
            Debug.Log("VEGETATION VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("VegetationValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
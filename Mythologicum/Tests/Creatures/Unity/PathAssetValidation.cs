using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PathAssetValidation
{
    private const string Running = "PathAssetValidation.running";
    private static int checks;
    static PathAssetValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); Run(); }
        };
    }
    public static void Start()
    {
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity", OpenSceneMode.Single);
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
        var assigned = new SerializedObject(controller).FindProperty("pathTilePrefab").objectReferenceValue;
        SessionState.SetBool("PathAssetValidation.assigned", AssetDatabase.GetAssetPath(assigned) == "Assets/Mythologicum/Prefabs/Paths/block2.prefab");
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/PathValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("PATH CHECK PASS: " + message);
    }
    private static Bounds VisualBounds(GameObject tile)
    {
        var renderers = tile.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void Run()
    {
        try
        {
            const string prefabPath = "Assets/Mythologicum/Prefabs/Paths/block2.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Check(prefab != null && prefab.name == "block2", "Only requested square tile is selected");
            Check(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Mythologicum/Prefabs/Paths" }).Length == 1, "Other road variants were removed");
            foreach (string path in AssetDatabase.GetDependencies(prefabPath, true))
                Check(!path.StartsWith("Assets/StarsandShellsStudio/"), "Dependency migrated: " + path);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                Check(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit"), "Original URP materials resolve");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            foreach (float cellSize in new[] { 1f, 2f, 3.5f })
            {
                var grid = new SquareBuildGrid(new Vector3(200f, 2.5f, 200f), cellSize, 4, 4);
                var environment = new EnvironmentGrid(grid);
                var plants = new GroundVegetation(grid, environment, Resources.Load<VegetationDefinition>("Environment/Vegetation"));
                var coverRoot = (GameObject)typeof(GroundVegetation).GetField("root", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(plants);
                var parent = new GameObject("Path validation " + cellSize);
                var paths = new PathConstruction(grid, parent.transform, material, prefab);
                for (int i = 0; i < 3; i++)
                {
                    var cell = new Vector2Int(i, 0);
                    Check(paths.TryPlace(cell), "Tile places on grid");
                    Check(grid.ContentAt(cell) == BuildCellContent.Path, "Tile reserves path cell");
                    var tile = parent.transform.GetChild(i).gameObject;
                    Check(tile.transform.GetChild(0).name.StartsWith("block2"), "Placed path uses block2 model");
                    Bounds bounds = VisualBounds(tile);
                    Check(Mathf.Abs(bounds.size.x - cellSize) < 0.001f && Mathf.Abs(bounds.size.z - cellSize) < 0.001f, "Tile fits exact cell dimensions");
                    Check(Mathf.Abs(bounds.size.y - PathConstruction.Height) < 0.001f, "Tile retains path surface height");
                    Check(Mathf.Abs(bounds.min.y - 2.5f) < 0.001f, "Tile bottom aligns to map plane");
                    Check((bounds.center - grid.CellCenter(cell) - Vector3.up * PathConstruction.Height / 2).sqrMagnitude < 0.00001f,
                        "Imported pivot is centered on cell");
                    Check(tile.GetComponent<Collider>().isTrigger && tile.GetComponent<DemolitionTarget>() != null, "Tile keeps demolition selection trigger");
                    Check(tile.GetComponentsInChildren<Collider>().All(c => c == tile.GetComponent<Collider>() || !c.enabled), "Imported colliders cannot block navigation");
                    if (i > 0) Check(Mathf.Abs(VisualBounds(parent.transform.GetChild(i - 1).gameObject).max.x - bounds.min.x) < 0.001f,
                        "Adjacent tiles meet without gaps or overlaps");
                }
                Check(!paths.TryPlace(new Vector2Int(0, 0)), "Duplicate tile placement is rejected");
                plants.Refresh();
                Check(!coverRoot.transform.GetChild(0).gameObject.activeSelf, "New tile clears ground vegetation");
                Check(paths.TryRemove(new Vector2Int(0, 0)), "Tile can be demolished");
                Check(grid.ContentAt(new Vector2Int(0, 0)) == BuildCellContent.Empty, "Demolition releases grid cell");
                plants.Refresh();
                Check(coverRoot.transform.GetChild(0).gameObject.activeSelf, "Demolition restores ground vegetation");
                Check(paths.TryPlace(new Vector2Int(0, 0)), "Tile can be rebuilt after demolition");
                plants.Dispose();
            }
            var fallbackParent = new GameObject("Fallback path");
            var fallback = new PathConstruction(new SquareBuildGrid(Vector3.zero, 2, 2, 2), fallbackParent.transform, material);
            Check(fallback.TryPlace(Vector2Int.zero), "Unconfigured path systems retain placeholder fallback");
            Check(SessionState.GetBool("PathAssetValidation.assigned", false), "Game scene assigns block2 to path construction");
            File.WriteAllText("PathValidationResult.txt", "PASS: " + checks + " checks");
            Debug.Log("PATH VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("PathValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
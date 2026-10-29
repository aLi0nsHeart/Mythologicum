using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FenceAssetValidation
{
    private const string Running = "FenceAssetValidation.running";
    private static int checks;
    private static double runAfter;
    static FenceAssetValidation()
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
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("FENCE CHECK PASS: " + message);
    }
    private static GameObject Root(FenceConstruction construction, FenceEdge edge) => Field<Dictionary<FenceEdge, GameObject>>(construction, "objects")[edge];
    private static Bounds View(FenceConstruction construction, FenceEdge edge, float cellSize, float height, GameObject expected)
    {
        GameObject root = Root(construction, edge);
        Transform wrapper = root.transform.Find("Fence model");
        Check(wrapper != null && wrapper.childCount == 1, "Imported fence replaces placeholder parts");
        GameObject model = wrapper.GetChild(0).gameObject;
        Check(model.GetComponentInChildren<MeshFilter>().sharedMesh == expected.GetComponentInChildren<MeshFilter>().sharedMesh, "Selected fence mesh is used");
        var renderers = wrapper.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
            Check(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap") != null), "Original fence palette uses URP");
        }
        Check(Mathf.Abs(bounds.size.x - (edge.AlongX ? cellSize : FenceConstruction.Thickness)) < 0.002f
            && Mathf.Abs(bounds.size.z - (edge.AlongX ? FenceConstruction.Thickness : cellSize)) < 0.002f, "Fence matches grid edge in both axes");
        Check(Mathf.Abs(bounds.size.y - height) < 0.002f && Mathf.Abs(bounds.min.y - root.transform.position.y) < 0.002f, "Fence matches gameplay height and ground level");
        Check((bounds.center - construction.Layout.Center(edge) - Vector3.up * height / 2).sqrMagnitude < 0.00001f, "Source pivot is centered on edge");
        Check(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Imported colliders are disabled");
        Check(root.GetComponent<Collider>().isTrigger && root.GetComponent<DemolitionTarget>() != null && root.GetComponent<FenceSegment>() != null, "Fence retains selection, demolition and segment identity");
        var barrier = root.transform.Find("Closed barrier");
        Check(barrier != null && barrier.gameObject.layer == 2 && barrier.GetComponent<Collider>().enabled && !barrier.GetComponent<Collider>().isTrigger, "Closed physical barrier is retained");
        return bounds;
    }
    private static void Run()
    {
        try
        {
            var catalog = Resources.Load<FenceVisualCatalog>("Fences/FenceVisuals");
            Check(catalog != null, "Fence visual catalog loads");
            var pairs = new[] {
                (catalog.normal, "Fence Type1 01"), (catalog.spiked, "Fence Type2 03"), (catalog.gate, "Fence Type2 01"),
                (catalog.heavy, "Fence Type6 Big"), (catalog.sanctified, "Fence Type4 Small")
            };
            foreach (var pair in pairs)
            {
                Check(pair.Item1 != null, "Requested fence prefab retained");
                string[] dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(pair.Item1), true);
                Check(dependencies.Contains("Assets/Mythologicum/Models/Fences/" + pair.Item2 + ".dae"), "Requested source model assigned: " + pair.Item2);
                Check(!dependencies.Any(p => p.StartsWith("Assets/BrokenVector/")), "Fence dependencies migrated");
                Check(pair.Item1.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "No demo or missing scripts");
            }
            Check(catalog.heavy != catalog.normal && catalog.sanctified != catalog.spiked, "Protective fence types retain distinct imported prefabs");
            Check(Enum.GetValues(typeof(FenceKind)).Length == 4 && (int)FenceKind.Wooden == 0 && (int)FenceKind.Spiked == 1,
                "Four active fence types preserve original serialized kind values");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            foreach (float cellSize in new[] { 1f, 2f, 3.5f })
            {
                var grid = new SquareBuildGrid(new Vector3(200 + cellSize * 100, 3, 200), cellSize, 10, 10);
                var parent = new GameObject("Fence fit " + cellSize);
                var construction = new FenceConstruction(grid, parent.transform, material, material, catalog);
                var first = new FenceEdge(new Vector2Int(1, 1), true);
                var adjacent = new FenceEdge(new Vector2Int(2, 1), true);
                var corner = new FenceEdge(new Vector2Int(3, 1), false);
                Check(construction.TryPlace(first) && construction.TryPlace(adjacent) && construction.TryPlace(corner), "Adjacent and corner segments place");
                Bounds a = View(construction, first, cellSize, FenceConstruction.Height, catalog.normal);
                Bounds b = View(construction, adjacent, cellSize, FenceConstruction.Height, catalog.normal);
                Bounds c = View(construction, corner, cellSize, FenceConstruction.Height, catalog.normal);
                Check(Mathf.Abs(a.max.x - b.min.x) < 0.002f, "Straight segments meet without gaps");
                Check(b.Intersects(c), "Corner segments join");
                var creatures = new CreatureNavigation(grid, construction.Layout);
                var climbers = new CreatureNavigation(grid, construction.Layout, null, true);
                Check(!creatures.CanCross(first.CellA, first.CellB), "Normal fence contains ordinary creatures");
                Check(climbers.CanCross(first.CellA, first.CellB), "Wooden fence retains climbing behavior");
                GameObject old = Root(construction, first);
                Check(construction.TryPlace(first, FenceKind.Spiked), "Normal fence upgrades to spiked");
                Check(!old.activeSelf && old.GetComponentsInChildren<Collider>(true).All(co => !co.enabled), "Upgrade retires old collision geometry");
                View(construction, first, cellSize, FenceConstruction.SpikedHeight, catalog.spiked);
                Check(!climbers.CanCross(first.CellA, first.CellB) && !construction.Layout.CanClimb(first), "Spiked fence deters climbing");
                Check(construction.TryPlaceGate(first) && construction.Layout.KindAt(first)==FenceKind.Spiked, "Spiked fence becomes a gate without losing its protection");
                View(construction,first,cellSize,FenceConstruction.SpikedHeight,catalog.gate);
                Check(Root(construction,first).GetComponent<DemolitionTarget>().Demolish() && construction.TryPlace(first,FenceKind.Wooden), "Gate can be rebuilt as a wooden segment");
                Check(construction.TryPlaceGate(first), "Normal fence becomes requested gate model");
                View(construction, first, cellSize, FenceConstruction.Height, catalog.gate);
                Check(construction.Layout.IsGate(first) && !construction.Layout.BlocksPath(first), "Gate retains authorized path access");
                Check(!creatures.CanCross(first.CellA, first.CellB) && !climbers.CanCross(first.CellA, first.CellB), "Gate stays closed to creatures");
                grid.TryOccupy(first.CellA, BuildCellContent.Path);
                grid.TryOccupy(first.CellB, BuildCellContent.Path);
                var visitors = new VisitorNavigation(grid, construction.Layout);
                Check(visitors.CanCross(first.CellA, first.CellB), "Normal visitor path travel can use gate");
                Check(!visitors.CanCross(first.CellA, first.CellB, true), "Panic ground travel cannot bypass gate barrier");
                Check(!construction.TryPlace(first, FenceKind.Spiked), "Gate cannot be overwritten by fence upgrade");
                Check(Root(construction, first).GetComponent<DemolitionTarget>().Demolish(), "Gate can be demolished");
                Check(!construction.Layout.HasFence(first) && !construction.Layout.IsGate(first), "Demolition releases fence and gate state");
                Check(creatures.CanCross(first.CellA, first.CellB), "Demolition restores creature crossing");
                grid.TryRemove(first.CellA, BuildCellContent.Path); grid.TryRemove(first.CellB, BuildCellContent.Path);
                Check(construction.TryPlace(first), "Fence can be rebuilt after demolition");
                var heavyEdge = new FenceEdge(new Vector2Int(5, 3), true);
                var sanctifiedEdge = new FenceEdge(new Vector2Int(7, 3), false);
                Check(construction.TryPlace(heavyEdge, FenceKind.Heavy) && construction.TryPlace(sanctifiedEdge, FenceKind.Sanctified),
                    "Both protective fence kinds can be built");
                View(construction, heavyEdge, cellSize, FenceConstruction.HeightFor(FenceKind.Heavy), catalog.heavy);
                View(construction, sanctifiedEdge, cellSize, FenceConstruction.HeightFor(FenceKind.Sanctified), catalog.sanctified);
                Check(!climbers.CanCross(heavyEdge.CellA, heavyEdge.CellB) && !climbers.CanCross(sanctifiedEdge.CellA, sanctifiedEdge.CellB),
                    "Protective fences do not silently inherit wooden climbing permission");
                Check(construction.Layout.CanPhase(heavyEdge) && !construction.Layout.CanPhase(sanctifiedEdge),
                    "Heavy permits phasing while sanctified blocks it");
                float heavyHealth = construction.Layout.HealthAt(heavyEdge);
                Check(construction.Layout.TryDamage(heavyEdge, 9999) == 0 && construction.Layout.HealthAt(heavyEdge) == heavyHealth,
                    "Heavy fence retains health under creature attacks");
                Check(construction.Layout.TryDamage(sanctifiedEdge, 20) > 0 && construction.Layout.HealthAt(sanctifiedEdge) < construction.Layout.MaximumHealthAt(sanctifiedEdge),
                    "Sanctified fence retains phasing protection while taking physical damage");
                Check(construction.TryPlaceGate(heavyEdge) && construction.Layout.KindAt(heavyEdge) == FenceKind.Heavy,
                    "Reinforced gate inherits its fence protection");
                Check(construction.TryPlaceGate(sanctifiedEdge) && !construction.Layout.CanPhase(sanctifiedEdge),
                    "Sanctified gate does not create a phasing loophole");
                View(construction,heavyEdge,cellSize,FenceConstruction.HeightFor(FenceKind.Heavy),catalog.gate);
                View(construction,sanctifiedEdge,cellSize,FenceConstruction.HeightFor(FenceKind.Sanctified),catalog.gate);
            }
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            var actual = Field<FenceConstruction>(controller, "fences");
            Check(Field<FenceVisualCatalog>(actual, "visuals") == catalog, "Actual game construction loads visual catalog");
            var xEdge = new FenceEdge(new Vector2Int(4, 4), true);
            var zEdge = new FenceEdge(new Vector2Int(6, 4), false);
            var gateEdge = new FenceEdge(new Vector2Int(8, 4), true);
            var actualHeavy = new FenceEdge(new Vector2Int(10,4), true);
            var actualSanctified = new FenceEdge(new Vector2Int(12,4), false);
            Check(actual.TryPlace(xEdge) && actual.TryPlace(zEdge, FenceKind.Spiked) && actual.TryPlace(gateEdge) && actual.TryPlaceGate(gateEdge)
                && actual.TryPlace(actualHeavy, FenceKind.Heavy) && actual.TryPlace(actualSanctified, FenceKind.Sanctified), "All active fence models place in actual game scene");
            View(actual, xEdge, 2f, FenceConstruction.Height, catalog.normal);
            View(actual, zEdge, 2f, FenceConstruction.SpikedHeight, catalog.spiked);
            View(actual, gateEdge, 2f, FenceConstruction.Height, catalog.gate);
            View(actual, actualHeavy, 2f, FenceConstruction.HeightFor(FenceKind.Heavy), catalog.heavy);
            View(actual, actualSanctified, 2f, FenceConstruction.HeightFor(FenceKind.Sanctified), catalog.sanctified);
            var fallback = new FenceConstruction(new SquareBuildGrid(Vector3.zero, 2f, 4, 4), new GameObject("Fallback fence").transform, material);
            Check(fallback.TryPlace(new FenceEdge(new Vector2Int(1, 1), true)), "Unconfigured callers retain primitive fallback");
            File.WriteAllText("FenceValidationResult.txt", "PASS: " + checks + " checks");
            Debug.Log("FENCE VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("FenceValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}

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

[InitializeOnLoad]
public static class BuildingAssetValidation
{
    private const string Running = "BuildingAssetValidation.running";
    private static int checks;
    private static double runAfter;
    static BuildingAssetValidation()
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
        Debug.Log("BUILDING CHECK PASS: " + message);
    }
    private static Bounds BoundsOf(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    private static void CheckView(GameObject root, BuildingFootprint footprint, SquareBuildGrid grid)
    {
        var definition = footprint.Definition;
        GameObject model = root.transform.GetChild(0).gameObject;
        Check(model.GetComponentInChildren<MeshFilter>().sharedMesh != null, "Imported building mesh resolves");
        Bounds bounds = BoundsOf(model);
        Vector3 center = footprint.Center(grid);
        Check(Mathf.Abs(bounds.min.y - grid.Origin.y) < 0.005f, "Model sits on ground");
        Check(bounds.min.x >= center.x - footprint.Width * grid.CellSize / 2f + 0.07f
            && bounds.max.x <= center.x + footprint.Width * grid.CellSize / 2f - 0.07f
            && bounds.min.z >= center.z - footprint.Depth * grid.CellSize / 2f + 0.07f
            && bounds.max.z <= center.z + footprint.Depth * grid.CellSize / 2f - 0.07f, "Art fits rotated grid footprint");
        Check(bounds.max.y <= grid.Origin.y + definition.height + 0.005f, "Model fits collision height");
        Vector3 scale = model.transform.localScale;
        Check(Mathf.Abs(scale.x - scale.y) < 0.001f && Mathf.Abs(scale.y - scale.z) < 0.001f, "Model proportions preserved");
        Check(root.GetComponent<BoxCollider>() != null && !root.GetComponent<BoxCollider>().isTrigger, "Building retains solid simulation collider");
        Check(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Imported colliders do not interfere with simulation");
        var marker = root.transform.Find(definition.RequiresPathConnection ? "Connect a path here" : "Animal release point");
        Check((marker.position - grid.CellCenter(footprint.AccessCell) - Vector3.up * 0.14f).sqrMagnitude < 0.00001f,
            "Access marker agrees with rotated access cell");
        Check(!footprint.Contains(footprint.AccessCell) && (footprint.DoorCell - footprint.AccessCell).sqrMagnitude == 1, "Door and access cells remain adjacent");
        if (definition.kind != BuildingKind.GoatDispenser)
        {
            Vector3 front = grid.CellCenter(footprint.AccessCell) - grid.CellCenter(footprint.DoorCell);
            Check(Vector3.Dot(model.transform.forward, front.normalized) > 0.99f, "Storefront faces path entrance");
        }
        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
        {
            Check(renderer.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap") != null), "URP palette and texture resolve");
            Check(!renderer.HasPropertyBlock(), "Open/closed status does not recolor imported art");
        }
    }
    private static void Run()
    {
        try
        {
            var catalog = Resources.Load<BuildingVisualCatalog>("Buildings/BuildingVisuals");
            Check(catalog != null && catalog.activeBuildings.Length == 5, "Five active building visuals configured");
            var expected = new Dictionary<BuildingKind, string> {
                { BuildingKind.RestArea, "Env_CommercialBuilding_04" }, { BuildingKind.Kiosk, "Env_CommercialBuilding_03" },
                { BuildingKind.ParkEntrance, "Env_CommercialBuilding_02" }, { BuildingKind.ExpeditionCentre, "Env_CommercialBuilding_01" },
                { BuildingKind.GoatDispenser, "Prop_RoofWindow_07" }
            };
            foreach (var entry in catalog.activeBuildings)
            {
                var dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(entry.prefab), true);
                Check(dependencies.Contains("Assets/Mythologicum/Models/Buildings/" + expected[entry.kind] + ".fbx"), "Requested model assigned: " + entry.kind);
                Check(!dependencies.Any(p => p.StartsWith("Assets/Pandazole_Ultimate_Pack/")), "Dependencies organized");
            }
            var reserved = new[] { (catalog.researchCentre, "Env_Motel_04"), (catalog.mercenary, "Env_ResidentBuilding_02"), (catalog.zookeeper, "Env_ResidentBuilding_05") };
            foreach (var pair in reserved)
            {
                Check(pair.Item1 != null && AssetDatabase.GetAssetPath(pair.Item1).Contains("/Future/"), "Future building retained separately");
                Check(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(pair.Item1), true).Contains("Assets/Mythologicum/Models/Buildings/" + pair.Item2 + ".fbx"), "Future role matches requested model");
                Check(!catalog.activeBuildings.Any(e => e.prefab == pair.Item1), "Future building excluded from active catalog");
            }
            Check(Enum.GetValues(typeof(BuildingKind)).Length == 5, "No future gameplay building kinds introduced");
            var custom = BuildingDefinition.Defaults();
            custom[2].constructionCost = 1234f; custom[2].foodPrice = 19f;
            catalog.ApplyTo(custom);
            Check(custom[2].constructionCost == 1234f && custom[2].foodPrice == 19f, "Applying visuals preserves economic tuning");
            var goat = custom.Single(d => d.kind == BuildingKind.GoatDispenser);
            Check(goat.width == 1 && goat.depth == 1, "Goat dispenser occupies exactly one cell");
            var camera = new GameObject("Validation camera").AddComponent<Camera>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var markerMaterial = new Material(material) { color = Color.cyan };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var grid = new SquareBuildGrid(new Vector3(200 + rotation * 200, 3, 200), 2, 80, 30);
                var fences = new FenceLayout(grid);
                var parent = new GameObject("Rotation " + rotation);
                var construction = new BuildingConstruction(grid, fences, parent.transform, material, markerMaterial);
                var definitions = BuildingDefinition.Defaults(); catalog.ApplyTo(definitions);
                foreach (var definition in definitions)
                {
                    var footprint = new BuildingFootprint(definition, new Vector2Int(3 + (int)definition.kind * 14, 5), rotation);
                    Check(construction.TryPlace(footprint), "Building places at rotation " + rotation + ": " + definition.kind);
                    var root = parent.transform.GetChild(parent.transform.childCount - 1).gameObject;
                    construction.Refresh(camera);
                    CheckView(root, footprint, grid);
                    Check(footprint.Cells().Count() == definition.width * definition.depth
                        && footprint.Cells().All(c => grid.ContentAt(c) == BuildCellContent.Building), "Whole footprint reserved");
                    var record = construction.Network.Records.Last();
                    Check(record.IsOpen == (definition.kind == BuildingKind.GoatDispenser), "Path requirement preserved");
                    Check(!construction.TryPlace(footprint), "Overlapping placement rejected");
                }
                foreach (var definition in definitions)
                {
                    var root = parent.transform.Cast<Transform>().Single(t => t.name == definition.displayName).gameObject;
                    var record = construction.Network.Records.Single(r => r.Footprint.Definition == definition);
                    var cells = record.Footprint.Cells().ToArray();
                    Check(root.GetComponent<DemolitionTarget>().Demolish(), "Imported building can be demolished");
                    Check(cells.All(c => grid.ContentAt(c) == BuildCellContent.Empty), "Demolition releases entire footprint");
                }
                Check(construction.Network.Records.Count == 0, "Demolition removes building records");
            }
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            var actualDefinitions = Field<BuildingDefinition[]>(controller, "buildingCatalog");
            Check(actualDefinitions.Length == 5 && actualDefinitions.All(d => d.visualPrefab != null), "Actual game loads mapped models before creating construction palette");
            var actualGrid = Field<SquareBuildGrid>(controller, "grid");
            var builder = Field<BuildingConstruction>(controller, "buildingConstruction");
            var roots = Field<GameObject>(controller, "buildings");
            var paths = Field<PathConstruction>(controller, "paths");
            var cover = Field<GroundVegetation>(controller, "groundVegetation");
            var coverRoot = Field<GameObject>(cover, "root");
            var creatures = Field<CreatureWorld>(controller, "creatureWorld");
            var anchors = new Dictionary<BuildingKind, Vector2Int> {
                { BuildingKind.ParkEntrance, new Vector2Int(2, 3) }, { BuildingKind.ExpeditionCentre, new Vector2Int(12, 10) },
                { BuildingKind.Kiosk, new Vector2Int(32, 8) }, { BuildingKind.RestArea, new Vector2Int(35, 30) },
                { BuildingKind.GoatDispenser, new Vector2Int(42, 42) }
            };
            foreach (var definition in actualDefinitions)
            {
                var footprint = new BuildingFootprint(definition, anchors[definition.kind], 0);
                Check(builder.TryPlace(footprint), "Building places in actual game scene: " + definition.kind);
                cover.Refresh();
                foreach (var cell in footprint.Cells())
                    Check(!coverRoot.transform.Find($"Ground cover [{cell.x}, {cell.y}]").gameObject.activeSelf, "Construction clears ground cover from footprint");
            }
            builder.Refresh(controller.GetComponent<Camera>());
            var navigation = new CreatureNavigation(actualGrid, controller.Barriers, creatures.IsClear);
            var entrance = builder.Network.Entrance;
            paths.TryPlace(entrance.Footprint.AccessCell);
            foreach (var record in builder.Network.Records.Where(r => r.Footprint.Definition.RequiresPathConnection))
            {
                var route = navigation.FindPath(entrance.Footprint.AccessCell, record.Footprint.AccessCell);
                Check(route.Count > 0, "Path can reach building access");
                foreach (var cell in route) if (actualGrid.ContentAt(cell) == BuildCellContent.Empty) paths.TryPlace(cell);
            }
            builder.Refresh(controller.GetComponent<Camera>());
            Check(builder.Network.Records.All(r => r.IsOpen), "All connected buildings operate with imported visuals");
            Check(controller.Visitors.TrySpawn() != null, "Visitors can arrive through new entrance");
            var dispenser = builder.Network.Records.Single(r => r.Footprint.Definition.kind == BuildingKind.GoatDispenser);
            dispenser.Footprint.Definition.dispenseInterval = 1f;
            creatures.Tick(1.1f);
            Check(creatures.Agents.Any(a => Field<ParkBuildingRecord>(a, "SourceDispenser") == dispenser), "One-cell dispenser successfully releases a goat");
            var kiosk = builder.Network.Records.Single(r => r.Footprint.Definition.kind == BuildingKind.Kiosk);
            var kioskCells = kiosk.Footprint.Cells().ToArray();
            var kioskRoot = roots.transform.Cast<Transform>().Single(t => t.name == kiosk.Footprint.Definition.displayName).gameObject;
            kioskRoot.GetComponent<DemolitionTarget>().Demolish();
            cover.Refresh();
            Check(kioskCells.All(c => coverRoot.transform.Find($"Ground cover [{c.x}, {c.y}]").gameObject.activeSelf), "Demolition restores vegetation across expanded footprint");
            File.WriteAllText("BuildingValidationResult.txt", "PASS: " + checks + " checks");
            Debug.Log("BUILDING VALIDATION PASSED: " + checks + " checks");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("BuildingValidationResult.txt", "FAIL: " + e);
            EditorApplication.Exit(1);
        }
    }
}
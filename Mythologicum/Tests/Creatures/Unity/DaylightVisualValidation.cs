using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using Mythologicum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class DaylightVisualValidation
{
    private const string Phase = "DaylightVisualValidation.phase";
    private static double readyAt;
    static DaylightVisualValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            string phase = SessionState.GetString(Phase, "");
            if (state == PlayModeStateChange.EnteredPlayMode && (phase == "before" || phase == "after"))
            {
                readyAt = EditorApplication.timeSinceStartup + 3;
                EditorApplication.update += Wait;
            }
            if (state == PlayModeStateChange.EnteredEditMode && phase == "apply")
                EditorApplication.delayCall += ApplyAndResume;
        };
    }
    public static void Start()
    {
        SessionState.SetString(Phase, "before");
        UnityEngine.Random.InitState(1234);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    private static void ApplyAndResume()
    {
        try
        {
            DaylightSceneSetup.Apply();
            SessionState.SetString(Phase, "after");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    private static void Wait()
    {
        if (EditorApplication.timeSinceStartup < readyAt) return;
        EditorApplication.update -= Wait;
        try { Capture(); } catch (Exception e) { Fail(e); }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("LIGHTING CHECK PASS: " + message);
    }
    private static void Capture()
    {
        bool after = SessionState.GetString(Phase, "") == "after";
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Real graphics device available for visual checks");
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
        var camera = controller.GetComponent<Camera>();
        var builder = Field<BuildingConstruction>(controller, "buildingConstruction");
        var definitions = Field<BuildingDefinition[]>(controller, "buildingCatalog");
        var grid = Field<SquareBuildGrid>(controller, "grid");
        var paths = Field<PathConstruction>(controller, "paths");
        var trees = Field<TreeConstruction>(controller, "trees");
        var fences = Field<FenceConstruction>(controller, "fences");
        var creatures = Field<CreatureWorld>(controller, "creatureWorld");
        controller.enabled = false;
        controller.GetComponent<IsometricCameraController>().enabled = false;
        var landmarks = GameObject.Find("Prototype Landmarks");
        if (landmarks != null) landmarks.SetActive(false);
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        UnityEngine.Random.InitState(1234);
        typeof(TreeConstruction).GetField("random", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(trees, new System.Random(1234));
        var anchors = new Dictionary<BuildingKind, Vector2Int> {
            { BuildingKind.ParkEntrance, new Vector2Int(17, 23) }, { BuildingKind.ExpeditionCentre, new Vector2Int(27, 25) },
            { BuildingKind.RestArea, new Vector2Int(16, 31) }, { BuildingKind.Kiosk, new Vector2Int(26, 17) },
            { BuildingKind.GoatDispenser, new Vector2Int(23, 18) }
        };
        foreach (var definition in definitions)
            Check(builder.TryPlace(new BuildingFootprint(definition, anchors[definition.kind], 0)), "Preview model placed: " + definition.kind);
        for (int i = 21; i < 25; i++)
        {
            fences.TryPlace(new FenceEdge(new Vector2Int(i, 19), true));
            fences.TryPlace(new FenceEdge(new Vector2Int(i, 23), true));
        }
        for (int i = 19; i < 23; i++)
        {
            fences.TryPlace(new FenceEdge(new Vector2Int(21, i), false));
            fences.TryPlace(new FenceEdge(new Vector2Int(25, i), false), FenceKind.Spiked);
        }
        fences.TryPlaceGate(new FenceEdge(new Vector2Int(22, 19), true));
        var navigation = new CreatureNavigation(grid, fences.Layout, creatures.IsClear);
        var entrance = builder.Network.Entrance;
        paths.TryPlace(entrance.Footprint.AccessCell);
        foreach (var record in builder.Network.Records.Where(r => r.Footprint.Definition.RequiresPathConnection))
            foreach (var cell in navigation.FindPath(entrance.Footprint.AccessCell, record.Footprint.AccessCell))
                if (grid.ContentAt(cell) == BuildCellContent.Empty) paths.TryPlace(cell);
        foreach (var cell in new[] { new Vector2Int(14,24), new Vector2Int(14,29), new Vector2Int(22,29), new Vector2Int(24,30),
            new Vector2Int(36,27), new Vector2Int(35,20), new Vector2Int(31,16), new Vector2Int(22,16), new Vector2Int(15,18), new Vector2Int(25,33) })
            trees.TryPlace(cell);
        creatures.TrySpawn(Resources.Load<CreatureDefinition>("Creatures/Goat"), new Vector2Int(23,21));
        creatures.TrySpawn(Resources.Load<CreatureDefinition>("Creatures/Goat"), new Vector2Int(22,20));
        creatures.TrySpawn(Resources.Load<CreatureDefinition>("Creatures/Chupacabra"), new Vector2Int(26,22));
        var visitorProfile = ScriptableObject.CreateInstance<CreatureDefinition>();
        visitorProfile.displayName = "Preview visitor";
        visitorProfile.bodyScale = new Vector3(0.5f, 0.85f, 0.5f);
        var people = Resources.Load<PeopleDefinition>("People/People");
        creatures.SpawnExternal(visitorProfile, new Vector2Int(24,24), people.civilians[0]);
        creatures.SpawnExternal(visitorProfile, new Vector2Int(25,24), people.civilians[3]);
        Field<GroundVegetation>(controller, "groundVegetation").Refresh();
        builder.Refresh(camera);
        foreach (var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) label.gameObject.SetActive(false);
        foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (animator.HasState(0, Animator.StringToHash("Base Layer.idle")))
            { animator.Play("Base Layer.idle", 0, 0); animator.Update(0.1f); }
        }
        Time.timeScale = 0f;
        camera.transform.position = new Vector3(29f, 30f, -29f);
        camera.transform.LookAt(new Vector3(0f, 1f, 0f));
        camera.fieldOfView = 50f;
        camera.aspect = 1.6f;
        var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
        if (after)
        {
            Check(RenderSettings.sun == sun && sun.shadows == LightShadows.Soft, "Sun is assigned and casts realtime soft shadows");
            Check(RenderSettings.skybox != null && camera.clearFlags == CameraClearFlags.Skybox, "Skybox is rendered by camera");
            Check(RenderSettings.ambientMode == AmbientMode.Trilight, "Gradient sky and ground ambient light enabled");
            Check(RenderSettings.fog && RenderSettings.fogStartDistance >= 60f, "Distance haze leaves nearby gameplay clear");
            var data = camera.GetUniversalAdditionalCameraData();
            Check(data.renderPostProcessing && data.antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing, "Grading and anti-aliasing enabled");
            var volume = GameObject.Find("Park Daylight").GetComponent<Volume>();
            Check(volume.isGlobal && volume.sharedProfile.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.Neutral, "Scene owns a neutral daylight volume");
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Check(pipeline != null && pipeline.shadowDistance >= 100f, "PC shadows cover zoomed-out camera range");
        }
        var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Lighting preview" };
        target.Create();
        camera.targetTexture = target;
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
        RenderTexture.active = target;
        var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
        pixels.Apply();
        Color[] colors = pixels.GetPixels();
        double mean = colors.Average(c => (double)c.grayscale);
        Check(mean > 0.03 && mean < 0.95, "Preview contains visible, nonblank rendered scene");
        File.WriteAllBytes(after ? "LightingAfter.png" : "LightingBefore.png", pixels.EncodeToPNG());
        Debug.Log("LIGHTING PREVIEW " + (after ? "AFTER" : "BEFORE") + ": mean=" + mean);
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(pixels);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        if (!after)
        {
            SessionState.SetString(Phase, "apply");
            Time.timeScale = 1f;
            EditorApplication.ExitPlaymode();
        }
        else
        {
            SessionState.SetString(Phase, "complete");
            File.WriteAllText("LightingValidationResult.txt", "PASS: daylight scene settings and GPU before/after renders validated");
            EditorApplication.Exit(0);
        }
    }
    private static void Fail(Exception e)
    {
        Debug.LogException(e);
        SessionState.SetString(Phase, "failed");
        File.WriteAllText("LightingValidationResult.txt", "FAIL: " + e);
        EditorApplication.Exit(1);
    }
}
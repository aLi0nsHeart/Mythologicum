using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BKPureNature;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Run in the isolated validation project with a real graphics device.</summary>
[InitializeOnLoad]
public static class FogIntegrationValidation
{
    const string Running = "FogIntegrationValidation.running";
    const string FallbackMode = "FogIntegrationValidation.fallback";
    static bool pending, fallback;
    static int stage, checks;
    static double readyAt, deadline;
    static Texture2D previousTexture;
    static ParkSaveData saved;
    static string Result => Path.Combine(Directory.GetCurrentDirectory(), fallback ? "FogFallbackValidationResults.txt" : "FogValidationResults.txt");
    static string Captures => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "FogValidation");

    static FogIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            fallback = SessionState.GetBool(FallbackMode, false);
            pending = true;
            readyAt = EditorApplication.timeSinceStartup + 2;
            deadline = readyAt + 60;
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        SessionState.SetBool(FallbackMode, false);
        StartValidation();
    }

    /// <summary>Run with -nographics to verify the ordinary distance-fog fallback.</summary>
    public static void StartFallback()
    {
        SessionState.SetBool(FallbackMode, true);
        StartValidation();
    }

    static void StartValidation()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "FogValidation";
        Directory.CreateDirectory(Captures);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!Application.isPlaying || !pending || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out waiting for fog validation stage " + stage + ".");
            if (fallback)
            {
                ValidateFallback();
                return;
            }
            if (stage == 2)
            {
                if (SceneManager.GetActiveScene().name != "MainMenuScene") return;
                Check(PureVolumetricFog.ActiveVolumes.Count == 0, "Leaving the park unregisters its fog volume");
                Check(previousTexture == null, "Leaving the park releases its generated height texture");
                Check(!RenderSettings.fog, "Park teardown preserves the main menu scene's fog settings");
                var navigation = UnityEngine.Object.FindFirstObjectByType<SceneNavigation>();
                Check(navigation != null, "Main menu remains available after fog cleanup");
                stage = 3;
                navigation.StartConfiguredGame(new MapGenerationOptions { width = 32, depth = 32, seed = -673, relief = 12 });
                WaitForNextScene(2);
                return;
            }
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            var atmosphere = UnityEngine.Object.FindFirstObjectByType<ParkAtmosphere>();
            if (controller?.Terrain == null || atmosphere?.Fog == null || !atmosphere.Fog.IsReady)
            {
                return;
            }
            if (stage == 0) Validate(controller, atmosphere);
            else if (stage == 1) ValidateRestored(controller, atmosphere);
            else if (stage == 3) ValidateSmallMap(controller, atmosphere);
        }
        catch (Exception error) { Fail(error); }
    }

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
        Debug.Log("FOG CHECK PASS: " + message);
    }

    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static bool Near(float a, float b, float tolerance = .005f) => Mathf.Abs(a - b) < tolerance;
    static void WaitForNextScene(double seconds)
    {
        readyAt = EditorApplication.timeSinceStartup + seconds;
        deadline = readyAt + 60;
    }

    static void Validate(BuildModeController controller, ParkAtmosphere atmosphere)
    {
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsComputeShaders,
            "Validation uses a real compute-capable graphics device");
        Check(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset,
            "Volumetric fog runs in the game's Universal Render Pipeline");
        foreach (var name in new[] { "PureVolumetricFog", "PureVolumetricFogComposite" })
        {
            var shader = Resources.Load<Shader>("PureVolumetricFog/" + name);
            Check(shader != null && shader.isSupported, "Retained shader is available and supported: " + name);
            Check(!ShaderUtil.GetShaderMessages(shader).Any(message => message.severity.ToString() == "Error"),
                "Retained shader has no compiler errors: " + name);
        }
        foreach (var name in new[] { "PureVolumetricFogNoise", "PureVolumetricFogHeightmap", "PureVolumetricFogHorizonShadow" })
            Check(Resources.Load<ComputeShader>("PureVolumetricFog/" + name) != null,
                "Retained compute resource is available: " + name);
        foreach (var name in new[] { "PC", "Mobile" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/" + name + "_Renderer.asset");
            var features = renderer.rendererFeatures.OfType<PureVolumetricFogRendererFeature>().ToArray();
            Check(features.Length == 1 && features[0].isActive
                && features[0].Resolution == PureVolumetricFogRendererFeature.FogResolution.Half,
                name + " renderer owns one active half-resolution fog feature");
        }
        Check(!RenderSettings.fog, "Ready volumetric fog replaces the old linear distance haze");
        Check(PureVolumetricFog.ActiveVolumes.Count == 1 && PureVolumetricFog.ActiveVolumes[0] == atmosphere.Fog,
            "Exactly one park atmosphere is active");
        var grid = Field<SquareBuildGrid>(controller, "grid");
        CheckHeightSource(grid, atmosphere);
        atmosphere.Initialize(grid);
        Check(!RenderSettings.fog && atmosphere.Fog.IsReady,
            "Repeated atmosphere initialization keeps the original legacy-fog state");
        atmosphere.enabled = false;
        Check(RenderSettings.fog && !atmosphere.Fog.enabled && PureVolumetricFog.ActiveVolumes.Count == 0,
            "Disabling the adapter restores legacy haze and suspends its volumetric renderer");
        atmosphere.enabled = true;
        Check(!RenderSettings.fog && atmosphere.Fog.enabled && atmosphere.Fog.IsReady
            && PureVolumetricFog.ActiveVolumes.Count == 1,
            "Re-enabling the adapter restores a single volumetric atmosphere");
        controller.enabled = false;
        var camera = controller.GetComponent<Camera>();
        Check(camera.GetUniversalAdditionalCameraData().scriptableRenderer != null,
            "Game camera initializes its renderer before GPU capture");
        var controls = controller.GetComponent<IsometricCameraController>();
        controls.enabled = false;
        Time.timeScale = 0;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
        var landmarks = GameObject.Find("Prototype Landmarks");
        if (landmarks != null) landmarks.SetActive(false);
        Vector3 centre = grid.Origin + new Vector3(grid.Width * grid.CellSize * .5f, 0, grid.Depth * grid.CellSize * .5f);
        centre.y = grid.GroundHeight(centre) + 2;
        camera.fieldOfView = 50;
        camera.farClipPlane = 500;
        camera.aspect = 1.6f;
        foreach (var viewpoint in new[] { "Near", "Far" })
        {
            var offset = viewpoint == "Near" ? new Vector3(29, 30, -29) : new Vector3(65, 65, -65);
            camera.transform.position = centre + offset;
            camera.transform.LookAt(centre);
            atmosphere.Fog.enabled = false;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70;
            RenderSettings.fogEndDistance = 180;
            var before = Capture(camera, "Fog" + viewpoint + "Legacy.png");
            atmosphere.Fog.enabled = true;
            RenderSettings.fog = false;
            var after = Capture(camera, "Fog" + viewpoint + "Volumetric.png");
            double difference = before.Zip(after, (a, b) => (double)(Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / 3).Average();
            Check(difference > .0005, viewpoint + " GPU capture changes when volumetric fog replaces the legacy haze (mean difference " + difference.ToString("F4") + ")");
        }
        Check(PureVolumetricFogRendererFeature.IsInstalled, "Camera renderer has installed the fog feature after GPU rendering");
        var handlesCamera = typeof(PureVolumetricFogRendererFeature).GetMethod("HandlesCamera", BindingFlags.NonPublic | BindingFlags.Static);
        Check(handlesCamera != null && (bool)handlesCamera.Invoke(null, new object[] { camera }),
            "The reduced-resolution renderer feature handled this camera's fog captures");
        var original = atmosphere.HeightTexture.GetPixels();
        Check(controller.Terrain.TryEdit(grid, new Vector2(grid.Width * .5f, grid.Depth * .5f), 3, 1,
            TerrainBrushMode.Raise, 0, out string reason), "Terrain brush accepts an edit in the empty park: " + reason);
        atmosphere.SyncTerrain();
        Check(atmosphere.UploadedRevision == controller.Terrain.Revision,
            "Landscaping uploads the current terrain revision to fog");
        Check(original.Zip(atmosphere.HeightTexture.GetPixels(), (a, b) => Mathf.Abs(a.r - b.r)).Any(delta => delta > .0001f),
            "Landscaping changes the fog's sampled height data");
        CheckHeightSource(grid, atmosphere);
        saved = controller.CapturePark();
        ParkSaveStore.Pending = saved;
        previousTexture = atmosphere.HeightTexture;
        stage = 1;
        WaitForNextScene(2);
        Time.timeScale = 1;
        SceneManager.LoadScene("GameScene");
    }

    static void CheckHeightSource(SquareBuildGrid grid, ParkAtmosphere atmosphere)
    {
        var texture = atmosphere.HeightTexture;
        var bounds = atmosphere.HeightBounds;
        Check(texture != null && texture.isReadable && texture.width >= grid.Width && texture.height >= grid.Depth,
            "Generated height source is readable and has at least one sample per terrain cell");
        Check(Near(bounds.min.x, grid.Origin.x) && Near(bounds.min.z, grid.Origin.z)
            && Near(bounds.size.x, grid.Width * grid.CellSize) && Near(bounds.size.z, grid.Depth * grid.CellSize),
            "Fog height bounds match the selected park dimensions");
        Check((atmosphere.Fog.TerrainBounds.center - bounds.center).sqrMagnitude < .0001f
            && (atmosphere.Fog.TerrainBounds.size - bounds.size).sqrMagnitude < .0001f,
            "Vendor fog consumes the generated mesh terrain's height bounds");
        foreach (var index in new[] { 0, 1, 7, 19, 37 })
        {
            int x = index * 17 % texture.width, z = index * 31 % texture.height;
            var world = new Vector3(bounds.min.x + (x + .5f) / texture.width * bounds.size.x, 0,
                bounds.min.z + (z + .5f) / texture.height * bounds.size.z);
            float reconstructed = bounds.min.y + texture.GetPixel(x, z).r * bounds.size.y;
            Check(Near(reconstructed, grid.GroundHeight(world), .015f),
                "Uploaded fog height agrees with mesh ground at sampled texel " + index);
        }
    }

    static Color[] Capture(Camera camera, string filename)
    {
        var target = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.Create();
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            var colors = image.GetPixels();
            double mean = colors.Average(color => (double)color.grayscale);
            Check(mean > .03 && mean < .97, "GPU capture contains a visible, nonblank park: " + filename);
            Check(colors.Count(color => color.r > .9f && color.b > .9f && color.g < .1f) < colors.Length / 100,
                "GPU capture has no significant missing-shader magenta: " + filename);
            File.WriteAllBytes(Path.Combine(Captures, filename), image.EncodeToPNG());
            return colors;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    static void ValidateRestored(BuildModeController controller, ParkAtmosphere atmosphere)
    {
        Check(previousTexture == null, "Reload releases the old park's generated fog height texture");
        Check(!RenderSettings.fog && PureVolumetricFog.ActiveVolumes.Count == 1,
            "Save loading restores one volumetric atmosphere without stacking legacy fog");
        Check(saved.terrain.heights.SequenceEqual(controller.CapturePark().terrain.heights),
            "Fog integration preserves edited terrain through save loading");
        CheckHeightSource(Field<SquareBuildGrid>(controller, "grid"), atmosphere);
        previousTexture = atmosphere.HeightTexture;
        stage = 2;
        WaitForNextScene(1);
        SceneManager.LoadScene("MainMenuScene");
    }

    static void ValidateSmallMap(BuildModeController controller, ParkAtmosphere atmosphere)
    {
        var grid = Field<SquareBuildGrid>(controller, "grid");
        Check(grid.Width == 32 && grid.Depth == 32, "A new small park still uses the main menu's map selection");
        CheckHeightSource(grid, atmosphere);
        Check(!RenderSettings.fog && PureVolumetricFog.ActiveVolumes.Count == 1,
            "Re-entering gameplay creates one correctly sized fog volume");
        Check(!Directory.Exists("Assets/BK/Pure_VolumetricFog"), "The original imported demo project has been removed");
        File.WriteAllText(Result, "PASS: " + checks + " fog GPU, mesh-height, landscaping, save/load, map-size and scene-cleanup checks.\nCaptures: " + Captures);
        pending = false;
        EditorApplication.Exit(0);
    }

    static void ValidateFallback()
    {
        if (stage == 0)
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            var atmosphere = UnityEngine.Object.FindFirstObjectByType<ParkAtmosphere>();
            if (controller?.Terrain == null || atmosphere?.Fog == null) return;
            Check(SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.supportsComputeShaders,
                "Fallback validation runs without volumetric-fog graphics support");
            Check(atmosphere.isActiveAndEnabled && !atmosphere.Fog.IsReady,
                "Atmosphere remains present but reports unsupported volumetric rendering");
            Check(RenderSettings.fog, "Unsupported graphics retain the original distance haze");
            atmosphere.Initialize(Field<SquareBuildGrid>(controller, "grid"));
            atmosphere.SyncTerrain();
            Check(RenderSettings.fog && !atmosphere.Fog.IsReady,
                "Repeated initialization preserves the distance-fog fallback");
            atmosphere.enabled = false;
            Check(RenderSettings.fog && !atmosphere.Fog.enabled,
                "Disabling the unsupported adapter retains distance fog");
            atmosphere.enabled = true;
            atmosphere.SyncTerrain();
            Check(RenderSettings.fog && !atmosphere.Fog.IsReady,
                "Re-enabling without GPU support keeps distance fog enabled");
            previousTexture = atmosphere.HeightTexture;
            stage = 1;
            WaitForNextScene(1);
            SceneManager.LoadScene("MainMenuScene");
            return;
        }
        if (SceneManager.GetActiveScene().name != "MainMenuScene") return;
        Check(PureVolumetricFog.ActiveVolumes.Count == 0,
            "Leaving unsupported gameplay unregisters its fog volume");
        Check(previousTexture == null, "Unsupported gameplay releases any generated height texture");
        Check(!RenderSettings.fog, "Fallback teardown preserves the main menu's own fog settings");
        File.WriteAllText(Result, "PASS: " + checks + " fog unsupported-device fallback and scene-cleanup checks.");
        pending = false;
        EditorApplication.Exit(0);
    }

    static void Fail(Exception error)
    {
        pending = false;
        Debug.LogException(error);
        File.WriteAllText(Result, "FAIL: " + error);
        EditorApplication.Exit(1);
    }
}

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
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Standalone native Play-mode validation; copy into the disposable project's Assets/Editor.</summary>
[InitializeOnLoad]
public static class SceneTransitionNativeChecks
{
    private const string Running = "SceneTransitionValidation.running";
    private static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    private static bool pending;
    private static int checks;
    private static double started;
    private static string phase;
    private static string ownedSlot;
    private static string runtimeError;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "SceneTransitionValidationResults.txt");
    private static string Captures => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "SceneTransitionValidation");

    static SceneTransitionNativeChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false);
            checks = 0; runtimeError = null; pending = true; started = EditorApplication.timeSinceStartup;
            steps.Clear(); steps.Push(Run());
        };
        Application.logMessageReceived += (condition, trace, type) =>
        {
            if (pending && runtimeError == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                runtimeError = condition + "\n" + trace;
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "SceneTransitionValidation";
        Directory.CreateDirectory(Captures);
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!pending || !Application.isPlaying) return;
        try
        {
            if (runtimeError != null) throw new Exception("Unexpected Unity runtime error during " + phase + ": " + runtimeError);
            if (EditorApplication.timeSinceStartup - started > 600)
                throw new Exception("Scene-transition validation timed out during " + phase);
            // Nested IEnumerator yields behave as they would in a normal Unity coroutine.
            for (int i = 0; i < 32; i++)
            {
                if (steps.Count == 0)
                {
                    pending = false;
                    File.WriteAllText(Result, "PASS: " + checks + " native scene-transition, preparation, input, pause, save-load and recovery checks.\nCaptures: " + Captures);
                    EditorApplication.Exit(0);
                    return;
                }
                var current = steps.Peek();
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose(); steps.Pop(); continue;
                }
                if (current.Current is IEnumerator child) { steps.Push(child); continue; }
                return;
            }
        }
        catch (Exception error)
        {
            pending = false;
            while (steps.Count > 0)
            {
                try { (steps.Pop() as IDisposable)?.Dispose(); } catch (Exception cleanup) { Debug.LogWarning(cleanup); }
            }
            try { CleanupOwnedSave(); } catch (Exception cleanup) { Debug.LogWarning(cleanup); }
            Debug.LogException(error);
            File.WriteAllText(Result, "FAIL during " + phase + ": " + error);
            EditorApplication.Exit(1);
        }
    }

    public static IEnumerator Run()
    {
        phase = "menu bootstrap";
        double deadline = EditorApplication.timeSinceStartup + 60;
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<MainMenuPresentation>() == null)
        {
            Require(EditorApplication.timeSinceStartup < deadline, "The initial menu did not become ready");
            yield return null;
        }
        Check(Application.companyName == "MythologicumValidation"
            && (Application.productName == "SceneTransitionValidation" || Application.productName == "SteampunkUIValidation"),
            "Scene/save checks use an isolated validation application identity");
        GameSettings.ResetDefaults();
        Check(SceneTransition.GameplayCanvas != null
            && SceneTransition.GameplayCanvas.gameObject.scene == SceneManager.GetActiveScene(), "The menu owns its scene canvas");
        var originalCanvas = SceneTransition.GameplayCanvas;
        var hiddenPopup = new GameObject("Inactive dropdown canvas probe", typeof(RectTransform), typeof(Canvas));
        hiddenPopup.transform.SetParent(originalCanvas.transform, false); hiddenPopup.SetActive(false);
        Check(SceneTransition.GameplayCanvas == originalCanvas, "An inactive dropdown canvas cannot become the gameplay UI parent");
        Object.DestroyImmediate(hiddenPopup);

        try
        {
            var options = new MapGenerationOptions { width = 32, depth = 32, seed = 89231, relief = 0, featureSize = 28, forestDensity = 0 };
            phase = "new park request";
            Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(options);
            Check(SceneTransition.IsLoading && SceneTransition.TargetScene == "GameScene", "New Game starts the shared asynchronous transition");
            Check(SceneManager.GetActiveScene().name == "MainMenuScene", "The loading overlay is created before replacing the menu scene");
            Check(SceneTransition.Overlay != null && SceneTransition.Overlay.Canvas.enabled && SceneTransition.Overlay.Root.activeInHierarchy,
                "The loading canvas exists before destination activation");
            Check(!SceneTransition.Load("MainMenuScene") && SceneTransition.TargetScene == "GameScene",
                "A duplicate scene request cannot replace the active destination");
            var duplicateOptions = options.Clone(); duplicateOptions.seed++;
            Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(duplicateOptions);
            Check(NewGameSetup.Pending != null && NewGameSetup.Pending.seed == options.seed,
                "A duplicate navigation callback cannot overwrite queued new-game settings");
            yield return WaitForOpaqueCover("new park", "MainMenuScene");
            Capture("NewGame-Loading");
            yield return WaitForCompletion("new park", "GameScene", true);

            var park = Object.FindFirstObjectByType<BuildModeController>();
            Check(park != null && park.IsReady && park.Personnel != null && park.Visitors != null,
                "The loading cover completes only after the actual park systems are ready");
            Check(park.MapOptions.width == 32 && park.MapOptions.depth == 32 && park.Terrain.Seed == options.seed,
                "New-game generation choices survive the asynchronous request");
            Check(!GameSaveMenu.IsOpen && Mathf.Approximately(Time.timeScale, 1), "A new park starts at normal running speed");
            CheckSceneOwnsParkUI();
            yield return null;
            CheckNoPersistentOrphans();

            // Preserve distinctive real state with a building and non-default cash, not just an empty map.
            var construction = Field<BuildingConstruction>(park, "buildingConstruction");
            var entrance = BuildingCatalogDefinition.Current.buildings.First(item => item.kind == BuildingKind.ParkEntrance).Copy();
            var grid = Field<SquareBuildGrid>(park, "grid");
            BuildingFootprint? entranceFootprint = null;
            for (int y = 1; y < grid.Depth - entrance.depth && !entranceFootprint.HasValue; y++)
                for (int x = 1; x < grid.Width - entrance.width && !entranceFootprint.HasValue; x++)
                {
                    var candidate = new BuildingFootprint(entrance, new Vector2Int(x, y), 0);
                    if (construction.CanPlace(candidate)) entranceFootprint = candidate;
                }
            Check(entranceFootprint.HasValue && construction.TryPlace(entranceFootprint.Value),
                "The save fixture contains an actual placed park entrance");
            park.Economy.Earn(37, ParkTransactionCategory.Other);
            GameSaveMenu.TogglePause();
            Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "The captured park is paused before saving");
            var saved = park.CapturePark(); park.ValidateSave(saved);
            ownedSlot = ParkSaveStore.NewManualSlotId();
            ParkSaveStore.WriteSlot(saved, ownedSlot, "Scene-transition validation park");
            var slot = ParkSaveStore.ListSlots().Single(item => item.Id == ownedSlot);
            Check(slot.IsValid && ParkSaveStore.ReadSlot(ownedSlot).buildings.Count == 1, "A readable owned save retains the fixture building");
            GameSaveMenu.TogglePause();
            SimulationSpeed.Select(3);

            phase = "return to menu";
            Object.FindFirstObjectByType<SceneNavigation>().MainMenu();
            Check(SceneTransition.IsLoading && SceneTransition.TargetScene == "MainMenuScene", "Return to menu uses the same transition coordinator");
            yield return WaitForCompletion("return to menu", "MainMenuScene", false);
            Check(Mathf.Approximately(Time.timeScale, 1) && !GameSaveMenu.IsOpen, "The main menu leaves prior park speed and pause state behind");
            yield return null;
            CheckNoPersistentOrphans();

            phase = "saved park request";
            var menu = Object.FindFirstObjectByType<MainMenuPresentation>();
            menu.MenuButtons.Single(button => button.name == Loc.InternalName("ui.menu.load_game")).onClick.Invoke();
            yield return null;
            Check(SaveBrowserPresentation.IsOpen, "Main-menu Load Game opens its real save browser");
            SaveBrowserPresentation.ActiveRoot.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Load " + ownedSlot).onClick.Invoke();
            Check(SceneTransition.IsLoading && SceneTransition.TargetScene == "GameScene" && ParkSaveStore.Pending != null,
                "Choosing a saved park queues its data before the asynchronous scene request");
            yield return WaitForOpaqueCover("saved park", "MainMenuScene");
            Capture("SavedPark-Loading");
            yield return WaitForCompletion("saved park", "GameScene", true);

            park = Object.FindFirstObjectByType<BuildModeController>();
            var restored = park.CapturePark();
            Check(restored.buildings.Count == saved.buildings.Count && restored.terrain.seed == saved.terrain.seed
                && Mathf.Approximately(restored.balance, saved.balance), "Saved construction, terrain identity and funds survive the transition");
            Check(ParkSaveStore.Pending == null && GameSaveMenu.IsOpen && Time.timeScale == 0,
                "The restored park consumes its pending save and stays paused after the cover disappears");
            GameSaveMenu.TogglePause();
            Check(!GameSaveMenu.IsOpen && Mathf.Approximately(Time.timeScale, SimulationSpeed.SelectedSpeed) && Time.timeScale > 0,
                "Resuming a restored park returns to a positive selected simulation speed");
            CheckSceneOwnsParkUI();
            yield return null;
            CheckNoPersistentOrphans();

            phase = "unknown scene rejection";
            SimulationSpeed.Select(2);
            Check(!SceneTransition.Load("MissingSceneTransitionFixture"), "An unavailable scene is rejected without starting a transition");
            yield return null;
            Check(!SceneTransition.IsLoading && !SceneTransition.HasFailed && SceneTransition.Overlay == null
                && Object.FindFirstObjectByType<BuildModeController>() == park && park.IsReady && Mathf.Approximately(Time.timeScale, 2),
                "A rejected scene request preserves the current ready park and running speed");

            phase = "preparation failure recovery";
            SceneTransition.BeginPreparation();
            SceneTransition.CompletePreparation();
            deadline = EditorApplication.timeSinceStartup + 10;
            while (SceneTransition.Overlay != null && (SceneTransition.CurrentProgress < 1 || SceneTransition.Overlay.Alpha >= .999f))
            {
                Require(SceneTransition.IsLoading && EditorApplication.timeSinceStartup < deadline, "The recovery fixture never reached fade-out");
                yield return null;
            }
            SceneTransition.FailPreparation(null);
            yield return null;
            yield return null;
            Check(SceneTransition.IsLoading && SceneTransition.HasFailed && SceneTransition.Overlay.ErrorVisible,
                "A preparation failure retains a visible recovery cover");
            Check(Time.timeScale == 0 && GameInput.BlocksGameplayInput && SceneTransition.Overlay.ReturnButton.interactable,
                "Failed preparation stays paused and offers an actionable return button");
            ParkSaveStore.Pending = saved; NewGameSetup.Pending = options.Clone();
            SceneTransition.Overlay.ReturnButton.onClick.Invoke();
            Check(ParkSaveStore.Pending == null && NewGameSetup.Pending == null && !SceneTransition.HasFailed,
                "The recovery action clears failed pending park requests");
            yield return WaitForCompletion("preparation recovery", "MainMenuScene", false);
            yield return null;
            CheckNoPersistentOrphans();

            phase = "direct game-scene entry";
            ParkSaveStore.Pending = null; NewGameSetup.Pending = null;
            // Deliberately bypass the production navigation to simulate pressing Play in GameScene.
            SceneManager.LoadScene("GameScene");
            deadline = EditorApplication.timeSinceStartup + 10;
            while (!SceneTransition.IsLoading)
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Direct GameScene entry never started preparation");
                yield return null;
            }
            Check(SceneTransition.Overlay != null && !Object.FindFirstObjectByType<BuildModeController>().IsReady,
                "Direct GameScene entry creates a preparation cover before park readiness");
            yield return WaitForCompletion("direct game-scene preparation", "GameScene", true);
            Check(Object.FindFirstObjectByType<BuildModeController>().IsReady && !GameSaveMenu.IsOpen && Mathf.Approximately(Time.timeScale, 1),
                "Direct GameScene entry finishes as a normal playable park");
            CheckSceneOwnsParkUI();
            yield return null;
            CheckNoPersistentOrphans();
        }
        finally { CleanupOwnedSave(); }
    }

    private static IEnumerator WaitForOpaqueCover(string operation, string outgoingScene)
    {
        phase = operation + " cover";
        double deadline = EditorApplication.timeSinceStartup + 10;
        while (SceneTransition.Overlay != null && SceneTransition.Overlay.Alpha < .999f)
        {
            Require(SceneTransition.IsLoading && EditorApplication.timeSinceStartup < deadline, operation + " did not show its loading cover");
            Require(SceneManager.GetActiveScene().name == outgoingScene, operation + " activated its destination before the cover was opaque");
            yield return null;
        }
        var view = SceneTransition.Overlay;
        Check(view != null && view.IsVisible && view.Group.blocksRaycasts && view.Canvas.sortingOrder >= 30000,
            operation + " shows an opaque loading cover above the scene interface");
        Check(Time.timeScale == 0 && GameInput.BlocksGameplayInput, operation + " blocks simulation and gameplay input while loading");
        var cover = view.GetComponentsInChildren<Image>(true).Single(image => image.name == "Loading cover");
        Check(cover.raycastTarget && Mathf.Approximately(cover.color.a, 1) && cover.rectTransform.anchorMin == Vector2.zero
            && cover.rectTransform.anchorMax == Vector2.one, operation + " uses a full-screen opaque pointer blocker");
    }

    private static IEnumerator WaitForCompletion(string operation, string expectedScene, bool expectParkPreparation)
    {
        phase = operation + " completion";
        double deadline = EditorApplication.timeSinceStartup + Math.Max(240, LoadingScreenDefinition.Current.preparationTimeoutSeconds + 10);
        bool observedBusy = false, observedPreparation = false;
        float previousProgress = -1;
        while (SceneTransition.IsLoading)
        {
            observedBusy = true;
            Require(!SceneTransition.HasFailed, operation + " entered the failure recovery state");
            Require(EditorApplication.timeSinceStartup < deadline, operation + " never completed");
            Require(SceneTransition.Overlay != null && SceneTransition.Overlay.Canvas.enabled
                && SceneTransition.Overlay.Group.blocksRaycasts, operation + " lost its loading input cover");
            Require(GameInput.BlocksGameplayInput && Time.timeScale == 0, operation + " allowed gameplay or simulation during loading");
            float progress = SceneTransition.CurrentProgress;
            Require(!float.IsNaN(progress) && progress >= 0 && progress <= 1 && progress + .0001f >= previousProgress,
                operation + " reported invalid or decreasing progress");
            previousProgress = progress;
            Require(Mathf.Approximately(SceneTransition.Overlay.ProgressImage.fillAmount, SceneTransition.Overlay.Progress),
                operation + " progress artwork disagrees with the reported phase progress");
            var controller = Object.FindFirstObjectByType<BuildModeController>();
            if (controller != null && !controller.IsReady) observedPreparation = true;
            yield return null;
        }
        Check(observedBusy && SceneManager.GetActiveScene().name == expectedScene && SceneTransition.Overlay == null,
            operation + " finishes in the intended scene and removes its cover");
        Check(!expectParkPreparation || observedPreparation, operation + " retains the cover during park preparation");
        if (expectedScene == "GameScene")
            Check(Object.FindFirstObjectByType<BuildModeController>()?.IsReady == true, operation + " ends only with a ready park");
    }

    private static void CheckSceneOwnsParkUI()
    {
        var canvas = SceneTransition.GameplayCanvas;
        Check(canvas != null && canvas.gameObject.scene == SceneManager.GetActiveScene(),
            "The game interface uses its active scene canvas");
        // ConstructionPalette is a presentation object, so inspect its actual named RectTransform.
        var palette = canvas.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Construction Palette");
        Check(palette.gameObject.scene == SceneManager.GetActiveScene() && palette.GetComponentInParent<LoadingScreenPresentation>() == null,
            "Park controls are not parented under the persistent loading screen");
        foreach (var name in new[] { "Building", "Park Management", "Encyclopedia", "Letters", "Pause" })
        {
            var button = canvas.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            var symbol = button.GetComponent<UIIcon>();
            Check(button.isActiveAndEnabled && symbol != null && symbol.IconOnly && symbol.isActiveAndEnabled,
                "The " + name + " dock control remains an active standalone icon after loading");
            Check(!button.GetComponentsInChildren<Text>(true).Any(text => text.isActiveAndEnabled),
                "The " + name + " dock control has no permanent text after loading");
            var image = symbol.IconImage;
            var artwork = UITheme.Current.Icon(symbol.Kind);
            Check(image != null && image.isActiveAndEnabled && image.color.a > .9f
                && new[] { artwork.normal, artwork.highlighted, artwork.pressed, artwork.disabled }.Contains(image.overrideSprite),
                "The " + name + " dock actually renders its icon artwork after loading; sprite="
                + image?.sprite?.name + ", rendered=" + image?.overrideSprite?.name);
            Check(button.GetComponent<Image>().color.a < .01f && button.transition == Selectable.Transition.None,
                "The " + name + " dock control has no rectangular button backing after loading");
        }
    }
    private static void CheckNoPersistentOrphans()
    {
        Check(Object.FindObjectsByType<SceneTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0
            && Object.FindObjectsByType<LoadingScreenPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0,
            "Completed transitions leave no persistent coordinator or loading-screen objects");
    }
    private static void Capture(string name)
    {
        Directory.CreateDirectory(Captures);
        // Batch editors do not reliably run ScreenCapture's end-of-frame export.
        // Render the real loading canvas through URP synchronously instead.
        var view = SceneTransition.Overlay;
        var canvas = view.Canvas;
        var camera = Object.FindFirstObjectByType<Camera>();
        Require(camera != null, "A scene camera is available for the loading capture");
        var oldTarget = camera.targetTexture; var oldMode = canvas.renderMode;
        var oldCamera = canvas.worldCamera; var oldDistance = canvas.planeDistance;
        var oldActive = RenderTexture.active;
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); target.Create();
        Texture2D pixels = null;
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
            typeof(LoadingScreenPresentation).GetMethod("RefreshLayout", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Captures, name + ".png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = oldTarget; canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            RenderTexture.active = oldActive; target.Release(); Object.DestroyImmediate(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            Canvas.ForceUpdateCanvases();
        }
    }
    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Check(bool condition, string message)
    { checks++; Require(condition, message); Debug.Log("SCENE TRANSITION PASS: " + message); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void CleanupOwnedSave()
    {
        if (ownedSlot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, ownedSlot));
        Require(SaveSlotPaths.IsManual(ownedSlot) && path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "Owned test save cleanup escaped its isolated application directory");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        ownedSlot = null;
    }
}

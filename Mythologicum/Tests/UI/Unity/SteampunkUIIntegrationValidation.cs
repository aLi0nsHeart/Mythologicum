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
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runs only in the disposable validation project, with a real graphics device.</summary>
[InitializeOnLoad]
public static class SteampunkUIIntegrationValidation
{
    const string Running = "SteampunkUIValidation.running";
    static bool pending;
    static int stage, checks, captures;
    static double readyAt, deadline;
    static ParkSaveData saved;
    static IEnumerator steps;
    static string Result => Path.Combine(Directory.GetCurrentDirectory(), "SteampunkUIValidationResults.txt");
    static string Captures => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "SteampunkUIValidation");

    static SteampunkUIIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); pending = true; WaitForScene();
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "SteampunkUIValidation";
        Directory.CreateDirectory(Captures);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }
    static void WaitForScene(double seconds = 1)
    { readyAt = EditorApplication.timeSinceStartup + seconds; deadline = readyAt + 120; }
    static void Tick()
    {
        if (!pending || !Application.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out in UI validation stage " + stage);
            if (stage == 0) PrepareIsolatedSave();
            else if (stage == 1 || stage == 2)
            {
                if (steps == null) steps = stage == 1 ? ValidateMenuWithoutSave() : ValidateGame();
                if (steps.MoveNext()) readyAt = EditorApplication.timeSinceStartup + .25;
                else steps = null;
            }
            else if (stage == 3) ValidateMenuWithSave();
            else ValidateLoadedGame();
        }
        catch (Exception error)
        {
            pending = false; Debug.LogException(error);
            File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1);
        }
    }
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); Debug.Log("STEAMPUNK UI PASS: " + message); }
    static bool Near(float a, float b, float tolerance = .005f) => Mathf.Abs(a - b) <= tolerance;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    static Button FindButton(Transform owner, string name) => owner.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
    static Canvas CurrentCanvas => UnityEngine.Object.FindFirstObjectByType<Canvas>();
    static Vector2Int Cell(int x, int y) => new Vector2Int(x, y);
    static Camera CurrentCamera => UnityEngine.Object.FindFirstObjectByType<Camera>();

    static void PrepareIsolatedSave()
    {
        if (SceneManager.GetActiveScene().name != "MainMenuScene" || GameObject.Find("Main menu") == null) return;
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "SteampunkUIValidation",
            "Save and settings checks use their own validation application identity");
        Check(ParkSaveStore.SavePath.Contains("MythologicumValidation") && ParkSaveStore.SavePath.Contains("SteampunkUIValidation"),
            "The test save belongs to the disposable validation identity");
        if (File.Exists(ParkSaveStore.SavePath)) File.Delete(ParkSaveStore.SavePath);
        stage = 1; WaitForScene(); SceneManager.LoadScene("MainMenuScene");
    }

    static IEnumerator ValidateMenuWithoutSave()
    {
        if (SceneManager.GetActiveScene().name != "MainMenuScene") yield break;
        var menu = GameObject.Find("Main menu"); if (menu == null) yield break;
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "UI validation uses a real graphics device");
        ValidateThemeAssets(); ValidateMenuActions(menu);
        Check(!FindButton(menu.transform, "Load Game").interactable, "Load Game is disabled when the save slot is empty");
        Capture("MainMenu-NoSave", menu, 1280, 720);
        FindButton(menu.transform, "Settings").onClick.Invoke();
        yield return null;
        Check(SettingsPresentation.IsOpen, "Main-menu Settings opens a working settings window");
        var settings = GameObject.Find("Settings window");
        var volume = settings.transform.Find("Master volume")?.GetComponent<Slider>()
            ?? settings.GetComponentsInChildren<Slider>(true).Single(slider => slider.name == "Master volume");
        volume.value = .35f;
        Check(Near(AudioListener.volume, .35f) && Near(GameSettings.MasterVolume, .35f), "Master volume changes actual game audio immediately");
        foreach (var item in new[] { new KeyValuePair<string, float>("Camera movement", 1.4f),
            new KeyValuePair<string, float>("Camera rotation", 1.6f), new KeyValuePair<string, float>("Camera zoom", 1.8f) })
            settings.GetComponentsInChildren<Slider>(true).Single(slider => slider.name == item.Key).value = item.Value;
        Check(Near(GameSettings.MoveSensitivity, 1.4f) && Near(GameSettings.RotationSensitivity, 1.6f)
            && Near(GameSettings.ZoomSensitivity, 1.8f), "All camera sensitivity controls update real game settings");
        var quality = settings.GetComponentsInChildren<Dropdown>(true).Single(dropdown => dropdown.name == "Quality");
        Check(quality.options.Select(option => option.text).SequenceEqual(QualitySettings.names), "Quality choices come from the project's actual quality levels");
        int oldQuality = QualitySettings.GetQualityLevel();
        int nextQuality = (oldQuality + 1) % Mathf.Max(1, QualitySettings.names.Length);
        quality.value = nextQuality;
        Check(QualitySettings.GetQualityLevel() == nextQuality, "Quality dropdown changes the active render quality");
        quality.value = oldQuality;
        var mode = settings.GetComponentsInChildren<Dropdown>(true).Single(dropdown => dropdown.name == "Window mode");
        Check(mode.options.Count >= 2, "Display settings offer fullscreen and windowed modes");
        Check(!string.IsNullOrWhiteSpace(mode.captionText.text) && mode.captionText.text == mode.options[mode.value].text,
            "The window-mode dropdown displays its currently selected option");
        Check(!string.IsNullOrWhiteSpace(quality.captionText.text) && quality.captionText.text == quality.options[quality.value].text,
            "The quality dropdown displays its currently selected option");
        Capture("Settings", settings, 1280, 720);
        mode.Show(); yield return null;
        var dropdownList = GameObject.Find("Dropdown List");
        Check(dropdownList != null, "The display dropdown creates its real option popup");
        Capture("Settings-DisplayOptions", dropdownList, 1280, 720);
        var options = settings.GetComponentsInChildren<Toggle>().Where(toggle => toggle.gameObject.activeInHierarchy).ToArray();
        Check(options.Length == mode.options.Count, "The display dropdown opens all real window-mode choices");
        mode.Hide(); yield return null;
        FindButton(settings.transform, "Back").onClick.Invoke();
        Check(!SettingsPresentation.IsOpen && menu.activeInHierarchy, "Back returns from Settings to the main menu");
        Check(PlayerPrefs.HasKey("Mythologicum.Settings.Volume") && Near(PlayerPrefs.GetFloat("Mythologicum.Settings.Volume"), .35f)
            && Near(PlayerPrefs.GetFloat("Mythologicum.Settings.CameraMove"), 1.4f), "Settings record audio and camera preferences for later sessions");
        FindButton(menu.transform, "Settings").onClick.Invoke();
        yield return null;
        settings = GameObject.Find("Settings window");
        Check(Near(settings.GetComponentsInChildren<Slider>(true).Single(slider => slider.name == "Master volume").value, .35f),
            "Settings retain their chosen values when reopened");
        FindButton(settings.transform, "Reset defaults").onClick.Invoke();
        Check(Near(GameSettings.MasterVolume, 1f) && Near(GameSettings.MoveSensitivity, 1f)
            && Near(GameSettings.RotationSensitivity, 1f) && Near(GameSettings.ZoomSensitivity, 1f), "Reset defaults restores audio and camera controls");
        FindButton(settings.transform, "Back").onClick.Invoke();
        FindButton(menu.transform, "New Game").onClick.Invoke();
        yield return null;
        Check(NewGameSetup.IsOpen && SceneManager.GetActiveScene().name == "MainMenuScene", "New Game opens map setup before entering a park");
        var setup = GameObject.Find("New park setup");
        Check(setup.GetComponentInChildren<RawImage>(true)?.texture != null, "The themed new-game window retains its generated map preview");
        Capture("NewGameSetup", setup, 1280, 720);
        FindButton(setup.transform, "Cancel").onClick.Invoke();
        Check(!NewGameSetup.IsOpen && menu.activeInHierarchy, "Cancelling setup returns to the main menu");
        FindButton(menu.transform, "New Game").onClick.Invoke();
        yield return null;
        setup = GameObject.Find("New park setup");
        FindButton(setup.transform, "Create park").onClick.Invoke();
        stage = 2; WaitForScene(2);
    }

    static void ValidateThemeAssets()
    {
        var theme = UITheme.Current;
        Check(theme != null && AssetDatabase.GetAssetPath(theme).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
            "The shared steampunk theme lives in the project's UI folder");
        foreach (var sprite in new[] { theme.windowFrame, theme.panelFrame, theme.buttonNormal, theme.buttonHover,
            theme.buttonPressed, theme.buttonDisabled, theme.inputField, theme.sliderTrack, theme.sliderFill,
            theme.sliderHandle, theme.scrollbarTrack, theme.scrollbarHandle })
            Check(sprite != null && AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                "A required theme role resolves a retained steampunk sprite: " + (sprite != null ? sprite.name : "unassigned"));
        foreach (var sprite in new[] { theme.checkmark, theme.envelope, theme.closeIcon })
            if (sprite != null)
                Check(AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                    "An optional theme icon resolves retained artwork: " + sprite.name);
        foreach (Font font in new[] { theme.bodyFont, theme.headingFont, theme.titleFont })
            Check(font != null, "Theme fonts are available for legacy UGUI text");
        Check(theme.bodyFont != null && AssetDatabase.GetAssetPath(theme.bodyFont).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
            "Body text uses a retained UI font");
        Check(theme.background != null && AssetDatabase.GetAssetPath(theme.background).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
            "The theme background resolves retained UI artwork");
        var forbidden = Directory.GetDirectories("Assets", "*", SearchOption.AllDirectories).Where(path =>
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Documentation/")) return false;
            string name = Path.GetFileName(path);
            return name.Equals("Third Party", StringComparison.OrdinalIgnoreCase) || name.Equals("ThirdParty", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Gentleland", StringComparison.OrdinalIgnoreCase);
        }).ToArray();
        Check(forbidden.Length == 0, "No forbidden vendor or Third Party folders remain outside documentation: " + string.Join(", ", forbidden));
    }
    static void ValidateMenuActions(GameObject menu)
    {
        var buttons = menu.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy).ToArray();
        Check(buttons.Length == 4 && buttons.Select(button => button.name).OrderBy(name => name).SequenceEqual(
            new[] { "New Game", "Load Game", "Settings", "Quit" }.OrderBy(name => name)), "Main menu exposes exactly the four requested actions");
        Check(FindButton(menu.transform, "New Game").interactable && FindButton(menu.transform, "Settings").interactable
            && FindButton(menu.transform, "Quit").interactable, "New Game, Settings and Quit remain available");
    }

    static BuildingDefinition Definition(BuildingKind kind)
    {
        var definition = JsonUtility.FromJson<BuildingDefinition>(JsonUtility.ToJson(BuildingDefinition.Defaults().Single(item => item.kind == kind)));
        definition.width = definition.depth = 2; definition.height = 2; definition.entranceColumn = 1;
        definition.visualPrefab = null; return definition;
    }
    static IEnumerator ValidateGame()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
        if (controller?.Terrain == null || controller.Personnel == null) yield break;
        Check(SceneManager.GetActiveScene().name == "GameScene", "Create park enters the actual game scene");
        controller.enabled = false; controller.GetComponent<IsometricCameraController>().enabled = false; Time.timeScale = 0;
        var canvas = CurrentCanvas; var grid = Field<SquareBuildGrid>(controller, "grid");
        var building = Field<BuildingConstruction>(controller, "buildingConstruction");
        var paths = Field<PathConstruction>(controller, "paths"); var fences = Field<FenceConstruction>(controller, "fences");
        var creatures = Field<CreatureWorld>(controller, "creatureWorld");
        Check(grid.Width >= 45 && grid.Depth >= 40, "Default new-game map retains room for the UI interaction fixture");
        foreach (string name in new[] { "Enrichment trees", "Natural rocks" })
            foreach (var target in GameObject.Find(name).GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        GameObject.Find("Prototype Landmarks")?.SetActive(false);
        controller.Terrain.ImportHeights(new float[(grid.Width + 1) * (grid.Depth + 1)]); Physics.SyncTransforms();
        var records = new Dictionary<BuildingKind, ParkBuildingRecord>();
        var placements = new[] { new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.ParkEntrance, Cell(5, 7)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.ResearchCentre, Cell(13, 7)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.ExpeditionCentre, Cell(21, 7)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.MercenaryHQ, Cell(29, 7)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.ZookeeperHut, Cell(37, 7)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.Kiosk, Cell(13, 15)),
            new KeyValuePair<BuildingKind, Vector2Int>(BuildingKind.RestArea, Cell(21, 15)) };
        foreach (var item in placements)
        {
            Check(building.TryPlace(new BuildingFootprint(Definition(item.Key), item.Value, 0)), "UI fixture places " + item.Key);
            records.Add(item.Key, building.Network.Records.Last());
        }
        for (int x = 6; x <= 43; x++) { paths.TryPlace(Cell(x, 6)); paths.TryPlace(Cell(x, 14)); }
        // The entrance itself occupies x=5..6/z=7..8, so connect the two service roads at x=10.
        for (int z = 6; z <= 14; z++) if (grid.ContentAt(Cell(10, z)) == BuildCellContent.Empty)
            Check(paths.TryPlace(Cell(10, z)), "The UI fixture connects its service roads at " + Cell(10, z));
        building.Network.Refresh(); Check(records.Values.All(record => record.IsOpen), "All UI fixture buildings have real entrance-connected paths: "
            + string.Join(", ", records.Select(item => item.Key + "=" + item.Value.IsOpen + " at " + item.Value.Footprint.AccessCell)));
        Check(controller.Personnel.TryHire(records[BuildingKind.ResearchCentre]), "Research UI has an actual hired researcher");
        Check(creatures.TrySpawn(Resources.Load<CreatureDefinition>("Creatures/Chupacabra"), Cell(19, 18)), "Creature inspector has a living mythological creature");
        var animal = creatures.Agents.Last();
        var guest = controller.Visitors.TrySpawn(); Check(guest != null, "Visitor HUD and inspector retain working visitor admission");
        controller.Visitors.RefreshReputation(); controller.Economy.Advance(10); BuildingEconomy.Tick(building.Network, controller.Economy, 10);
        controller.Economy.Earn(25, ParkTransactionCategory.Food);
        var palette = Field<ConstructionPalette>(controller, "palette"); palette.Refresh(true);
        Field<VisitorPresentation>(controller, "visitorPresentation").Refresh();
        yield return null;
        Capture("InGame-HUD", canvas.gameObject, 1280, 720);
        palette.Toggle(ConstructionCategory.Buildings); yield return null;
        Capture("Construction-Buildings", palette.Root, 1280, 720); palette.Close();
        palette.Toggle(ConstructionCategory.Enclosures); yield return null;
        Capture("Construction-Enclosures", palette.Root, 1280, 720); palette.Close();
        var buildingInfo = Field<BuildingInspection>(controller, "buildingInspection");
        buildingInfo.SetInspecting(true); buildingInfo.Select(records[BuildingKind.ResearchCentre]);
        yield return null;
        Capture("Building-Inspector", Field<GameObject>(buildingInfo, "panel"), 1280, 720);
        buildingInfo.Select(null);
        var creatureInfo = Field<CreatureInspection>(controller, "creatureInspection");
        creatureInfo.SetInspecting(true); creatureInfo.Select(animal);
        yield return null;
        Capture("Creature-Inspector", Field<GameObject>(creatureInfo, "panel"), 1280, 720);
        creatureInfo.Select(null);
        var visitorInfo = Field<VisitorPresentation>(controller, "visitorPresentation"); visitorInfo.SetInspecting(true); visitorInfo.Select(guest);
        yield return null;
        Capture("Visitor-Inspector", Field<GameObject>(visitorInfo, "panel"), 1280, 720); visitorInfo.Select(null);

        for (int x = 18; x < 22; x++) { fences.TryPlace(new FenceEdge(Cell(x, 17), true)); fences.TryPlace(new FenceEdge(Cell(x, 21), true)); }
        for (int z = 17; z < 21; z++) { fences.TryPlace(new FenceEdge(Cell(18, z), false)); fences.TryPlace(new FenceEdge(Cell(22, z), false)); }
        var enclosures = Field<EnclosureMap>(controller, "enclosureMap"); enclosures.Refresh(building.Network.IsPathConnected);
        var enclosureInfo = Field<EnclosurePresentation>(controller, "enclosurePresentation"); enclosureInfo.SetInspecting(true); enclosureInfo.Select(Cell(19, 18)); enclosureInfo.Refresh();
        Check(Field<GameObject>(enclosureInfo, "panel").activeSelf, "Enclosure information remains selectable after restyling");
        yield return null;
        Capture("Enclosure-Inspector", Field<GameObject>(enclosureInfo, "panel"), 1280, 720); enclosureInfo.Select(null); enclosureInfo.Refresh();
        controller.Progression.Inbox.Post("ui-test-fence", ParkNoticeKind.FenceOpened, "Fence opened", "The brass notification frame preserves readable event text.", Cell(19, 18));
        var notifications = Field<NotificationPresentation>(controller, "notifications"); notifications.Toggle();
        yield return null;
        Capture("Notifications", Field<GameObject>(notifications, "panel"), 1280, 720); notifications.Close();
        Invoke(controller, "OpenExpeditions", records[BuildingKind.ExpeditionCentre]);
        yield return null;
        var progression = Field<ProgressionPresentation>(controller, "management");
        Capture("Expeditions", Field<GameObject>(progression, "map"), 1280, 720);
        FindButton(Field<GameObject>(progression, "map").transform, "Buy team").onClick.Invoke();
        Check(controller.Progression.Teams == 1, "Restyled expedition controls preserve their purchase action"); progression.Close();
        Invoke(controller, "OpenTechnologies", records[BuildingKind.ResearchCentre]);
        yield return null;
        Capture("Technology-Research", Field<GameObject>(progression, "research"), 1280, 720); progression.Close();
        Invoke(controller, "OpenParkManagement");
        yield return null;
        var management = Field<ParkManagementPresentation>(controller, "parkManagement"); var managementRoot = Field<GameObject>(management, "root");
        foreach (string tab in new[] { "Statistics", "Finances", "Prices & budget", "Star rating" })
        {
            FindButton(managementRoot.transform, tab).onClick.Invoke(); yield return null;
            Capture("Management-" + tab.Replace(" & ", "-").Replace(' ', '-'), managementRoot, 1280, 720);
        }
        Capture("Management-Stars-1920", managementRoot, 1920, 1080); management.Close();
        Invoke(controller, "SelectTerrainBrush", TerrainBrushMode.Raise);
        yield return null;
        var terrain = Field<TerrainEditingPresentation>(controller, "terrainEditing");
        Capture("Terrain-Tools", Field<GameObject>(terrain, "root"), 1280, 720);
        FindButton(Field<GameObject>(terrain, "root").transform, "Radius +").onClick.Invoke();
        Check(terrain.Radius == 4, "Restyled terrain controls preserve their radius action");
        FindButton(Field<GameObject>(terrain, "root").transform, "Done").onClick.Invoke();
        var pause = controller.GetComponent<GameSaveMenu>(); Time.timeScale = 1; Invoke(pause, "Toggle");
        yield return null;
        Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "Pause still freezes the simulation after restyling");
        var overlay = Field<GameObject>(pause, "overlay"); Capture("Pause", overlay, 1280, 720);
        FindButton(overlay.transform, "Quit to main menu").onClick.Invoke();
        yield return null;
        var confirm = GameObject.Find("Confirm action"); Check(confirm != null, "Quit-to-menu retains its confirmation dialog");
        Capture("Pause-Confirmation", confirm, 1280, 720); FindButton(confirm.transform, "Cancel").onClick.Invoke();
        Check(SceneManager.GetActiveScene().name == "GameScene" && GameSaveMenu.IsOpen, "Cancelling quit preserves the current paused park");
        if (overlay.GetComponentsInChildren<Button>(true).Any(button => button.name == "Settings"))
        {
            FindButton(overlay.transform, "Settings").onClick.Invoke(); Check(SettingsPresentation.IsOpen, "Settings are available from the in-game pause menu");
            yield return null;
            Capture("Pause-Settings", GameObject.Find("Settings window"), 1280, 720);
            FindButton(GameObject.Find("Settings window").transform, "Back").onClick.Invoke();
            Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "Returning from settings preserves the pause state");
        }
        FindButton(overlay.transform, "Save game").onClick.Invoke();
        Check(File.Exists(ParkSaveStore.SavePath), "The restyled Save game action writes the actual save slot");
        saved = ParkSaveStore.Read(); controller.ValidateSave(saved);
        stage = 3; WaitForScene(); UnityEngine.Object.FindFirstObjectByType<SceneNavigation>().MainMenu();
    }

    static void ValidateMenuWithSave()
    {
        if (SceneManager.GetActiveScene().name != "MainMenuScene") return;
        var menu = GameObject.Find("Main menu"); if (menu == null) return;
        ValidateMenuActions(menu);
        Check(FindButton(menu.transform, "Load Game").interactable, "Load Game becomes available when a save exists");
        Capture("MainMenu-WithSave", menu, 1920, 1080);
        FindButton(menu.transform, "Load Game").onClick.Invoke(); stage = 4; WaitForScene(2);
    }
    static void ValidateLoadedGame()
    {
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>(); if (controller?.Terrain == null || controller.Personnel == null) return;
        controller.enabled = false;
        Check(SceneManager.GetActiveScene().name == "GameScene" && GameSaveMenu.IsOpen && Time.timeScale == 0,
            "Main-menu Load Game restores the park and opens its pause menu");
        var actual = controller.CapturePark();
        Check(actual.buildings.Count == saved.buildings.Count && actual.creatures.Count == saved.creatures.Count
            && actual.visitors.Count == saved.visitors.Count && actual.staff.Count == saved.staff.Count, "The themed menu preserves the saved park population and buildings");
        Check(Near(actual.balance, saved.balance) && Near(actual.income, saved.income) && Near(actual.expenses, saved.expenses),
            "UI replacement does not charge or reset saved money");
        Check(actual.reputation.reviewedVisitors == saved.reputation.reviewedVisitors && actual.reputation.fatalities == saved.reputation.fatalities,
            "UI replacement preserves saved park reputation history");
        Capture("Loaded-Park-Pause", Field<GameObject>(controller.GetComponent<GameSaveMenu>(), "overlay"), 1280, 720);
        File.WriteAllText(Result, "PASS: " + checks + " steampunk asset, menu, settings, UI layout, GPU, interaction and save/load checks.\nCaptures: " + captures + " in " + Captures);
        pending = false; EditorApplication.Exit(0);
    }

    static void Capture(string name, GameObject focus, int width, int height)
    {
        var canvas = CurrentCanvas; var camera = CurrentCamera;
        UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
        Check(focus != null && focus.activeInHierarchy, "The " + name + " interface is visible");
        // Pointer input must use the production canvas mode and the real Game view.
        // The temporary render-target camera below exists only to export larger screenshots.
        var inputCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        ValidateVectorGraphics(canvas, name);
        foreach (var selectable in focus.GetComponentsInChildren<Selectable>())
            CheckClickable(selectable, inputCamera, Screen.width, Screen.height, name);
        var oldTarget = camera.targetTexture; var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera;
        var oldDistance = canvas.planeDistance; var oldActive = RenderTexture.active;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
            // New dialogs can be created and captured within this same editor callback.
            // Populate CanvasRenderer absolute depths before asking GraphicRaycaster to sort them.
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            foreach (var button in focus.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>() == null) CheckOnScreen((RectTransform)button.transform, camera, width, height, name + "/" + button.name);
                var graphic = button.targetGraphic as Image;
                Check(graphic != null && graphic.sprite != null && AssetDatabase.GetAssetPath(graphic.sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                    "The " + name + " button uses integrated artwork: " + button.name);
                foreach (var label in button.GetComponentsInChildren<Text>()) Check(label.font != null, "A themed button retains readable text: " + button.name);
            }
            foreach (var selectable in focus.GetComponentsInChildren<Selectable>())
            {
                if (selectable is Button) continue;
                var graphic = selectable.targetGraphic as Image;
                Check(graphic != null && graphic.sprite != null && AssetDatabase.GetAssetPath(graphic.sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                    "The " + name + " input control uses integrated artwork: " + selectable.name);
            }
            foreach (var text in focus.GetComponentsInChildren<Text>())
            {
                if (text.GetComponentInParent<ScrollRect>() != null || text.rectTransform.rect.height < 80 || text.GetComponentInParent<InputField>() != null) continue;
                Check(text.preferredHeight <= text.rectTransform.rect.height + 8, "The " + name + " text fits vertically: " + text.name
                    + "; preferred " + text.preferredHeight + "; available " + text.rectTransform.rect.height
                    + "; font " + text.font.name + " size " + text.fontSize + "; lines " + text.cachedTextGenerator.lineCount
                    + "; source lines " + (text.text.Count(value => value == '\n') + 1));
            }
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            var colors = image.GetPixels(); double mean = colors.Average(color => (double)color.grayscale);
            Check(mean > .015 && mean < .98, "The " + name + " GPU capture is nonblank");
            Check(colors.Count(color => color.r > .9f && color.b > .9f && color.g < .1f) < colors.Length / 100,
                "The " + name + " capture has no significant missing-shader magenta");
            File.WriteAllBytes(Path.Combine(Captures, name + ".png"), image.EncodeToPNG()); captures++;
        }
        catch
        {
            RenderTexture.active = target;
            if (image == null)
            {
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            }
            File.WriteAllBytes(Path.Combine(Captures, name + "-Failure.png"), image.EncodeToPNG());
            throw;
        }
        finally
        {
            camera.targetTexture = oldTarget; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            RenderTexture.active = oldActive; if (image != null) UnityEngine.Object.DestroyImmediate(image);
            target.Release(); UnityEngine.Object.DestroyImmediate(target); Canvas.ForceUpdateCanvases();
        }
    }
    static void CheckOnScreen(RectTransform rect, Camera camera, int width, int height, string name)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            var pixel = RectTransformUtility.WorldToScreenPoint(camera, corner);
            Check(pixel.x >= -3 && pixel.x <= width + 3 && pixel.y >= -3 && pixel.y <= height + 3,
                "The " + name + " control stays inside " + width + "x" + height);
        }
    }
    static void ValidateVectorGraphics(Canvas canvas, string page)
    {
        foreach (var graphic in canvas.GetComponentsInChildren<Graphic>())
        {
            if (!(graphic is EnvelopeGraphic) && !(graphic is ExpeditionMapGraphic) && !(graphic is StarRatingGraphic)) continue;
            // Do not use Graphic.canvasRenderer until after this check: its getter silently adds a missing renderer.
            var renderer = graphic.GetComponent<CanvasRenderer>();
            Check(renderer != null, "The " + page + " vector graphic has its own renderer: " + HierarchyPath(graphic.transform));
            var mesh = renderer.GetMesh();
            var parentGraphic = graphic.transform.parent.GetComponent<Graphic>();
            var parentRenderer = parentGraphic != null ? parentGraphic.GetComponent<CanvasRenderer>() : null;
            string detail = HierarchyPath(graphic.transform) + "; depth " + renderer.absoluteDepth + "; cull " + renderer.cull
                + "; verts " + (mesh != null ? mesh.vertexCount : 0) + "; materials " + renderer.materialCount
                + "; texture " + (graphic.mainTexture != null ? graphic.mainTexture.name : "none")
                + "; tint " + graphic.color + "; renderer tint " + renderer.GetColor()
                + "; shader " + (graphic.materialForRendering != null ? graphic.materialForRendering.shader.name : "none")
                + "; parent depth " + (parentRenderer != null ? parentRenderer.absoluteDepth.ToString() : "none");
            Debug.Log("STEAMPUNK VECTOR DIAGNOSTIC " + page + ": " + detail);
            Check(mesh != null && mesh.vertexCount > 0 && renderer.materialCount > 0 && !renderer.cull,
                "The " + page + " vector graphic has visible geometry: " + detail);
            Check(parentRenderer == null || renderer.absoluteDepth > parentRenderer.absoluteDepth,
                "The " + page + " vector graphic draws above its panel: " + detail);
        }
    }
    static void CheckClickable(Selectable button, Camera camera, int width, int height, string page)
    {
        if (!button.interactable || !button.gameObject.activeInHierarchy) return;
        var rect = (RectTransform)button.transform;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        if (point.x < 0 || point.x > width || point.y < 0 || point.y > height) return;
        var scroll = button.GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.viewport != null && !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, point, camera)) return;
        Check(EventSystem.current != null, "The " + page + " interface retains its EventSystem");
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
        var hit = hits.FirstOrDefault(result => result.module is GraphicRaycaster);
        var hitGraphic = hit.gameObject != null ? hit.gameObject.GetComponent<Graphic>() : null;
        var targetGraphic = button.targetGraphic;
        bool contains = RectTransformUtility.RectangleContainsScreenPoint(rect, point, camera);
        bool acceptsRaycast = targetGraphic != null && targetGraphic.Raycast(point, camera);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, camera, out Vector2 projectedLocal);
        Check(hit.gameObject != null && hit.gameObject.GetComponentInParent<Selectable>() == button,
            "Artwork does not intercept clicks on " + page + "/" + button.name
            + "; target " + HierarchyPath(button.transform) + " depth " + (button.targetGraphic != null ? button.targetGraphic.depth.ToString() : "none")
            + "; hit " + (hit.gameObject != null ? HierarchyPath(hit.gameObject.transform) : "none")
            + " depth " + (hitGraphic != null ? hitGraphic.depth.ToString() : "none")
            + "; point " + point + "; contains " + contains + "; acceptsRaycast " + acceptsRaycast
            + "; local from ray " + projectedLocal + "; local center " + rect.rect.center + "; rect " + rect.rect
            + "; camera " + (camera != null ? camera.pixelRect + " aspect " + camera.aspect : "production overlay")
            + "; screen " + Screen.width + "x" + Screen.height
            + "; target cull " + (targetGraphic != null ? targetGraphic.canvasRenderer.cull.ToString() : "none")
            + "; raycastTarget " + (targetGraphic != null ? targetGraphic.raycastTarget.ToString() : "none")
            + "; hits " + string.Join(" | ", hits.Select(result => HierarchyPath(result.gameObject.transform) + " depth " + result.depth)));
    }
    static string HierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }
}

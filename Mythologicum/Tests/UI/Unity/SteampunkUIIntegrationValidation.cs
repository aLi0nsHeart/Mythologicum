using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
        ValidateCursor(true);
        ValidateButtonIcon(FindButton(menu.transform, "New Game"), UIIconKind.Play, true);
        ValidateButtonIcon(FindButton(menu.transform, "Settings"), UIIconKind.Settings);
        ValidateButtonIcon(FindButton(menu.transform, "Quit"), UIIconKind.Quit);
        Check(!FindButton(menu.transform, "Load Game").interactable, "Load Game is disabled when the save slot is empty");
        Capture("MainMenu-NoSave", menu, 1280, 720);
        FindButton(menu.transform, "Settings").onClick.Invoke();
        yield return null;
        Check(SettingsPresentation.IsOpen, "Main-menu Settings opens a working settings window");
        var settings = GameObject.Find("Settings window");
        ValidateMovementInstructions(settings);
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
        Check(theme.verticalInfoFrame != null && theme.verticalInfoFrame.name == "Steampunk_UI_Frames_1_2",
            "Vertical information frames use the designer's exact selected artwork");
        Check(theme.horizontalInfoFrame != null && theme.horizontalInfoFrame.name == "Steampunk_UI_Dialogue_2_0",
            "Horizontal information frames use the designer's exact selected artwork");
        Check(theme.expeditionMapBackground != null && theme.expeditionMapBackground.name == "Steampunk_UI_Dialogue_1_1",
            "The expedition map retains the designer's exact parchment background");
        foreach (Font font in new[] { theme.bodyFont, theme.headingFont, theme.titleFont })
            Check(font != null, "Theme fonts are available for legacy UGUI text");
        Check(theme.bodyFont != null && AssetDatabase.GetAssetPath(theme.bodyFont).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
            "Body text uses a retained UI font");
        Check(theme.background != null && AssetDatabase.GetAssetPath(theme.background).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
            "The theme background resolves retained UI artwork");
        Check(theme.envelope != null && theme.envelope.name == "Steampunk_UI_gameplay_icons_4_12",
            "Read mail uses the exact parchment-and-quill icon requested by the designer");
        Check(theme.envelopeUnread != null && theme.envelopeUnread.name == "Steampunk_UI_gameplay_icons_4_14",
            "Unread mail uses the exact highlighted notification icon requested by the designer");
        Check(theme.Icon(UIIconKind.Building)?.normal != null
            && theme.Icon(UIIconKind.Building).normal.name == "Steampunk_UI_gameplay_icons_4_5",
            "Building uses the exact selected steampunk scroll icon");
        foreach (var kind in new[] { UIIconKind.Building, UIIconKind.ParkManagement, UIIconKind.Encyclopedia,
            UIIconKind.Pause, UIIconKind.Play, UIIconKind.Settings, UIIconKind.Quit, UIIconKind.Funds })
        {
            var set = theme.Icon(kind);
            Check(set != null && set.normal != null && set.highlighted != null && set.pressed != null && set.disabled != null,
                "The " + kind + " icon has normal, hover, pressed and disabled artwork");
            foreach (var sprite in new[] { set.normal, set.highlighted, set.pressed, set.disabled })
                Check(AssetDatabase.GetAssetPath(sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                    "The " + kind + " icon state comes from the integrated UI art: " + sprite.name);
        }
        Check(theme.cursorSource != null && theme.cursorSource.name == "Steampunk_UI_inventory_cursor_icons_6",
            "The cursor is derived from the exact requested inventory cursor sprite");
        Check(theme.cursorTexture != null && AssetDatabase.GetAssetPath(theme.cursorTexture)
            .StartsWith("Assets/Mythologicum/UI/Cursors/", StringComparison.Ordinal),
            "The cursor uses its own cropped project texture rather than the full sprite atlas");
        var cursorImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(theme.cursorTexture)) as TextureImporter;
        Check(cursorImporter != null && cursorImporter.textureType == TextureImporterType.Cursor && cursorImporter.isReadable
            && !cursorImporter.mipmapEnabled && cursorImporter.textureCompression == TextureImporterCompression.Uncompressed,
            "The custom cursor is imported as a readable, uncompressed cursor without mipmaps");
        Check(theme.cursorTexture.width > 0 && theme.cursorTexture.height > 0 && theme.cursorTexture.width <= 64
            && theme.cursorTexture.height <= 64 && theme.cursorTexture.width < theme.cursorSource.texture.width,
            "The cursor is small enough for hardware cursors and contains only the selected icon");
        Check(theme.cursorHotspot.x >= 0 && theme.cursorHotspot.x < theme.cursorTexture.width
            && theme.cursorHotspot.y >= 0 && theme.cursorHotspot.y < theme.cursorTexture.height,
            "The cursor's click hotspot is inside the cropped image");
        var cursorPixels = theme.cursorTexture.GetPixels32();
        Check(cursorPixels.Count(pixel => pixel.a > 32) > 20 && cursorPixels.Count(pixel => pixel.a < 10) > 20,
            "The cropped cursor has visible artwork and transparent surrounding pixels");
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
        var palette = Field<ConstructionPalette>(controller, "palette");
        ValidateGameplayChrome(canvas, palette);
        ValidateParkStatus(controller, true);
        ValidateCursor(false);
        ValidateFrameworkEdgeCases(canvas);
        ValidateButtonIcon(FindButton(palette.Root.transform, "Building"), UIIconKind.Building, true);
        ValidateButtonIcon(FindButton(palette.Root.transform, "Park Management"), UIIconKind.ParkManagement);
        ValidateButtonIcon(FindButton(palette.Root.transform, "Encyclopedia"), UIIconKind.Encyclopedia);
        ValidateButtonIcon(FindButton(palette.Root.transform, "Pause"), UIIconKind.Pause);
        var hoverButton = FindButton(palette.Root.transform, "Building");
        var hover = hoverButton.GetComponent<UIHoverTooltip>();
        Check(hover != null && !hover.IsVisible, "Standalone HUD icons keep their names hidden until hovered");
        var hoverPointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                ((RectTransform)hoverButton.transform).TransformPoint(((RectTransform)hoverButton.transform).rect.center)),
            button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.Execute(hoverButton.gameObject, hoverPointer, ExecuteEvents.pointerEnterHandler);
        yield return null;
        yield return null;
        Check(hover.IsVisible && GameObject.Find("HUD tooltip") != null,
            "Hovering a standalone icon reveals its name after the tooltip delay, even with the simulation paused");
        Check(GameObject.Find("HUD tooltip").GetComponentsInChildren<Text>().Any(text => text.text.Contains("Building")),
            "The Building tooltip explains the unlabeled icon");
        Check(GameObject.Find("HUD tooltip").GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget),
            "The delayed HUD tooltip does not steal input from its icon or the world");
        Capture("InGame-Hover-Building", canvas.gameObject, 1280, 720);
        ExecuteEvents.Execute(hoverButton.gameObject, hoverPointer, ExecuteEvents.pointerDownHandler);
        Check(!hover.IsVisible, "Pressing a standalone icon immediately hides its tooltip");
        ExecuteEvents.Execute(hoverButton.gameObject, hoverPointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(hoverButton.gameObject, hoverPointer, ExecuteEvents.pointerExitHandler);
        yield return null;
        Check(!hover.IsVisible, "Leaving an icon removes its tooltip and restores the uncluttered HUD");
        palette.Refresh(false);
        FindButton(palette.Root.transform, "Building").onClick.Invoke();
        yield return null;
        Check(palette.IsOpen, "Building unfolds the construction categories");
        var tools = GameObject.Find("Building tools");
        Check(tools != null, "Building has its own expandable construction row");
        Check(tools.GetComponentsInChildren<Button>().Select(button => button.name).SequenceEqual(new[]
            { "Buildings (B)", "Enclosures (H)", "Terrain (T)", "Paths (P)", "Fences (F)", "Creatures (C)", "Demolish (X)" }),
            "Building groups the seven requested categories in their requested order");
        Check(FindButton(tools.transform, "Buildings (B)").interactable && FindButton(tools.transform, "Terrain (T)").interactable,
            "New parks can choose buildings and terrain before placing an entrance");
        Check(!FindButton(tools.transform, "Paths (P)").interactable && !FindButton(tools.transform, "Fences (F)").interactable
            && !FindButton(tools.transform, "Enclosures (H)").interactable && !FindButton(tools.transform, "Creatures (C)").interactable,
            "The nested construction menu preserves the park-entrance requirement");
        Capture("Building-Tools-BeforeEntrance", palette.Root, 1280, 720);
        FindButton(palette.Root.transform, "Building").onClick.Invoke();
        Check(!palette.IsOpen, "Clicking Building again folds away its categories");
        palette.Toggle(ConstructionCategory.Terrain);
        Check(palette.IsOpen && GameObject.Find("Terrain Choices") != null,
            "A direct construction-category shortcut unfolds its parent Building row");
        palette.Close();
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
        building.Refresh(CurrentCamera);
        creatures.RefreshExternal(guest.Actor);
        CheckNoWorldLabels("Buildings, staff, visitors and creatures");
        var cameraChecks = ValidateCameraFollow(controller, animal, guest, records[BuildingKind.ResearchCentre]);
        while (cameraChecks.MoveNext()) yield return cameraChecks.Current;
        controller.Visitors.RefreshReputation(); controller.Economy.Advance(10); BuildingEconomy.Tick(building.Network, controller.Economy, 10);
        controller.Economy.Earn(25, ParkTransactionCategory.Food);
        ValidateParkStatus(controller, true);
        string previousFunds = GameObject.Find("Park status").GetComponentsInChildren<Text>().Single(text => text.name == "Park funds").text;
        controller.Economy.Earn(7.25f, ParkTransactionCategory.Other);
        ValidateParkStatus(controller, true);
        Check(GameObject.Find("Park status").GetComponentsInChildren<Text>().Single(text => text.name == "Park funds").text != previousFunds,
            "The compact funds display updates immediately when real park funds change");
        palette.Refresh(true);
        Field<VisitorPresentation>(controller, "visitorPresentation").Refresh();
        yield return null;
        Capture("InGame-HUD", canvas.gameObject, 1280, 720);
        Capture("InGame-HUD-1920", canvas.gameObject, 1920, 1080);
        FindButton(palette.Root.transform, "Building").onClick.Invoke();
        yield return null;
        Check(FindButton(GameObject.Find("Building tools").transform, "Paths (P)").interactable,
            "Placing the entrance enables the nested path tools");
        FindButton(GameObject.Find("Building tools").transform, "Buildings (B)").onClick.Invoke();
        yield return null;
        Capture("Construction-Buildings", palette.Root, 1280, 720);
        Capture("Construction-Buildings-1920", palette.Root, 1920, 1080); palette.Close();
        palette.Toggle(ConstructionCategory.Enclosures); yield return null;
        Capture("Construction-Enclosures", palette.Root, 1280, 720); palette.Close();
        FindButton(palette.Root.transform, "Building").onClick.Invoke();
        FindButton(GameObject.Find("Building tools").transform, "Demolish (X)").onClick.Invoke();
        Check(!palette.IsOpen, "Demolish selects the existing tool and folds the Building menu");
        Check(Field<object>(controller, "tool").ToString() == "Demolish", "The nested Demolish action selects the real demolition tool");
        Invoke(controller, "OpenParkManagement");
        Field<ParkManagementPresentation>(controller, "parkManagement").Close();
        var buildingInfo = Field<BuildingInspection>(controller, "buildingInspection");
        foreach (var kind in new[] { BuildingKind.ParkEntrance, BuildingKind.Kiosk, BuildingKind.RestArea })
        {
            buildingInfo.SetInspecting(true); buildingInfo.Select(records[kind]);
            yield return null;
            RefreshResponsiveInspectors();
            var shortPanel = Field<GameObject>(buildingInfo, "panel");
            ValidateInfoFrame(shortPanel, true);
            var shortScrolls = shortPanel.GetComponentsInChildren<ScrollRect>()
                .Where(scroll => scroll.gameObject.activeInHierarchy).ToArray();
            Check(shortScrolls.Length == 1, kind + " information has one content surface rather than nested scrolling boxes");
            ValidateScrollInput(shortScrolls.Single(), false, kind + " information");
            if (kind == BuildingKind.Kiosk)
            {
                Capture("Building-Kiosk", shortPanel, 1280, 720);
                Capture("Building-Kiosk-1920", shortPanel, 1920, 1080);
            }
        }
        var designerChecks = ValidateDesignerControls(controller, buildingInfo, records[BuildingKind.Kiosk]);
        while (designerChecks.MoveNext()) yield return designerChecks.Current;
        buildingInfo.SetInspecting(true); buildingInfo.Select(records[BuildingKind.ResearchCentre]);
        yield return null;
        ValidateInfoFrame(Field<GameObject>(buildingInfo, "panel"), true);
        Capture("Building-Inspector", Field<GameObject>(buildingInfo, "panel"), 1280, 720);
        buildingInfo.Select(null);
        var creatureInfo = Field<CreatureInspection>(controller, "creatureInspection");
        creatureInfo.SetInspecting(true); creatureInfo.Select(animal);
        yield return null;
        ValidateInfoFrame(Field<GameObject>(creatureInfo, "panel"), true);
        Capture("Creature-Inspector", Field<GameObject>(creatureInfo, "panel"), 1280, 720);
        Capture("Creature-Inspector-1920", Field<GameObject>(creatureInfo, "panel"), 1920, 1080);
        creatureInfo.Select(null);
        var visitorInfo = Field<VisitorPresentation>(controller, "visitorPresentation"); visitorInfo.SetInspecting(true); visitorInfo.Select(guest);
        yield return null;
        ValidateInfoFrame(Field<GameObject>(visitorInfo, "panel"), true);
        Capture("Visitor-Inspector", Field<GameObject>(visitorInfo, "panel"), 1280, 720); visitorInfo.Select(null);

        for (int x = 18; x < 22; x++) { fences.TryPlace(new FenceEdge(Cell(x, 17), true)); fences.TryPlace(new FenceEdge(Cell(x, 21), true)); }
        for (int z = 17; z < 21; z++) { fences.TryPlace(new FenceEdge(Cell(18, z), false)); fences.TryPlace(new FenceEdge(Cell(22, z), false)); }
        var damagedEdge = new FenceEdge(Cell(18, 17), true);
        Check(fences.Layout.TryDamage(damagedEdge, 10f) > 0, "The label check includes a genuinely damaged fence");
        fences.Refresh(CurrentCamera);
        CheckNoWorldLabels("Damaged fences and all placed park objects");
        var enclosures = Field<EnclosureMap>(controller, "enclosureMap"); enclosures.Refresh(building.Network.IsPathConnected);
        var enclosureInfo = Field<EnclosurePresentation>(controller, "enclosurePresentation"); enclosureInfo.SetInspecting(true); enclosureInfo.Select(Cell(19, 18)); enclosureInfo.Refresh();
        Check(Field<GameObject>(enclosureInfo, "panel").activeSelf, "Enclosure information remains selectable after restyling");
        yield return null;
        Capture("Enclosure-Inspector", Field<GameObject>(enclosureInfo, "panel"), 1280, 720); enclosureInfo.Select(null); enclosureInfo.Refresh();
        var notifications = Field<NotificationPresentation>(controller, "notifications");
        controller.Progression.Inbox.MarkAllRead(); notifications.Refresh();
        ValidateMailIcon(canvas, false);
        controller.Progression.Inbox.Post("ui-test-fence", ParkNoticeKind.FenceOpened, "Fence opened", "The brass notification frame preserves readable event text.", Cell(19, 18));
        notifications.Refresh();
        ValidateMailIcon(canvas, true);
        yield return null;
        Capture("InGame-UnreadMail", canvas.gameObject, 1280, 720);
        notifications.Toggle();
        ValidateMailIcon(canvas, false);
        yield return null;
        Capture("Notifications", Field<GameObject>(notifications, "panel"), 1280, 720); notifications.Close();
        Invoke(controller, "OpenExpeditions", records[BuildingKind.ExpeditionCentre]);
        yield return null;
        var progression = Field<ProgressionPresentation>(controller, "management");
        Capture("Expeditions", Field<GameObject>(progression, "map"), 1280, 720);
        var worldMap = Field<GameObject>(progression, "map").GetComponentsInChildren<Image>()
            .Single(image => image.name == "World map");
        Check(worldMap.sprite != null && worldMap.sprite.name == "Steampunk_UI_Dialogue_1_1",
            "The actual expedition region map renders on the chosen parchment artwork");
        Check(worldMap.color.a > .9f, "The world map parchment is visible behind its continent drawing and region controls");
        FindButton(Field<GameObject>(progression, "map").transform, "Buy team").onClick.Invoke();
        Check(controller.Progression.Teams == 1, "Restyled expedition controls preserve their purchase action"); progression.Close();
        Invoke(controller, "OpenTechnologies", records[BuildingKind.ResearchCentre]);
        yield return null;
        Capture("Technology-Research", Field<GameObject>(progression, "research"), 1280, 720); progression.Close();
        FindButton(palette.Root.transform, "Park Management").onClick.Invoke();
        yield return null;
        var management = Field<ParkManagementPresentation>(controller, "parkManagement"); var managementRoot = Field<GameObject>(management, "root");
        ValidateInfoFrame(managementRoot, false);
        foreach (string tab in new[] { "Statistics", "Finances", "Prices & budget", "Star rating" })
        {
            FindButton(managementRoot.transform, tab).onClick.Invoke(); yield return null;
            Capture("Management-" + tab.Replace(" & ", "-").Replace(' ', '-'), managementRoot, 1280, 720);
        }
        Capture("Management-Stars-1920", managementRoot, 1920, 1080); management.Close();
        FindButton(palette.Root.transform, "Encyclopedia").onClick.Invoke();
        yield return null;
        var encyclopedia = Field<EncyclopediaPresentation>(controller, "encyclopedia");
        var book = GameObject.Find("Encyclopedia window");
        Check(encyclopedia.IsOpen && book != null && controller.ManagementOpen,
            "The bottom Encyclopedia button opens a modal and blocks world construction input");
        Check(!palette.IsOpen && !management.IsOpen && !progression.IsOpen,
            "Opening the encyclopedia folds construction and closes other management windows");
        FindButton(book.transform, "Park").onClick.Invoke();
        yield return null;
        Capture("Encyclopedia-Park", book, 1280, 720);
        Check(book.GetComponentsInChildren<Button>().Any(button => button.name == "Getting started"),
            "The Park encyclopedia tab contains a browsable guide list");
        FindButton(book.transform, "Creatures").onClick.Invoke();
        var chupacabra = Resources.Load<CreatureDefinition>("Creatures/Chupacabra");
        FindButton(book.transform, chupacabra.displayName).onClick.Invoke();
        yield return null;
        var article = book.GetComponentsInChildren<Text>().Single(text => text.name == "Encyclopedia article");
        var articleSummary = book.GetComponentsInChildren<Text>().Single(text => text.name == "Encyclopedia article summary");
        var research = controller.Personnel.Research;
        Check(Near(research.Progress(chupacabra), 0) && articleSummary.text.Contains("0%"),
            "Creature biographies use the existing research system's initial progress");
        Check(article.text.Contains(research.Description(chupacabra)) && !article.text.Contains("Food: ")
            && !article.text.Contains("Habitat: ") && !article.text.Contains("Containment: "),
            "Unresearched creature biographies conceal the locked care and containment discoveries");
        Capture("Encyclopedia-Creature-Unknown", book, 1280, 720);
        var unknownArticleScroll = book.GetComponentsInChildren<ScrollRect>()
            .Single(scroll => scroll.name == "Encyclopedia article scroll");
        ValidateScrollInput(unknownArticleScroll, false, "Unresearched creature biography");
        Check(research.Study(chupacabra, 25), "The encyclopedia fixture advances real researcher knowledge");
        encyclopedia.Refresh(); yield return null;
        Check(articleSummary.text.Contains("25%") && article.text.Contains(research.Description(chupacabra))
            && article.text.Contains("Food: ") && !article.text.Contains("Habitat: "),
            "The open biography expands its first discovery without leaking later research stages");
        Capture("Encyclopedia-Creature-FirstDiscovery", book, 1280, 720);
        Check(research.Study(chupacabra, 75), "The encyclopedia fixture completes real species research");
        encyclopedia.Refresh(); yield return null;
        Check(articleSummary.text.Contains("100%") && article.text.Contains(research.Description(chupacabra))
            && article.text.Contains("Habitat: ") && article.text.Contains("Sleep: ") && article.text.Contains("Space: ")
            && article.text.Contains("Group: ") && article.text.Contains("Containment: "),
            "Completed research expands the same biography with all unlocked creature-care information");
        Capture("Encyclopedia-Creature-Researched", book, 1280, 720);
        var articleScroll = book.GetComponentsInChildren<ScrollRect>().Single(scroll => scroll.name == "Encyclopedia article scroll");
        Check(articleScroll.content.rect.height >= article.preferredHeight,
            "The researched biography grows its scrollable content to fit all unlocked notes");
        if (articleScroll.content.rect.height > articleScroll.viewport.rect.height + 1)
        {
            articleScroll.verticalNormalizedPosition = .4f; encyclopedia.Refresh();
            Check(Near(articleScroll.verticalNormalizedPosition, .4f, .03f), "Refreshing encyclopedia progress preserves the reader's scroll position");
        }
        FindButton(book.transform, "Management").onClick.Invoke();
        foreach (string entry in new[] { "Economy & prices", "Staff", "Creature care" })
        {
            FindButton(book.transform, entry).onClick.Invoke(); yield return null;
            Check(!string.IsNullOrWhiteSpace(article.text), "The Management encyclopedia explains " + entry);
        }
        Capture("Encyclopedia-Management", book, 1280, 720);
        Capture("Encyclopedia-Management-1920", book, 1920, 1080);
        Capture("Encyclopedia-Management-4x3", book, 1024, 768);
        Capture("Encyclopedia-Management-Ultrawide", book, 2560, 1080);
        Check(controller.CloseManagementWindow() && !encyclopedia.IsOpen,
            "The normal management-window close route dismisses the encyclopedia");
        buildingInfo.Select(records[BuildingKind.ResearchCentre]);
        yield return null;
        RefreshResponsiveInspectors();
        var researchPanel = Field<GameObject>(buildingInfo, "panel");
        var researchScroll = researchPanel.GetComponentsInChildren<ScrollRect>()
            .Single(scroll => scroll.gameObject.activeInHierarchy);
        ValidateScrollInput(researchScroll, true, "Research workplace with fully unlocked care notes");
        researchScroll.verticalNormalizedPosition = .4f;
        buildingInfo.Refresh(); UIOverflowScroll.Attach(researchScroll).Refresh();
        Check(Near(researchScroll.verticalNormalizedPosition, .4f, .035f),
            "Refreshing a long building report preserves its current reading position");
        Capture("Building-Research-Expanded", researchPanel, 1280, 720);
        foreach (int repeat in new[] { 0, 1 })
        {
            buildingInfo.Select(records[BuildingKind.Kiosk]); RefreshResponsiveInspectors();
            var compactScroll = researchPanel.GetComponentsInChildren<ScrollRect>()
                .Single(scroll => scroll.gameObject.activeInHierarchy);
            ValidateScrollInput(compactScroll, false, "Short kiosk report after long research report, selection " + repeat);
            buildingInfo.Select(records[BuildingKind.ResearchCentre]); RefreshResponsiveInspectors();
        }
        buildingInfo.Select(null);
        Invoke(controller, "SelectTerrainBrush", TerrainBrushMode.Raise);
        yield return null;
        var terrain = Field<TerrainEditingPresentation>(controller, "terrainEditing");
        Capture("Terrain-Tools", Field<GameObject>(terrain, "root"), 1280, 720);
        FindButton(Field<GameObject>(terrain, "root").transform, "Radius +").onClick.Invoke();
        Check(terrain.Radius == 4, "Restyled terrain controls preserve their radius action");
        FindButton(Field<GameObject>(terrain, "root").transform, "Done").onClick.Invoke();
        FindButton(palette.Root.transform, "Building").onClick.Invoke();
        Check(palette.IsOpen, "Construction is open before testing the bottom Pause action");
        var pause = controller.GetComponent<GameSaveMenu>(); Time.timeScale = 1;
        FindButton(palette.Root.transform, "Pause").onClick.Invoke();
        yield return null;
        Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "Pause still freezes the simulation after restyling");
        Check(!palette.IsOpen, "The bottom Pause action folds construction before opening its menu");
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
            ValidateMovementInstructions(GameObject.Find("Settings window"));
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
        Check(Near(controller.Personnel.Research.Progress(Resources.Load<CreatureDefinition>("Creatures/Chupacabra")), 100),
            "The park save preserves the encyclopedia's completed species research");
        ValidateParkStatus(controller, false);
        ValidateCursor(false);
        Capture("Loaded-Park-Pause", Field<GameObject>(controller.GetComponent<GameSaveMenu>(), "overlay"), 1280, 720);
        File.WriteAllText(Result, "PASS: " + checks + " steampunk asset, menu, settings, UI layout, GPU, interaction and save/load checks.\nCaptures: " + captures + " in " + Captures);
        pending = false; EditorApplication.Exit(0);
    }

    static void CheckNoWorldLabels(string population)
    {
        var labels = UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
            .Where(label => label.gameObject.activeInHierarchy).ToArray();
        Check(labels.Length == 0, population + " have no floating TextMesh labels: "
            + string.Join(", ", labels.Select(label => label.name)));
        Check(!UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Any(canvas => canvas.renderMode == RenderMode.WorldSpace && canvas.GetComponentsInChildren<Text>()
                .Any(text => text.enabled && !string.IsNullOrWhiteSpace(text.text))),
            population + " do not replace their removed floating labels with world-space text canvases");
    }

    static void ValidateInfoFrame(GameObject panel, bool vertical)
    {
        UIThemeApplicator.ApplyAll(CurrentCanvas); Canvas.ForceUpdateCanvases();
        string expected = vertical ? "Steampunk_UI_Frames_1_2" : "Steampunk_UI_Dialogue_2_0";
        var surfaces = panel.GetComponentsInChildren<Image>().Where(image => image.sprite != null
            && image.sprite.name == expected && image.isActiveAndEnabled).ToArray();
        Check(surfaces.Length > 0, panel.name + " uses the selected " + (vertical ? "vertical" : "horizontal") + " information frame");
        foreach (var image in surfaces) ValidateFrameProportions(image, panel.name);
    }

    static void ValidateFrameProportions(Image image, string page)
    {
        Check(image.type == Image.Type.Simple && image.preserveAspect,
            page + " renders the complete frame proportionally rather than stretching its ornament: " + image.name);
        var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
        float width = Vector3.Distance(corners[0], corners[3]);
        float height = Vector3.Distance(corners[0], corners[1]);
        float sourceAspect = image.sprite.rect.width / image.sprite.rect.height;
        Check(height > 0 && Mathf.Abs(width / height / sourceAspect - 1f) < .01f,
            page + " keeps the artwork's original aspect ratio: " + image.sprite.name
                + "; displayed=" + width + "x" + height + "; source=" + image.sprite.rect.size);
    }

    static void ValidateScrollInput(ScrollRect scroll, bool overflowExpected, string report)
    {
        UIOverflowScroll.Attach(scroll).Refresh(); Canvas.ForceUpdateCanvases();
        Check(scroll.content != null && scroll.viewport != null, report + " retains a real content viewport");
        float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
        Check(overflowExpected ? overflow > 2f : overflow <= 1f,
            report + (overflowExpected ? " has genuinely overflowing content" : " contains no artificial blank overflow")
                + "; content=" + scroll.content.rect.height + "; viewport=" + scroll.viewport.rect.height);
        Check(scroll.vertical == overflowExpected, report + " only enables vertical scrolling when its content needs it");
        if (scroll.verticalScrollbar != null)
            Check(scroll.verticalScrollbar.gameObject.activeInHierarchy == overflowExpected,
                report + " only shows its scrollbar when its content needs it");
        scroll.verticalNormalizedPosition = 1; scroll.StopMovement();
        Vector2 start = scroll.content.anchoredPosition;
        Vector2 point = RectTransformUtility.WorldToScreenPoint(CurrentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CurrentCanvas.worldCamera,
            scroll.viewport.TransformPoint(scroll.viewport.rect.center));
        var wheel = new PointerEventData(EventSystem.current) { position = point, scrollDelta = new Vector2(0, -3f) };
        ExecuteEvents.Execute(scroll.gameObject, wheel, ExecuteEvents.scrollHandler);
        Check(overflowExpected ? Vector2.Distance(scroll.content.anchoredPosition, start) > 1f
                : Vector2.Distance(scroll.content.anchoredPosition, start) < .01f,
            report + (overflowExpected ? " responds to a real scroll-wheel event" : " ignores scroll-wheel input while all content fits"));
        scroll.verticalNormalizedPosition = 1; scroll.StopMovement(); start = scroll.content.anchoredPosition;
        var drag = new PointerEventData(EventSystem.current) { position = point, pressPosition = point,
            button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(scroll.gameObject, drag, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(scroll.gameObject, drag, ExecuteEvents.beginDragHandler);
        drag.position = point + Vector2.up * 80; drag.delta = Vector2.up * 80;
        ExecuteEvents.Execute(scroll.gameObject, drag, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(scroll.gameObject, drag, ExecuteEvents.endDragHandler);
        Check(overflowExpected ? Vector2.Distance(scroll.content.anchoredPosition, start) > 1f
                : Vector2.Distance(scroll.content.anchoredPosition, start) < .01f,
            report + (overflowExpected ? " responds to a real drag event" : " does not drag when all content fits"));
        scroll.verticalNormalizedPosition = 1; scroll.StopMovement();
    }

    static bool SameColor(Color a, Color b) => Mathf.Abs(a.r - b.r) < .005f && Mathf.Abs(a.g - b.g) < .005f
        && Mathf.Abs(a.b - b.b) < .005f && Mathf.Abs(a.a - b.a) < .005f;

    static Text DesignerText(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.font = UITheme.BodyFont; text.fontSize = 18;
        text.text = name; text.color = color; text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(350, 50);
        UIThemeElement.Mark(go, UIThemeRole.Text);
        return text;
    }

    static void ApplyDesignerChanges(Canvas canvas)
    {
        UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
        RefreshResponsiveInspectors(); UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
    }

    static Type DesignerEditorType(string name)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(candidate => candidate != null);
        Check(type != null, "The designer authoring command is available: " + name);
        return type;
    }

    static object DesignerCommand(Type owner, string method, params object[] arguments)
    {
        var command = owner.GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Check(command != null, "The designer authoring operation is available: " + method);
        return command.Invoke(null, arguments);
    }

    static T DesignerAssetReadback<T>(string sourcePath, string readbackPath) where T : UnityEngine.Object
    {
        if (AssetDatabase.LoadMainAssetAtPath(readbackPath) != null) AssetDatabase.DeleteAsset(readbackPath);
        File.WriteAllBytes(readbackPath, File.ReadAllBytes(sourcePath));
        AssetDatabase.ImportAsset(readbackPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var loaded = AssetDatabase.LoadAssetAtPath<T>(readbackPath);
        Check(loaded != null, "The saved designer asset is readable from a fresh disk copy: " + typeof(T).Name);
        return loaded;
    }

    static IEnumerator ValidateDesignerControls(BuildModeController controller, BuildingInspection inspector, ParkBuildingRecord kiosk)
    {
        var canvas = CurrentCanvas; var theme = UITheme.Current; var layout = UILayoutSettings.Current;
        Check(theme != null && layout != null && AssetDatabase.GetAssetPath(layout) == "Assets/Mythologicum/UI/Resources/ExhibitionLayout.asset",
            "Designer layout settings load from the persistent project asset rather than a temporary runtime default");
        Check(Path.GetFullPath(Directory.GetCurrentDirectory()).EndsWith(Path.Combine("LocalCache", "SaveValidation"), StringComparison.OrdinalIgnoreCase),
            "Designer asset-writing checks run only inside the disposable validation project");
        string originalTheme = EditorJsonUtility.ToJson(theme), originalLayout = EditorJsonUtility.ToJson(layout);
        string themeAssetPath = AssetDatabase.GetAssetPath(theme), layoutAssetPath = AssetDatabase.GetAssetPath(layout);
        byte[] originalThemeBytes = File.ReadAllBytes(themeAssetPath), originalLayoutBytes = File.ReadAllBytes(layoutAssetPath);
        var originalSelection = Selection.activeObject;
        bool designerSettingsOpen = false;
        var fixtures = new GameObject("Designer configuration fixtures", typeof(RectTransform));
        fixtures.transform.SetParent(canvas.transform, false);
        var unrelated = DesignerText(fixtures.transform, "Operating accounts", Color.white);
        var warning = DesignerText(fixtures.transform, "Designer warning", new Color(.95f, .1f, .1f));
        inspector.SetInspecting(true); inspector.Select(kiosk);
        var panel = Field<GameObject>(inspector, "panel"); var frame = panel.GetComponent<UIFramedPanel>();
        var accounts = Field<Text>(inspector, "accounts");
        var apply = panel.GetComponentsInChildren<Button>().Single(button => button.name == "Apply price");
        var applyText = apply.GetComponentInChildren<Text>(); var price = Field<InputField>(inspector, "price");
        var funds = GameObject.Find("Park status").GetComponentsInChildren<Text>().Single(text => text.name == "Park funds");
        bool originalInteractable = apply.interactable;
        var accountsStyle = accounts.GetComponent<UIThemeElement>();
        const string savedThemePath = "Assets/Editor/DesignerValidationTheme.asset";
        const string textReadbackPath = "Assets/Editor/DesignerAuthoringThemeReadback.asset";
        const string layoutReadbackPath = "Assets/Editor/DesignerAuthoringLayoutReadback.asset";
        UITheme savedTheme = null;
        try
        {
            ApplyDesignerChanges(canvas);
            var alternativeFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Mythologicum/UI/Fonts/Arvo.ttf");
            Check(alternativeFont != null, "A retained project font is available for the live designer preview");
            Color bodyColor = new Color(.24f, .82f, .58f), buttonColor = new Color(.51f, .78f, .98f);
            Color inputColor = new Color(.97f, .86f, .35f), hudColor = new Color(.76f, .85f, 1f);
            theme.bodyText.font = alternativeFont; theme.bodyText.overrideColor = true; theme.bodyText.color = bodyColor;
            theme.bodyText.fitMode = UITextFitMode.FixedSize;
            var serializedTheme = new SerializedObject(theme);
            var bodySize = serializedTheme.FindProperty("bodyText")?.FindPropertyRelative("fontSize");
            Check(bodySize != null, "Body font size is a serialized Inspector control");
            bodySize.intValue = 20; serializedTheme.ApplyModifiedPropertiesWithoutUndo();
            theme.buttonText.fontSize = 19; theme.buttonText.fitMode = UITextFitMode.FixedSize;
            theme.buttonText.overrideColor = true; theme.buttonText.color = buttonColor;
            theme.inputText.fontSize = 21; theme.inputText.fitMode = UITextFitMode.FixedSize;
            theme.inputText.overrideColor = true; theme.inputText.color = inputColor;
            theme.hudText.fontSize = 31; theme.hudText.fitMode = UITextFitMode.FixedSize;
            theme.hudText.overrideColor = true; theme.hudText.color = hudColor;
            theme.textShadows = true; theme.shadowColor = new Color(.12f, .04f, .2f, .7f); theme.shadowOffset = new Vector2(2, -2);
            theme.frameTint = new Color(.87f, .93f, 1f);
            theme.panelLiningColor = new Color(.12f, .08f, .035f, .9f);
            theme.iconTint = new Color(.83f, .94f, 1f);
            yield return null; ApplyDesignerChanges(canvas);
            Check(accounts.font == alternativeFont && accounts.fontSize == 20 && SameColor(accounts.color, bodyColor),
                "Editing the theme updates the real building report's font, size and colour while the screen is open");
            Check(applyText.fontSize == 19 && SameColor(applyText.color, buttonColor),
                "Button captions use their independently editable size and colour profile");
            Check(price.textComponent.fontSize == 21 && SameColor(price.textComponent.color, inputColor),
                "Editable prices use the input text profile rather than inheriting button styling");
            Check(funds.fontSize == 31 && SameColor(funds.color, hudColor),
                "The funds HUD responds to its separate editable typography profile");
            var shadow = accounts.GetComponent<Shadow>();
            Check(shadow != null && shadow.enabled && SameColor(shadow.effectColor, theme.shadowColor)
                && shadow.effectDistance == theme.shadowOffset, "Text shadow colour and offset are live theme controls");
            Check(SameColor(panel.GetComponentsInChildren<Image>().Single(image => image.name == "Panel artwork").color, theme.frameTint)
                && SameColor(panel.GetComponentsInChildren<Image>().Single(image => image.name == "Panel lining").color, theme.panelLiningColor),
                "Panel artwork tint and backing colour update through the shared appearance asset");
            var dockIcon = FindButton(Field<ConstructionPalette>(controller, "palette").Root.transform, "Building").GetComponent<UIIcon>();
            dockIcon.Refresh();
            Check(SameColor(dockIcon.IconImage.color, theme.iconTint), "The icon tint responds to the shared appearance asset");
            for (int repeat = 0; repeat < 3; repeat++) UIThemeApplicator.ApplyAll(canvas);
            Check(accounts.fontSize == 20, "Repeated theme refreshes do not compound font-size adjustments");
            theme.buttonHover = theme.buttonPressed;
            var buttonPointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(apply.gameObject, buttonPointer, ExecuteEvents.pointerEnterHandler);
            for (int repeat = 0; repeat < 3; repeat++) UIThemeApplicator.ApplyAll(canvas);
            Check(apply.GetComponent<Image>().sprite == theme.buttonHover,
                "A designer's hover artwork survives repeated shared-theme refreshes");
            ExecuteEvents.Execute(apply.gameObject, buttonPointer, ExecuteEvents.pointerExitHandler);
            theme.globalTextScale = 1.25f;
            yield return null; ApplyDesignerChanges(canvas);
            Check(accounts.fontSize == 25 && price.textComponent.fontSize == 26 && funds.fontSize == 39,
                "Global text scaling applies predictably across body, input and HUD profiles");
            theme.globalTextScale = 1f;
            yield return null; ApplyDesignerChanges(canvas);
            float normalHeight = accounts.preferredHeight;
            theme.bodyText.fontSize = 36;
            yield return null; ApplyDesignerChanges(canvas);
            var reportScroll = panel.GetComponentsInChildren<ScrollRect>().Single(scroll => scroll.gameObject.activeInHierarchy);
            Check(accounts.preferredHeight > normalHeight + 40f, "Larger designer text remeasures the real report content");
            ValidateScrollInput(reportScroll, true, "Kiosk report with intentionally large designer text");
            theme.bodyText.fontSize = 20;
            yield return null; ApplyDesignerChanges(canvas);
            ValidateScrollInput(reportScroll, false, "Kiosk report after restoring a fitting designer text size");

            Color individualColor = new Color(.72f, .46f, .95f);
            theme.textOverrides = (theme.textOverrides ?? Array.Empty<UITextOverride>()).Concat(new[]
            {
                new UITextOverride { elementNameOrPath = "Operating accounts", screenName = "Building information",
                    appearance = new UITextAppearance { fontSize = 23, fitMode = UITextFitMode.FixedSize,
                        overrideColor = true, color = individualColor } }
            }).ToArray();
            yield return null; ApplyDesignerChanges(canvas);
            Check(accounts.fontSize == 23 && SameColor(accounts.color, individualColor),
                "A persistent named text exception changes the intended inspector label");
            Check(unrelated.fontSize == 20 && SameColor(unrelated.color, bodyColor),
                "A screen-scoped text exception leaves an identically named label on another screen unchanged");
            accountsStyle.LocalAppearance = new UITextAppearance { fontSize = 25, fitMode = UITextFitMode.FixedSize,
                overrideColor = true, color = new Color(.9f, .6f, .45f), overrideAlignment = true, alignment = TextAnchor.MiddleCenter };
            accountsStyle.LocalAppearanceEnabled = true;
            yield return null; ApplyDesignerChanges(canvas);
            Check(accounts.fontSize == 25 && accounts.alignment == TextAnchor.MiddleCenter
                && SameColor(accounts.color, accountsStyle.LocalAppearance.color),
                "An authored element's local appearance can override the shared profile without editing code");
            accountsStyle.LocalAppearanceEnabled = false;
            theme.bodyText.overrideColor = false; theme.buttonText.overrideColor = false;
            theme.dangerColor = new Color(.93f, .43f, .17f); theme.mutedColor = new Color(.46f, .56f, .66f);
            theme.buttonDisabledTint = new Color(.28f, .38f, .45f, .65f); apply.interactable = false;
            theme.buttonDisabled = theme.buttonPressed;
            theme.textShadows = false;
            yield return null; ApplyDesignerChanges(canvas);
            Check(SameColor(warning.color, theme.dangerColor), "Warnings use the editable warning colour after typography changes");
            Check(SameColor(applyText.color, theme.mutedColor) && SameColor(apply.GetComponent<Image>().color, theme.buttonDisabledTint),
                "Disabled controls retain the editable disabled text and surface colours");
            Check(apply.GetComponent<Image>().sprite == theme.buttonDisabled,
                "A designer's disabled artwork remains applied while a control is unavailable");
            Check(!accounts.GetComponent<Shadow>().enabled, "Text shadows can be switched off through the theme asset");
            theme.textOutlines = true; theme.outlineColor = new Color(.15f,.1f,.3f,1); theme.outlineOffset = new Vector2(2,-1);
            ApplyDesignerChanges(canvas);
            var textOutline = accounts.GetComponent<Outline>();
            Check(textOutline != null && textOutline.enabled && SameColor(textOutline.effectColor, theme.outlineColor)
                && textOutline.effectDistance == theme.outlineOffset, "Enabling outlines adds the configured effect to ordinary labels");
            theme.textOutlines = false; ApplyDesignerChanges(canvas);
            Check(!textOutline.enabled, "Ordinary text outlines can be disabled again without changing HUD outlines");

            EditorJsonUtility.FromJsonOverwrite(originalTheme, theme); apply.interactable = originalInteractable;
            yield return null; ApplyDesignerChanges(canvas);
            var settings = SettingsPresentation.Open(canvas); designerSettingsOpen = true;
            var display = settings.GetComponentsInChildren<Dropdown>().Single(dropdown => dropdown.name == "Window mode");
            int authoredOptionSize = display.itemText.fontSize;
            theme.globalTextScale = 1.1f; theme.inputText.fontSize = 20; theme.inputText.fitMode = UITextFitMode.FixedSize;
            yield return null; ApplyDesignerChanges(canvas);
            display.Show(); yield return null; ApplyDesignerChanges(canvas);
            var optionLabels = GameObject.Find("Dropdown List").GetComponentsInChildren<Text>()
                .Where(text => text.GetComponentInParent<Toggle>() != null).ToArray();
            Check(optionLabels.Length == display.options.Count && optionLabels.All(text => text.fontSize == 22),
                "Opened dropdown choices retain the configured input size and global text scale");
            display.Hide(); yield return null;
            theme.inputText.fontSize = 0; theme.inputText.sizeScale = 1.1f;
            ApplyDesignerChanges(canvas); display.Show(); yield return null; ApplyDesignerChanges(canvas);
            optionLabels = GameObject.Find("Dropdown List").GetComponentsInChildren<Text>()
                .Where(text => text.GetComponentInParent<Toggle>() != null).ToArray();
            int inheritedOptionSize = Mathf.RoundToInt(authoredOptionSize * 1.1f * 1.1f);
            Check(optionLabels.All(text => text.fontSize == inheritedOptionSize),
                "Cloned dropdown choices inherit their authored base size without applying scale a second time; expected "
                    + inheritedOptionSize + "; actual " + string.Join(",", optionLabels.Select(text => text.fontSize)));
            display.Hide(); SettingsPresentation.CloseActive(); designerSettingsOpen = false;
            EditorJsonUtility.FromJsonOverwrite(originalTheme, theme);
            yield return null; ApplyDesignerChanges(canvas);
            var panelSettings = layout.Panel("Building information");
            Check(panelSettings != null, "Building inspector geometry has a named entry in the editable layout asset");
            var rect = (RectTransform)panel.transform; Vector2 authoredPosition = rect.anchoredPosition;
            panelSettings.preferredWidth = 420f; panelSettings.overridePosition = true;
            panelSettings.position = authoredPosition + new Vector2(-12, -10);
            yield return null; ApplyDesignerChanges(canvas);
            Check(Near(rect.rect.width, 420f) && Vector2.Distance(rect.anchoredPosition, panelSettings.position) < .1f,
                "Changing panel width and position in the layout asset updates the open inspector");
            ValidateInfoFrame(panel, true);
            float bodyWidth = frame.Content.rect.width;
            var insets = layout.verticalBodyInsets; insets.left += .03f; layout.verticalBodyInsets = insets;
            yield return null; ApplyDesignerChanges(canvas);
            Check(frame.Content.rect.width < bodyWidth - 10f,
                "Frame safe-area settings adjust the actual content area without reshaping the artwork");
            layout.elementOverrides = (layout.elementOverrides ?? Array.Empty<UIElementLayoutOverride>()).Concat(new[]
            {
                new UIElementLayoutOverride { elementNameOrPath = "Apply price", screenName = "Building information",
                    overridePosition = true, position = new Vector2(100, -3), overrideSize = true, size = new Vector2(110, 32) }
            }).ToArray();
            yield return null; ApplyDesignerChanges(canvas);
            var applyRect = (RectTransform)apply.transform;
            Check(Vector2.Distance(applyRect.anchoredPosition, new Vector2(100, -3)) < .1f
                && Vector2.Distance(applyRect.rect.size, new Vector2(110, 32)) < .1f,
                "A persistent element geometry exception overrides a generated control's position and dimensions");
            var palette = Field<ConstructionPalette>(controller, "palette");
            layout.toolbarIconSize = 72f; layout.buildingIconPosition += new Vector2(5, 6);
            palette.Refresh(true);
            yield return null; ApplyDesignerChanges(canvas);
            var buildingIcon = (RectTransform)FindButton(palette.Root.transform, "Building").transform;
            Check(Vector2.Distance(buildingIcon.sizeDelta, new Vector2(72, 72)) < .1f
                && Vector2.Distance(buildingIcon.anchoredPosition, layout.buildingIconPosition) < .1f,
                "The icon dock's size and curved positions are controlled by the layout asset");

            var designerWindow = DesignerEditorType("Mythologicum.Editor.UIDesignerWindow");
            Selection.activeGameObject = accounts.gameObject;
            string textPath = (string)DesignerCommand(designerWindow, "ElementPath", accounts.transform);
            var savedAppearance = new UITextAppearance { font = alternativeFont, fontSize = 22, fitMode = UITextFitMode.FixedSize,
                overrideColor = true, color = new Color(.58f, .81f, .94f) };
            DesignerCommand(designerWindow, "SaveText", theme, textPath, savedAppearance);
            yield return null; ApplyDesignerChanges(canvas);
            Check(accounts.fontSize == 22 && accounts.font == alternativeFont && SameColor(accounts.color, savedAppearance.color),
                "The Designer Tools text-save command updates the actual selected label");
            var themeReadback = DesignerAssetReadback<UITheme>(themeAssetPath, textReadbackPath);
            Check(AssetDatabase.IsOpenForEdit(theme) && themeReadback.textOverrides.Any(entry => entry != null
                && entry.elementNameOrPath == textPath && entry.appearance != null && entry.appearance.fontSize == 22
                && entry.appearance.font == alternativeFont && SameColor(entry.appearance.color, savedAppearance.color)),
                "The Designer Tools text exception is written to the editable theme asset on disk");
            Selection.activeGameObject = apply.gameObject;
            string geometryPath = (string)DesignerCommand(designerWindow, "ElementPath", apply.transform);
            applyRect.anchoredPosition = new Vector2(120, -4);
            applyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 128);
            applyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 34);
            DesignerCommand(designerWindow, "SaveGeometry", applyRect, geometryPath);
            applyRect.anchoredPosition = new Vector2(-30, -20);
            applyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 80);
            yield return null; ApplyDesignerChanges(canvas);
            Check(Vector2.Distance(applyRect.anchoredPosition, new Vector2(120, -4)) < .1f
                && Vector2.Distance(applyRect.rect.size, new Vector2(128, 34)) < .1f,
                "The Designer Tools geometry-save command persists after a generated control is laid out again");
            var layoutReadback = DesignerAssetReadback<UILayoutSettings>(layoutAssetPath, layoutReadbackPath);
            Check(AssetDatabase.IsOpenForEdit(layout) && layoutReadback.elementOverrides.Any(entry => entry != null
                && entry.elementNameOrPath == geometryPath && entry.overridePosition && entry.overrideSize
                && Vector2.Distance(entry.position, new Vector2(120, -4)) < .1f
                && Vector2.Distance(entry.size, new Vector2(128, 34)) < .1f),
                "The selected control's saved dimensions are serialized in the layout asset on disk");
            theme.bodyFont = alternativeFont; theme.textColor = new Color(.72f, .84f, .91f);
            theme.bodyText.fontSize = 19;
            theme.sliderFill = null;
            var savedBodyColor = theme.textColor;
            DesignerCommand(DesignerEditorType("Mythologicum.Editor.SteampunkArtSetup"), "Apply");
            Check(theme.sliderFill != null && theme.bodyFont == alternativeFont && SameColor(theme.textColor, savedBodyColor)
                && theme.bodyText.fontSize == 19 && theme.textOverrides.Any(entry => entry.elementNameOrPath == textPath),
                "Repairing a missing UI reference preserves the designer's font, palette, typography and saved exceptions");
            Check(AssetDatabase.GetAssetPath(theme.sliderFill).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                "The repair command restores missing control artwork from retained project assets");

            savedTheme = UnityEngine.Object.Instantiate(theme);
            savedTheme.bodyText = new UITextAppearance { font = alternativeFont, fontSize = 21, fitMode = UITextFitMode.FixedSize,
                overrideColor = true, color = individualColor };
            savedTheme.textOverrides = Array.Empty<UITextOverride>();
            if (AssetDatabase.LoadMainAssetAtPath(savedThemePath) != null) AssetDatabase.DeleteAsset(savedThemePath);
            AssetDatabase.CreateAsset(savedTheme, savedThemePath); EditorUtility.SetDirty(savedTheme); AssetDatabase.SaveAssetIfDirty(savedTheme);
            Resources.UnloadAsset(savedTheme); savedTheme = AssetDatabase.LoadAssetAtPath<UITheme>(savedThemePath);
            Check(savedTheme != null && savedTheme.bodyText.fontSize == 21 && savedTheme.bodyText.font == alternativeFont
                && SameColor(savedTheme.bodyText.color, individualColor),
                "A designer theme retains its font, size and colour after an actual asset save and reload");
            layout.activeTheme = savedTheme;
            yield return null; ApplyDesignerChanges(canvas);
            Check(UITheme.Current == savedTheme && accounts.fontSize == 21 && SameColor(accounts.color, individualColor),
                "Assigning another saved active theme switches the real open interface to that appearance");
        }
        finally
        {
            if (designerSettingsOpen) SettingsPresentation.CloseActive();
            accountsStyle.LocalAppearanceEnabled = false; accountsStyle.LocalAppearance = new UITextAppearance();
            apply.interactable = originalInteractable;
            EditorJsonUtility.FromJsonOverwrite(originalTheme, theme); EditorJsonUtility.FromJsonOverwrite(originalLayout, layout);
            EditorUtility.SetDirty(theme); AssetDatabase.SaveAssetIfDirty(theme);
            EditorUtility.SetDirty(layout); AssetDatabase.SaveAssetIfDirty(layout);
            File.WriteAllBytes(themeAssetPath, originalThemeBytes); File.WriteAllBytes(layoutAssetPath, originalLayoutBytes);
            AssetDatabase.ImportAsset(themeAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(layoutAssetPath, ImportAssetOptions.ForceUpdate);
            Selection.activeObject = originalSelection;
            fixtures.SetActive(false); UnityEngine.Object.Destroy(fixtures);
            ApplyDesignerChanges(canvas); Field<ConstructionPalette>(controller, "palette").Refresh(true);
            if (AssetDatabase.LoadMainAssetAtPath(savedThemePath) != null) AssetDatabase.DeleteAsset(savedThemePath);
            if (AssetDatabase.LoadMainAssetAtPath(textReadbackPath) != null) AssetDatabase.DeleteAsset(textReadbackPath);
            if (AssetDatabase.LoadMainAssetAtPath(layoutReadbackPath) != null) AssetDatabase.DeleteAsset(layoutReadbackPath);
        }
        yield return null; ApplyDesignerChanges(canvas);
        Check(UITheme.Current == theme, "The designer checks restore the original active theme and preserve the artist's existing font choices");
        inspector.Select(null);
    }

    static void ValidateFrameworkEdgeCases(Canvas canvas)
    {
        var list = new GameObject("Exact-fit list validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        list.transform.SetParent(canvas.transform, false);
        var listRect = (RectTransform)list.transform;
        listRect.anchorMin = listRect.anchorMax = listRect.pivot = new Vector2(.5f, .5f);
        listRect.sizeDelta = new Vector2(240, 160);
        list.GetComponent<Image>().color = Color.clear; UIThemeElement.Mark(list, UIThemeRole.Ignore);
        var viewportObject = new GameObject("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(list.transform, false);
        var viewport = (RectTransform)viewportObject.transform;
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;
        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        var content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1);
        content.sizeDelta = new Vector2(0, 160);
        var rows = new GameObject[6];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new GameObject("32-pixel row " + i, typeof(RectTransform));
            rows[i].transform.SetParent(content, false);
            var row = (RectTransform)rows[i].transform;
            row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(0, 1);
            row.anchoredPosition = new Vector2(0, -32 * i); row.sizeDelta = new Vector2(0, 32);
        }
        rows[5].SetActive(false);
        var scroll = list.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
        Canvas.ForceUpdateCanvases();
        ValidateScrollInput(scroll, false, "Five 32-pixel rows in an exactly 160-pixel viewport");
        Check(Near(content.rect.height, 160f, .05f), "An exactly fitting list does not acquire padding that invents overflow");
        ((RectTransform)rows[4].transform).sizeDelta = new Vector2(0, 33);
        var overflowState = UIOverflowScroll.Attach(scroll); overflowState.Refresh();
        Check(Near(content.rect.height, 161f, .05f) && overflowState.HasOverflow && scroll.vertical,
            "One extra pixel of real row content enables scrolling at the exact-fit boundary");
        ((RectTransform)rows[4].transform).sizeDelta = new Vector2(0, 32);
        ValidateScrollInput(scroll, false, "Exact-fit list after its extra pixel is removed");
        rows[5].SetActive(true);
        ValidateScrollInput(scroll, true, "A sixth real row in the 160-pixel viewport");
        rows[5].SetActive(false);
        ValidateScrollInput(scroll, false, "Exact-fit list after its extra row is removed");
        list.SetActive(false); UnityEngine.Object.Destroy(list);

        var corner = new GameObject("Corner frame validation", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        corner.transform.SetParent(canvas.transform, false);
        var cornerRect = (RectTransform)corner.transform;
        cornerRect.anchorMin = cornerRect.anchorMax = cornerRect.pivot = Vector2.zero;
        cornerRect.anchoredPosition = new Vector2(24, 210);
        UIFramedPanel.Attach(corner, true, 500);
        Capture("Native-Frame-Corner-600", corner, 1000, 600);
        corner.SetActive(false); UnityEngine.Object.Destroy(corner);
    }

    static IEnumerator ValidateCameraFollow(BuildModeController controller, CreatureAgent animal,
        VisitorAgent guest, ParkBuildingRecord building)
    {
        var camera = controller.GetComponent<IsometricCameraController>();
        Vector3 originalFocus = camera.SavedFocus;
        float originalYaw = camera.SavedYaw, originalZoom = camera.SavedZoom;
        var toolType = Field<object>(controller, "tool").GetType();
        Invoke(controller, "SelectTool", Enum.Parse(toolType, "None"));
        camera.RestorePose(originalFocus, originalYaw, 18f);
        camera.enabled = true;
        Check((bool)Invoke(controller, "TryInspectObject", animal.GetComponent<Collider>()),
            "Click routing recognizes a creature's actual collider");
        Check(camera.IsFollowing && camera.FollowingTarget == animal.transform,
            "Selecting a living creature attaches the camera to that creature");
        for (int i = 0; i < 8; i++) yield return null;
        Check(camera.SavedZoom < 18f - 1f, "Selecting a small creature smoothly zooms in from the overview");
        var animalViewport = CurrentCamera.WorldToViewportPoint(animal.GetComponent<Collider>().bounds.center);
        var creatureInfo = Field<CreatureInspection>(controller, "creatureInspection");
        float creatureViewportX = animalViewport.x;
        Check(animalViewport.z > 0 && animalViewport.x > .15f
            && animalViewport.x < InspectorLeft(Field<GameObject>(creatureInfo, "panel")) - .04f
            && Near(animalViewport.y, .5f, .08f),
            "The selected creature is centered in the park area left clear by its information panel");
        Check(Field<GameObject>(creatureInfo, "panel").activeSelf,
            "Camera focus preserves the selected creature's information panel");
        Capture("Camera-Follow-Creature", Field<GameObject>(creatureInfo, "panel"), 1280, 720);
        Vector3 oldPosition = animal.transform.position, oldFocus = camera.SavedFocus;
        animal.transform.position += new Vector3(4f, 0f, 2f);
        Physics.SyncTransforms();
        for (int i = 0; i < 6; i++) yield return null;
        var movedViewport = CurrentCamera.WorldToViewportPoint(animal.GetComponent<Collider>().bounds.center);
        Check(camera.IsFollowing && (camera.SavedFocus - oldFocus).sqrMagnitude > 9f
            && Near(movedViewport.x, creatureViewportX, .045f) && Near(movedViewport.y, animalViewport.y, .08f),
            "The attached camera follows the creature as it moves across the park; following=" + camera.IsFollowing
                + "; focus " + oldFocus + " -> " + camera.SavedFocus + "; viewport=" + movedViewport
                + "; expected x=" + creatureViewportX + "; original viewport=" + animalViewport
                + "; actor=" + animal.transform.position + "; collider=" + animal.GetComponent<Collider>().bounds.center);
        animal.transform.position = oldPosition;
        Physics.SyncTransforms();
        Check((bool)Invoke(controller, "TryInspectObject", guest.Actor.GetComponent<Collider>())
            && camera.FollowingTarget == guest.Actor.transform,
            "Selecting a visitor switches camera attachment to that visitor");
        var worker = controller.Personnel.Staff.First();
        Check((bool)Invoke(controller, "TryInspectObject", worker.Actor.GetComponent<Collider>())
            && camera.FollowingTarget == worker.Actor.transform,
            "Selecting a staff member follows the person rather than their workplace");
        var selection = UnityEngine.Object.FindObjectsByType<BuildingSelection>(FindObjectsSortMode.None)
            .Single(candidate => candidate.Record == building);
        Check((bool)Invoke(controller, "TryInspectObject", selection.GetComponent<Collider>())
            && camera.FollowingTarget == selection.transform,
            "Selecting a building moves camera attachment to that building");
        for (int i = 0; i < 6; i++) yield return null;
        var viewport = CurrentCamera.WorldToViewportPoint(selection.GetComponent<Collider>().bounds.center);
        float buildingLeft = InspectorLeft(Field<GameObject>(Field<BuildingInspection>(controller, "buildingInspection"), "panel"));
        Check(viewport.z > 0 && viewport.x > .15f && viewport.x < buildingLeft - .04f && Near(viewport.y, .5f, .08f),
            "The focused building remains visibly framed beside its information panel");
        ValidateSelectionBounds(selection.GetComponent<Collider>().bounds, buildingLeft);
        var previousKeyboard = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>("Camera follow validation keyboard");
        float oldTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 1;
            float oldYaw = camera.SavedYaw;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update();
            keyboard.MakeCurrent();
            // The batch editor has separate Editor and Player input-state buffers. Consume the
            // injected state in the same update before another loop swaps the active buffer.
            Invoke(camera, "Update");
            Check(camera.IsFollowing && !Near(camera.SavedYaw, oldYaw),
                "The player can rotate the view while it remains attached to a selected object; "
                    + CameraInputDiagnostic(camera, keyboard, controller, oldYaw));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            for (int i = 0; i < 4; i++) yield return null;
            ValidateSelectionBounds(selection.GetComponent<Collider>().bounds, buildingLeft);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update();
            keyboard.MakeCurrent(); Invoke(camera, "Update");
            Check(!camera.IsFollowing, "Manual WASD camera movement releases the selected-object attachment; "
                + CameraInputDiagnostic(camera, keyboard, controller, camera.SavedYaw));
        }
        finally
        {
            Time.timeScale = oldTimeScale; InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
        }
        Invoke(controller, "TryInspectObject", animal.GetComponent<Collider>());
        Invoke(controller, "SelectTool", Enum.Parse(toolType, "Demolish"));
        Check(!camera.IsFollowing, "Starting a construction tool releases the selected-object camera");
        Invoke(controller, "SelectTool", Enum.Parse(toolType, "None"));
        Invoke(controller, "TryInspectObject", animal.GetComponent<Collider>());
        Check(controller.CloseManagementWindow() && !camera.IsFollowing && !GameSaveMenu.IsOpen
            && !Field<GameObject>(creatureInfo, "panel").activeSelf,
            "The Escape close route releases the selected creature and inspector before opening Pause");
        Invoke(controller, "TryInspectObject", animal.GetComponent<Collider>());
        typeof(CreatureAgent).GetProperty("Alive").SetValue(animal, false);
        yield return null;
        Check(!camera.IsFollowing, "The camera releases a selected creature as soon as it dies");
        typeof(CreatureAgent).GetProperty("Alive").SetValue(animal, true);
        var disposable = new GameObject("Camera follow validation target");
        camera.Follow(disposable.transform, new Bounds(disposable.transform.position, Vector3.one));
        UnityEngine.Object.Destroy(disposable);
        yield return null;
        Check(!camera.IsFollowing, "Destroying a selected object safely clears the camera attachment");
        camera.Follow(animal.transform, new Bounds(animal.transform.position, Vector3.one));
        camera.FocusOn(originalFocus);
        Check(!camera.IsFollowing, "A location focus command releases object follow");
        camera.Follow(animal.transform, new Bounds(animal.transform.position, Vector3.one));
        camera.RestorePose(originalFocus, originalYaw, originalZoom);
        Check(!camera.IsFollowing && Near(camera.SavedZoom, originalZoom),
            "Restoring a saved camera pose releases follow and restores its saved zoom");
        camera.enabled = false;
        Field<BuildingInspection>(controller, "buildingInspection").Select(null);
        Field<VisitorPresentation>(controller, "visitorPresentation").Select(null);
        creatureInfo.Select(null);
    }

    static void ValidateSelectionBounds(Bounds bounds, float inspectorLeft)
    {
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var viewport = CurrentCamera.WorldToViewportPoint(bounds.center
                        + Vector3.Scale(bounds.extents, new Vector3(x, y, z)));
                    Check(viewport.z > 0 && viewport.x > -.02f && viewport.x < inspectorLeft + .02f
                        && viewport.y > -.02f && viewport.y < 1.02f,
                        "Every selected building corner stays inside the park view beside the inspector: " + viewport);
                }
    }

    static float InspectorLeft(GameObject panel)
    {
        var corners = new Vector3[4]; ((RectTransform)panel.transform).GetWorldCorners(corners);
        Camera input = CurrentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CurrentCanvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(input, corners[0]).x / Screen.width;
    }

    static string CameraInputDiagnostic(IsometricCameraController camera, Keyboard keyboard,
        BuildModeController controller, float oldYaw)
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var input = selected != null ? selected.GetComponent<InputField>() : null;
        return "following=" + camera.IsFollowing + "; yaw=" + oldYaw + " -> " + camera.SavedYaw
            + "; currentKeyboard=" + (Keyboard.current == keyboard) + "; Q=" + keyboard.qKey.isPressed
            + "; W=" + keyboard.wKey.isPressed + "; updateType=" + InputState.currentUpdateType
            + "; focusedInput=" + (input != null && input.isFocused) + "; selected=" + selected
            + "; pause=" + GameSaveMenu.IsOpen + "; settings=" + SettingsPresentation.IsOpen
            + "; management=" + controller.ManagementOpen + "; cameraEnabled=" + camera.enabled
            + "; delta=" + Time.unscaledDeltaTime;
    }

    static void ValidateMovementInstructions(GameObject settings)
    {
        var instructions = settings.GetComponentsInChildren<Text>().SingleOrDefault(text => text.name == "Movement keys");
        Check(instructions != null, "Movement instructions are available inside Settings");
        string keys = instructions.text.Replace(" ", "").ToLowerInvariant();
        Check(keys.Contains("wasd") && keys.Contains("arrow") && keys.Contains("q/e") && keys.Contains("mousewheel")
            && keys.Contains("esc"), "Settings explain movement, rotation, zoom and cancelling with the actual game controls");
        Check(instructions.preferredHeight <= instructions.rectTransform.rect.height + 2,
            "All movement instruction lines fit inside Settings; preferred " + instructions.preferredHeight
            + "; available " + instructions.rectTransform.rect.height);
    }

    static void ValidateCursor(bool exerciseLifecycle)
    {
        var cursors = UnityEngine.Object.FindObjectsByType<ExhibitionCursor>(FindObjectsSortMode.None);
        Check(cursors.Length == 1 && cursors[0].isActiveAndEnabled,
            "Exactly one active custom-cursor controller survives menu and park scene changes");
        var cursor = cursors[0];
        if (exerciseLifecycle)
        {
            cursor.enabled = false;
            Check(!ExhibitionCursor.IsApplied && ExhibitionCursor.CurrentTexture == null,
                "Disabling the custom cursor restores the system pointer");
            cursor.enabled = true;
        }
        cursor.ApplyThemeCursor();
        if (ExhibitionCursor.IsApplied)
            Check(ExhibitionCursor.CurrentTexture == UITheme.Current.cursorTexture
                && ExhibitionCursor.CurrentHotspot == UITheme.Current.cursorHotspot,
                "A focused game uses the configured cropped cursor and its click hotspot");
        else
            Check(!Application.isFocused && ExhibitionCursor.CurrentTexture == null,
                "An unfocused batch-mode game safely leaves the system pointer in control");
    }

    static void ValidateButtonIcon(Button button, UIIconKind kind, bool exerciseStates = false)
    {
        UIThemeApplicator.ApplyAll(CurrentCanvas);
        var icon = button.GetComponent<UIIcon>();
        Check(icon != null && icon.Kind == kind, "The " + button.name + " control has a meaningful " + kind + " icon");
        var set = UITheme.Current.Icon(kind);
        var image = icon.IconImage;
        Check(image != null && image.sprite != null && !image.raycastTarget && image.preserveAspect,
            "The " + button.name + " icon preserves its shape and does not block pointer input");
        var label = button.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.transform.parent == button.transform);
        if (icon.IconOnly)
        {
            Check(label == null || !label.isActiveAndEnabled,
                "The " + button.name + " HUD icon has no permanent text label");
            var backing = button.GetComponent<Image>();
            Check(backing != null && backing.color.a <= .01f && backing.raycastTarget,
                "The " + button.name + " standalone icon has an invisible input target without an extra box");
            Check(button.targetGraphic == image && image.rectTransform.rect.width >= 48 && image.rectTransform.rect.height >= 48,
                "The " + button.name + " standalone artwork fills a usable icon-sized target");
        }
        else
            Check(label != null && label.isActiveAndEnabled && label.rectTransform.offsetMin.x >= icon.LabelPadding - .5f,
                "The " + button.name + " menu label leaves room for its icon");
        if (!exerciseStates) return;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        icon.OnPointerUp(pointer); icon.OnPointerExit(pointer); icon.OnDeselect(new BaseEventData(EventSystem.current)); icon.Refresh();
        bool wasEnabled = button.interactable;
        button.interactable = true; icon.Refresh();
        Check(image.sprite == set.normal, "The " + button.name + " icon uses its normal artwork at rest");
        icon.OnPointerEnter(pointer); icon.Refresh();
        Check(image.sprite == set.highlighted, "The " + button.name + " icon responds to hover with imported artwork");
        icon.OnPointerDown(pointer); icon.Refresh();
        Check(image.sprite == set.pressed, "The " + button.name + " icon responds to pressing with imported artwork");
        button.interactable = false; icon.Refresh();
        Check(image.sprite == set.disabled, "The " + button.name + " icon has readable disabled artwork");
        button.interactable = wasEnabled;
        icon.OnPointerUp(pointer); icon.OnPointerExit(pointer); icon.OnDeselect(new BaseEventData(EventSystem.current)); icon.Refresh();
        Check(image.sprite == (wasEnabled ? set.normal : set.disabled),
            "The " + button.name + " icon returns to the correct state after interaction");
    }

    static void ValidateParkStatus(BuildModeController controller, bool refresh)
    {
        var status = Field<ParkStatusPresentation>(controller, "parkStatus");
        Check(status != null, "The park owns a bare funds-and-stars presentation");
        if (refresh) status.Refresh();
        var root = GameObject.Find("Park status");
        Check(root != null && root.activeInHierarchy, "Current funds and stars are visible in the park view");
        var rect = (RectTransform)root.transform;
        Check(rect.anchorMin == new Vector2(0, 1) && rect.anchorMax == new Vector2(0, 1)
            && rect.rect.width <= 360 && rect.rect.height <= 130,
            "The bare funds-and-stars display stays compact in the upper-left corner");
        Check(root.GetComponentsInChildren<Image>(true).Length == 0,
            "The upper-left status has no backplate, border or decorative money icon");
        var labels = root.GetComponentsInChildren<Text>().Where(text => text.isActiveAndEnabled).ToArray();
        var funds = labels.Single(text => text.name == "Park funds");
        Check(labels.Length == 1 && funds.text == controller.Economy.Balance.ToString("$#,##0;-$#,##0;$0", CultureInfo.InvariantCulture),
            "The funds display shows only a dollar amount from the actual balance, without a label or numeric star counter");
        Check(funds.preferredHeight <= funds.rectTransform.rect.height + 2,
            "The plain dollar balance fits inside its visible row");
        var stars = root.GetComponentsInChildren<StarRatingGraphic>().Single();
        Check(Near(Field<float>(stars, "rating"), controller.Visitors.Reputation.Stars),
            "The five bare stars reflect the actual park rating");
        var fundsBounds = new Vector3[4]; var starBounds = new Vector3[4];
        funds.rectTransform.GetWorldCorners(fundsBounds); stars.rectTransform.GetWorldCorners(starBounds);
        Check(starBounds.Max(point => point.y) <= fundsBounds.Min(point => point.y) + 1,
            "The rating stars sit immediately beneath the dollar balance");
    }

    static void ValidateMailIcon(Canvas canvas, bool unread)
    {
        var letter = FindButton(canvas.transform, "Letters");
        var symbol = letter.GetComponent<UIIcon>();
        Check(symbol != null && symbol.IconOnly && symbol.Kind == (unread ? UIIconKind.MailUnread : UIIconKind.Mail),
            "Mail remains a standalone icon when its notification state changes");
        var icon = symbol.IconImage;
        Sprite expected = unread ? UITheme.Current.envelopeUnread : UITheme.Current.envelope;
        Check(icon.sprite == expected && !icon.raycastTarget && icon.preserveAspect,
            unread ? "A new unread letter switches Mail to the requested highlighted notification icon"
                : "Reading all letters returns Mail to the requested normal parchment icon");
        Check(letter.GetComponentsInChildren<EnvelopeGraphic>(true).Length == 0,
            "Mail uses the imported artwork instead of the old drawn envelope");
        ValidateButtonIcon(letter, unread ? UIIconKind.MailUnread : UIIconKind.Mail);
    }

    static void ValidateGameplayChrome(Canvas canvas, ConstructionPalette palette)
    {
        var topButtons = palette.Root.GetComponentsInChildren<Button>()
            .Where(button => button.transform.parent == palette.Root.transform).ToArray();
        Check(topButtons.Select(button => button.name).SequenceEqual(new[] { "Building", "Park Management", "Encyclopedia", "Pause" }),
            "The HUD preserves the four main menu actions");
        var arc = new[] { FindButton(palette.Root.transform, "Building"), FindButton(palette.Root.transform, "Park Management"),
            FindButton(palette.Root.transform, "Encyclopedia"), FindButton(canvas.transform, "Letters"), FindButton(palette.Root.transform, "Pause") };
        var canvasRect = (RectTransform)canvas.transform;
        var centers = arc.Select(button =>
        {
            var rect = (RectTransform)button.transform;
            return (Vector2)canvasRect.InverseTransformPoint(rect.TransformPoint(rect.rect.center)) - canvasRect.rect.min;
        }).ToArray();
        for (int i = 0; i < arc.Length; i++)
        {
            var symbol = arc[i].GetComponent<UIIcon>();
            Check(symbol != null && symbol.IconOnly, "The " + arc[i].name + " HUD action is a standalone icon");
            Check(!arc[i].GetComponentsInChildren<Text>().Any(text => text.transform.parent == arc[i].transform && text.isActiveAndEnabled),
                "The " + arc[i].name + " arc icon has no permanent text label");
            var rect = (RectTransform)arc[i].transform;
            Check(rect.rect.width >= 54 && rect.rect.width <= 88 && rect.rect.height >= 54 && rect.rect.height <= 88,
                "The " + arc[i].name + " icon has a clear compact hit area");
            Check(centers[i].x > 0 && centers[i].x < canvasRect.rect.width * .36f && centers[i].y > 0 && centers[i].y < 230,
                "The " + arc[i].name + " icon stays together with the lower-left HUD arc");
            if (i > 0) Check(centers[i].x > centers[i - 1].x + 50,
                "The HUD arc keeps adjacent icons separate and ordered");
        }
        Check(Mathf.Abs(centers[0].y - centers[1].y) <= 20 && centers[0].y > centers[4].y + 75
            && centers[2].y < centers[1].y && centers[3].y < centers[2].y && centers[4].y < centers[3].y,
            "The standalone icons form the requested descending lower-left curve");
        Check(!palette.IsOpen, "The Building categories start folded away");
        var visibleText = canvas.GetComponentsInChildren<Text>().Where(text => text.isActiveAndEnabled).ToArray();
        Check(!canvas.GetComponentsInChildren<Transform>().Any(item => item.gameObject.activeInHierarchy && item.name == "Park overview"),
            "The old top-left finance and visitor HUD is removed");
        Check(!visibleText.Any(text => text.text.IndexOf("WASD", StringComparison.OrdinalIgnoreCase) >= 0),
            "Movement instructions no longer cover the park view");
        Check(!visibleText.Any(text => text.text.Contains("Choose a construction category below")),
            "The generic construction-category prompt no longer covers the park view");
        Check(!visibleText.Any(text => text.text.Contains("Vegetation art:") || text.text.Contains("LUX ART STUDIOS")),
            "The vegetation credit is no longer displayed over the park view");
        var pauseButtons = canvas.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy
            && button.GetComponentsInChildren<Text>().Any(text => text.text == "Pause")).ToArray();
        Check(pauseButtons.Length == 1 && pauseButtons[0].transform.parent == palette.Root.transform,
            "Pause is reachable only from its new bottom-toolbar position");
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
            Canvas.ForceUpdateCanvases(); RefreshResponsiveInspectors();
            UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
            // New dialogs can be created and captured within this same editor callback.
            // Populate CanvasRenderer absolute depths before asking GraphicRaycaster to sort them.
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            foreach (var frameImage in focus.GetComponentsInChildren<Image>())
            {
                if (frameImage.sprite == null || !frameImage.isActiveAndEnabled) continue;
                string spriteName = frameImage.sprite.name;
                if (spriteName != "Steampunk_UI_Frames_1_2" && spriteName != "Steampunk_UI_Dialogue_2_0"
                    && spriteName != "Steampunk_UI_Dialogue_1_1") continue;
                ValidateFrameProportions(frameImage, name + " at " + width + "x" + height);
                CheckOnScreen(frameImage.rectTransform, camera, width, height, name + "/" + frameImage.name);
            }
            foreach (var button in focus.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>() == null) CheckOnScreen((RectTransform)button.transform, camera, width, height, name + "/" + button.name);
                var graphic = button.targetGraphic as Image;
                Check(graphic != null && graphic.sprite != null && AssetDatabase.GetAssetPath(graphic.sprite).StartsWith("Assets/Mythologicum/UI/", StringComparison.Ordinal),
                    "The " + name + " button uses integrated artwork: " + button.name);
                foreach (var label in button.GetComponentsInChildren<Text>()) Check(label.font != null, "A themed button retains readable text: " + button.name);
            }
            foreach (var icon in focus.GetComponentsInChildren<UIIcon>())
            {
                var set = UITheme.Current.Icon(icon.Kind);
                var graphic = icon.IconImage;
                Check(set != null && graphic != null && graphic.enabled && !graphic.raycastTarget && graphic.preserveAspect,
                    "The " + name + " icon is visible, keeps its shape and leaves clicks to its control: " + icon.name);
                Check(graphic.sprite == set.normal || graphic.sprite == set.highlighted || graphic.sprite == set.pressed || graphic.sprite == set.disabled,
                    "The " + name + " icon displays a valid imported state: " + icon.name);
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
            RefreshResponsiveInspectors(); UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
        }
    }
    static void RefreshResponsiveInspectors()
    {
        // Exporting at another resolution changes the root canvas within this same editor
        // callback. Run the production sizing path before rendering instead of waiting a frame.
        var canvas = CurrentCanvas;
        foreach (var frame in canvas.GetComponentsInChildren<UIFramedPanel>()) frame.Refresh();
        Canvas.ForceUpdateCanvases();
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
        if (controller != null)
        {
            Field<CreatureInspection>(controller, "creatureInspection")?.Refresh();
            Field<BuildingInspection>(controller, "buildingInspection")?.Refresh();
            Field<VisitorPresentation>(controller, "visitorPresentation")?.Refresh();
        }
        Canvas.ForceUpdateCanvases();
        foreach (var scroll in canvas.GetComponentsInChildren<ScrollRect>()) UIOverflowScroll.Attach(scroll).Refresh();
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

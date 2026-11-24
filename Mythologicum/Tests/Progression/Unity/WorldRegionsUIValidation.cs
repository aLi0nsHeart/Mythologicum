using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Copy into the disposable validation project; never run against the player's project identity.</summary>
[InitializeOnLoad]
public static class WorldRegionsUIValidation
{
    const string Running = "WorldRegionsUIValidation.running";
    static bool pending;
    static int checks, captures;
    static double readyAt, deadline;
    static IEnumerator steps;
    static string Result => Path.Combine(Directory.GetCurrentDirectory(), "WorldRegionsUIValidationResults.txt");
    static string Captures => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "WorldRegionsUIValidation");
    static Canvas CurrentCanvas => SceneTransition.GameplayCanvas;
    static Camera CurrentCamera => UnityEngine.Object.FindFirstObjectByType<Camera>();

    static WorldRegionsUIValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); pending = true;
            readyAt = EditorApplication.timeSinceStartup + 1; deadline = readyAt + 180;
        };
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "WorldRegionsUIValidation";
        Directory.CreateDirectory(Captures);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }

    static string RectValues(Rect value)=>string.Format(CultureInfo.InvariantCulture,
        "(x={0:R}, y={1:R}, width={2:R}, height={3:R})",value.x,value.y,value.width,value.height);

    static void Tick()
    {
        if (!pending || !Application.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("World region UI validation timed out");
            if (SceneTransition.HasFailed) throw new Exception("Production scene preparation failed");
            if (SceneTransition.IsLoading) return;
            if (steps == null) steps = Run();
            if (steps.MoveNext()) readyAt = EditorApplication.timeSinceStartup + .15;
            else
            {
                pending = false;
                File.WriteAllText(Result, "PASS: " + checks + " world-region UI checks, " + captures + " GPU captures.");
                EditorApplication.Exit(0);
            }
        }
        catch (Exception error)
        {
            pending = false; Debug.LogException(error);
            File.WriteAllText(Result, "FAIL after " + checks + " checks: " + error);
            EditorApplication.Exit(1);
        }
    }

    static void Check(bool value, string why)
    { checks++; if (!value) throw new Exception(why); Debug.Log("WORLD REGIONS UI PASS: " + why); }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static Button FindButton(Transform owner, string name) => owner.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
    static List<KeyValuePair<ExpeditionRegion, Button>> Pins(ProgressionPresentation presentation) => Field<List<KeyValuePair<ExpeditionRegion, Button>>>(presentation, "pins");

    static IEnumerator Run()
    {
        while (CurrentCanvas == null || GameObject.Find("Main menu") == null) yield return null;
        Check(Application.companyName == "MythologicumValidation" && Application.productName == "WorldRegionsUIValidation",
            "Validation uses its own save and preference identity");
        Check(ParkSaveStore.SavePath.Contains("WorldRegionsUIValidation"), "Validation never uses the player's save location");
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "UI validation has a real graphics device");
        GameSettings.ResetDefaults();
        yield return null;
        Click(FindButton(GameObject.Find("Main menu").transform, "New Game"));
        yield return null;
        Check(NewGameSetup.IsOpen, "The production New Game action opens park setup");
        Click(FindButton(GameObject.Find("New park setup").transform, "Create park"));
        yield return null;
        while (UnityEngine.Object.FindFirstObjectByType<BuildModeController>()?.Progression == null) yield return null;
        Check(SceneManager.GetActiveScene().name == "GameScene", "The production setup loads the park scene");
        var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
        controller.enabled = false; controller.GetComponent<IsometricCameraController>().enabled = false; Time.timeScale = 0;
        var progression = controller.Progression;
        ValidateCatalog(progression);
        checks += WorldRegionMigrationNativeChecks.RunChecks();
        var home = PrepareCentre(controller);
        var presentation = Field<ProgressionPresentation>(controller, "management");
        typeof(BuildModeController).GetMethod("OpenExpeditions", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, new object[] { home });
        yield return null;
        var map = Field<GameObject>(presentation, "map");
        Check(map.activeInHierarchy && controller.ManagementOpen, "The real expedition centre opens the map modal");
        Check(Field<string>(presentation, "selectedRegion") == "north-america", "A new map starts with North America selected");
        Check(map.GetComponentsInChildren<Image>().Single(image => image.name == "World map").sprite == UITheme.Current.expeditionMapBackground,
            "World orientation retains the configured parchment artwork");
        ValidateChoices(presentation);
        foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
        {
            Loc.SetLanguage(language); presentation.Refresh(); yield return null;
            ValidatePreviews(presentation, controller, language.ToString());
            Check(map.GetComponentsInChildren<Text>().All(label => !label.text.Contains("expedition.")
                && !label.text.Contains("park.progression_presentation.")), language + " leaves no untranslated expedition localization IDs on screen");
        }
        Loc.SetLanguage(GameLanguage.GERMAN); presentation.Refresh(); yield return null;
        foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
        {
            var capture = Capture("World-German-" + size.x + "x" + size.y, map, presentation, controller, size.x, size.y);
            while (capture.MoveNext()) yield return null;
        }
        Loc.SetLanguage(GameLanguage.ENGLISH); presentation.Refresh(); yield return null;
        ValidatePurchasesAndDispatch(presentation, controller);
    }

    static void ValidateCatalog(ParkProgression progression)
    {
        var asset = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
        Check(asset != null, "Unity imports the editable default progression asset");
        var expected = WorldRegionCatalog.CreateDefaults(); var imported = asset.rules.regions;
        Check(expected.Length == 23 && imported.Length == 23 && progression.Rules.regions.Length == 23,
            "The imported asset and live progression both contain twenty-three regions");
        Check(imported.Select(region => region.id).SequenceEqual(expected.Select(region => region.id)), "The imported asset keeps canonical region IDs and order");
        foreach (var reference in expected)
        {
            var actual = imported.Single(region => region.id == reference.id);
            Check(actual.continent == reference.continent && actual.name == reference.name
                && actual.species.SequenceEqual(reference.species) && (actual.mapPosition - reference.mapPosition).sqrMagnitude < .000001f,
                "Imported names, grouping, position and species agree with the catalog for " + actual.id);
        }
    }

    static ParkBuildingRecord PrepareCentre(BuildModeController controller)
    {
        foreach (string name in new[] { "Enrichment trees", "Natural rocks" })
        {
            var root = GameObject.Find(name);
            if (root != null) foreach (var target in root.GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        }
        var grid = Field<SquareBuildGrid>(controller, "grid");
        controller.Terrain.ImportHeights(new float[(grid.Width + 1) * (grid.Depth + 1)]); Physics.SyncTransforms();
        var construction = Field<BuildingConstruction>(controller, "buildingConstruction");
        foreach (var item in new[] { new KeyValuePair<BuildingKind, int>(BuildingKind.ParkEntrance, 5), new KeyValuePair<BuildingKind, int>(BuildingKind.ExpeditionCentre, 8) })
        {
            var definition = JsonUtility.FromJson<BuildingDefinition>(JsonUtility.ToJson(BuildingDefinition.Defaults().Single(value => value.kind == item.Key)));
            definition.width = definition.depth = 1; definition.height = 2; definition.entranceColumn = 0; definition.visualPrefab = null;
            Check(construction.TryPlace(new BuildingFootprint(definition, new Vector2Int(item.Value, 7), 0)), "Fixture places the actual " + item.Key);
        }
        var paths = Field<PathConstruction>(controller, "paths");
        for (int x = 5; x <= 8; x++) Check(paths.TryPlace(new Vector2Int(x, 6)), "Fixture connects the centre at path " + x);
        construction.Network.Refresh();
        var centre = construction.Network.Records.Single(record => record.Footprint.Definition.kind == BuildingKind.ExpeditionCentre);
        Check(centre.IsOpen, "The expedition centre uses a real entrance-connected path"); return centre;
    }

    static void ValidateChoices(ProgressionPresentation presentation, string context = "initial overview")
    {
        var area = Field<RectTransform>(presentation, "mapArea");
        var graphic = Field<ExpeditionMapGraphic>(presentation, "mapGraphic");
        var map = Field<GameObject>(presentation, "map");
        Check(graphic.Viewport == new Rect(0, 0, 1, 1), "The expedition map displays one complete world viewport");
        Check(area.GetComponent<RectMask2D>() != null && graphic.maskable && graphic.transform.parent == area,
            "The map's silhouettes and pins share the same clipped parchment content area");
        var pins = Pins(presentation).Where(pair => pair.Value.gameObject.activeInHierarchy).ToArray();
        Check(pins.Length == 23 && pins.Select(pair => pair.Key.id).Distinct().Count() == 23
            && new HashSet<string>(pins.Select(pair => pair.Key.id)).SetEquals(WorldRegionCatalog.RegionIds),
            "All twenty-three unique regions are visible simultaneously on the world map");
        Check(pins.All(pair => pair.Value.GetComponentInParent<ScrollRect>() == null),
            "Every world region can be chosen without scrolling");
        var pinButtons = new HashSet<Button>(pins.Select(pair => pair.Value));
        var missionControls = map.GetComponentsInChildren<Button>().Where(button => !pinButtons.Contains(button)).ToArray();
        for (int i = 0; i < pins.Length; i++)
        {
            var region = pins[i].Key; var button = pins[i].Value; var rect = (RectTransform)button.transform;
            Check(button.GetComponentInChildren<Text>().text == (i + 1).ToString(), "The global map has readable unique marker " + (i + 1));
            Check(button.interactable, "Every region remains selectable for preview: " + region.id);
            Check(Fits(rect, area), "The full " + region.id + " hitbox stays inside the world map");
            var overlaps = pins.Take(i).Where(other => Bounds(rect).Overlaps(Bounds((RectTransform)other.Value.transform))).ToArray();
            Check(overlaps.Length == 0,
                context + ": the " + region.id + " hitbox does not overlap another region, including the European cluster"
                + (overlaps.Length == 0 ? "" : "; Screen=" + Screen.width + "x" + Screen.height
                    + "; map=" + RectDiagnostic(area) + "; pin=" + region.id + " " + RectDiagnostic(rect)
                    + "; overlaps=" + string.Join(" | ", overlaps.Select(other => other.Key.id + " " + RectDiagnostic((RectTransform)other.Value.transform)))));
            Check(missionControls.All(other => !Bounds(rect).Overlaps(Bounds((RectTransform)other.transform))),
                "Mission controls leave the " + region.id + " map hitbox unobstructed");
            CheckClickable(button);
        }
        Check(!Bounds(area).Overlaps(Bounds(Field<Text>(presentation, "regionText").rectTransform))
            && !Bounds(area).Overlaps(Bounds(Field<Text>(presentation, "missionPreview").rectTransform)),
            "The full world drawing area is separate from the region and mission details");
    }

    static void ValidatePreviews(ProgressionPresentation presentation, BuildModeController controller, string context)
    {
        var progression = controller.Progression;
        float funds = controller.Economy.Balance, expenses = controller.Economy.Expenses;
        var permits = progression.Rules.regions.ToDictionary(region => region.id, region => progression.HasAccess(region.id));
        int missions = progression.Missions.Count;
        var speciesName = Field<Func<string, string>>(presentation, "speciesName");
        foreach (var pair in Pins(presentation))
        {
            var region = pair.Key;
            Click(pair.Value);
            Check(Field<string>(presentation, "selectedRegion") == region.id, context + " clicks the actual region ID " + region.id);
            string details = Field<Text>(presentation, "regionText").text;
            Check(!string.IsNullOrWhiteSpace(region.DisplayName) && region.DisplayName != region.name && details.Contains(region.DisplayName),
                context + " displays the full localized region name " + region.id);
            CheckTextFits(Field<Text>(presentation, "regionText"), context + " region details for " + region.id);
            CheckTextFits(Field<Text>(presentation, "missionPreview"), context + " mission preview for " + region.id);
            var hover = pair.Value.GetComponent<UIHoverTooltip>();
            Check(hover != null && hover.Caption == region.DisplayName, context + " gives marker " + region.id + " its full localized hover name");
            if (progression.RegionHasCreatures(region.id))
                Check(region.species.All(species => details.Contains(speciesName(species))), context + " details list the assigned creatures for " + region.id);
            else
            {
                Check(!Field<Button>(presentation, "buyRegion").interactable && !Field<Button>(presentation, "send").interactable,
                    context + " previews future region " + region.id + " with purchase and dispatch disabled");
                Check(details.Contains(Loc.Text("park.progression_presentation.no_creature_species_assigned_yet_this_location_cannot_be_p")),
                    context + " explains why future region " + region.id + " has no expedition yet");
            }
        }
        Check(controller.Economy.Balance == funds && controller.Economy.Expenses == expenses && progression.Missions.Count == missions
            && permits.All(pair => progression.HasAccess(pair.Key) == pair.Value),
            context + " previews all twenty-three regions without spending, granting permits or dispatching missions");
        Check(Field<ExpeditionMapGraphic>(presentation, "mapGraphic").Viewport == new Rect(0, 0, 1, 1)
            && Pins(presentation).All(pair => pair.Value.gameObject.activeInHierarchy),
            context + " selections keep all twenty-three regions in the single world overview");
    }

    static void ValidatePurchasesAndDispatch(ProgressionPresentation presentation, BuildModeController controller)
    {
        var progression = controller.Progression; var economy = controller.Economy;
        economy.UnlimitedFunds = false; economy.Earn(1000000); presentation.Refresh();
        if (progression.AvailableTeams == 0)
        {
            float before = economy.Balance, price = progression.NextTeamCost;
            int teams = progression.Teams;
            Click(Field<Button>(presentation, "buyTeam"));
            Check(progression.Teams == teams + 1 && progression.AvailableTeams > 0
                && Mathf.Abs(before - economy.Balance - price) < .01f,
                "The real team-purchase button supplies the first team for its displayed price");
        }
        Check(progression.AvailableTeams > 0, "The new park has a real expedition team available for dispatch validation");
        foreach (var pair in Pins(presentation).Where(pair => progression.RegionHasCreatures(pair.Key.id)))
        {
            var region = pair.Key; Click(pair.Value);
            var buy = Field<Button>(presentation, "buyRegion");
            if (!progression.HasAccess(region.id))
            {
                float before = economy.Balance;
                Click(buy);
                Check(progression.HasAccess(region.id) && Mathf.Abs(before - economy.Balance - region.accessCost) < .01f,
                    "The real purchase button buys only " + region.id + " and charges its permit price");
            }
            Check(!buy.interactable, "The purchased " + region.id + " permit cannot be bought twice");
            var difficulty = Field<List<KeyValuePair<ExpeditionDifficulty, Button>>>(presentation, "difficulties")
                .First(choice => progression.Eligible(region.id, choice.Key.id).Count > 0);
            Click(difficulty.Value);
            float balance = economy.Balance; int count = progression.Missions.Count, teams = progression.AvailableTeams;
            Click(Field<Button>(presentation, "send"));
            var mission = progression.Missions.Last();
            Check(progression.Missions.Count == count + 1 && mission.region == region.id && mission.difficulty == difficulty.Key.id
                && mission.teams == 1 && progression.AvailableTeams == teams - 1
                && Mathf.Abs(balance - economy.Balance - difficulty.Key.costPerTeam) < .01f,
                "The real dispatch button sends one team to the selected canonical region " + region.id + " for the displayed difficulty price");
            progression.Tick(mission.remaining + 1); presentation.Refresh();
            Check(progression.Missions.Count == count && progression.AvailableTeams == teams,
                "The ordinary mission completion returns the team after " + region.id);
        }
        foreach (var pair in Pins(presentation).Where(pair => !progression.RegionHasCreatures(pair.Key.id)))
        {
            float before = economy.Balance; Click(pair.Value);
            Check(!Field<Button>(presentation, "buyRegion").interactable && !Field<Button>(presentation, "send").interactable
                && !progression.HasAccess(pair.Key.id) && economy.Balance == before,
                "Even a funded park cannot purchase or dispatch to future region " + pair.Key.id);
        }
    }

    static Rect Bounds(RectTransform rect)
    {
        var camera = CurrentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CurrentCanvas.worldCamera;
        return ScreenBounds(rect, camera);
    }
    static Rect WorldBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y), corners.Max(point => point.x), corners.Max(point => point.y));
    }
    static string RectDiagnostic(RectTransform rect)
    {
        var camera = CurrentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CurrentCanvas.worldCamera;
        return "world=" + RectValues(WorldBounds(rect)) + ", screen=" + RectValues(ScreenBounds(rect, camera))
            + ", rect=" + RectValues(rect.rect) + ", sizeDelta=" + rect.sizeDelta.ToString("R")
            + ", anchorMin=" + rect.anchorMin.ToString("R") + ", anchorMax=" + rect.anchorMax.ToString("R")
            + ", pivot=" + rect.pivot.ToString("R") + ", anchoredPosition=" + rect.anchoredPosition.ToString("R")
            + ", localScale=" + rect.localScale.ToString("R");
    }
    static void CheckTextFits(Text text, string context)
    {
        Check(text.preferredHeight <= text.rectTransform.rect.height + 1f,
            context + " remains readable in its panel (required " + text.preferredHeight + ", available " + text.rectTransform.rect.height + ")");
    }
    static bool Fits(RectTransform child, RectTransform parent)
    {
        var inner = Bounds(child); var outer = Bounds(parent);
        return inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;
    }
    static void Click(Button button)
    {
        Check(button != null && button.gameObject.activeInHierarchy && button.interactable, "A visible enabled button handles " + button.name);
        var target = CheckClickable(button);
        var data = new PointerEventData(EventSystem.current) { position = PointerCentre(button), button = PointerEventData.InputButton.Left };
        ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerClickHandler);
    }
    static Vector2 PointerCentre(Button button)
    {
        var camera = CurrentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : CurrentCanvas.worldCamera;
        var rect = (RectTransform)button.transform;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
    static GameObject CheckClickable(Button button)
    {
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = PointerCentre(button) }, hits);
        var hit = hits.FirstOrDefault(value => value.module is GraphicRaycaster);
        Check(hit.gameObject != null && hit.gameObject.GetComponentInParent<Button>() == button, "Artwork leaves the real pointer target reachable: " + button.name);
        return hit.gameObject;
    }

    static IEnumerator Capture(string name, GameObject map, ProgressionPresentation presentation, BuildModeController controller, int width, int height)
    {
        var camera = CurrentCamera; var canvas = CurrentCanvas;
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var panel in canvas.GetComponentsInChildren<UIFramedPanel>()) panel.Refresh();
            UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            ValidateChoices(presentation, name + " capture " + width + "x" + height);
            ValidatePreviews(presentation, controller, width + "x" + height);
            var area = Field<RectTransform>(presentation, "mapArea");
            var areaPixels = ScreenBounds(area, camera);
            float scale = Mathf.Min(width / 1280f, height / 720f);
            Check(areaPixels.width >= 600f * scale && areaPixels.height >= 300f * scale,
                "The usable world drawing is substantially larger than the former 330x151 map at " + width + "x" + height + ": " + RectValues(areaPixels));
            Check(areaPixels.xMin >= 0 && areaPixels.yMin >= 0 && areaPixels.xMax <= width && areaPixels.yMax <= height,
                "The entire world drawing stays on screen at " + width + "x" + height);
            var tooltipButton = Pins(presentation).Single(pair => pair.Key.id == "western-europe").Value;
            Click(tooltipButton);
            var hover = tooltipButton.GetComponent<UIHoverTooltip>();
            var hoverEvent = new PointerEventData(EventSystem.current) { position = PointerCentre(tooltipButton) };
            ExecuteEvents.Execute(tooltipButton.gameObject, hoverEvent, ExecuteEvents.pointerEnterHandler);
            double tooltipDeadline = EditorApplication.timeSinceStartup + 3;
            while (!hover.IsVisible && EditorApplication.timeSinceStartup < tooltipDeadline) yield return null;
            Check(hover.IsVisible, "A real pointer hover reveals the full Western Europe name at " + width + "x" + height);
            CheckClickable(tooltipButton);
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            var graphic=map.GetComponentInChildren<ExpeditionMapGraphic>();
            Check(graphic.canvasRenderer.hasRectClipping && !graphic.canvasRenderer.cull,
                "The GPU world map is visible and clipped to its parchment viewport for " + name);
            var mesh=graphic.canvasRenderer.GetMesh();
            Check(mesh!=null && mesh.vertexCount>0 && mesh.vertices.All(vertex=>
            {
                var local=graphic.rectTransform.rect;
                return vertex.x>=local.xMin-.01f && vertex.x<=local.xMax+.01f
                    && vertex.y>=local.yMin-.01f && vertex.y<=local.yMax+.01f;
            }), "The rendered land stays inside the full world drawing for " + name);
            var evidence = new StringBuilder();
            evidence.AppendLine("Resolution: " + width + "x" + height);
            evidence.AppendLine("Language: " + Loc.Language);
            evidence.AppendLine("World viewport: " + RectValues(graphic.Viewport));
            evidence.AppendLine("World drawing screen bounds: " + RectValues(areaPixels));
            evidence.AppendLine("Selected region: " + Field<string>(presentation, "selectedRegion"));
            evidence.AppendLine("Hovered region caption: " + hover.Caption);
            foreach (var pair in Pins(presentation))
                evidence.AppendLine(pair.Key.id + ": " + RectValues(ScreenBounds((RectTransform)pair.Value.transform, camera)));
            foreach (var button in map.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>() != null && button.name != "Content scrollbar")
                {
                    var scroll = button.GetComponentInParent<ScrollRect>();
                    Check(Fits((RectTransform)button.transform, scroll.viewport), "The full region button is visible at " + width + "x" + height + ": " + button.name);
                }
                foreach (var corner in Corners((RectTransform)button.transform))
                {
                    var pixel = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    Check(pixel.x >= -2 && pixel.x <= width + 2 && pixel.y >= -2 && pixel.y <= height + 2, "The control stays on screen at " + width + "x" + height + ": " + button.name);
                }
                var label = button.GetComponentInChildren<Text>();
                Check(label != null && label.font != null && !string.IsNullOrWhiteSpace(label.text), "The visible button has a readable label: " + button.name);
                Check(button.GetComponent<Image>().sprite != null, "The shared theme supplies button artwork: " + button.name);
            }
            var tooltip = Field<RectTransform>(hover, "popup");
            Check(Fits(tooltip, (RectTransform)canvas.transform), "The full localized hover name stays inside the canvas for " + name);
            evidence.AppendLine("Hover popup screen bounds: " + RectValues(ScreenBounds(tooltip, camera)));
            RenderTexture.active = target; image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            var colors = image.GetPixels(); double mean = colors.Average(color => (double)color.grayscale);
            Check(mean > .015 && mean < .98, "GPU capture contains rendered artwork for " + name);
            Check(colors.Count(color => color.r > .9f && color.b > .9f && color.g < .1f) < colors.Length / 100, "GPU capture has no significant missing-shader magenta for " + name);
            File.WriteAllBytes(Path.Combine(Captures, name + ".png"), image.EncodeToPNG());
            File.WriteAllText(Path.Combine(Captures, name + "-bounds.txt"), evidence.ToString());
            hover.Hide(); captures++;
        }
        finally
        {
            camera.targetTexture = oldTarget; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
            RenderTexture.active = oldActive; if (image != null) UnityEngine.Object.DestroyImmediate(image);
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
            Canvas.ForceUpdateCanvases(); foreach (var panel in canvas.GetComponentsInChildren<UIFramedPanel>()) panel.Refresh();
            UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases();
        }
    }
    static Rect ScreenBounds(RectTransform rect, Camera camera)
    {
        var points = Corners(rect).Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner)).ToArray();
        return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
    }
    static Vector3[] Corners(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return corners; }
}

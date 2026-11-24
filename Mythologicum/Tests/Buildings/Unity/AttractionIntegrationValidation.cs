using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Research, prepared-park construction, inspection and actual scene save/load. Disposable project only.</summary>
[InitializeOnLoad]
public static class AttractionIntegrationValidation
{
    private const string Running = "AttractionIntegrationValidation.running";
    private static IEnumerator steps;
    private static int checks;
    private static double began;
    private static string slot;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(), "AttractionIntegrationResults.txt");
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; Debug.Log("ATTRACTION PASS: " + message); }
    static AttractionIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); began = EditorApplication.timeSinceStartup; checks = 0; steps = Run();
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation"; PlayerSettings.productName = "AttractionIntegrationValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity"); EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (steps == null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup - began > 240 || SceneTransition.HasFailed) throw new Exception("Attraction scene preparation failed or timed out.");
            if (steps.MoveNext()) return;
            steps = null; Cleanup(); File.WriteAllText(Result, "PASS: " + checks + " attraction data, models, windows, research, prices, inspection and actual scene save/load checks."); EditorApplication.Exit(0);
        }
        catch (Exception error)
        { steps = null; Cleanup(); Debug.LogException(error); File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>() == null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            { width = 50, depth = 50, relief = 0, seed = 942551, forestDensity = 0, featureSize = 28 });
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady != true) yield return null;
        var park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        Call(park, "SelectTool", Enum.ToObject(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), 0));
        foreach (var name in new[] { "Enrichment trees", "Natural rocks" })
            foreach (var target in GameObject.Find(name).GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var grid = Field<SquareBuildGrid>(park, "grid"); var buildings = Field<BuildingConstruction>(park, "buildingConstruction");
        var paths = Field<PathConstruction>(park, "paths"); var fences = Field<FenceConstruction>(park, "fences");
        var creatures = Field<CreatureWorld>(park, "creatureWorld");
        var definitions = Field<BuildingDefinition[]>(park, "buildingCatalog"); park.Economy.UnlimitedFunds = true;
        ParkBuildingRecord Place(BuildingKind kind, Vector2Int cell)
        {
            var definition = definitions.Single(item => item.kind == kind).Copy(); definition.width = definition.depth = 1; definition.entranceColumn = 0;
            var footprint = new BuildingFootprint(definition, cell, 0);
            Check(buildings.TryPlace(footprint), "A real attraction fixture building is constructible: " + kind);
            return buildings.Network.Records.Last();
        }
        Place(BuildingKind.ParkEntrance, new Vector2Int(2, 2));
        for (int x = 1; x < 40; x++) Check(paths.TryPlace(new Vector2Int(x, 5)), "The attraction service road is clear");
        for (int y = 1; y < 5; y++) paths.TryPlace(new Vector2Int(1, y));
        Check(paths.TryPlace(buildings.Network.Entrance.Footprint.AccessCell), "The public road connects to the main entrance door");
        var research = Place(BuildingKind.ResearchCentre, new Vector2Int(34, 6)); buildings.Network.Refresh();
        Check(buildings.Network.Entrance.IsOpen && research.IsOpen, "Entrance and laboratory have actual connected access paths");
        Check(park.Personnel.TryHire(research), "A hired researcher can unlock the attractions");
        Check(!park.Progression.StartResearch(research, "petting-zoo"), "Petting research requires the attraction foundation first");
        foreach (string technology in new[] { "attractions", "petting-zoo", "viewing-tower", "viewing-window", "balloon-ride", "predator-feeding-gallery", "trickster-theatre", "cabinet-of-curiosities", "expedition-pavilion", "clockwork-carousel" })
        {
            Check(!park.Progression.Completed(technology) && park.Progression.StartResearch(research, technology), "The real research system starts " + technology);
            park.Progression.AdvanceResearch(research, float.MaxValue);
            Check(park.Progression.Completed(technology), "The real research system unlocks " + technology);
        }
        for (int x = 3; x < 9; x++) { fences.TryPlace(new FenceEdge(new Vector2Int(x, 7), true), FenceKind.Heavy); fences.TryPlace(new FenceEdge(new Vector2Int(x, 13), true), FenceKind.Heavy); }
        for (int y = 7; y < 13; y++) { fences.TryPlace(new FenceEdge(new Vector2Int(3, y), false), FenceKind.Heavy); fences.TryPlace(new FenceEdge(new Vector2Int(9, y), false), FenceKind.Heavy); }
        var pettingDefinition = definitions.Single(item => item.kind == BuildingKind.PettingZoo).Copy();
        typeof(BuildModeController).GetField("selectedBuilding", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(park, pettingDefinition);
        var pettingEdge = new FenceEdge(new Vector2Int(4, 7), true);
        BuildingFootprint snapped = default;
        foreach (var candidate in new[]
        {
            new { Edge = pettingEdge, Outside = new Vector2Int(4, 6) },
            new { Edge = new FenceEdge(new Vector2Int(4, 13), true), Outside = new Vector2Int(4, 13) },
            new { Edge = new FenceEdge(new Vector2Int(3, 9), false), Outside = new Vector2Int(2, 9) },
            new { Edge = new FenceEdge(new Vector2Int(9, 9), false), Outside = new Vector2Int(9, 9) }
        })
        {
            var arguments = new object[] { candidate.Edge, null };
            Check((bool)Call(park, "TrySnapPettingEntrance", arguments), "The placement tool resolves a secure enclosure on every fence side");
            var candidateFootprint = (BuildingFootprint)arguments[1];
            Check(candidateFootprint.IsFenceMounted && candidateFootprint.AccessCell == candidate.Outside
                && candidateFootprint.RearEdge.Equals(candidate.Edge), "Snapping automatically faces the public entrance out of the pen");
            Check(!candidateFootprint.Cells().Any(), "The fence-mounted entrance has no ordinary building footprint");
            if (candidate.Edge.Equals(pettingEdge)) snapped = candidateFootprint;
        }
        var noFence = new object[] { new FenceEdge(new Vector2Int(15, 20), true), null };
        Check(!(bool)Call(park, "TrySnapPettingEntrance", noFence), "The entrance tool rejects ground without a fence");
        Check(paths.TryPlace(snapped.AccessCell), "An existing path can occupy the tile immediately outside the petting fence");
        Call(park, "BeginConstructionAction", "attraction.petting_zoo.name");
        Check(buildings.TryPlace(snapped), "The snapped gateway mounts directly onto a real fence beside the existing path");
        Call(park, "CommitConstructionAction", 0f);
        Check(park.UndoLastConstruction() && grid.ContentAt(snapped.AccessCell) == BuildCellContent.Path
            && fences.Layout.HasFence(pettingEdge), "Undo removes the gateway while keeping the original fence and path");
        Check(buildings.TryPlace(snapped), "A fence accepts a new petting entrance after gateway Undo");
        var petting = buildings.Network.Records.Last();
        var selection = Object.FindObjectsByType<BuildingSelection>(FindObjectsSortMode.None).Single(item => ReferenceEquals(item.Record, petting));
        Vector3 edgeCenter = fences.Layout.Center(pettingEdge);
        Check(Vector3.Distance(selection.transform.position, edgeCenter) < .001f
            && Vector3.Distance(buildings.PreviewPosition(snapped), edgeCenter) < .001f,
            "The placed entrance and its placement preview share the fence centre");
        Check(selection.GetComponent<BoxCollider>().size.z < grid.CellSize * .5f,
            "The mounted entrance collider hugs the fence instead of blocking an entire path tile");
        Check(grid.ContentAt(snapped.AccessCell) == BuildCellContent.Path && petting.IsOpen,
            "The original public path remains connected and opens the mounted attraction");
        fences.Refresh();
        Check(!fences.CanPlaceGate(pettingEdge) && !fences.CanPlaceWindow(pettingEdge),
            "A mounted petting entrance reserves its fence segment against other gate or window tools");
        var pettingFence = Object.FindObjectsByType<FenceSegment>(FindObjectsSortMode.None).Single(item => item.Edge.Equals(pettingEdge));
        Check((bool)Call(park, "TryInspectObject", pettingFence.GetComponentInChildren<Collider>())
            && ReferenceEquals(Field<ParkBuildingRecord>(Field<BuildingInspection>(park, "buildingInspection"), "selected"), petting),
            "Clicking the underlying fence selects the mounted petting entrance's building information");
        int column = 10;
        foreach (var definition in definitions.Where(item => item.IsAttraction && item.kind != BuildingKind.PettingZoo && item.kind != BuildingKind.ViewingWindow))
        { Check(park.Progression.BuildingUnlocked(definition.kind), "Research unlocks the attraction catalogue choice"); Place(definition.kind, new Vector2Int(column, 6)); column += 3; }
        var roomDefinition = definitions.Single(item => item.kind == BuildingKind.ViewingWindow);
        Check(roomDefinition.width == 2 && roomDefinition.depth == 1 && park.Progression.BuildingUnlocked(roomDefinition.kind),
            "Completed viewing-window research exposes the new two-by-one building");
        typeof(BuildModeController).GetField("selectedBuilding", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(park, roomDefinition);
        for (int y = 6; y <= 13; y++) Check(paths.TryPlace(new Vector2Int(1, y)), "The upper viewing-room access road stays outside the enclosure");
        for (int x = 2; x <= 6; x++) Check(paths.TryPlace(new Vector2Int(x, 13)), "A public path reaches the viewing-room entrance beside the fence");
        buildings.Network.Refresh();
        var roomEdge = new FenceEdge(new Vector2Int(6, 13), true);
        var roomArguments = new object[] { roomEdge, null };
        Check((bool)Call(park, "TrySnapViewingWindow", roomArguments), "The viewing-window tool snaps to the upper enclosure fence");
        var roomFootprint = (BuildingFootprint)roomArguments[1];
        Check(roomFootprint.IsFenceMounted && roomFootprint.Cells().Count() == 2 && roomFootprint.AccessCell == new Vector2Int(6, 13)
            && roomFootprint.Cells().All(cell => cell.y == 12), "The snapped room projects exactly one tile into the enclosure");
        Call(park, "BeginConstructionAction", "attraction.window.place");
        Check(buildings.TryPlace(roomFootprint), "The viewing room builds on two inside tiles beside an existing public path");
        Call(park, "CommitConstructionAction", 0f);
        Check(park.UndoLastConstruction() && roomFootprint.Cells().All(grid.IsAvailable)
            && roomFootprint.AttachmentEdges().All(fences.Layout.HasFence) && grid.ContentAt(roomFootprint.AccessCell) == BuildCellContent.Path,
            "Viewing-room Undo releases the interior floor while preserving both fences and its public path");
        Check(buildings.TryPlace(roomFootprint), "A viewing room can be placed again after Undo");
        var viewingRoom = buildings.Network.Records.Last(); viewingRoom.SetPrice(6);
        var roomSelection = Object.FindObjectsByType<BuildingSelection>(FindObjectsSortMode.None).Single(item => ReferenceEquals(item.Record, viewingRoom));
        Check(Vector3.Distance(roomSelection.transform.position, roomFootprint.Center(grid)) < .001f
            && Vector3.Distance(buildings.PreviewPosition(roomFootprint), roomSelection.transform.position) < .001f,
            "The room preview and placed building share the inside footprint centre");
        Check(roomFootprint.Cells().All(cell => grid.ContentAt(cell) == BuildCellContent.Building
            && !new CreatureNavigation(grid, fences.Layout).IsWalkable(cell)), "Both room tiles are protected from ordinary animal movement");
        fences.Refresh();
        Check(roomFootprint.AttachmentEdges().All(edge => !fences.CanPlaceGate(edge) && !fences.CanPlaceWindow(edge)),
            "Both viewing-room attachment edges reject overlapping staff gates and legacy windows");
        Check(Object.FindObjectsByType<FenceSegment>(FindObjectsSortMode.None).Single(item => item.Edge.Equals(roomFootprint.RearEdge)).name.Contains("gate")
            && !fences.Layout.IsGate(roomFootprint.RearEdge), "The room doorway uses gate artwork while retaining its closed animal barrier");
        var animalProfile = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Goat")); animalProfile.name = "Goat";
        animalProfile.initialHunger = 0; animalProfile.hungerPerSecond = 0; animalProfile.randomEscapeChance = animalProfile.hungryEscapeChance = animalProfile.agitatedEscapeChance = animalProfile.crowdedEscapeChance = 0;
        Check(creatures.TrySpawn(animalProfile, new Vector2Int(5, 9)), "The petting pen contains a real suitable creature");
        Check((Vector2Int?)Call(park, "PettingEntrance", petting) == petting.Footprint.RearAccessCell, "The physical gateway resolves into the secured petting enclosure");
        fences.Refresh(); Check(!fences.Layout.IsGate(petting.Footprint.RearEdge), "Visitor petting access keeps the fence closed to animals");
        var windowEdge = new FenceEdge(new Vector2Int(6, 7), true); paths.TryPlace(new Vector2Int(6, 6)); buildings.Network.Refresh();
        Call(park, "BeginConstructionAction", "attraction.window.place");
        Check(fences.TryPlaceWindow(windowEdge, 7), "A viewing window installs on the existing reinforced fence");
        Call(park, "CommitConstructionAction", 0f);
        Check(park.UndoLastConstruction() && fences.Layout.HasFence(windowEdge) && !fences.Layout.HasWindow(windowEdge), "Window Undo restores the original fence instead of demolishing it");
        Check(fences.TryPlaceWindow(windowEdge, 7), "The restored fence accepts a new viewing window");
        var window = fences.Layout.WindowAt(windowEdge); window.RestoreAccounts(7, 21, 3.5f);
        park.Economy.Earn(21, ParkTransactionCategory.Attractions); Call(park, "TickWindowEconomy", .1f);
        // Real passengers join before the short existing petting/window save fixture.
        // Disable automatic arrivals and needs only in this isolated save fixture.
        var visitorRules = park.Visitors.Definition;
        visitorRules.maximumVisitors = 0; visitorRules.initialHunger = visitorRules.hungerPerSecond = visitorRules.energyDrainPerSecond = 0;
        visitorRules.initialEnergy = 100; visitorRules.minimumStaySeconds = visitorRules.maximumStaySeconds = 100000;
        visitorRules.mood.baselineLossPerSecond = 0;
        var balloon = buildings.Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.BalloonRide);
        var tower = buildings.Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.ViewingTower);
        balloon.SetPrice(4); tower.SetPrice(0);
        var balloonGuest = (VisitorAgent)Call(park.Visitors, "RestoreGuestWithType", balloon.Footprint.AccessCell, VisitorType.Wonderlust);
        Check((bool)Call(park.Visitors, "TryAttraction", balloonGuest,
            new VisitorNavigation(grid, fences.Layout, creatures.IsClear).Reachable(balloonGuest.Actor.Cell)),
            "An actual visitor reserves the real balloon before the park is saved");
        for (int step = 0; step < 1000 && (park.Visitors.CaptureRide(balloon)?.phase != AttractionRidePhase.Riding
            || park.Visitors.CaptureRide(balloon).elapsed < 10); step++) park.Visitors.Tick(.05f);
        Check(park.Visitors.CaptureRide(balloon)?.phase == AttractionRidePhase.Riding
            && park.Visitors.CaptureAttraction(balloonGuest, buildings.Network.Records).insideInterior
            && balloonGuest.Actor.transform.position.y > balloon.Footprint.Center(grid).y + 8,
            "The saved park contains a real balloon passenger visibly flying above the station");
        var towerGuest = (VisitorAgent)Call(park.Visitors, "RestoreGuestWithType", tower.Footprint.AccessCell, VisitorType.Adventurers);
        Check((bool)Call(park.Visitors, "TryAttraction", towerGuest,
            new VisitorNavigation(grid, fences.Layout, creatures.IsClear).Reachable(towerGuest.Actor.Cell)),
            "An actual visitor chooses the connected viewing tower");
        for (int step = 0; step < 100 && (!park.Visitors.CaptureAttraction(towerGuest, buildings.Network.Records).insideInterior
            || park.Visitors.CaptureAttraction(towerGuest, buildings.Network.Records).phase != VisitorAttractionPhase.Visiting); step++) park.Visitors.Tick(.05f);
        Check(park.Visitors.CaptureAttraction(towerGuest, buildings.Network.Records).insideInterior
            && towerGuest.Actor.transform.position.y > tower.Footprint.Center(grid).y + tower.Footprint.Definition.attraction.rideHeight,
            "The saved viewing-tower visitor stands on its actual elevated platform");
        var roomGuest = (VisitorAgent)Call(park.Visitors, "RestoreGuestWithType", viewingRoom.Footprint.AccessCell, VisitorType.ThrillSeekers);
        var roomEntry = new VisitorAttractionSave { building = buildings.Network.Records.ToList().IndexOf(viewingRoom),
            phase = VisitorAttractionPhase.EnteringViewingRoom, hasViewingRoom = true,
            access = viewingRoom.Footprint.AccessCell, interior = viewingRoom.Footprint.RearAccessCell, remaining = 5, quotedPrice = 6, interest = .95f };
        park.Visitors.RestoreAttraction(roomGuest, roomEntry, buildings.Network.Records);
        for (int step = 0; step < 100 && roomGuest.Activity != VisitorActivity.EnjoyingAttraction; step++) park.Visitors.Tick(.05f);
        Check(roomGuest.Activity == VisitorActivity.EnjoyingAttraction && viewingRoom.Footprint.Contains(roomGuest.Actor.Cell),
            "The visitor walks through the real entrance and stands inside the protected viewing room");
        typeof(VisitorAgent).GetField("StateTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(roomGuest, .2f);
        var guest = (VisitorAgent)Call(park.Visitors, "RestoreGuestWithType", petting.Footprint.RearAccessCell, VisitorType.Children);
        var snapshot = new VisitorAttractionSave { building = buildings.Network.Records.ToList().IndexOf(petting), phase = VisitorAttractionPhase.Visiting,
            access = petting.Footprint.AccessCell, interior = petting.Footprint.RearAccessCell, hasPettingPortal = true, insidePetting = true,
            remaining = .2f, quotedPrice = 3, interest = .95f };
        park.Visitors.RestoreAttraction(guest, snapshot, buildings.Network.Records);
        Check(creatures.FindThreat(creatures.Agents.First(item => !Field<bool>(item, "ExternallyControlled")), 20) == null,
            "Suitable pettable creatures do not fear their authorized visitors");
        var inspector = Field<BuildingInspection>(park, "buildingInspection"); inspector.SelectWindow(window, fences.Layout); inspector.Refresh();
        Check(Field<UnityEngine.UI.Text>(inspector, "title").text == Loc.Text("attraction.viewing_window.name"), "Viewing windows open their editable-price information panel");
        var palette = Field<ConstructionPalette>(park, "palette"); palette.Refresh(true); palette.Toggle(ConstructionCategory.Attractions);
        yield return null;
        Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "SteampunkUIValidation"));
        typeof(SteampunkUIIntegrationValidation).GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { "Attractions-Catalogue", palette.Root, 1280, 720 });
        palette.Close(); inspector.Select(petting); inspector.Refresh();
        var saved = park.CapturePark(); park.ValidateSave(saved);
        Check(saved.buildings.Count(item => item.kind >= BuildingKind.PettingZoo) == 9 && saved.fences.Single(item => item.window).windowRevenue == 21,
            "Park capture includes every new building and the viewing window accounts");
        Check(saved.buildings.Single(item => item.kind == BuildingKind.PettingZoo).fenceMounted,
            "The park save explicitly retains fence-mounted gateway geometry");
        Check(saved.visitors.Single(item => item.attraction.insidePetting).attraction.insidePetting, "A paid petting visit keeps its inside-access state in the park save");
        Check(saved.buildings.Single(item => item.kind == BuildingKind.ViewingWindow).fenceMounted
            && saved.visitors.Single(item => item.attraction.insideViewingRoom).cell == viewingRoom.Footprint.RearAccessCell,
            "Park capture validates an indoor visitor on the saved viewing-room building tile");
        var balloonIndex = buildings.Network.Records.ToList().IndexOf(balloon);
        var savedFlight = saved.buildings[balloonIndex].ride;
        var savedBalloonGuest = saved.visitors.Single(item => item.visitorType == VisitorType.Wonderlust);
        Check(saved.buildings[balloonIndex].hasRide && savedFlight != null && savedFlight.phase == AttractionRidePhase.Riding && savedFlight.elapsed > 0
            && savedBalloonGuest.attraction.insideInterior && savedBalloonGuest.attraction.hasQuote,
            "Park capture retains the shared active flight, physical seat and unpaid passenger quote");
        Check(saved.buildings.All(item => item.hasRide == (item.kind == BuildingKind.BalloonRide || item.kind == BuildingKind.ClockworkCarousel)),
            "Only batch-ride buildings mark a serialized ride snapshot as present");
        var wrongRoomSave = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        wrongRoomSave.visitors.Single(item => item.attraction.insideViewingRoom).cell = research.Footprint.Cells().First();
        bool rejectedWrongRoom = false;
        try { park.ValidateSave(wrongRoomSave); } catch (InvalidDataException) { rejectedWrongRoom = true; }
        Check(rejectedWrongRoom, "Indoor viewing authorization cannot restore a visitor inside an unrelated building");
        checks += AttractionDataNativeChecks.RunChecks(); checks += AttractionVisualNativeChecks.RunChecks(); checks += ViewingWindowConstructionNativeChecks.RunChecks();
        slot = ParkSaveStore.NewManualSlotId(); ParkSaveStore.WriteSlot(saved, slot, "Attraction validation"); ParkSaveStore.Pending = ParkSaveStore.ReadSlot(slot);
        Object.Destroy(animalProfile); Check(SceneTransition.Load("GameScene"), "The attraction park uses the production scene-loading path");
        while (SceneTransition.IsLoading) yield return null;
        park = Object.FindFirstObjectByType<BuildModeController>(); park.enabled = false; Time.timeScale = 0;
        var restored = park.CapturePark(); park.ValidateSave(restored);
        Check(restored.buildings.Count == saved.buildings.Count && restored.fences.Count == saved.fences.Count && restored.visitors.Count == 4,
            "Scene loading retains all attraction buildings, windows and visitors");
        Check(restored.buildings.All(item => item.hasRide == (item.kind == BuildingKind.BalloonRide || item.kind == BuildingKind.ClockworkCarousel)),
            "A production JSON scene round-trip preserves explicit ride presence without inventing static-building cycles");
        Check(restored.fences.Single(item => item.window).windowPrice == 7 && restored.fences.Single(item => item.window).windowRevenue == 21,
            "Viewing window prices and accounts survive a real scene load");
        Check(restored.visitors.Single(item => item.attraction.insidePetting).attraction.insidePetting
            && park.Visitors.Visitors.Single(item => item.Type == VisitorType.Children).AttractionName == Loc.Text("attraction.petting_zoo.name"),
            "An inside petting visit resumes with its authorized gateway");
        var restoredPetting = Field<BuildingConstruction>(park, "buildingConstruction").Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.PettingZoo);
        Check(restored.buildings.Single(item => item.kind == BuildingKind.PettingZoo).fenceMounted && restoredPetting.Footprint.IsFenceMounted
            && restoredPetting.Footprint.RearEdge.Equals(pettingEdge) && restoredPetting.Footprint.AccessCell == snapped.AccessCell,
            "Scene loading preserves the exact snapped edge and both gateway access cells");
        var restoredRoom = Field<BuildingConstruction>(park, "buildingConstruction").Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.ViewingWindow);
        var restoredRoomGuest = park.Visitors.Visitors.Single(item => item.Type == VisitorType.ThrillSeekers);
        Check(restoredRoom.Footprint.IsFenceMounted && restoredRoom.Footprint.Contains(restoredRoomGuest.Actor.Cell)
            && restored.visitors.Single(item => item.attraction.insideViewingRoom).attraction.quotedPrice == 6,
            "Scene loading restores the actual visitor inside the protected room with the unpaid quoted price");
        var restoredBuildings = Field<BuildingConstruction>(park, "buildingConstruction");
        var restoredBalloon = restoredBuildings.Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.BalloonRide);
        var restoredTower = restoredBuildings.Network.Records.Single(item => item.Footprint.Definition.kind == BuildingKind.ViewingTower);
        var restoredBalloonGuest = park.Visitors.Visitors.Single(item => item.Type == VisitorType.Wonderlust);
        var restoredTowerGuest = park.Visitors.Visitors.Single(item => item.Type == VisitorType.Adventurers);
        Check(park.Visitors.CaptureRide(restoredBalloon).phase == AttractionRidePhase.Riding
            && Mathf.Abs(park.Visitors.CaptureRide(restoredBalloon).elapsed - savedFlight.elapsed) < .001f
            && restoredBalloonGuest.Actor.transform.position.y > restoredBalloon.Footprint.Center(grid).y + 8,
            "A production scene load resumes the balloon and its actual passenger at the saved flight progress");
        Check(restoredTowerGuest.Actor.transform.position.y > restoredTower.Footprint.Center(grid).y + restoredTower.Footprint.Definition.attraction.rideHeight,
            "A production scene load restores the visitor on top of the viewing platform");
        park.Visitors.Definition.maximumVisitors = 0;
        park.Visitors.Definition.hungerPerSecond = park.Visitors.Definition.energyDrainPerSecond = 0;
        float income = park.Economy.Income; park.Visitors.Tick(.3f); park.Visitors.Tick(.3f);
        Check(Mathf.Approximately(park.Economy.Income - income, 9), "Resumed petting and viewing-room visits charge their original quotes exactly once");
        for (int step = 0; step < 100 && restoredRoomGuest.Actor.Cell != restoredRoom.Footprint.AccessCell; step++) park.Visitors.Tick(.05f);
        Check(restoredRoomGuest.Actor.Cell == restoredRoom.Footprint.AccessCell, "The saved indoor visitor physically exits back onto the public path");
        for (int step = 0; step < 1000 && park.Visitors.CaptureAttraction(restoredBalloonGuest, restoredBuildings.Network.Records).hasInterior; step++) park.Visitors.Tick(.05f);
        Check(restoredBalloonGuest.Actor.Cell == restoredBalloon.Footprint.AccessCell
            && Mathf.Approximately(park.Economy.Income - income, 13),
            "The restored flight lands, unloads its visitor and charges its original quote exactly once");
    }
    private static void Cleanup()
    {
        if (slot == null) return;
        string directory = Path.GetFullPath(Path.GetDirectoryName(ParkSaveStore.SavePath));
        string path = Path.GetFullPath(SaveSlotPaths.Resolve(directory, slot));
        if (!SaveSlotPaths.IsManual(slot) || !path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Cleanup escaped its owned attraction save.");
        foreach (string suffix in new[] { "", ".bak", ".tmp" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
        slot = null;
    }
}

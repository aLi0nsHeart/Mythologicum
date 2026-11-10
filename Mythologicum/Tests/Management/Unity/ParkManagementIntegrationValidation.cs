using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Native scene/UI/visitor/save checks; run in the isolated project with a real GPU.</summary>
[InitializeOnLoad]
public static class ParkManagementIntegrationValidation
{
    const string Running = "ParkManagementValidation.running";
    static bool pending;
    static int checks, stage;
    static double readyAt, deadline;
    static ParkSaveData expected;
    static string Result => Path.Combine(Directory.GetCurrentDirectory(), "ParkManagementValidationResults.txt");
    static string Captures => Path.Combine(Directory.GetCurrentDirectory(), "LocalCache", "ParkManagementValidation");

    static ParkManagementIntegrationValidation()
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
        PlayerSettings.productName = "ParkManagementValidation";
        Directory.CreateDirectory(Captures);
        SessionState.SetBool(Running, true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");
        EditorApplication.EnterPlaymode();
    }
    static void WaitForScene() { readyAt = EditorApplication.timeSinceStartup + 1; deadline = readyAt + 100; }
    static void Tick()
    {
        if (!pending || !Application.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timed out waiting for management stage " + stage);
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            if (controller?.Terrain == null || controller.Personnel == null) return;
            if (stage == 0) Validate(controller);
            else if (stage == 1) ValidateRestored(controller);
            else ValidateLegacy(controller);
        }
        catch (Exception error)
        {
            pending = false; Debug.LogException(error); File.WriteAllText(Result, "FAIL: " + error); EditorApplication.Exit(1);
        }
    }
    static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); Debug.Log("MANAGEMENT PASS: " + message); }
    static bool Near(double first, double second, double tolerance = .02) => Math.Abs(first - second) <= tolerance;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static object Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    static void SetProperty(object owner, string name, object value) => owner.GetType().GetProperty(name).GetSetMethod(true).Invoke(owner, new[] { value });
    static Button Button(Transform root, string name) => root.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
    static Vector2Int Cell(int x, int z) => new Vector2Int(x, z);

    static BuildingDefinition Definition(BuildingKind kind)
    {
        var source = BuildingDefinition.Defaults().Single(item => item.kind == kind);
        var copy = JsonUtility.FromJson<BuildingDefinition>(JsonUtility.ToJson(source));
        copy.width = copy.depth = 2; copy.height = 2; copy.entranceColumn = 1; copy.visualPrefab = null;
        return copy;
    }

    static void Validate(BuildModeController controller)
    {
        Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Management validation uses a real rendering device");
        controller.enabled = false; controller.GetComponent<IsometricCameraController>().enabled = false; Time.timeScale = 0;
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        var management = Field<ParkManagementPresentation>(controller, "parkManagement");
        var palette = Field<ConstructionPalette>(controller, "palette");
        var grid = Field<SquareBuildGrid>(controller, "grid");
        var building = Field<BuildingConstruction>(controller, "buildingConstruction");
        var paths = Field<PathConstruction>(controller, "paths");
        var fences = Field<FenceConstruction>(controller, "fences");
        var visitors = controller.Visitors;
        var creatures = Field<CreatureWorld>(controller, "creatureWorld");
        var economy = controller.Economy;
        Check(!building.Network.HasEntrance, "The authored scene starts without an already constructed entrance");
        var open = Button(palette.Root.transform, "Park management");
        Check(open.interactable, "Park Management remains available before constructing the entrance");
        open.onClick.Invoke();
        Check(management.IsOpen && controller.ManagementOpen, "The bottom-row button opens the management window and blocks construction input");
        Check(controller.CloseManagementWindow() && !management.IsOpen && !controller.ManagementOpen,
            "Escape's close-management API closes this window without opening Pause");

        foreach (string root in new[] { "Enrichment trees", "Natural rocks" })
            foreach (var target in GameObject.Find(root).GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        GameObject.Find("Prototype Landmarks")?.SetActive(false);
        controller.Terrain.ImportHeights(new float[(grid.Width + 1) * (grid.Depth + 1)]);
        Physics.SyncTransforms();
        var entrance = new BuildingFootprint(Definition(BuildingKind.ParkEntrance), Cell(5, 7), 0);
        var researcher = new BuildingFootprint(Definition(BuildingKind.ResearchCentre), Cell(13, 7), 0);
        var kiosk = new BuildingFootprint(Definition(BuildingKind.Kiosk), Cell(21, 7), 0);
        var rest = new BuildingFootprint(Definition(BuildingKind.RestArea), Cell(27, 7), 0);
        var distantKiosk = new BuildingFootprint(Definition(BuildingKind.Kiosk), Cell(32, 7), 0);
        foreach (var footprint in new[] { entrance, researcher, kiosk, rest, distantKiosk })
            Check(building.TryPlace(footprint), "Place actual fixture building: " + footprint.Definition.displayName);
        for (int x = 6; x <= 33; x++) Check(paths.TryPlace(Cell(x, 6)), "Connect public path cell " + x);
        building.Network.Refresh();
        Check(building.Network.Records.All(record => record.IsOpen), "The fixture's price and employee buildings connect to the entrance");
        economy.Restore(5000, 0, 0, false);
        var lab = building.Network.Records.Single(record => record.Footprint.Definition.kind == BuildingKind.ResearchCentre);
        Check(controller.Personnel.TryHire(lab), "A real researcher can be hired in the connected workplace");
        Check(Near(economy.ExpensesInWindow(ParkTransactionCategory.Hiring), lab.Footprint.Definition.hireCost),
            "Hiring enters the new investment category exactly once");
        economy.Advance(10); BuildingEconomy.Tick(building.Network, economy, 10);
        double upkeep = building.Network.Records.Sum(record => record.Footprint.Definition.operatingCostPerSecond * 10d);
        double salary = lab.Footprint.Definition.staffSalaryPerSecond * 10d;
        Check(Near(economy.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep), upkeep), "Actual buildings record base upkeep separately");
        Check(Near(economy.ExpensesInWindow(ParkTransactionCategory.Salary), salary), "Actual employees record their role's wage separately");
        Check(Near(economy.OperatingExpensesPerMinute, upkeep + salary), "Operating expense totals contain base costs and salaries without double charging");
        Check(economy.TrySpend(100, ParkTransactionCategory.Construction) && Near(economy.InvestmentExpensesPerMinute, 150),
            "Construction and hiring appear as investments rather than recurring operating costs");
        float tracked = economy.TrackedSeconds, expiringIncome = economy.IncomePerMinute;
        economy.Advance(0);
        Check(Near(tracked, economy.TrackedSeconds) && Near(expiringIncome, economy.IncomePerMinute), "Zero simulation time does not age the financial window");

        var oni = Resources.Load<CreatureDefinition>("Creatures/Oni");
        var hitotsume = Resources.Load<CreatureDefinition>("Creatures/Hitotsume");
        Check(creatures.TrySpawn(oni, Cell(16, 10)), "Place a high-rated creature near connected viewing paths");
        Check(creatures.TrySpawn(hitotsume, Cell(35, 35)), "Place another species far from every public viewing path");
        for (int x = 15; x <= 17; x++)
        { Check(fences.TryPlace(new FenceEdge(Cell(x, 9), true)), "Build lower enclosure barrier"); Check(fences.TryPlace(new FenceEdge(Cell(x, 12), true)), "Build upper enclosure barrier"); }
        for (int z = 9; z < 12; z++)
        { Check(fences.TryPlace(new FenceEdge(Cell(15, z), false)), "Build left enclosure barrier"); Check(fences.TryPlace(new FenceEdge(Cell(18, z), false)), "Build right enclosure barrier"); }
        Physics.SyncTransforms(); visitors.RefreshReputation();
        Check(visitors.Reputation.DisplayedSpecies == 1 && visitors.Reputation.DisplayedCreatures == 1,
            "Only the creature viewable from connected paths contributes to exhibition appeal");
        Check(visitors.Reputation.Stars > 1 && visitors.Reputation.RecommendedAdmissionPrice > 15,
            "Displaying a dangerous creature behind a fence raises stars and acceptable admission value");
        foreach (var target in GameObject.Find("Paths").GetComponentsInChildren<DemolitionTarget>())
            if (target.name != "Path [6, 6]") target.Demolish();
        building.Network.Refresh(); visitors.RefreshReputation();
        Check(visitors.Reputation.DisplayedCreatures == 0 && Near(visitors.Reputation.Stars, 0),
            "Disconnecting the viewing paths removes inaccessible exhibits from the rating");
        for (int x = 7; x <= 33; x++) Check(paths.TryPlace(Cell(x, 6)), "Restore viewing path cell " + x);
        building.Network.Refresh(); visitors.RefreshReputation();

        var entryRecord = building.Network.Entrance;
        entryRecord.SetPrice(15);
        float balanceBefore = economy.Balance;
        var guest = visitors.TrySpawn();
        Check(guest != null && Near(economy.Balance - balanceBefore, 15), "Affordable admission creates a paying guest");
        Check(Near(economy.IncomeInWindow(ParkTransactionCategory.Admission), 15), "Admission revenue enters its own financial category");
        MoveGuest(guest, grid, Cell(8, 6));
        entryRecord.SetPrice(100000);
        balanceBefore = economy.Balance; int rejected = visitors.RejectedAdmissions;
        Check(visitors.TrySpawn() == null && visitors.RejectedAdmissions == rejected + 1 && Near(balanceBefore, economy.Balance),
            "An absurd entry price is refused and cannot generate free money");
        entryRecord.SetPrice(15);

        open.onClick.Invoke();
        var modal = Field<GameObject>(management, "root");
        Button(modal.transform, "Prices & budget").onClick.Invoke();
        var input = modal.GetComponentsInChildren<InputField>(true).Single(field => field.name == "Price input " + entryRecord.RuntimeId);
        input.text = "25"; Button(modal.transform, "Apply price " + entryRecord.RuntimeId).onClick.Invoke();
        Check(Near(entryRecord.Price, 25), "The management pricing page changes the actual entrance price");
        input.text = "NaN"; Button(modal.transform, "Apply price " + entryRecord.RuntimeId).onClick.Invoke();
        Check(Near(entryRecord.Price, 25), "The pricing page rejects non-finite amounts");
        entryRecord.SetPrice(15);

        var meal = building.Network.Records.Single(record => record.Footprint.Anchor == kiosk.Anchor);
        var affordableMeal = building.Network.Records.Single(record => record.Footprint.Anchor == distantKiosk.Anchor);
        MoveGuest(guest, grid, kiosk.AccessCell); guest.Actor.Vitals.Restore(80, 0);
        SetProperty(guest, "Activity", VisitorActivity.Arriving); meal.SetPrice(100000); affordableMeal.SetPrice(100000);
        int refusedServices = visitors.ExpensiveServiceRefusals;
        Invoke(visitors, "Think", guest);
        Check(visitors.ExpensiveServiceRefusals == refusedServices + 1 && guest.PriceComplaint != null,
            "A hungry guest complains about an overpriced reachable kiosk");
        affordableMeal.SetPrice(8); Invoke(visitors, "Think", guest);
        Check(Field<ParkBuildingRecord>(guest, "Service") == affordableMeal, "A hungry guest chooses an affordable farther kiosk over an overpriced nearer one");
        MoveGuest(guest, grid, distantKiosk.AccessCell); Invoke(visitors, "CompleteActivity", guest);
        Check(guest.Activity == VisitorActivity.Eating, "A fairly priced kiosk begins serving the guest");
        affordableMeal.SetPrice(100000); SetField(guest, "StateTimer", 0f); Invoke(visitors, "CompleteActivity", guest);
        Check(Near(economy.IncomeInWindow(ParkTransactionCategory.Food), 8) && Near(affordableMeal.Revenue, 8), "Completed food service records its original quoted price once despite a price change during service");
        meal.SetPrice(8); affordableMeal.SetPrice(8);
        var bench = building.Network.Records.Single(record => record.Footprint.Definition.kind == BuildingKind.RestArea);
        bench.SetPrice(3); MoveGuest(guest, grid, rest.AccessCell); guest.Actor.Vitals.Restore(20, 0);
        SetProperty(guest.Actor, "Energy", new CreatureEnergy(10)); SetProperty(guest, "Activity", VisitorActivity.Arriving);
        Invoke(visitors, "Think", guest); Invoke(visitors, "CompleteActivity", guest);
        Check(guest.Activity == VisitorActivity.Resting, "The guest can choose a fairly priced rest service");
        SetProperty(guest.Actor, "Energy", new CreatureEnergy(100)); Invoke(visitors, "CompleteActivity", guest);
        Check(Near(economy.IncomeInWindow(ParkTransactionCategory.Rest), 3) && Near(bench.Revenue, 3), "Completed rest service records its own revenue category");

        foreach (string page in new[] { "Statistics", "Finances", "Prices & budget", "Star rating" })
        {
            Button(modal.transform, page).onClick.Invoke(); management.Refresh();
            Check(modal.transform.Find("Window/" + page + " page").gameObject.activeSelf, "The actual " + page + " tab opens");
            Capture(controller, canvas, modal, page.Replace(" & ", "-").Replace(' ', '-'), 1280, 720);
        }
        Button(modal.transform, "Star rating").onClick.Invoke(); Capture(controller, canvas, modal, "Stars-1920", 1920, 1080);
        Check(controller.CloseManagementWindow() && !management.IsOpen, "The management window closes after price editing and tab changes");

        int reviews = visitors.Reputation.ReviewedVisitors; float guestScore = visitors.Reputation.GuestScore;
        PrepareDeparture(guest, grid, entrance.AccessCell, 95); visitors.Tick(.01f);
        Check(visitors.Reputation.ReviewedVisitors == reviews + 1 && visitors.Reputation.GuestScore > guestScore,
            "A satisfied guest's actual departure improves park reputation");
        guestScore = visitors.Reputation.GuestScore;
        var unhappy = visitors.TrySpawn(); Check(unhappy != null, "A second guest can visit after the first leaves");
        PrepareDeparture(unhappy, grid, entrance.AccessCell, 5); visitors.Tick(.01f);
        Check(visitors.Reputation.ReviewedVisitors == reviews + 2 && visitors.Reputation.GuestScore < guestScore,
            "An unhappy guest's actual departure reduces park reputation");
        var victim = visitors.TrySpawn(); Check(victim != null, "A third guest enters for the safety-event fixture");
        float starsBefore = visitors.Reputation.Stars; int fatalities = visitors.Reputation.Fatalities;
        creatures.ApplyDamage(victim.Actor, victim.Actor.Health.Maximum + 1); visitors.Tick(.01f);
        Check(visitors.Reputation.Fatalities == fatalities + 1 && visitors.Reputation.Stars < starsBefore,
            "A real visitor death reduces star rating and enters safety history");
        visitors.Tick(.01f);
        Check(visitors.Reputation.Fatalities == fatalities + 1 && visitors.Reputation.ReviewedVisitors == reviews + 2,
            "Deaths and completed reviews are processed once rather than replayed each frame");

        expected = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(controller.CapturePark()));
        controller.ValidateSave(expected);
        Check(expected.finance != null && expected.reputation != null, "The actual save includes recent finance and reputation data");
        var corrupt = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(expected));
        corrupt.reputation.guestScore = float.NaN;
        CheckRejects(controller, corrupt, "Save validation rejects a non-finite reputation before loading");
        corrupt = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(expected));
        corrupt.finance.buckets[0].income = new double[0];
        CheckRejects(controller, corrupt, "Save validation rejects a malformed financial bucket before loading");
        ParkSaveStore.Pending = expected; stage = 1; Time.timeScale = 1; WaitForScene(); SceneManager.LoadScene("GameScene");
    }

    static void MoveGuest(VisitorAgent guest, SquareBuildGrid grid, Vector2Int cell)
    { SetProperty(guest.Actor, "Cell", cell); guest.Actor.transform.position = grid.ActorPosition(cell, guest.Actor.Definition.bodyScale.y); Field<List<Vector2Int>>(guest.Actor,"Route").Clear(); SetField(guest.Actor,"Goal",null); }
    static void PrepareDeparture(VisitorAgent guest, SquareBuildGrid grid, Vector2Int cell, float satisfaction)
    {
        MoveGuest(guest, grid, cell); guest.Mood.Restore(satisfaction, 50, .5f, true);
        SetProperty(guest, "Activity", VisitorActivity.Leaving); SetField(guest, "LeavingPark", true); SetField(guest, "DecisionTimer", 100f);
    }
    static void CheckRejects(BuildModeController controller, ParkSaveData data, string message)
    { bool rejected = false; try { controller.ValidateSave(data); } catch (InvalidDataException) { rejected = true; } Check(rejected, message); }

    static void Capture(BuildModeController controller, Canvas canvas, GameObject modal, string name, int width, int height)
    {
        var camera = controller.GetComponent<Camera>();
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); target.Create();
        var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; var oldDistance = canvas.planeDistance;
        Texture2D image = null;
        try
        {
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            var backdrop = modal.transform.Find("Window").GetComponent<Image>();
            Check(backdrop != null && backdrop.color.a >= .99f, "The management card has an opaque readable backdrop");
            var starGraphic = modal.GetComponentInChildren<StarRatingGraphic>();
            if (starGraphic != null)
            {
                var renderer = starGraphic.GetComponent<CanvasRenderer>();
                Check(renderer != null, "The active star graphic owns a CanvasRenderer");
                var starMesh = renderer.GetMesh();
                Check(starMesh != null && starMesh.vertexCount == 55 && starMesh.subMeshCount == 1 && starMesh.GetIndexCount(0) == 150,
                    "The rating graphic submits all five star meshes to its CanvasRenderer");
            }
            foreach (var button in modal.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>() != null) continue;
                CheckOnScreen((RectTransform)button.transform, camera, width, height, button.name);
            }
            foreach (var button in Field<ConstructionPalette>(controller, "palette").Root.GetComponentsInChildren<Button>())
                CheckOnScreen((RectTransform)button.transform, camera, width, height, "Bottom toolbar " + button.name);
            foreach(var text in modal.GetComponentsInChildren<Text>())
                if(text.GetComponentInParent<Button>()==null && text.GetComponentInParent<ScrollRect>()==null && text.rectTransform.rect.height>100)
                    Check(text.preferredHeight<=text.rectTransform.rect.height+5, text.name+" fits its page without vertical clipping");
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            var pixels = image.GetPixels(); double mean = pixels.Average(pixel => (double)pixel.grayscale);
            Check(mean > .02 && mean < .95, "The " + name + " management page produces a nonblank GPU capture");
            Check(pixels.Count(pixel => pixel.r > .9f && pixel.b > .9f && pixel.g < .1f) < pixels.Length / 100,
                "The " + name + " capture has no substantial missing-shader magenta");
            File.WriteAllBytes(Path.Combine(Captures, name + ".png"), image.EncodeToPNG());
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
        Check(corners.All(corner => { var point = camera.WorldToScreenPoint(corner); return point.x >= -2 && point.y >= -2 && point.x <= width + 2 && point.y <= height + 2; }),
            name + " stays inside the " + width + "x" + height + " viewport");
    }
    static void ValidateRestored(BuildModeController controller)
    {
        controller.enabled = false; controller.GetComponent<IsometricCameraController>().enabled = false;
        var actual = controller.CapturePark(); var money = controller.Economy; var reputation = controller.Visitors.Reputation;
        Check(GameSaveMenu.IsOpen && Time.timeScale == 0, "Loading opens the actual pause menu before the park advances");
        Check(Near(actual.balance, expected.balance) && Near(actual.income, expected.income) && Near(actual.expenses, expected.expenses),
            "Loading does not charge admission, hiring, wages or construction again");
        var expectedMoney = new ParkEconomy(); expectedMoney.RestoreFinance(expected.finance);
        Check(Near(money.IncomePerMinute, expectedMoney.IncomePerMinute) && Near(money.ExpensesPerMinute, expectedMoney.ExpensesPerMinute),
            "Loading preserves the recent income and expense window");
        Check(Near(money.TrackedSeconds, expected.finance.trackedSeconds), "Pause and load preserve the financial clock");
        Check(reputation.ReviewedVisitors == expected.reputation.reviewedVisitors && reputation.Fatalities == expected.reputation.fatalities
            && Near(reputation.GuestScore, expected.reputation.guestScore) && Near(reputation.SafetyPenalty, expected.reputation.safetyPenalty),
            "Loading preserves reviews and safety history without replaying outcomes");
        Check(actual.rejectedAdmissions == expected.rejectedAdmissions && actual.expensiveServiceRefusals == expected.expensiveServiceRefusals,
            "Admission and service-price refusal counters survive loading");
        Check(reputation.DisplayedSpecies == 1 && reputation.DisplayedCreatures == 1, "Loading recomputes exhibition appeal from the restored paths and creatures");
        var legacy = JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(expected));
        legacy.finance = null; legacy.reputation = null; legacy.rejectedAdmissions = legacy.expensiveServiceRefusals = 0;
        controller.ValidateSave(legacy); Check(true, "Existing version-three saves remain valid without the new optional fields");
        ParkSaveStore.Pending = legacy; stage = 2; Time.timeScale = 1; WaitForScene(); SceneManager.LoadScene("GameScene");
    }
    static void ValidateLegacy(BuildModeController controller)
    {
        controller.enabled = false;
        Check(Near(controller.Economy.IncomePerMinute, 0) && Near(controller.Economy.ExpensesPerMinute, 0),
            "An older save begins with an empty recent financial window");
        Check(Near(controller.Visitors.Reputation.GuestScore, 60) && controller.Visitors.Reputation.DisplayedCreatures == 1,
            "An older save receives neutral visitor reputation and actual exhibition appeal");
        Check(Near(controller.Economy.Balance, expected.balance), "Older-save compatibility retains park funds");
        Check(controller.Visitors.Reputation.ReviewedVisitors == 0 && controller.Visitors.Reputation.Fatalities == 0,
            "Legacy saves do not invent historic visitor outcomes");
        File.WriteAllText(Result, "PASS: " + checks + " park-management UI, GPU, pricing, visitor feedback, accounting, exhibit, save/load and legacy checks.\nCaptures: " + Captures);
        pending = false; EditorApplication.Exit(0);
    }
}

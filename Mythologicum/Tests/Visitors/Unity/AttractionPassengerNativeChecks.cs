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
using Object = UnityEngine.Object;

/// <summary>Actual procedural models, passenger positions and authoritative group rides.</summary>
[InitializeOnLoad]
public static class AttractionPassengerNativeChecks
{
    private const string Running = "AttractionPassengerNativeChecks.running";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    static AttractionPassengerNativeChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running, false)) return;
            SessionState.SetBool(Running, false); EditorApplication.delayCall += Execute;
        };
    }
    public static void Start()
    {
        PlayerSettings.companyName = "MythologicumValidation";
        PlayerSettings.productName = "AttractionPassengerValidation";
        if (File.Exists("AttractionPassengerNativeResults.txt")) File.Delete("AttractionPassengerNativeResults.txt");
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        SessionState.SetBool(Running, true); EditorApplication.EnterPlaymode();
    }
    private static void Execute()
    {
        try
        {
            int count = RunChecks();
            File.WriteAllText("AttractionPassengerNativeResults.txt", "PASS: " + count + " actual passenger, platform, balloon group, landing, carousel and safe exit checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("AttractionPassengerNativeResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static object Call(object owner, string name, params object[] args)
        => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception("Passengers: " + reason); checks++; }
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    private sealed class Fixture : IDisposable
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly BuildingConstruction Construction;
        public ParkBuildingNetwork Buildings => Construction.Network;
        public readonly CreatureWorld Creatures;
        public readonly VisitorWorld Visitors;
        public readonly ParkEconomy Economy = new ParkEconomy(10000);
        public readonly ParkBuildingRecord Venue;
        public AttractionVisualAnimator Visual => Construction.AttractionVisualFor(Venue);
        private readonly GameObject root;
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        private readonly VisitorDefinition definition;
        public Fixture(BuildingKind kind, int index, int capacity = 2)
        {
            root = new GameObject("Passenger fixture " + kind);
            Grid = new SquareBuildGrid(new Vector3(82000 + index * 150, 0, 82000), 2, 28, 22);
            Fences = new FenceLayout(Grid);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            Construction = new BuildingConstruction(Grid, Fences, root.transform, material, material);
            Check(Construction.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, V(1, 3), 0)), "the passenger fixture has a real entrance");
            for (int x = 0; x < Grid.Width; x++) Grid.TryOccupy(V(x, 2), BuildCellContent.Path);
            var data = AttractionDefinition.Building(kind);
            data.attractionPrice = 3; data.serviceSeconds = 4; data.serviceCapacity = capacity;
            data.attraction.entrySeconds = .25f; data.attraction.exitSeconds = .25f;
            data.attraction.boardingWindowSeconds = .5f;
            data.attraction.maximumPrice = 100;
            // This fixture checks physical use. Existing attraction suites cover real exhibit requirements.
            data.attraction.requiresCreatures = data.attraction.requiresDangerousCreatures = false;
            data.attraction.requiresFearFeeder = data.attraction.requiresPettableCreatures = false;
            data.attraction.rideHeight = kind == BuildingKind.BalloonRide ? 8 : data.attraction.rideHeight;
            data.attraction.rideRadius = 3;
            Check(Construction.TryPlace(new BuildingFootprint(data, V(7, 3), 0)), "the actual attraction model is constructible: " + kind);
            Venue = Buildings.Records.Last(); Buildings.Refresh(); Check(Venue.IsOpen, "the attraction has connected public access");
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>();
            simulation.populationLimit = 200; simulation.placementSpacingTiles = 0;
            Creatures = new CreatureWorld(Grid, Fences, Buildings, null, material, Array.Empty<CreatureDefinition>(), null, simulation);
            definition = ScriptableObject.CreateInstance<VisitorDefinition>();
            definition.people = null; definition.maximumVisitors = 0;
            definition.initialHunger = definition.hungerPerSecond = definition.energyDrainPerSecond = 0;
            definition.initialEnergy = 100; definition.minimumStaySeconds = definition.maximumStaySeconds = 100000;
            definition.walkSpeed = definition.runSpeed = 20;
            definition.mood.baselineLossPerSecond = 0;
            definition.attractionVisitGapSeconds = 1000; definition.attractionDecisionSeconds = 1000;
            Visitors = new VisitorWorld(Grid, Fences, Buildings, Creatures, definition, Economy);
            Visitors.ConfigureAttractionVisuals(Construction.AttractionVisualFor);
            Construction.AttractionOccupancy = Visitors.AttractionOccupancy;
            Construction.CanDemolish = Visitors.CanDemolishAttraction;
            Check(Visual != null, "a constructed attraction exposes its actual passenger visual");
        }
        public VisitorAgent Guest(VisitorType type = VisitorType.Wonderlust)
            => (VisitorAgent)Call(Visitors, "RestoreGuestWithType", Venue.Footprint.AccessCell, type);
        public bool Choose(VisitorAgent guest) => (bool)Call(Visitors, "TryAttraction", guest,
            new VisitorNavigation(Grid, Fences, Creatures.IsClear).Reachable(guest.Actor.Cell));
        public VisitorAttractionSave State(VisitorAgent guest) => Visitors.CaptureAttraction(guest, Buildings.Records);
        public void Step(int frames = 1, float dt = .05f)
        {
            for (int index = 0; index < frames; index++)
            {
                Creatures.Tick(dt); Visitors.Tick(dt); Construction.Refresh(null);
            }
        }
        public bool Until(Func<bool> done, int frames = 500)
        { for (int index = 0; index < frames; index++) { if (done()) return true; Step(); } return done(); }
        public void Dispose()
        {
            Visitors.Dispose(); root.SetActive(false); Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition); Object.DestroyImmediate(simulation); Object.DestroyImmediate(material);
        }
    }

    public static int RunChecks()
    {
        Check(Application.isPlaying, "passenger tests use normal Play-mode construction and demolition");
        var originalScene = SceneManager.GetActiveScene();
        var fixtureScene = SceneManager.CreateScene("Actual attraction passengers"); SceneManager.SetActiveScene(fixtureScene);
        var random = UnityEngine.Random.state; checks = 1;
        try
        {
            int index = 0;
            foreach (var kind in new[] { BuildingKind.ViewingTower, BuildingKind.PredatorFeedingGallery,
                BuildingKind.TricksterTheatre, BuildingKind.CabinetOfCuriosities, BuildingKind.ExpeditionPavilion })
            {
                using (var f = new Fixture(kind, index++))
                {
                    var guest = f.Guest(); Vector3 outside = guest.Actor.transform.position;
                    Check(f.Choose(guest), "a guest reserves the physical attraction: " + kind);
                    Check(f.Until(() => f.State(guest).insideInterior && guest.Activity == VisitorActivity.EnjoyingAttraction),
                        "a guest enters the actual attraction before enjoying it: " + kind);
                    var state = f.State(guest);
                    Check(state.hasInterior && state.seat >= 0 && f.Venue.Footprint.Contains(guest.Actor.Cell),
                        "an indoor/platform visit owns a real building seat: " + kind);
                    Vector3 seated = f.Visual.PassengerPosition(state.seat, state.seatCapacity, guest.Actor.Definition.bodyScale.y);
                    Check(Vector3.Distance(guest.Actor.transform.position, seated) < .05f
                        && Vector3.Distance(guest.Actor.transform.position, outside) > .5f,
                        "the visitor model occupies its actual visual seat instead of the public entrance: " + kind);
                    if (kind == BuildingKind.ViewingTower)
                        Check(guest.Actor.transform.position.y > f.Venue.Footprint.Center(f.Grid).y + f.Venue.Footprint.Definition.attraction.rideHeight,
                            "tower visitors are visibly standing on the elevated platform");
                    f.Step(3);
                    Check(Vector3.Distance(guest.Actor.transform.position, seated) < .05f,
                        "ordinary creature and visitor ticks do not snap passengers back to the ground: " + kind);
                    Check(f.Until(() => !f.State(guest).hasInterior && guest.Actor.Cell == f.Venue.Footprint.AccessCell),
                        "the visitor leaves through the public entrance: " + kind);
                    Check(Mathf.Approximately(f.Economy.Income, 3), "a physical visit earns one ticket: " + kind);
                }
            }
            using (var f = new Fixture(BuildingKind.CabinetOfCuriosities, index++))
            {
                var quoted = f.Guest();
                Check(f.Choose(quoted) && f.Until(() => f.State(quoted).phase == VisitorAttractionPhase.EnteringAttraction
                    && f.State(quoted).hasQuote), "a static attraction quotes its ticket when the visitor starts entering");
                Check(f.Venue.SetPrice(999), "the operator can raise the price for future visitors during entry");
                Check(f.Until(() => quoted.Activity == VisitorActivity.EnjoyingAttraction),
                    "a visitor finishes entering with its accepted quote even when the new ticket is unaffordable");
                Check(f.Until(() => !f.State(quoted).hasInterior) && Mathf.Approximately(f.Economy.Income, 3),
                    "the static attraction charges the entry-time quote once after the completed visit");
            }
            using (var f = new Fixture(BuildingKind.BalloonRide, index++))
            {
                f.Visual.SetRideProgress(.5f, true);
                var savedCapacityPositions = Enumerable.Range(0, 4).Select(slot => f.Visual.PassengerPosition(slot, 4, 1)).ToArray();
                Check(f.Venue.Footprint.Definition.serviceCapacity == 2
                    && savedCapacityPositions.SelectMany((point, slot) => savedCapacityPositions.Skip(slot + 1)
                        .Select(other => Vector3.Distance(point, other))).All(distance => distance > .2f),
                    "four saved passenger places remain distinct when the current balloon model was generated for two");
                Check(savedCapacityPositions.All(point => point.y > f.Venue.Footprint.Center(f.Grid).y + 4),
                    "saved-capacity seat overrides still follow the real airborne basket");
                f.Visual.SetRideProgress(0, false);
                var first = f.Guest(); var second = f.Guest();
                Check(f.Choose(first) && f.Choose(second), "two people reserve the same balloon group");
                Check(f.Until(() => f.Visitors.CaptureRide(f.Venue).phase == AttractionRidePhase.Riding), "a full boarded group departs together");
                var firstState = f.State(first); var secondState = f.State(second);
                Check(firstState.insideInterior && secondState.insideInterior && firstState.seat != secondState.seat,
                    "each balloon passenger has a distinct reserved basket position");
                var waiting = f.Guest();
                Check(!f.Choose(waiting) && !f.State(waiting).insideInterior, "new arrivals cannot board the airborne balloon");
                f.Step(22);
                Check(first.Actor.transform.position.y > f.Venue.Footprint.Center(f.Grid).y + 4,
                    "actual passenger actors rise with the balloon basket");
                Vector3 before = first.Actor.transform.position; var rideBefore = f.Visitors.CaptureRide(f.Venue);
                f.Step(1, 0);
                Check(first.Actor.transform.position == before && f.Visitors.CaptureRide(f.Venue).elapsed == rideBefore.elapsed,
                    "zero simulation time freezes passengers and the shared ride clock");
                var rideSave = JsonUtility.FromJson<AttractionRideSave>(JsonUtility.ToJson(rideBefore));
                var savedFirst = JsonUtility.FromJson<VisitorAttractionSave>(JsonUtility.ToJson(f.State(first)));
                var savedSecond = JsonUtility.FromJson<VisitorAttractionSave>(JsonUtility.ToJson(f.State(second)));
                f.Visitors.RestoreRide(f.Venue, rideSave);
                f.Visitors.RestoreAttraction(first, savedFirst, f.Buildings.Records);
                f.Visitors.RestoreAttraction(second, savedSecond, f.Buildings.Records);
                Check(f.Economy.Income == 0 && Vector3.Distance(first.Actor.transform.position, before) < .05f,
                    "restoring a flying group preserves its real position and unpaid quote");
                var target = f.Construction.AttractionVisualFor(f.Venue).GetComponentInParent<DemolitionTarget>();
                Check(target != null && !target.Demolish() && f.Buildings.Contains(f.Venue),
                    "normal demolition cannot remove a balloon carrying passengers");
                Check(f.Venue.SetPrice(99), "the operator can change future ticket prices during a flight");
                Check(f.Until(() => f.Visitors.CaptureRide(f.Venue).phase == AttractionRidePhase.Unloading), "the flight returns to the unloading phase");
                Check(first.Actor.transform.position.y < f.Venue.Footprint.Center(f.Grid).y + 3
                    && second.Actor.transform.position.y < f.Venue.Footprint.Center(f.Grid).y + 3,
                    "the balloon lands before passengers unload");
                Check(f.Until(() => !f.State(first).hasInterior && !f.State(second).hasInterior), "both people leave the landed balloon");
                Check(Mathf.Approximately(f.Economy.Income, 6), "both original quotes are charged once after a complete ride");
                f.Venue.SetPrice(3);
                Check(f.Until(() => f.Visitors.CaptureRide(f.Venue).phase == AttractionRidePhase.Boarding), "the landed empty balloon reopens boarding");
                typeof(VisitorAgent).GetField("NextAttractionDecision", Flags).SetValue(waiting, 0f);
                Check(f.Choose(waiting), "a waiting visitor can reserve the next grounded group");
                Check(f.Until(() => f.Visitors.CaptureRide(f.Venue).phase == AttractionRidePhase.Riding), "a partial next group departs after the boarding window");
                Check(f.State(waiting).insideInterior, "the next flight contains its new visitor");
                Check(f.Until(() => !f.State(waiting).hasInterior && f.Economy.Income == 9), "a second full landing and unloading cycle completes independently");
            }
            using (var f = new Fixture(BuildingKind.ClockworkCarousel, index++))
            {
                var first = f.Guest(VisitorType.Children); var second = f.Guest(VisitorType.Children);
                Check(f.Choose(first) && f.Choose(second), "children reserve distinct carousel places");
                Check(f.Until(() => f.Visitors.CaptureRide(f.Venue).phase == AttractionRidePhase.Riding), "the loaded carousel begins its group ride");
                Vector3 before = first.Actor.transform.position; f.Step(20);
                Check(Vector3.Distance(first.Actor.transform.position, before) > .3f,
                    "the actual visitor model travels around with the carousel");
                var state = f.State(first);
                Check(Vector3.Distance(first.Actor.transform.position, f.Visual.PassengerPosition(state.seat, state.seatCapacity,
                    first.Actor.Definition.bodyScale.y)) < .05f, "carousel passengers remain attached to their real rotating seats");
                Check(f.Until(() => !f.State(first).hasInterior && !f.State(second).hasInterior), "the carousel stops and unloads its group");
                Check(Mathf.Approximately(f.Economy.Income, 6), "the carousel earns one completed ticket per rider");
            }
        }
        finally
        {
            UnityEngine.Random.state = random; SceneManager.SetActiveScene(originalScene);
            SceneManager.UnloadSceneAsync(fixtureScene);
        }
        return checks;
    }
}

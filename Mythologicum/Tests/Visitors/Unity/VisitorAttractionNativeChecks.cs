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
using Object = UnityEngine.Object;

/// <summary>Actual paid visits and safe-contact movement; disposable native editor only.</summary>
public static class VisitorAttractionNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Flags).GetValue(owner);
    private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    private static void Check(bool value, string reason) { if (!value) throw new Exception("Attractions: " + reason); checks++; }
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    public static void Run()
    {
        try
        {
            PlayerSettings.companyName = "MythologicumValidation"; PlayerSettings.productName = "VisitorAttractionValidation";
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int count = RunChecks();
            File.WriteAllText("VisitorAttractionNativeResults.txt", "PASS: " + count + " paid attraction, quote, capacity, creature, petting movement and save-state checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        { File.WriteAllText("VisitorAttractionNativeResults.txt", "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly ParkBuildingNetwork Buildings;
        public readonly CreatureWorld Creatures;
        public readonly VisitorWorld Visitors;
        public readonly VisitorDefinition VisitorDefinition;
        public readonly ParkEconomy Economy = new ParkEconomy(10000);
        public readonly CreatureDefinition Species;
        private readonly HashSet<Object> owned;
        public Fixture(HashSet<Object> owned, List<VisitorWorld> worlds, int index)
        {
            this.owned = owned;
            Grid = new SquareBuildGrid(new Vector3(60000 + index * 100, 0, 60000), 2, 24, 16);
            Fences = new FenceLayout(Grid); Buildings = new ParkBuildingNetwork(Grid, Fences);
            Buildings.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1,
                admissionPrice = 0 }, V(1, 3), 0));
            for (int x = 0; x < Grid.Width; x++) Grid.TryOccupy(V(x, 2), BuildCellContent.Path);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.populationLimit = 200; simulation.placementSpacingTiles = 0;
            Species = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(Species);
            Species.name = "Attraction native species"; Species.displayName = "Attraction specimen";
            Species.minimumSpace = 0; Species.requiredTrees = 0; Species.minimumCompanions = 0;
            Species.pettable = true; Species.diet = CreatureDiet.None; Species.threat = 0; Species.damage = 0;
            Species.initialHunger = 0; Species.energyDrainPerSecond = Species.hungerPerSecond = 0;
            Species.pauseMinSeconds = Species.pauseMaxSeconds = 10000;
            Species.randomEscapeChance = Species.hungryEscapeChance = Species.agitatedEscapeChance = Species.crowdedEscapeChance = 0;
            Creatures = new CreatureWorld(Grid, Fences, Buildings, null, material, new[] { Species }, null, simulation);
            VisitorDefinition = ScriptableObject.CreateInstance<VisitorDefinition>(); owned.Add(VisitorDefinition);
            VisitorDefinition.people = null; VisitorDefinition.maximumVisitors = 0; VisitorDefinition.spawnInterval = 100000;
            VisitorDefinition.initialHunger = VisitorDefinition.hungerPerSecond = VisitorDefinition.energyDrainPerSecond = 0;
            VisitorDefinition.initialEnergy = 100; VisitorDefinition.minimumStaySeconds = 100000; VisitorDefinition.maximumStaySeconds = 100000;
            VisitorDefinition.walkSpeed = VisitorDefinition.runSpeed = 30; VisitorDefinition.arrivalPathCellsPerFrame = 10000;
            VisitorDefinition.mood.initialSatisfaction = 40; VisitorDefinition.mood.baselineLossPerSecond = 0;
            VisitorDefinition.attractionVisitGapSeconds = 1000;
            Visitors = new VisitorWorld(Grid, Fences, Buildings, Creatures, VisitorDefinition, Economy); worlds.Add(Visitors);
            Visitors.ConfigureAttractions(pettingEntry: PettingEntry);
        }
        private Vector2Int? PettingEntry(ParkBuildingRecord home)
        {
            if (!Buildings.Contains(home) || !home.IsOpen || !Fences.HasFence(home.Footprint.RearEdge)
                || Fences.IsGate(home.Footprint.RearEdge) || Fences.HasWindow(home.Footprint.RearEdge)) return null;
            var entry = home.Footprint.RearAccessCell;
            var region = new CreatureNavigation(Grid, Fences, Creatures.IsClear).Reachable(entry, Grid.Width * Grid.Depth);
            if (region.Count == 0 || region.Any(cell => cell.x == 0 || cell.y == 0 || cell.x == Grid.Width - 1 || cell.y == Grid.Depth - 1)) return null;
            bool any = false;
            foreach (var animal in Creatures.Agents)
            {
                if (!animal.Alive || Field<bool>(animal, "ExternallyControlled") || !region.Contains(animal.Cell)) continue;
                any = true;
                if (!animal.Definition.pettable || animal.Definition.threat > home.Footprint.Definition.attraction.maximumCreatureThreat) return null;
            }
            return any ? entry : (Vector2Int?)null;
        }
        public ParkBuildingRecord Venue(BuildingKind kind)
        {
            var data = AttractionDefinition.Building(kind);
            data.width = data.depth = 1; data.attractionPrice = 3; data.serviceSeconds = .3f; data.serviceCapacity = 1;
            data.attraction.maximumPrice = 100; data.attraction.exhibitRadius = 12;
            var record = Buildings.TryPlace(new BuildingFootprint(data, V(7, 3), 0));
            Buildings.Refresh(); Check(record != null && record.IsOpen, "a new attraction has real connected access: " + kind);
            return record;
        }
        public ParkBuildingRecord ViewingRoom()
        {
            var data = AttractionDefinition.Building(BuildingKind.ViewingWindow);
            data.attractionPrice = 3; data.serviceSeconds = 10; data.serviceCapacity = 1;
            data.attraction.maximumPrice = 100; data.attraction.exhibitRadius = 12;
            var record = Buildings.TryPlace(new BuildingFootprint(data, V(7, 2), 0, true));
            Buildings.Refresh(); Check(record != null && record.IsOpen, "a two-by-one viewing room connects to the existing outside path");
            return record;
        }
        public void Pen(int left, int bottom, int right, int top)
        {
            for (int x = left; x < right; x++) { Fences.TryAdd(new FenceEdge(V(x, bottom), true)); Fences.TryAdd(new FenceEdge(V(x, top), true)); }
            for (int y = bottom; y < top; y++) { Fences.TryAdd(new FenceEdge(V(left, y), false)); Fences.TryAdd(new FenceEdge(V(right, y), false)); }
            Buildings.Refresh();
        }
        public CreatureAgent Animal(Vector2Int cell, CreatureDefinition species = null)
        {
            Check(Creatures.TrySpawn(species ?? Species, cell), "an actual exhibit animal can be placed");
            return Creatures.Agents[Creatures.Agents.Count - 1];
        }
        public VisitorAgent Guest(VisitorType type = VisitorType.Children)
            => (VisitorAgent)Call(Visitors, "RestoreGuestWithType", Buildings.Entrance.Footprint.AccessCell, type);
        public bool Choose(VisitorAgent guest) => (bool)Call(Visitors, "TryAttraction", guest,
            new VisitorNavigation(Grid, Fences, Creatures.IsClear).Reachable(guest.Actor.Cell));
        public void Step(int count = 1, bool creatures = false)
        { for (int i = 0; i < count; i++) { if (creatures) Creatures.Tick(.05f); Visitors.Tick(.05f); } }
        public bool Until(Func<bool> predicate, bool creatures = false, int maximum = 500)
        { for (int i = 0; i < maximum; i++) { if (predicate()) return true; Step(1, creatures); } return predicate(); }
    }

    public static int RunChecks()
    {
        checks = 0;
        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        var random = UnityEngine.Random.state;
        var owned = new HashSet<Object>(); var worlds = new List<VisitorWorld>();
        int population = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var originalWindow = ViewingWindowDefinition.Current;
        try
        {
            int fixtureId = 0;
            foreach (var kind in new[] { BuildingKind.PettingZoo, BuildingKind.ViewingTower, BuildingKind.BalloonRide,
                BuildingKind.PredatorFeedingGallery, BuildingKind.TricksterTheatre, BuildingKind.CabinetOfCuriosities,
                BuildingKind.ExpeditionPavilion, BuildingKind.ClockworkCarousel })
            {
                var f = new Fixture(owned, worlds, fixtureId++); var venue = f.Venue(kind);
                VisitorType type = kind == BuildingKind.BalloonRide || kind == BuildingKind.CabinetOfCuriosities ? VisitorType.Wonderlust
                    : kind == BuildingKind.ViewingTower || kind == BuildingKind.ExpeditionPavilion ? VisitorType.Adventurers
                    : kind == BuildingKind.PredatorFeedingGallery ? VisitorType.ThrillSeekers : VisitorType.Children;
                if (kind == BuildingKind.PettingZoo) { f.Pen(5, 4, 11, 9); f.Animal(V(7, 6)); }
                else if (venue.Footprint.Definition.attraction.requiresCreatures)
                {
                    f.Species.feedsOnFear = kind == BuildingKind.TricksterTheatre;
                    if (f.Species.feedsOnFear) f.Species.initialHunger = 60;
                    f.Species.threat = kind == BuildingKind.PredatorFeedingGallery ? 100 : kind == BuildingKind.TricksterTheatre ? 20 : 0;
                    f.Pen(8, 1, 9, 2); f.Animal(V(8, 1));
                }
                var guest = f.Guest(type); Check(guest != null && f.Choose(guest), "the intended visitor type chooses " + kind);
                Check(f.Until(() => f.Visitors.CaptureAttraction(guest, f.Buildings.Records).phase == VisitorAttractionPhase.Visiting), "visitor reaches the actual paid experience: " + kind);
                Check(f.Visitors.AttractionOccupancy(venue) == 1, "only active visits drive attraction occupancy: " + kind);
                if (kind == BuildingKind.TricksterTheatre)
                {
                    var performer = f.Creatures.Agents[0];
                    Check(performer.Activity == CreatureActivity.Scaring && performer.Vitals.Hunger == 60 && guest.Actor.Vitals.Fear == 0,
                        "a controlled trickster jump begins without immediately granting food or panicking its audience");
                    Check(f.Until(() => performer.Vitals.Hunger < 60, true), "a completed stage scare feeds the actual fear-feeding creature");
                }
                if (kind == BuildingKind.PettingZoo)
                {
                    Check(Field<ParkBuildingRecord>(guest.Actor, "PettingHost") == venue && guest.Actor.Cell.y >= 4,
                        "petting visitors physically enter the secured enclosure");
                    Check(ConstructionRouteGraph.CellDistance(guest.Actor.Cell, f.Creatures.Agents[0].Cell) <= 1,
                        "petting visitors approach a suitable animal rather than remaining at the gate");
                }
                Check(f.Until(() => venue.Revenue == 3), "completed visit credits its building: " + kind);
                Check(f.Economy.Income == 3, "attraction income is charged exactly once: " + kind);
                Check(f.Until(() => Field<object>(guest, "Attraction") == null), "visitor leaves the completed experience: " + kind);
                f.Step(10); Check(venue.Revenue == 3, "a cooldown prevents immediate repeat purchases: " + kind);
            }

            // Quoted fees survive changes and a mid-visit DTO round trip without charging on restore.
            var quote = new Fixture(owned, worlds, fixtureId++); var carousel = quote.Venue(BuildingKind.ClockworkCarousel);
            carousel.Footprint.Definition.serviceSeconds = 2;
            var buyer = quote.Guest(); Check(quote.Choose(buyer) && quote.Until(() => quote.Visitors.CaptureAttraction(buyer, quote.Buildings.Records).phase == VisitorAttractionPhase.Visiting), "a quote fixture starts its paid visit");
            var saved = quote.Visitors.CaptureAttraction(buyer, quote.Buildings.Records);
            var roundTrip = JsonUtility.FromJson<VisitorAttractionSave>(JsonUtility.ToJson(saved));
            VisitorWorld.ValidateAttractionSave(roundTrip, quote.Buildings.Records, quote.Grid, quote.Fences);
            Check(roundTrip.quotedPrice == 3 && roundTrip.remaining > 0 && carousel.Revenue == 0, "the save retains the original uncharged quote and remaining visit");
            Call(quote.Visitors, "CancelAttraction", buyer, false);
            Check(carousel.SetPrice(99), "the player can change prices while an earlier quote is active");
            quote.Visitors.RestoreAttraction(buyer, roundTrip, quote.Buildings.Records);
            Check(quote.Economy.Income == 0 && buyer.Activity == VisitorActivity.EnjoyingAttraction, "restoring an active visit never charges it");
            Check(quote.Until(() => carousel.Revenue == 3), "completion uses the saved quote even when the current ticket changes");
            quote.Step(20); Check(quote.Economy.Income == 3, "the restored receipt is credited only once");
            var memory = quote.Visitors.CaptureAttraction(buyer, quote.Buildings.Records);
            Check(memory.history.Count == 1 && memory.choiceCooldown > 0 && memory.history[0].remaining > 0,
                "completed-attraction cooldowns are captured as relative times");

            // Missing exhibits and demolished venues cannot sell a fake experience.
            var empty = new Fixture(owned, worlds, fixtureId++); var gallery = empty.Venue(BuildingKind.PredatorFeedingGallery);
            var watcher = empty.Guest(VisitorType.ThrillSeekers);
            Check(!empty.Choose(watcher) && empty.Economy.Income == 0 && empty.Visitors.AttractionAvailabilityReason(gallery) != "",
                "an empty creature-linked attraction has an explicit unavailable reason and takes no payment");
            var demolition = new Fixture(owned, worlds, fixtureId++); var ride = demolition.Venue(BuildingKind.ClockworkCarousel);
            var rider = demolition.Guest(); Check(demolition.Choose(rider), "a visitor can reserve a ride");
            Check(demolition.Buildings.Remove(ride), "the reserved ride can be demolished"); demolition.Step(2);
            Check(Field<object>(rider, "Attraction") == null && demolition.Economy.Income == 0, "demolition cancels the reservation without payment");

            // New viewing rooms physically admit visitors while containing nearby predators.
            var roomFixture = new Fixture(owned, worlds, fixtureId++); roomFixture.Pen(6, 3, 12, 9);
            var room = roomFixture.ViewingRoom(); roomFixture.Species.threat = 100;
            var predator = roomFixture.Animal(V(7, 5));
            var roomGuest = roomFixture.Guest(VisitorType.ThrillSeekers); var waitingGuest = roomFixture.Guest(VisitorType.ThrillSeekers);
            Check(roomFixture.Choose(roomGuest) && !roomFixture.Choose(waitingGuest), "a reserved viewing-room seat counts toward capacity while walking");
            Check(roomFixture.Until(() => roomGuest.Activity == VisitorActivity.EnjoyingAttraction), "a visitor enters the protected room instead of watching from the path");
            Check(roomGuest.Actor.Cell == room.Footprint.RearAccessCell && room.Footprint.Contains(roomGuest.Actor.Cell)
                && roomFixture.Grid.ContentAt(roomGuest.Actor.Cell) == BuildCellContent.Building,
                "the paying visitor stands one square inside the enclosure within the reserved building");
            Check(room.Footprint.Cells().All(cell => !new CreatureNavigation(roomFixture.Grid, roomFixture.Fences).IsWalkable(cell)),
                "animals cannot use the viewing room as an entrance or stand inside it");
            roomFixture.Step(3);
            Check(!Field<bool>(roomGuest, "Panicked") && roomGuest.Actor.Vitals.Fear == 0 && predator.Alive,
                "observing a dangerous enclosed creature through the protected room does not trigger an escape panic");
            var roomSave = JsonUtility.FromJson<VisitorAttractionSave>(JsonUtility.ToJson(roomFixture.Visitors.CaptureAttraction(roomGuest, roomFixture.Buildings.Records)));
            VisitorWorld.ValidateAttractionSave(roomSave, roomFixture.Buildings.Records, roomFixture.Grid, roomFixture.Fences);
            Check(roomSave.hasViewingRoom && roomSave.insideViewingRoom && !roomSave.hasWindow && roomSave.quotedPrice == 3,
                "a room save retains its indoor authorization and original unpaid ticket quote");
            Check(room.SetPrice(99), "viewing-room prices remain adjustable during a visit");
            roomSave.remaining = .1f;
            roomFixture.Visitors.RestoreAttraction(roomGuest, roomSave, roomFixture.Buildings.Records);
            Check(roomFixture.Economy.Income == 0, "restoring an indoor room visit does not charge early");
            Check(roomFixture.Until(() => room.Revenue == 3), "the indoor visit completes using its saved quote");
            var paidExit = roomFixture.Visitors.CaptureAttraction(roomGuest, roomFixture.Buildings.Records);
            Check(paidExit.completed && paidExit.phase == VisitorAttractionPhase.ExitingViewingRoom,
                "a paid visitor retains the exit phase until it physically reaches the public path");
            VisitorWorld.ValidateAttractionSave(paidExit, roomFixture.Buildings.Records, roomFixture.Grid, roomFixture.Fences);
            roomFixture.Visitors.RestoreAttraction(roomGuest, paidExit, roomFixture.Buildings.Records);
            Check(roomFixture.Until(() => Field<object>(roomGuest, "Attraction") == null && roomGuest.Actor.Cell == room.Footprint.AccessCell),
                "a visitor leaves the protected room through its original public entrance");
            roomFixture.Step(20); Check(roomFixture.Economy.Income == 3, "the restored viewing-room visit is paid exactly once");

            var removedRoomFixture = new Fixture(owned, worlds, fixtureId++); removedRoomFixture.Pen(6, 3, 12, 9);
            var removedRoom = removedRoomFixture.ViewingRoom(); removedRoomFixture.Animal(V(7, 5));
            var displaced = removedRoomFixture.Guest(VisitorType.ThrillSeekers);
            Check(removedRoomFixture.Choose(displaced) && removedRoomFixture.Until(() => displaced.Activity == VisitorActivity.EnjoyingAttraction),
                "a second real room visit reaches its interior before demolition");
            Check(removedRoomFixture.Buildings.Remove(removedRoom), "an occupied viewing room can be demolished");
            Check(removedRoomFixture.Until(() => Field<object>(displaced, "Attraction") == null && displaced.Actor.Cell == removedRoom.Footprint.AccessCell)
                && removedRoomFixture.Economy.Income == 0, "demolition returns the indoor guest to the public path without charging");

            var disconnectedRoomFixture = new Fixture(owned, worlds, fixtureId++); disconnectedRoomFixture.Pen(6, 3, 12, 9);
            var disconnectedRoom = disconnectedRoomFixture.ViewingRoom(); disconnectedRoomFixture.Animal(V(7, 5));
            var disconnectedGuest = disconnectedRoomFixture.Guest(VisitorType.ThrillSeekers);
            Check(disconnectedRoomFixture.Choose(disconnectedGuest)
                && disconnectedRoomFixture.Until(() => disconnectedGuest.Activity == VisitorActivity.EnjoyingAttraction),
                "a viewing-room visitor reaches the interior before the access path is removed");
            Check(disconnectedRoomFixture.Grid.TryRemove(disconnectedRoom.Footprint.AccessCell, BuildCellContent.Path),
                "the public entrance path can be removed during an active indoor visit");
            disconnectedRoomFixture.Buildings.Refresh();
            Check(!disconnectedRoom.IsOpen && disconnectedRoomFixture.Grid.IsAvailable(disconnectedRoom.Footprint.AccessCell),
                "removing the path closes the room while leaving clear ground outside its entrance");
            Check(disconnectedRoomFixture.Until(() => Field<object>(disconnectedGuest, "Attraction") == null
                && disconnectedGuest.Actor.Cell == disconnectedRoom.Footprint.AccessCell) && disconnectedRoomFixture.Economy.Income == 0,
                "the interrupted guest exits onto clear entrance ground without being trapped or charged");

            // Legacy viewing windows retain their prior fence-upgrade visit and save semantics.
            var windowSettings = Object.Instantiate(originalWindow); owned.Add(windowSettings); windowSettings.serviceSeconds = .3f; windowSettings.serviceCapacity = 1;
            typeof(ViewingWindowDefinition).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, windowSettings);
            var windowFixture = new Fixture(owned, worlds, fixtureId++); windowFixture.Pen(8, 1, 9, 2); windowFixture.Animal(V(8, 1));
            var edge = new FenceEdge(V(8, 2), true);
            Check(windowFixture.Fences.TrySetWindow(edge, 5), "an existing fence accepts a paid viewing window");
            var window = windowFixture.Fences.WindowAt(edge); var first = windowFixture.Guest(VisitorType.ThrillSeekers); var second = windowFixture.Guest(VisitorType.ThrillSeekers);
            Check(windowFixture.Choose(first) && !windowFixture.Choose(second), "a reserved window seat counts toward its capacity");
            Check(windowFixture.Until(() => first.Activity == VisitorActivity.EnjoyingAttraction), "the paid window has a resumable active visit");
            var windowSave = JsonUtility.FromJson<VisitorAttractionSave>(JsonUtility.ToJson(windowFixture.Visitors.CaptureAttraction(first, windowFixture.Buildings.Records)));
            VisitorWorld.ValidateAttractionSave(windowSave, windowFixture.Buildings.Records, windowFixture.Grid, windowFixture.Fences);
            Check(windowSave.hasWindow && windowSave.building == -1 && windowSave.windowCell == edge.Start && windowSave.windowAlongX == edge.AlongX,
                "window saves identify their live fence edge without a building record");
            Call(windowFixture.Visitors, "CancelAttraction", first, false);
            windowFixture.Visitors.RestoreAttraction(first, windowSave, windowFixture.Buildings.Records);
            Check(window.Revenue == 0 && first.Activity == VisitorActivity.EnjoyingAttraction, "restoring a window reservation never credits its ticket early");
            Check(windowFixture.Until(() => window.Revenue == 5), "a completed close-up window visit earns its ticket");
            Check(windowFixture.Fences.HasFence(edge) && !windowFixture.Fences.IsGate(edge), "window service does not open the physical barrier");

            // An interrupted petting visit leaves through the authorized portal even after demolition.
            var pet = new Fixture(owned, worlds, fixtureId++); var petting = pet.Venue(BuildingKind.PettingZoo); petting.Footprint.Definition.serviceSeconds = 3;
            pet.Pen(5, 4, 11, 9); pet.Animal(V(7, 6)); var child = pet.Guest();
            Check(pet.Choose(child) && pet.Until(() => child.Activity == VisitorActivity.EnjoyingAttraction), "the cancellation fixture enters an actual petting enclosure");
            Check(pet.Buildings.Remove(petting), "a petting gateway may be demolished during a visit");
            var orphan = pet.Visitors.CaptureAttraction(child, pet.Buildings.Records);
            VisitorWorld.ValidateAttractionSave(orphan, pet.Buildings.Records, pet.Grid, pet.Fences);
            Check(orphan.building == -1 && orphan.insidePetting && orphan.interrupted, "a save retains a demolished gateway only as a safe exit");
            Check(pet.Until(() => Field<object>(child, "Attraction") == null && child.Actor.Cell == petting.Footprint.AccessCell),
                "the visitor physically exits after the gateway is removed");
            Check(pet.Economy.Income == 0 && Field<ParkBuildingRecord>(child.Actor, "PettingHost") == null,
                "an interrupted petting visit has neither a payment nor a stale contact authorization");
        }
        finally
        {
            typeof(ViewingWindowDefinition).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, originalWindow);
            foreach (var world in worlds)
                foreach (var field in world.GetType().GetFields(Flags))
                {
                    if (field.GetValue(world) is Object value && !AssetDatabase.Contains(value)) owned.Add(value);
                    if (field.GetValue(world) is IDictionary dictionary)
                        foreach (var item in dictionary.Values) if (item is Object asset && !AssetDatabase.Contains(asset)) owned.Add(asset);
                }
            EditorSceneManager.CloseScene(scene, true);
            foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
        }
        Check(original.isLoaded && original.isDirty == dirty && SceneManager.GetActiveScene() == original, "the original scene and dirty flag are restored");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == population, "all temporary visitor and creature actors are removed");
        return checks;
    }
}

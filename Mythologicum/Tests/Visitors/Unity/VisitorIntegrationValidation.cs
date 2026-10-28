using System;
using System.IO;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class VisitorIntegrationValidation
{
    private const string Running = "VisitorIntegrationValidation.running";
    private static int checks;
    static VisitorIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); Run(); }
        };
    }

    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/VisitorValidation.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Debug.Log("VISITOR CHECK PASS: " + message);
    }
    private static Vector2Int V(int x, int y) => new Vector2Int(x,y);

    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly ParkBuildingNetwork Network;
        public readonly CreatureWorld Creatures;
        public readonly VisitorWorld Visitors;
        public readonly VisitorDefinition Definition;
        public readonly CreatureDefinition Goat, Predator;
        public readonly ParkEconomy Economy = new ParkEconomy(5000);
        public Fixture(Action<VisitorDefinition> configure = null)
        {
            foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(old);
            UnityEngine.Random.InitState(1234);
            Grid = new SquareBuildGrid(Vector3.zero, 2, 12, 12);
            Fences = new FenceLayout(Grid); Network = new ParkBuildingNetwork(Grid, Fences);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(12,-.5f,12);
            ground.transform.localScale = new Vector3(24,1,24);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(12,20,-5); camera.transform.rotation = Quaternion.Euler(45,0,0);
            Goat = ScriptableObject.CreateInstance<CreatureDefinition>();
            Goat.displayName = "Exhibit goat"; Goat.diet = CreatureDiet.Herbivore; Goat.tags = CreatureTags.Goat;
            Goat.initialHunger = 0; Goat.hungerPerSecond = 0; Goat.walkSpeed = 0;
            Goat.energyDrainPerSecond = 0; Goat.initialEnergy = 100; Goat.fearedTags = CreatureTags.None;
            Goat.threat = 5; Goat.maximumHealth = 40;
            Predator = ScriptableObject.CreateInstance<CreatureDefinition>();
            Predator.displayName = "Exhibit predator"; Predator.tags = CreatureTags.Predator;
            Predator.diet = CreatureDiet.Carnivore; Predator.preyTags = CreatureTags.Visitor;
            Predator.initialHunger = 100; Predator.hungerPerSecond = 0; Predator.energyDrainPerSecond = 0;
            Predator.initialEnergy = 100; Predator.runSpeed = 8; Predator.walkSpeed = 0;
            Predator.visionRange = 40; Predator.decisionInterval = .1f;
            Predator.threat = 40; Predator.damage = 60; // One-hit visitor fixture; visitor health is 60.
            Goat.randomEscapeChance = Goat.hungryEscapeChance = Goat.agitatedEscapeChance = Goat.crowdedEscapeChance = 0;
            Predator.randomEscapeChance = Predator.hungryEscapeChance = Predator.agitatedEscapeChance = Predator.crowdedEscapeChance = 0;
            Creatures = new CreatureWorld(Grid, Fences, Network, camera,
                new Material(Shader.Find("Standard")), new[] { Goat, Predator }, new EnvironmentGrid(Grid));
            Definition = ScriptableObject.CreateInstance<VisitorDefinition>();
            Definition.spawnInterval = 10000; Definition.maximumVisitors = 1;
            Definition.hungerPerSecond = 0; Definition.energyDrainPerSecond = 0;
            Definition.walkSpeed = 4; Definition.decisionInterval = .1f;
            Definition.minimumStaySeconds = 1000; Definition.maximumStaySeconds = 10000;
            configure?.Invoke(Definition);
            Visitors = new VisitorWorld(Grid, Fences, Network, Creatures, Definition, Economy);
        }
        public ParkBuildingRecord Entrance(bool path = true)
        {
            var record = Network.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, V(1,2), 0));
            if (path) Path();
            return record;
        }
        public void Path()
        {
            for (int x = 1; x <= 10; x++) Grid.TryOccupy(V(x,1), BuildCellContent.Path);
            Network.Refresh();
        }
        public ParkBuildingRecord Service(BuildingKind kind, int x, float seconds = 2, int capacity = 1, float recovery = 10)
        {
            return Network.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = kind, width = 1, depth = 1, serviceSeconds = seconds, serviceCapacity = capacity,
                    foodRelief = 65, foodPrice = 8, restRecoveryPerSecond = recovery }, V(x,2), 0));
        }
        public void Tick(float seconds)
        { for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { Creatures.Tick(.05f); Visitors.Tick(.05f); } }
        public bool Until(Func<bool> condition, float timeout)
        {
            for (float elapsed = 0; elapsed < timeout; elapsed += .05f)
            { if (condition()) return true; Tick(.05f); }
            return condition();
        }
        public void ContainPredator()
        {
            for (int x = 3; x <= 5; x++)
            { Fences.TryAdd(new FenceEdge(V(x,2), true), FenceKind.Heavy); Fences.TryAdd(new FenceEdge(V(x,5), true), FenceKind.Heavy); }
            for (int y = 2; y <= 4; y++)
            { Fences.TryAdd(new FenceEdge(V(3,y), false), FenceKind.Heavy); Fences.TryAdd(new FenceEdge(V(6,y), false), FenceKind.Heavy); }
        }
    }

    private static void CheckArrivalsAndDeparture()
    {
        var f = new Fixture();
        Check(f.Visitors.TrySpawn() == null && f.Economy.Income == 0, "Guests and ticket revenue require a park entrance");
        var entrance = f.Entrance(false);
        Check(entrance != null && !entrance.IsOpen && f.Visitors.TrySpawn() == null,
            "Entrance without an access path does not admit visitors");
        f.Path(); var guest = f.Visitors.TrySpawn();
        Check(guest != null && guest.Actor.Alive && guest.Actor.Definition.tags == CreatureTags.Visitor,
            "Connected entrance registers visitor in shared creature actor system");
        Check(f.Economy.Income == 15 && f.Economy.Balance == 5015,
            "Admitting visitor charges one ticket into the economy");
        Check(f.Visitors.TrySpawn() == null && f.Economy.Income == 15,
            "Population limit or occupied arrival does not charge phantom tickets");
        Check(f.Until(() => guest.Actor.Cell != V(1,1), 5), "Arriving visitor starts exploring public paths");
        f.Definition.minimumStaySeconds = 0; guest.Mood.Punish(100);
        var actor = guest.Actor;
        Check(f.Until(() => f.Visitors.DepartedCount == 1, 20), "Unhappy guest walks back and departs through park entrance");
        bool registered = false; foreach (var remaining in f.Creatures.Agents) if (remaining == actor) registered = true;
        Check(!registered && f.Visitors.Population == 0 && f.Visitors.LostCount == 0,
            "Normal departure removes actor from predator targets without reporting a death");

        f = new Fixture(def => { def.spawnInterval = .2f; def.maximumVisitors = 2; });
        f.Entrance();
        Check(f.Until(() => f.Visitors.Population == 2, 8), "Open park automatically admits visitors over time");
        f.Tick(3);
        Check(f.Visitors.Population == 2 && f.Economy.Income == 30, "Automatic arrivals respect capacity and charge each ticket once");
    }

    private static void CheckViewingAndServices()
    {
        var f = new Fixture(); f.Entrance(); f.Creatures.TrySpawn(f.Goat, V(5,3));
        var guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Watching, 10), "Visitor finds an actual visible creature and a reachable viewing spot");
        float satisfaction = guest.Mood.Satisfaction; f.Tick(2);
        Check(guest.Mood.Satisfaction > satisfaction, "Watching a living visible creature improves satisfaction");
        Check(f.Visitors.AverageSatisfaction > 0 && guest.TimeInPark > 0, "Visitor overview exposes actual mood and visit age");

        f = new Fixture(def => def.initialHunger = 90); f.Entrance(); var kiosk = f.Service(BuildingKind.Kiosk, 8);
        guest = f.Visitors.TrySpawn();
        Check(kiosk.IsOpen && f.Until(() => guest.Activity == VisitorActivity.SeekingFood, 1), "Hungry visitor chooses a connected kiosk");
        Check(guest.Actor.Cell != kiosk.Footprint.AccessCell && guest.Actor.Vitals.Hunger == 90,
            "Selecting distant kiosk does not instantly serve or teleport visitor");
        Check(f.Until(() => guest.Activity == VisitorActivity.Eating, 10), "Hungry guest walks along paths before eating");
        Check(guest.Actor.Cell == kiosk.Footprint.AccessCell && f.Economy.Income == 15,
            "Meal begins at dedicated kiosk entrance without premature food revenue");
        f.Tick(.5f);
        Check(guest.Actor.Vitals.Hunger == 90 && f.Economy.Income == 15, "Food and payment wait for meal duration");
        Check(f.Until(() => guest.Actor.Vitals.Hunger < 40, 3), "Finishing food relieves visitor hunger");
        Check(f.Economy.Income == 23, "One completed kiosk meal earns its configured price");
        f.Tick(3); Check(f.Economy.Income == 23, "Completed meal cannot repeatedly charge guest");

        f = new Fixture(def => def.initialEnergy = 0); f.Entrance(); var rest = f.Service(BuildingKind.RestArea, 5);
        guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Resting, 10), "Tired guest walks to a connected rest area");
        float energy = guest.Actor.Energy.Energy; f.Tick(1);
        Check(guest.Actor.Energy.Energy > energy && f.Economy.Income == 15,
            "Rest area restores energy without charging money");
        Check(f.Until(() => guest.Actor.Energy.Energy >= f.Definition.comfortableEnergy, 12), "Guest rests until comfortably recovered");

        f = new Fixture(def => { def.initialEnergy = 0; def.maximumVisitors = 2; });
        f.Entrance(); f.Service(BuildingKind.RestArea, 8, capacity: 1, recovery: 1);
        var first = f.Visitors.TrySpawn();
        Check(f.Until(() => first.Actor.Cell.x >= 2, 5), "First guest clears arrival for rest capacity scenario");
        var second = f.Visitors.TrySpawn(); Check(second != null, "Second tired guest enters capacity scenario");
        bool overCapacity = false;
        for (int i = 0; i < 160; i++)
        {
            f.Tick(.05f); int occupied = 0;
            foreach (var candidate in f.Visitors.Visitors)
                if (candidate.Activity == VisitorActivity.Resting || candidate.Activity == VisitorActivity.SeekingRest) occupied++;
            if (occupied > 1) overCapacity = true;
        }
        Check(!overCapacity && f.Visitors.Population == 2, "Rest reservations respect single-place capacity");
        Check(f.Economy.Income == 30, "Waiting and resting remain free for both guests");
    }

    private static void CheckServiceInterruptionAndStranding()
    {
        var f = new Fixture(def => def.initialHunger = 90); f.Entrance(); var kiosk = f.Service(BuildingKind.Kiosk, 5, 4);
        var guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Eating, 10), "Kiosk demolition scenario reaches eating");
        f.Network.Remove(kiosk); f.Tick(5);
        Check(guest.Actor.Vitals.Hunger == 90 && f.Economy.Income == 15 && guest.Activity != VisitorActivity.Eating,
            "Demolished kiosk interrupts meal without food or phantom payment");

        f = new Fixture(def => def.initialEnergy = 0); f.Entrance(); var rest = f.Service(BuildingKind.RestArea, 5);
        guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Resting, 10), "Rest demolition scenario reaches resting");
        f.Tick(.5f); float before = guest.Actor.Energy.Energy; f.Network.Remove(rest); f.Tick(2);
        Check(guest.Actor.Energy.Energy <= before && guest.Activity != VisitorActivity.Resting,
            "Demolished rest area immediately stops energy recovery");

        f = new Fixture(); f.Entrance(); f.Creatures.TrySpawn(f.Goat, V(9,3));
        guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Actor.Cell.x >= 5, 15), "Disconnection scenario moves visitor away from entrance");
        f.Grid.TryRemove(V(3,1), BuildCellContent.Path); f.Network.Refresh();
        f.Definition.minimumStaySeconds = 0; guest.Mood.Punish(100);
        Check(f.Until(() => guest.Activity == VisitorActivity.Stranded, 2), "Broken path strands departing guest instead of teleporting out");
        Check(f.Visitors.StrandedCount == 1 && f.Visitors.DepartedCount == 0, "Stranded visitors remain represented in park overview");
        f.Grid.TryOccupy(V(3,1), BuildCellContent.Path); f.Network.Refresh();
        Check(f.Until(() => f.Visitors.DepartedCount == 1, 15), "Repairing path lets stranded guest resume departure");
    }

    private static void CheckFearAndPredation()
    {
        var f = new Fixture(); f.Entrance(); f.ContainPredator();
        f.Creatures.TrySpawn(f.Predator, V(4,3)); var guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Watching, 12), "Guest can watch a predator contained behind fences");
        f.Tick(2);
        Check(guest.Actor.Vitals.Fear == 0 && guest.Activity != VisitorActivity.Fleeing && f.Visitors.Population == 1,
            "Safely contained predator does not panic visitors or eat through a barrier");

        f = new Fixture(def => { def.fearRange = 8; def.fleeSafeDistance = 10; });
        f.Entrance(); f.Predator.preyTags = CreatureTags.None;
        for (int x = 4; x <= 10; x++) f.Grid.TryRemove(V(x,1), BuildCellContent.Path);
        f.Network.Refresh();
        // Place danger beside the path; the entrance at (1,2) must not occlude this perception test.
        f.Creatures.TrySpawn(f.Predator, V(3,3)); guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Fleeing && guest.Actor.Vitals.Fear > 0, 2),
            "Reachable nearby predator creates visitor fear and fleeing");
        Check(f.Until(() => f.Grid.ContentAt(guest.Actor.Cell) != BuildCellContent.Path, 8),
            "Panicked visitor can run off paths onto open ground");
        f.Predator.tags = CreatureTags.None; // The exit becomes safe after the escape is demonstrated.
        f.Predator.threat = 0f;
        Check(f.Until(() => f.Visitors.DepartedCount == 1, 40), "Escaping guest rejoins paths and leaves through park entrance");

        f = new Fixture(def => { def.walkSpeed = .2f; def.runSpeed = .2f; def.fearRange = 10; });
        f.Entrance(); f.Predator.runSpeed = 10; f.Predator.attackSeconds = 1; f.Predator.feedingSeconds = 1;
        // A predator already on the public path has a clear line of sight to the arriving guest.
        f.Creatures.TrySpawn(f.Predator, V(2,1)); guest = f.Visitors.TrySpawn();
        Check(f.Until(() => guest.Activity == VisitorActivity.Captured, 8), "Escaped hungry predator targets and catches visitor using shared creature system");
        Check(guest.Actor.Alive && f.Visitors.LostCount == 0, "Visible attack happens before visitor death is recorded");
        Check(f.Until(() => f.Visitors.LostCount == 1, 5), "Completed predator attack records one lost visitor");
        Check(f.Visitors.Population == 0 && f.Visitors.DepartedCount == 0, "Killed visitor is removed from active visits without counting normal departure");
        f.Tick(3); Check(f.Visitors.LostCount == 1, "Visitor death cannot be recorded repeatedly");
    }

    private static void Run()
    {
        try
        {
            var profile = Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors");
            Check(profile != null, "Authored default visitor profile loads through Resources");
            Check(profile.spawnInterval == 8 && profile.maximumVisitors == 30 && profile.ticketPrice == 15,
                "Default visitor arrival and ticket values deserialize correctly");
            CheckArrivalsAndDeparture(); CheckViewingAndServices(); CheckServiceInterruptionAndStranding(); CheckFearAndPredation();
            string result = "Visitor integration validation: " + checks + " checks passed.";
            File.WriteAllText("VisitorValidationResults.txt", result); Debug.Log(result); EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("VisitorValidationResults.txt", "FAILED after " + checks + " checks: " + exception);
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
}

using System;
using System.IO;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CreatureIntegrationValidation
{
    private const string Running = "CreatureIntegrationValidation.running";
    private static int checks;
    static CreatureIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            {
                SessionState.SetBool(Running, false);
                Run();
            }
        };
    }

    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/Validation.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
        Debug.Log("CREATURE CHECK PASS: " + message);
    }

    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly ParkBuildingNetwork Network;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition Goat, Predator;
        public Fixture(int width = 12, int depth = 12)
        {
            foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(old);
            UnityEngine.Random.InitState(1234);
            Grid = new SquareBuildGrid(Vector3.zero, 2, width, depth);
            Fences = new FenceLayout(Grid);
            Network = new ParkBuildingNetwork(Grid, Fences);
            Environment = new EnvironmentGrid(Grid);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(width, -0.5f, depth);
            ground.transform.localScale = new Vector3(width * 2, 1, depth * 2);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(width, 20, -5);
            camera.transform.rotation = Quaternion.Euler(45,0,0);
            Goat = ScriptableObject.CreateInstance<CreatureDefinition>();
            Goat.displayName = "Test goat"; Goat.diet = CreatureDiet.Herbivore;
            Goat.tags = CreatureTags.Goat; Goat.fearedTags = CreatureTags.None;
            Goat.initialHunger = 0; Goat.hungerPerSecond = 0; Goat.walkSpeed = 0;
            Goat.runSpeed = 4.5f; Goat.decisionInterval = 0.1f; Goat.visionRange = 100;
            Predator = ScriptableObject.CreateInstance<CreatureDefinition>();
            Predator.displayName = "Test Chupacabra"; Predator.diet = CreatureDiet.Carnivore;
            Predator.tags = CreatureTags.Predator; Predator.preyTags = CreatureTags.Goat;
            Predator.initialHunger = 100; Predator.hungerPerSecond = 0;
            Predator.runSpeed = 5.5f; Predator.visionRange = 100; Predator.decisionInterval = 0.1f;
            // Regression scenarios keep fatigue disabled unless a test explicitly enables it.
            Goat.initialEnergy = Predator.initialEnergy = 100;
            Goat.energyDrainPerSecond = Predator.energyDrainPerSecond = 0;
            World = new CreatureWorld(Grid, Fences, Network, camera,
                new Material(Shader.Find("Standard")), new[] { Goat, Predator }, Environment);
        }
        public void Tick(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / 0.05f); i++) World.Tick(0.05f);
        }
        public CreatureAgent Agent(string name)
        {
            foreach (CreatureAgent agent in UnityEngine.Object.FindObjectsByType<CreatureAgent>(FindObjectsSortMode.None))
                if (agent.Alive && agent.Definition.displayName == name) return agent;
            return null;
        }

        public bool Until(Func<bool> condition, float timeout)
        {
            for (float elapsed = 0; elapsed < timeout; elapsed += .05f)
            {
                if (condition()) return true;
                World.Tick(.05f);
            }
            return condition();
        }
    }

    private static void CheckSleepAndClimbing()
    {
        var f = new Fixture();
        f.Goat.initialEnergy = 100; f.Goat.energyDrainPerSecond = 1;
        f.World.TrySpawn(f.Goat, V(4,4)); var idle = f.Agent("Test goat");
        f.Tick(1);
        Check(idle.Energy.Energy < 100 && idle.Energy.Energy >= 98.9f,
            "Ordinary non-sleeping activity slowly drains creature energy");

        f = new Fixture();
        f.Predator.energyDrainPerSecond = 1; f.Predator.strenuousEnergyMultiplier = 4;
        f.Predator.runSpeed = 1;
        f.World.TrySpawn(f.Predator, V(1,1)); f.World.TrySpawn(f.Goat, V(8,1));
        var hunter = f.Agent("Test Chupacabra");
        Check(f.Until(() => hunter.Activity == CreatureActivity.Hunting, 2),
            "Hunger selects hunting before exertion test");
        float huntingEnergy = hunter.Energy.Energy; f.Tick(.5f);
        Check(huntingEnergy - hunter.Energy.Energy > 1.5f,
            "Hunting drains energy faster than ordinary activity");

        f = new Fixture();
        f.Goat.minimumCompanions = 1; f.Goat.energyDrainPerSecond = 1;
        f.Goat.pauseMinSeconds = f.Goat.pauseMaxSeconds = 0;
        f.Goat.socializingEnergyMultiplier = 4; f.Goat.socializingSeconds = 3;
        f.World.TrySpawn(f.Goat, V(4,4)); f.World.TrySpawn(f.Goat, V(5,4));
        var social = f.Agent("Test goat");
        Check(f.Until(() => social.Activity.ToString() == "Socializing", 2),
            "Nearby herd companions choose a socializing activity");
        float socialEnergy = social.Energy.Energy; f.Tick(.5f);
        Check(socialEnergy - social.Energy.Energy > 1.5f,
            "Socializing drains energy at the species activity multiplier");

        f = new Fixture();
        f.Goat.initialEnergy = 0; f.Goat.sleepRecoveryPerSecond = 40;
        f.Goat.sleepThreshold = 20; f.Goat.wakeThreshold = 80;
        f.World.TrySpawn(f.Goat, V(4,4)); var goat = f.Agent("Test goat");
        Check(f.Until(() => goat.Activity.ToString() == "Sleeping", 2),
            "Exhausted goat falls asleep on ordinary ground");
        float before = goat.Energy.Energy;
        f.Tick(.5f);
        Check(goat.Energy.Energy > before && goat.Energy.Exhaustion < 100,
            "Sleeping restores energy and reduces exhaustion");
        Check(f.Until(() => goat.Energy.Energy >= 80 && goat.Activity.ToString() != "Sleeping", 5),
            "Rested goat wakes at its configured energy threshold");

        f = new Fixture();
        f.Predator.initialHunger = 0; f.Predator.preyTags = CreatureTags.None;
        f.Predator.initialEnergy = 0; f.Predator.requiresTreeForSleep = true;
        f.Predator.sleepRecoveryPerSecond = 40;
        f.World.TrySpawn(f.Predator, V(4,4)); var predator = f.Agent("Test Chupacabra");
        f.Tick(4);
        Check(predator.Energy.Energy == 0 && predator.Activity.ToString() != "Sleeping",
            "Tree-dependent exhausted creature cannot recover by resting on bare ground");
        f.Environment.AddTree(V(5,4));
        Check(f.Until(() => predator.Activity.ToString() == "ClimbingTree", 10),
            "Adding a reachable tree lets exhausted Chupacabra seek and climb it");
        float groundHeight = predator.transform.position.y;
        f.Tick(.3f);
        Check(predator.transform.position.y > groundHeight,
            "Tree climbing animation raises the creature above ground");
        Check(f.Until(() => predator.Activity.ToString() == "Sleeping", 5),
            "Tree climb ends with sleeping above ground");
        Check(predator.transform.position.y > 1.5f,
            "Tree-dependent sleep has an elevated visible position");
        Check(f.Until(() => predator.Activity.ToString() == "DescendingTree", 8),
            "Rested tree sleeper begins a visible descent");
        Check(f.Until(() => predator.Activity.ToString() != "DescendingTree", 5)
            && predator.transform.position.y < 1.5f,
            "Tree descent returns creature to normal ground height");

        f = new Fixture();
        f.Predator.initialHunger = 0; f.Predator.preyTags = CreatureTags.None;
        f.Predator.initialEnergy = 0; f.Predator.requiresTreeForSleep = true;
        f.Predator.sleepRecoveryPerSecond = 1;
        f.Environment.AddTree(V(5,4));
        f.World.TrySpawn(f.Predator, V(4,4)); predator = f.Agent("Test Chupacabra");
        Check(f.Until(() => predator.Activity.ToString() == "Sleeping", 12),
            "Tree removal scenario reaches sleeping state");
        f.Environment.RemoveTree(V(5,4)); f.Tick(.5f);
        Check(predator.Activity.ToString() != "Sleeping",
            "Demolishing occupied sleep tree interrupts sleeping");
        before = predator.Energy.Energy; f.Tick(5);
        Check(predator.Energy.Energy <= before,
            "Removed tree cannot continue to restore sleep energy");

        f = new Fixture(6,3);
        f.Predator.canClimbWoodenFences = true;
        f.Predator.fenceClimbSeconds = 1; f.Predator.climbEnergyCost = 5;
        for (int y = 0; y < 3; y++) f.Fences.TryAdd(new FenceEdge(V(3,y), false));
        f.World.TrySpawn(f.Predator, V(2,1)); f.World.TrySpawn(f.Goat, V(4,1));
        predator = f.Agent("Test Chupacabra");
        Check(f.Until(() => predator.Activity.ToString() == "ClimbingFence", 8),
            "Hungry climbing predator chooses to climb blocking wooden fence");
        groundHeight = predator.transform.position.y; f.Tick(.25f);
        Check(predator.transform.position.y > groundHeight,
            "Fence crossing animates creature lifting over barrier");
        Check(f.Until(() => predator.Cell.x >= 3, 4),
            "Completed wooden fence climb transfers creature to the other side");
        Check(predator.Energy.Energy < 100,
            "Climbing wooden barrier consumes energy");
        Check(f.Until(() => f.World.Population == 1, 15),
            "Predator can hunt prey beyond a climbable wooden barrier");

        f = new Fixture(6,3);
        f.Predator.canClimbWoodenFences = true; f.Predator.failedClimbSeconds = 1;
        f.Predator.climbRetrySeconds = 5;
        for (int y = 0; y < 3; y++) f.Fences.TryAdd(new FenceEdge(V(3,y), false), FenceKind.Spiked);
        f.World.TrySpawn(f.Predator, V(2,1)); f.World.TrySpawn(f.Goat, V(4,1));
        predator = f.Agent("Test Chupacabra");
        Check(f.Until(() => predator.Activity.ToString() == "FailedClimb", 8),
            "Spiked barrier produces a visible failed climb attempt");
        f.Tick(15);
        Check(f.World.Population == 2 && predator.Cell.x < 3,
            "Failed climbing never crosses spiked fence or reaches prey");

        f = new Fixture();
        f.Goat.initialEnergy = 0; f.Goat.fearedTags = CreatureTags.Predator;
        f.Goat.fearRange = 3; f.Goat.sleepRecoveryPerSecond = 1;
        f.Predator.initialHunger = 0; f.Predator.preyTags = CreatureTags.None;
        f.Predator.walkSpeed = 0;
        f.World.TrySpawn(f.Goat, V(4,4)); goat = f.Agent("Test goat");
        Check(f.Until(() => goat.Activity.ToString() == "Sleeping", 2),
            "Exhausted goat sleeps before a threat approaches");
        f.World.TrySpawn(f.Predator, V(3,4)); f.Tick(.3f);
        Check(goat.Activity == CreatureActivity.Fleeing && goat.Vitals.Fear > 0,
            "Nearby predator wakes sleeping goat and fear overrides exhaustion");
    }

    private static void CheckConstructionDuringTraversal()
    {
        var f = new Fixture();
        f.Predator.initialHunger = 0; f.Predator.preyTags = CreatureTags.None;
        f.Predator.initialEnergy = 0; f.Predator.requiresTreeForSleep = true;
        f.Predator.sleepRecoveryPerSecond = 1;
        f.Environment.AddTree(V(5,4));
        f.World.TrySpawn(f.Predator, V(4,4)); var predator = f.Agent("Test Chupacabra");
        Check(f.Until(() => predator.Activity == CreatureActivity.ClimbingTree, 10),
            "Tree demolition during ascent scenario reaches climbing state");
        f.Tick(.3f); f.Environment.RemoveTree(V(5,4));
        Check(f.Until(() => predator.Activity == CreatureActivity.DescendingTree, 1),
            "Removing tree midway through ascent changes climb into descent");
        f.Tick(4);
        Check(predator.transform.position.y < 1.5f && predator.Energy.Energy == 0
            && predator.Activity != CreatureActivity.Sleeping,
            "Interrupted tree ascent returns safely to ground without recovering energy");

        f = new Fixture(6,3);
        f.Predator.canClimbWoodenFences = true; f.Predator.fenceClimbSeconds = 2;
        for (int y = 0; y < 3; y++) f.Fences.TryAdd(new FenceEdge(V(3,y), false));
        f.World.TrySpawn(f.Predator, V(2,1)); f.World.TrySpawn(f.Goat, V(4,1));
        predator = f.Agent("Test Chupacabra");
        Check(f.Until(() => predator.Activity == CreatureActivity.ClimbingFence, 8),
            "Fence upgrade during traversal scenario reaches climbing state");
        f.Tick(.3f);
        for (int y = 0; y < 3; y++) f.Fences.TrySetKind(new FenceEdge(V(3,y), false), FenceKind.Spiked);
        Check(f.Until(() => predator.Activity == CreatureActivity.FailedClimb, 1),
            "Upgrading an occupied wooden barrier to spikes interrupts the climb");
        f.Tick(8);
        Check(predator.Cell.x < 3 && f.World.Population == 2,
            "Fence upgrade during traversal keeps predator and prey on their original sides");

        f = new Fixture(7,3);
        f.Predator.canClimbWoodenFences = true; f.Predator.walkSpeed = 0;
        for (int y = 0; y < 3; y++)
        {
            f.Fences.TryAdd(new FenceEdge(V(2,y), false), FenceKind.Spiked);
            var gate = new FenceEdge(V(4,y), false);
            f.Fences.TryAdd(gate); f.Fences.TrySetGate(gate);
        }
        f.World.TrySpawn(f.Predator, V(2,1)); f.World.TrySpawn(f.Goat, V(5,1));
        predator = f.Agent("Test Chupacabra");
        Check(!f.Until(() => predator.Activity == CreatureActivity.FailedClimb, 10),
            "Prey behind a different closed barrier does not trigger attempts at an unrelated spiked area");
        Check(f.World.Population == 2 && predator.Cell.x == 2,
            "Unrelated fence scenario preserves closed gate containment");
    }

    private static void CheckNeedsAndBehaviour()
    {
        var f = new Fixture();
        f.Predator.initialHunger = 0; f.Predator.hungerPerSecond = 0;
        f.Predator.preyTags = CreatureTags.None; f.Predator.walkSpeed = 0;
        f.Predator.requiredTrees = 2; f.Predator.minimumSpace = 100;
        f.Predator.minimumCompanions = 0; f.Predator.maximumCompanions = 0;
        f.World.TrySpawn(f.Predator, V(4,4)); f.Tick(.5f);
        CreatureAgent predator = f.Agent("Test Chupacabra");
        Check(predator.NearbyTrees == 0 && predator.Wellbeing.Scores.Environment == 0,
            "Chupacabra evaluates missing trees outside any assigned enclosure");
        Check(predator.AccessibleArea >= 100 && predator.Wellbeing.Scores.Space == 100,
            "Open terrain provides physically accessible creature space");
        f.Environment.AddTree(V(5,4)); f.Environment.AddTree(V(4,5)); f.Tick(1);
        Check(predator.NearbyTrees == 2 && predator.Wellbeing.Scores.Environment == 100,
            "Two nearby trees satisfy Chupacabra environment need dynamically");
        Check(predator.Companions == 0 && predator.Wellbeing.Scores.Herd == 100,
            "Solitary creature is socially satisfied alone");
        f.World.TrySpawn(f.Predator, V(6,4)); f.Tick(.5f);
        Check(predator.Companions == 1 && predator.Wellbeing.Scores.Herd == 0,
            "Nearby same-species companion reduces solitary creature satisfaction");
        f = new Fixture();
        f.Goat.minimumCompanions = 2; f.Goat.maximumCompanions = 8;
        f.World.TrySpawn(f.Goat, V(4,4)); f.Tick(.5f);
        var goat = f.Agent("Test goat");
        Check(goat.Companions == 0 && goat.Wellbeing.Scores.Herd == 0,
            "Herd animal lacks social satisfaction alone");
        f.World.TrySpawn(f.Goat, V(5,4)); f.World.TrySpawn(f.Goat, V(4,5)); f.Tick(.5f);
        Check(goat.Companions == 2 && goat.Wellbeing.Scores.Herd == 100,
            "Two reachable companions satisfy goat herd need");

        f = new Fixture();
        f.Goat.minimumSpace = 48;
        for (int x = 3; x <= 4; x++)
        {
            f.Fences.TryAdd(new FenceEdge(V(x,3), true));
            f.Fences.TryAdd(new FenceEdge(V(x,5), true));
        }
        for (int y = 3; y <= 4; y++)
        {
            f.Fences.TryAdd(new FenceEdge(V(3,y), false));
            f.Fences.TryAdd(new FenceEdge(V(5,y), false));
        }
        f.World.TrySpawn(f.Goat, V(3,3)); f.Tick(.5f);
        goat = f.Agent("Test goat");
        Check(Mathf.Abs(goat.AccessibleArea - 16) < .01f && goat.Wellbeing.Scores.Space < 34,
            "Small fenced enclosure reduces space score to accessible square metres");
        f.Fences.TryRemove(new FenceEdge(V(3,3), true)); f.Tick(.5f);
        Check(goat.AccessibleArea > 48 && goat.Wellbeing.Scores.Space == 100,
            "Opening enclosure dynamically restores available space");

        f = new Fixture();
        f.Predator.minimumSpace = 10000; f.Predator.requiredTrees = 2;
        f.Predator.minimumCompanions = 2; f.Predator.maximumCompanions = 8;
        f.Predator.happinessChangePerSecond = 20;
        f.World.TrySpawn(f.Predator, V(3,3)); f.Tick(6);
        predator = f.Agent("Test Chupacabra");
        Check(predator.Wellbeing.Happiness < 50 && predator.Wellbeing.Agitation > 0,
            "Unmet environment, space, herd and hunger needs cause agitation");

        f = new Fixture();
        f.Goat.pauseMinSeconds = 2; f.Goat.pauseMaxSeconds = 2;
        f.Goat.walkSpeed = 1.5f;
        f.World.TrySpawn(f.Goat, V(4,4));
        goat = f.Agent("Test goat");
        Check(f.Until(() => goat.Activity.ToString() == "Resting", 20),
            "Calm roaming includes resting pauses");

        f = new Fixture();
        f.Predator.runSpeed = 0; f.Predator.interactionRange = 2.1f;
        f.Predator.attackSeconds = 1; f.Predator.feedingSeconds = 2;
        f.World.TrySpawn(f.Predator, V(3,3)); f.World.TrySpawn(f.Goat, V(4,3));
        predator = f.Agent("Test Chupacabra");
        goat = f.Agent("Test goat");
        Check(f.Until(() => predator.Activity.ToString() == "Attacking", 1),
            "Predator starts visible attack phase on nearby reachable prey");
        Check(goat.Alive && predator.Vitals.Hunger == 100,
            "Attack starts before prey death or hunger relief");
        Quaternion initialAttackRotation = predator.transform.Find("Animated placeholder").localRotation;
        f.Tick(.3f);
        Check(goat.Alive && predator.Vitals.Hunger == 100,
            "Attack duration delays killing and nutrition");
        Check(Quaternion.Angle(initialAttackRotation, predator.transform.Find("Animated placeholder").localRotation) > 1,
            "Attacking placeholder visibly changes pose over time");
        Check(f.Until(() => predator.Activity.ToString() == "Feeding", 2),
            "Completed attack transitions into feeding");
        Check(!goat.Alive && predator.Vitals.Hunger == 100,
            "Killed prey does not give instant nutrition");
        Check(Quaternion.Angle(Quaternion.identity, goat.transform.Find("Animated placeholder").localRotation) > 70,
            "Killed prey has a visible collapsed pose");
        Quaternion initialFeedingRotation = predator.transform.Find("Animated placeholder").localRotation;
        f.Tick(.5f);
        Check(predator.Vitals.Hunger == 100, "Feeding duration delays nutrition");
        Check(Quaternion.Angle(initialFeedingRotation, predator.transform.Find("Animated placeholder").localRotation) > 1,
            "Feeding placeholder visibly changes pose over time");
        Check(f.Until(() => predator.Vitals.Hunger < 50, 3),
            "Finishing feeding relieves predator hunger");
    }

    private static void Run()
    {
        try
        {
            CreatureDefinition[] loadedProfiles = Resources.LoadAll<CreatureDefinition>("Creatures");
            Check(loadedProfiles.Length == 2, "Resources.LoadAll finds exactly two creature profiles");
            Check(Array.Exists(loadedProfiles, profile => profile.diet == CreatureDiet.Carnivore
                && profile.displayName == "Chupacabra" && profile.preyTags.HasFlag(CreatureTags.Goat)),
                "Loaded Chupacabra profile is a goat-hunting carnivore");
            Check(Array.Exists(loadedProfiles, profile => profile.diet == CreatureDiet.Herbivore
                && profile.displayName == "Goat" && profile.fearedTags.HasFlag(CreatureTags.Predator)),
                "Loaded goat profile is a predator-fearing herbivore");
            Check(Resources.Load<CreatureDefinition>("Creatures/Chupacabra") != null, "Chupacabra asset loads through Resources");
            Check(Resources.Load<CreatureDefinition>("Creatures/Goat") != null, "Goat asset loads through Resources");
            Check(Resources.Load<CreatureDefinition>("Creatures/Goat").fearRange == 3f, "Actual goat asset fear range is three metres");
            Check(Resources.Load<CreatureDefinition>("Creatures/Goat").visionRange == 14f, "General goat vision unchanged");
            Check(Resources.Load<CreatureDefinition>("Creatures/Chupacabra").requiresTreeForSleep
                && Resources.Load<CreatureDefinition>("Creatures/Chupacabra").canClimbWoodenFences,
                "Authored Chupacabra profile requires tree sleep and enables fence climbing");
            Check(!Resources.Load<CreatureDefinition>("Creatures/Goat").requiresTreeForSleep
                && !Resources.Load<CreatureDefinition>("Creatures/Goat").canClimbWoodenFences,
                "Authored goat profile sleeps on ground and cannot climb fences");
            CheckNeedsAndBehaviour();
            CheckSleepAndClimbing();
            CheckConstructionDuringTraversal();
            var f = new Fixture();
            Check(f.World.TrySpawn(f.Predator, V(1,1)), "CreatureWorld spawns predator on empty natural ground");
            Check(!f.World.CanSpawn(V(1,1)), "Spawn rejects occupied creature position");
            Check(f.World.TrySpawn(f.Goat, V(3,1)), "CreatureWorld spawns goat independently of enclosures");
            f.Tick(9);
            Check(f.World.Population == 1, "Hungry predator hunts and consumes reachable goat");
            Check(f.Agent("Test Chupacabra").Vitals.Hunger < 50, "Successful hunting reduces hunger");

            f = new Fixture();
            f.Goat.initialHunger = 90; f.Goat.grazeSeconds = 0.2f;
            f.World.TrySpawn(f.Goat, V(2,2)); f.Tick(1);
            Check(f.Agent("Test goat").Vitals.Hunger < 50, "Hungry herbivore grazes natural ground");

            f = new Fixture();
            f.Goat.initialHunger = 100; f.Goat.grazeSeconds = 0.2f; f.Goat.fearedTags = CreatureTags.Predator;
            f.Predator.preyTags = CreatureTags.None; f.Predator.walkSpeed = 0;
            f.World.TrySpawn(f.Goat, V(4,4)); f.World.TrySpawn(f.Predator, V(2,4)); f.Tick(1);
            CreatureAgent scared = f.Agent("Test goat");
            Check(scared.Activity == CreatureActivity.Fleeing && scared.Vitals.Fear > 0, "Visible predator creates fear and fleeing");
            Check(scared.Vitals.Hunger == 100, "Fear overrides grazing even at maximum hunger");
            Check(scared.transform.position.x > f.Grid.CellCenter(V(4,4)).x, "Fleeing goat moves away from predator");

            f = new Fixture();
            f.Goat.fearedTags = CreatureTags.Predator; f.Goat.fearRange = 3f;
            f.Predator.preyTags = CreatureTags.None; f.Predator.walkSpeed = 0;
            f.World.TrySpawn(f.Goat, V(4,4)); f.World.TrySpawn(f.Predator, V(2,4)); f.Tick(0.5f);
            var calm = f.Agent("Test goat");
            Check(calm.Vitals.Fear == 0 && calm.Activity != CreatureActivity.Fleeing, "Four metres does not frighten goat despite general vision");
            f = new Fixture();
            f.Goat.fearedTags = CreatureTags.Predator; f.Goat.fearRange = 3f;
            f.Predator.preyTags = CreatureTags.None; f.Predator.walkSpeed = 0;
            f.World.TrySpawn(f.Goat, V(4,4)); f.World.TrySpawn(f.Predator, V(3,4));
            calm = f.Agent("Test goat");
            f.Tick(0.2f);
            Check(calm.Vitals.Fear > 0 && calm.Activity == CreatureActivity.Fleeing, "Predator at two metres frightens goat");

            f = new Fixture(5,3);
            for (int y = 0; y < 3; y++) f.Fences.TryAdd(new FenceEdge(V(2,y), false));
            f.Fences.TrySetGate(new FenceEdge(V(2,1), false));
            f.World.TrySpawn(f.Predator, V(1,1)); f.World.TrySpawn(f.Goat, V(2,1)); f.Tick(8);
            Check(f.World.Population == 2, "Closed staff gate prevents predation through a fence");
            Check(f.Agent("Test Chupacabra").Cell.x < 2 && f.Agent("Test goat").Cell.x >= 2,
                "Creature movement stays on respective sides of closed barrier");
            f.Fences.TryRemove(new FenceEdge(V(2,1), false)); f.Tick(10);
            Check(f.World.Population == 1, "Opening fence gap makes prey reachable for hunting");

            f = new Fixture(8,5);
            var entrance = new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 };
            var dispenser = new BuildingDefinition { kind = BuildingKind.GoatDispenser, width = 1, depth = 1,
                dispenseInterval = 0.4f, maximumDispensed = 3, dispensedCreature = f.Goat };
            Check(f.Network.TryPlace(new BuildingFootprint(entrance, V(1,1), 0)) != null, "Test park entrance placed");
            ParkBuildingRecord record = f.Network.TryPlace(new BuildingFootprint(dispenser, V(4,1), 0));
            Check(record != null, "Test goat dispenser placed");
            Check(record.IsOpen, "Dispenser opens without paths");
            f.Tick(0.5f); Check(f.World.Population == 1, "Path-free dispenser releases goat at configured interval");
            for (int x = 1; x <= 4; x++) f.Grid.TryOccupy(V(x,0), BuildCellContent.Path);
            f.Network.Refresh(); Check(record.IsOpen, "Path connection opens dispenser");
            f.Grid.TryRemove(V(3,0), BuildCellContent.Path); f.Network.Refresh();
            Check(record.IsOpen, "Removing path leaves dispenser open");
            f.Tick(2); Check(f.World.Population == 3, "Path-free production continues to living limit");
            f.Grid.TryOccupy(V(3,0), BuildCellContent.Path); f.Network.Refresh(); f.Tick(4);
            Check(f.World.Population == 3, "Dispenser resumes and respects maximum living goats");

            string result = "Creature integration validation: " + checks + " checks passed.";
            File.WriteAllText("CreatureValidationResults.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("CreatureValidationResults.txt", "FAILED after " + checks + " checks: " + exception);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}

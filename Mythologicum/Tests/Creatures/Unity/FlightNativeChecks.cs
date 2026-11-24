using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Called from a prepared disposable Play-mode integration fixture.</summary>
public static class FlightNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static void Check(bool value, string message) { if (!value) throw new Exception("Flight native: " + message); checks++; }
    private static object Call(object owner, string name, params object[] values) => owner.GetType().GetMethod(name, Hidden).Invoke(owner, values);
    private static T Get<T>(object owner, string name)
    {
        var field = owner.GetType().GetField(name, Hidden);
        return (T)(field != null ? field.GetValue(owner) : owner.GetType().GetProperty(name, Hidden).GetValue(owner));
    }
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name, Hidden).SetValue(owner, value);
    }
    private static bool Until(CreatureWorld world, Func<bool> condition, float seconds)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { if (condition()) return true; world.Tick(.05f); }
        return condition();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("Flight native fixture");
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly EnvironmentGrid Environment;
        public readonly SpecialEnclosureMap Special;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition Bird, Goat;
        public Fixture(int index, bool hungry = false)
        {
            Grid = new SquareBuildGrid(new Vector3(145000 + index * 100, 0, 145000), 2, 16, 14);
            Fences = new FenceLayout(Grid); Environment = new EnvironmentGrid(Grid);
            Special = new SpecialEnclosureMap(Grid, Fences);
            Bird = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Thunderbird")); Bird.name = "Thunderbird";
            Goat = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Goat")); Goat.name = "Goat";
            foreach (var profile in new[] { Bird, Goat })
            {
                profile.needsFood = false; profile.initialHunger = 0; profile.hungerPerSecond = 0; profile.Thirst.needsWater = false;
                profile.initialEnergy = 100; profile.energyDrainPerSecond = profile.sleepRecoveryPerSecond = 0;
                profile.threat = profile.fearRange = 0; profile.fearedTags = CreatureTags.None;
                profile.requiredTrees = profile.requiredGravestones = profile.minimumCompanions = 0; profile.maximumCompanions = -1;
                profile.minimumSpace = profile.minimumOpenGroundFraction = profile.happinessThreshold = profile.happinessChangePerSecond = 0;
                profile.decisionInterval = .05f; profile.pauseMinSeconds = profile.pauseMaxSeconds = 10000;
                profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
            }
            Bird.Flight.cruiseHeight = 4; Bird.Flight.maximumHeight = 14; Bird.Flight.riseSpeed = Bird.Flight.descentSpeed = 6;
            Bird.Flight.flightSpeed = Bird.Flight.pursuitSpeed = 6;
            Bird.treeClimbSeconds = .4f; Bird.climbEnergyCost = 0;
            Bird.needsFood = hungry; Bird.initialHunger = hungry ? 90 : 0; Bird.visionRange = 50;
            Bird.preyTags = CreatureTags.Goat; Bird.damage = 80; Bird.attackSeconds = .3f; Bird.feedingSeconds = 2;
            Bird.Flight.lightningAttack = true; Bird.Flight.lightningSeconds = .3f;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material,
                new[] { Bird, Goat }, Environment, simulation);
            World.ConfigureFlightContainment(Special.CanFlyAcross, Special.FlightCeiling, () => { Special.Refresh(); return Special.Revision; });
            Get<GameObject>(World, "root").transform.SetParent(root.transform, false);
        }
        public CreatureAgent SpawnBird(Vector2Int cell)
        { Check(World.TrySpawn(Bird, cell), "The actual flight world spawns a Thunderbird"); return World.Agents.Last(); }
        public FenceEdge Cage(FenceKind kind)
        {
            for (int x = 4; x < 10; x++) { Fences.TryAdd(new FenceEdge(V(x, 4), true), kind); Fences.TryAdd(new FenceEdge(V(x, 10), true), kind); }
            for (int z = 4; z < 10; z++) { Fences.TryAdd(new FenceEdge(V(4, z), false), kind); Fences.TryAdd(new FenceEdge(V(10, z), false), kind); }
            return new FenceEdge(V(10, 7), false);
        }
        public void Dispose()
        {
            root.SetActive(false); World.Dispose(); Object.Destroy(root); Object.Destroy(material);
            Object.Destroy(simulation); Object.Destroy(Bird); Object.Destroy(Goat);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        var definition = Resources.Load<CreatureDefinition>("Creatures/Thunderbird");
        Check(definition != null && definition.Flight.canFly && definition.Flight.lightningAttack,
            "The imported Thunderbird enables optional flight and its lightning presentation");
        Check(definition.Shelter.mode == CreatureShelterMode.TreeCanopy && definition.Shelter.requiredForSleep,
            "The imported sky creature needs an actual tree roost");
        var random = UnityEngine.Random.state; UnityEngine.Random.InitState(517933);
        try
        {
            using (var fixture = new Fixture(0))
            {
                var bird = fixture.SpawnBird(V(7, 7));
                Check(Until(fixture.World, () => fixture.World.FlightAltitude(bird) > 3.9f, 4), "A live bird visibly rises to cruise altitude");
                Check(Mathf.Abs(bird.transform.position.y - fixture.Grid.CellCenter(bird.Cell).y - bird.Definition.bodyScale.y - fixture.World.FlightAltitude(bird)) < .03f,
                    "Flight altitude changes the simulation actor, not only a decorative body offset");
                var body = Get<Transform>(bird, "Body"); var wing = body.Find("Left wing");
                Quaternion before = wing.localRotation; fixture.World.Tick(.07f);
                Check(Quaternion.Angle(before, wing.localRotation) > 1, "Named wing pivots animate while flying");
                fixture.World.RestoreFlightAltitude(bird, 1.25f);
                Check(Mathf.Abs(fixture.World.FlightAltitude(bird) - 1.25f) < .001f, "Saved altitude can be restored without changing shelter elevation");
                fixture.Cage(FenceKind.Wooden);
                Check(fixture.World.CanApproach(bird, V(3, 7)), "Ordinary ground fencing does not confine a bird");
                Set(bird, "DecisionTimer", 1000f); Set(bird, "WanderTimer", 1000f); Set(bird, "Activity", CreatureActivity.Wandering);
                Check((bool)Call(fixture.World, "SetGoal", bird, V(3, 7), false), "The ordinary planner supplies a flying route over wood");
                Check(Until(fixture.World, () => bird.Cell == V(3, 7), 12), "A visible flyer physically crosses an ordinary fence");
                Check(fixture.World.Sedate(bird, 20), "Existing staff sedation accepts a flying creature");
                fixture.World.Tick(.05f);
                Check(fixture.World.FlightAltitude(bird) == 0 && !bird.IsAirborne && bird.IsSedated,
                    "Sedation clears airborne motion instead of leaving the animal suspended");
            }
            using (var fixture = new Fixture(1))
            {
                var breach = fixture.Cage(FenceKind.Aviary); var bird = fixture.SpawnBird(V(7, 7));
                // Let takeoff exceed the ground actor's real reach before testing protection.
                Check(Until(fixture.World, () => fixture.World.FlightAltitude(bird) > fixture.Goat.interactionRange + .25f, 3), "A bird can fly within a completed aviary");
                float roof = fixture.Special.FlightCeiling(bird.Cell);
                Check(bird.transform.position.y + bird.Definition.Flight.bodyHeadroom + bird.Definition.Flight.roofClearance <= roof + .02f,
                    "The complete roof limits the actor and wing headroom");
                Check(!fixture.World.CanApproach(bird, V(3, 7)), "The completed cage contains the bird without assigning it to the enclosure");
                var groundActor = fixture.World.SpawnExternal(fixture.Goat, bird.Cell);
                Check(groundActor != null && !(bool)Call(fixture.World, "CanInteract", groundActor, bird), "A ground actor cannot hit a bird above its interaction reach");
                fixture.Fences.TryRemove(breach);
                Check(fixture.World.CanApproach(bird, V(3, 7)), "A broken perimeter releases cached flight routes");
                Set(bird, "DecisionTimer", 1000f); Set(bird, "WanderTimer", 1000f); Set(bird, "Activity", CreatureActivity.Wandering);
                Check((bool)Call(fixture.World, "SetGoal", bird, V(3, 7), false), "The escaped bird can choose the far side of an intact wall");
                float highest = 0;
                Check(Until(fixture.World, () => { highest = Mathf.Max(highest, fixture.World.FlightAltitude(bird)); return bird.Cell == V(3, 7); }, 20),
                    "After a breach the bird can leave over a remaining tall aviary wall");
                Check(highest >= FenceConstruction.HeightFor(FenceKind.Aviary), "The bird gains enough height before crossing a tall unroofed wall");
            }
            using (var fixture = new Fixture(2))
            {
                fixture.Environment.AddTree(V(7, 7)); var bird = fixture.SpawnBird(V(7, 7));
                Check(Until(fixture.World, () => fixture.World.FlightAltitude(bird) > 3.9f, 4), "The roost fixture begins in actual free flight");
                Set(bird, "Energy", new CreatureEnergy(0)); Set(bird, "DecisionTimer", 0f);
                Check(Until(fixture.World, () => bird.Activity == CreatureActivity.Sleeping, 12), "An exhausted flyer lands and enters the reachable tree roost");
                Check(Get<Vector2Int?>(bird, "SleepTree") == V(7, 7) && fixture.World.FlightAltitude(bird) == 0 && Get<float>(bird, "Elevation") > 0,
                    "Tree sleep uses the existing physical shelter and independent roost elevation");
                Set(bird, "Energy", new CreatureEnergy(100)); Set(bird, "DecisionTimer", 0f);
                Check(Until(fixture.World, () => bird.Activity != CreatureActivity.Sleeping && fixture.World.FlightAltitude(bird) > .5f, 12),
                    "Waking safely leaves the tree and resumes ordinary flight");
            }
            using (var fixture = new Fixture(3, true))
            {
                var breach = fixture.Cage(FenceKind.Aviary); var bird = fixture.SpawnBird(V(7, 7));
                var prey = fixture.World.SpawnExternal(fixture.Goat, V(3, 7));
                Check(prey != null, "A real edible goat stands outside the completed cage");
                for (int i = 0; i < 120; i++) fixture.World.Tick(.05f);
                Check(prey.Alive && bird.GetComponentsInChildren<LineRenderer>().Length == 0,
                    "A contained bird cannot deal lightning damage through an intact aviary");
                fixture.Fences.TryRemove(breach);
                Check(Until(fixture.World, () => !prey.Alive, 25), "A hungry free bird follows its actual route and kills reachable prey");
                var beams = bird.GetComponentsInChildren<LineRenderer>();
                Check(beams.Length == 2 && beams.All(beam => beam.enabled && beam.positionCount == 2)
                    && beams[0].sharedMaterial == beams[1].sharedMaterial,
                    "A successful attack emits paired eye beams with one shared material");
                float hunger = bird.Vitals.Hunger;
                Check(Until(fixture.World, () => bird.Vitals.Hunger < hunger, 8), "The normal feeding clock supplies hunger relief after the lightning kill");
                Check(beams.All(beam => !beam.enabled), "The bounded lightning effect stops after its attack");
            }
        }
        finally { UnityEngine.Random.state = random; }
        return checks;
    }
}

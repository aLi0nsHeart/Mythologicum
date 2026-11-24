using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real creature decisions, movement and feeding; called by the disposable needs integration fixture.</summary>
public static class CreatureNeedsPriorityNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("Creature need priorities: " + message); checks++; }
    private static T Field<T>(object owner, string name)
    {
        var field = owner.GetType().GetField(name, Hidden);
        return (T)(field != null ? field.GetValue(owner) : owner.GetType().GetProperty(name, Hidden).GetValue(owner));
    }
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name, Hidden).SetValue(owner, value);
    }
    private static object Call(object owner, string name, params object[] arguments)
        => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("Needs priority native fixture");
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        public readonly SquareBuildGrid Grid;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureDefinition Profile, Goat;
        public readonly CreatureWorld World;
        public CreatureAgent Actor;

        public Fixture(int index, bool hungry = false, bool carnivore = false)
        {
            Grid = new SquareBuildGrid(new Vector3(174000 + index * 80, 0, 174000), 2, 14, 12);
            Environment = new EnvironmentGrid(Grid);
            Profile = Object.Instantiate(Resources.Load<CreatureDefinition>(carnivore ? "Creatures/Wendigo" : "Creatures/Waldschrat"));
            Profile.name = "Needs priority " + index;
            Goat = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Goat"));
            foreach (var definition in new[] { Profile, Goat })
            {
                definition.preyTags = definition.fearedTags = CreatureTags.None;
                definition.fearRange = definition.threat = definition.hungerPerSecond = 0;
                definition.initialEnergy = definition.energyDrainPerSecond = 0;
                definition.Thirst.needsWater = false;
                definition.minimumSpace = definition.requiredTrees = definition.requiredGravestones = 0;
                definition.minimumCompanions = 0; definition.maximumCompanions = -1;
                definition.randomEscapeChance = definition.hungryEscapeChance = definition.agitatedEscapeChance = definition.crowdedEscapeChance = 0;
                definition.happinessChangePerSecond = 0;
                definition.decisionInterval = .05f; definition.pauseMinSeconds = definition.pauseMaxSeconds = 100;
                definition.walkSpeed = definition.runSpeed = 6;
                definition.canBreakFences = definition.canClimbWoodenFences = definition.canBurrowFences = false;
            }
            Profile.diet = carnivore ? CreatureDiet.Carnivore : CreatureDiet.Herbivore;
            Profile.needsFood = hungry; Profile.initialHunger = hungry ? 100 : 0;
            Profile.grazeSeconds = .3f; Profile.attackSeconds = .1f; Profile.feedingSeconds = .3f;
            Profile.foodRelief = 70; Profile.damage = 100; Profile.visionRange = 30;
            Profile.preyTags = carnivore ? CreatureTags.Goat : CreatureTags.None;
            Profile.Stalking.enabled = false;
            Profile.requiresTreeForSleep = Profile.requiresGravestoneForSleep = Profile.requiresBurrowForSleep = false;
            Profile.prefersTreeShadeForSleep = true;
            Profile.shelter = new CreatureShelterSettings { mode = CreatureShelterMode.TreeShade, requiredForSleep = true, retreatWhenAfraid = false, searchRadius = 40 };
            Profile.sleepRecoveryPerSecond = 10; Profile.Behaviour.sleepSearchRetrySeconds = 2;
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            var fences = new FenceLayout(Grid);
            World = new CreatureWorld(Grid, fences, new ParkBuildingNetwork(Grid, fences), null, material, new[] { Profile, Goat }, Environment, simulation);
            Field<GameObject>(World, "root").transform.SetParent(root.transform, false);
            Check(World.TrySpawn(Profile, new Vector2Int(5, 5)), "the ordinary world spawns the exhausted creature");
            Actor = World.Agents.Last(); Set(Actor, "DecisionTimer", 0f);
        }
        public void Think() => Call(World, "Think", Actor);
        public bool Until(Func<bool> condition, float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++) { if (condition()) return true; World.Tick(.05f); }
            return condition();
        }
        public void Dispose()
        {
            root.SetActive(false); World.Dispose(); Object.Destroy(root);
            Object.Destroy(material); Object.Destroy(simulation); Object.Destroy(Profile); Object.Destroy(Goat);
        }
    }

    public static int RunChecks()
    {
        checks = 0; var random = UnityEngine.Random.state; UnityEngine.Random.InitState(37911);
        try
        {
            using (var fixture = new Fixture(0, true))
            {
                fixture.Think();
                Check(fixture.Actor.Activity == CreatureActivity.Grazing,
                    "an awake starving herbivore can graze instead of endlessly seeking missing shade");
                Check(fixture.Until(() => fixture.Actor.Vitals.Hunger < 80, 3), "actual grazing relieves urgent hunger");
                Check(fixture.Actor.Energy.Energy == 0 && fixture.Actor.Activity != CreatureActivity.Sleeping,
                    "an unsuccessful sleep search never grants free rest recovery");
            }
            using (var fixture = new Fixture(1, true, true))
            {
                fixture.Goat.maximumHealth = 10;
                var prey = fixture.World.SpawnExternal(fixture.Goat, new Vector2Int(8, 5));
                fixture.Think();
                Check(prey != null && fixture.Actor.Activity == CreatureActivity.Hunting,
                    "an awake starving carnivore can pursue real prey before its unavailable sleeping site");
                Check(fixture.Until(() => fixture.Actor.Vitals.Hunger < 80, 8) && !prey.Alive,
                    "ordinary hunting and timed feeding can satisfy that need");
            }
            using (var fixture = new Fixture(2))
            {
                fixture.Think(); float after = Field<float>(fixture.Actor, "SleepRetryAfter");
                Check(after > 0 && fixture.Actor.Activity != CreatureActivity.Sleeping,
                    "missing required shelter schedules a bounded retry rather than false sleep");
                fixture.Think();
                Check(Field<float>(fixture.Actor, "SleepRetryAfter") == after,
                    "decisions during a failed search cooldown do not repeat the search or reset its deadline");
                fixture.Environment.AddTree(new Vector2Int(6, 5));
                Check(fixture.Until(() => fixture.Actor.Activity == CreatureActivity.Sleeping && fixture.Actor.Energy.Energy > 0, 8),
                    "after the retry, a newly supplied tree becomes a reachable real sleeping site");
            }
            using (var fixture = new Fixture(3))
            {
                fixture.Think();
                fixture.Profile.agitatedEscapeChance = 1; fixture.Profile.Behaviour.maximumEscapeChance = 1;
                fixture.Actor.Wellbeing.Restore(10, 1);
                // With a full per-minute chance the decision is deterministic for any positive dt.
                Call(fixture.World, "TickEscape", fixture.Actor, .1f);
                Check(Field<bool>(fixture.Actor, "EscapeIntent"), "failed rest does not suppress a legitimate agitation-driven escape roll");
                fixture.Think();
                Check(fixture.Actor.Activity == CreatureActivity.Escaping && Field<bool>(fixture.Actor, "EscapeIntent"),
                    "an unsuccessful shelter search lets the existing physical escape attempt proceed");
            }
            using (var fixture = new Fixture(4, true))
            {
                fixture.Profile.shelter.mode = CreatureShelterMode.Ground;
                Set(fixture.Actor, "Activity", CreatureActivity.Sleeping);
                fixture.Think();
                Check(fixture.Actor.Activity == CreatureActivity.Sleeping,
                    "a creature already sleeping on a valid site is not switched back and forth to food every decision");
                fixture.World.Tick(.1f);
                Check(fixture.Actor.Energy.Energy > 0, "committed real sleep still restores energy normally");
            }
        }
        finally { UnityEngine.Random.state = random; }
        return checks;
    }
}

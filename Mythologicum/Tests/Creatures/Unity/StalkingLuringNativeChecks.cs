using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Prepared Play-mode helper: physical stalking, shore luring, capture and cancellation.</summary>
public static class StalkingLuringNativeChecks
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks, fixtureIndex;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Members).GetValue(owner);
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Members);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name, Members).SetValue(owner, value);
    }
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception("Predator intent: " + reason); checks++; }

    private sealed class Fixture : IDisposable
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition HunterProfile, PreyProfile;
        public readonly CreatureAgent Hunter;
        public readonly VisitorWorld Visitors;
        public readonly VisitorDefinition VisitorProfile;
        private readonly CreatureSimulationDefinition simulation;
        private readonly Material material;
        public Fixture(bool aquatic)
        {
            Grid = new SquareBuildGrid(new Vector3(170000 + 100 * fixtureIndex++, 0, 170000), 2, 24, 14);
            Fences = new FenceLayout(Grid); Environment = new EnvironmentGrid(Grid);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            HunterProfile = ScriptableObject.CreateInstance<CreatureDefinition>(); HunterProfile.name = "Native stalker/lurer";
            HunterProfile.tags = CreatureTags.Predator; HunterProfile.preyTags = CreatureTags.Goat | CreatureTags.Visitor | CreatureTags.Staff;
            HunterProfile.diet = CreatureDiet.Carnivore; HunterProfile.initialHunger = 90; HunterProfile.hungerPerSecond = 0;
            HunterProfile.damage = 50; HunterProfile.threat = 100; HunterProfile.visionRange = 40; HunterProfile.fearRange = .1f;
            HunterProfile.Thirst.needsWater = false; HunterProfile.energyDrainPerSecond = 0; HunterProfile.minimumSpace = 0;
            HunterProfile.happinessThreshold = HunterProfile.happinessChangePerSecond = 0;
            HunterProfile.decisionInterval = .05f; HunterProfile.walkSpeed = 4; HunterProfile.runSpeed = 8;
            HunterProfile.attackSeconds = .15f; HunterProfile.feedingSeconds = .25f;
            HunterProfile.pauseMinSeconds = HunterProfile.pauseMaxSeconds = 10000;
            HunterProfile.randomEscapeChance = HunterProfile.hungryEscapeChance = HunterProfile.agitatedEscapeChance = HunterProfile.crowdedEscapeChance = 0;
            HunterProfile.Aquatic.canSwim = HunterProfile.Aquatic.dragPreyToWater = aquatic;
            HunterProfile.Aquatic.preferWater = false; HunterProfile.Aquatic.swimSpeed = 8;
            HunterProfile.Aquatic.captureSeconds = .2f; HunterProfile.Aquatic.drowningSeconds = .5f;
            HunterProfile.Aquatic.waterSearchRadius = 40; HunterProfile.Aquatic.dragSpeedMultiplier = .7f;
            HunterProfile.Lure.enabled = aquatic; HunterProfile.Lure.range = 24; HunterProfile.Lure.durationSeconds = 30;
            HunterProfile.Stalking.enabled = !aquatic; HunterProfile.Stalking.stalkSpeedMultiplier = .25f;
            HunterProfile.Stalking.maximumStalkSeconds = 9; HunterProfile.Stalking.preferredCoverDistance = 6;
            PreyProfile = ScriptableObject.CreateInstance<CreatureDefinition>(); PreyProfile.name = "Native passive prey";
            PreyProfile.tags = CreatureTags.Goat; PreyProfile.diet = CreatureDiet.None; PreyProfile.needsFood = false;
            PreyProfile.maximumHealth = 40; PreyProfile.damage = PreyProfile.threat = 0;
            PreyProfile.energyDrainPerSecond = 0; PreyProfile.fearRange = .1f; PreyProfile.Thirst.needsWater = false;
            PreyProfile.minimumSpace = 0;
            if (aquatic)
            {
                for (int x = 5; x <= 9; x++) for (int z = 4; z <= 8; z++) Grid.Habitat.TrySetWater(new Vector2Int(x, z), 1);
                for (int x = 10; x < 20; x++) Grid.TryOccupy(new Vector2Int(x, 6), BuildCellContent.Path);
            }
            var buildings = new ParkBuildingNetwork(Grid, Fences);
            World = new CreatureWorld(Grid, Fences, buildings, null, material, new[] { HunterProfile, PreyProfile }, Environment, simulation);
            VisitorProfile = ScriptableObject.CreateInstance<VisitorDefinition>(); VisitorProfile.maximumVisitors = 0;
            VisitorProfile.spawnInterval = 10000; VisitorProfile.walkSpeed = 2; VisitorProfile.fearRange = 12;
            VisitorProfile.hungerPerSecond = VisitorProfile.energyDrainPerSecond = 0; VisitorProfile.prewarmVisitorModels = false;
            Visitors = new VisitorWorld(Grid, Fences, buildings, World, VisitorProfile, new ParkEconomy(10000));
            World.ConfigureLuring(Visitors);
            Check(World.TrySpawn(HunterProfile, aquatic ? new Vector2Int(7, 6) : new Vector2Int(3, 5)), "the fixture can place its shared predator actor");
            Hunter = World.Agents[0];
            Set(Hunter, "DecisionTimer", 0f);
        }
        public CreatureAgent Prey(Vector2Int cell)
        {
            if (!World.TrySpawn(PreyProfile, cell)) throw new Exception("Cannot spawn passive prey.");
            var prey = World.Agents.Last(); Set(prey, "DecisionTimer", 10000f); Set(prey, "StateTimer", 10000f); return prey;
        }
        public VisitorAgent Guest(VisitorType type, Vector2Int cell) => (VisitorAgent)typeof(VisitorWorld)
            .GetMethod("RestoreGuestWithType", Members).Invoke(Visitors, new object[] { cell, type });
        public void Step() { World.Tick(.05f); Visitors.Tick(.05f); }
        public bool Until(Func<bool> done, int maximum = 1000)
        { for (int i = 0; i < maximum; i++) { if (done()) return true; Step(); } return done(); }
        public void AquariumRing()
        {
            for (int x = 5; x <= 9; x++) { Fences.TryAdd(new FenceEdge(new Vector2Int(x, 4), true), FenceKind.Aquarium); Fences.TryAdd(new FenceEdge(new Vector2Int(x, 9), true), FenceKind.Aquarium); }
            for (int z = 4; z <= 8; z++) { Fences.TryAdd(new FenceEdge(new Vector2Int(5, z), false), FenceKind.Aquarium); Fences.TryAdd(new FenceEdge(new Vector2Int(10, z), false), FenceKind.Aquarium); }
        }
        public void Dispose()
        {
            Field<GameObject>(World, "root").SetActive(false); Visitors.Dispose(); World.Dispose();
            Object.Destroy(HunterProfile); Object.Destroy(PreyProfile); Object.Destroy(VisitorProfile);
            Object.Destroy(simulation); Object.Destroy(material);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        using (var f = new Fixture(false))
        {
            f.Environment.AddTree(new Vector2Int(4, 6)); f.Environment.AddTree(new Vector2Int(7, 6));
            var prey = f.Prey(new Vector2Int(10, 5));
            Check(f.Until(() => f.Hunter.Activity == CreatureActivity.Stalking), "a hungry forest predator enters a real slow stalking journey");
            var goal = Field<Vector2Int?>(f.Hunter, "Goal");
            Check(goal.HasValue && Field<CreatureAgent>(f.Hunter, "Prey") == prey, "the stalking intent commits to a prey and cover point");
            Check(!f.World.CanSee(prey, f.Hunter, 40) && f.World.IsConcealedPredator(f.Hunter), "crouching among actual trees conceals the stalker from its prey");
            Vector3 previous = f.Hunter.transform.position;
            for (int i = 0; i < 10; i++)
            {
                f.Step();
                Check(f.Hunter.Activity == CreatureActivity.Stalking && Field<Vector2Int?>(f.Hunter, "Goal") == goal,
                    "repeated decisions preserve the same cover route instead of oscillating between candidates");
            }
            Check(Vector3.Distance(previous, f.Hunter.transform.position) <= f.HunterProfile.walkSpeed * f.HunterProfile.Stalking.stalkSpeedMultiplier * .5f + .02f,
                "the real actor moves at its slower species-configured stalking speed");
            Check(Field<Transform>(f.Hunter, "Body").localPosition.y < 0 && prey.Health.Current == prey.Health.Maximum,
                "the visible body crouches while prey remains unharmed during the approach");
            Check(f.Until(() => f.Hunter.Activity == CreatureActivity.Hunting), "the bounded cover wait turns into a committed rush");
            Check(!f.World.IsConcealedPredator(f.Hunter), "a rushing predator becomes visible again");
            Check(f.Until(() => !prey.Alive), "the rush reaches ordinary physical combat rather than awarding a remote kill");
        }
        using (var f = new Fixture(false))
        {
            f.Environment.AddTree(new Vector2Int(4, 6)); f.Environment.AddTree(new Vector2Int(7, 6)); f.Prey(new Vector2Int(10, 5));
            Check(f.Until(() => f.Hunter.Activity == CreatureActivity.Stalking), "the cover-removal fixture starts stalking");
            f.Environment.RemoveTree(new Vector2Int(4, 6)); f.Environment.RemoveTree(new Vector2Int(7, 6));
            Check(f.Until(() => f.Hunter.Activity == CreatureActivity.Hunting, 20) && !f.World.IsConcealedPredator(f.Hunter),
                "removing cover ends concealment and commits the hunter instead of keeping a stale forest goal");
        }
        using (var f = new Fixture(true))
        {
            var child = f.Guest(VisitorType.Children, new Vector2Int(15, 6));
            var adult = f.Guest(VisitorType.Adventurers, new Vector2Int(15, 5));
            Check(f.Visitors.CanRespondToLure(child, f.Hunter) && !f.Visitors.CanRespondToLure(adult, f.Hunter), "only the configured child visitor type can respond");
            var start = child.Actor.transform.position;
            Check(f.Until(() => child.IsLured), "an underwater predator starts a real visitor lure");
            Check(!adult.IsLured && Vector3.Distance(start, child.Actor.transform.position) < .3f,
                "starting the call neither affects adults nor teleports the child");
            Check(f.World.IsConcealedLure(f.Hunter) && !f.World.CanSee(child.Actor, f.Hunter, 40), "the calling creature remains hidden underwater while its victim approaches");
            var bank = Field<Vector2Int>(child, "LureBank");
            Check(!f.Grid.Habitat.IsWater(bank), "lure routes end at a dry shoreline cell, never deep water");
            Check(f.Until(() => child.Actor.Cell == bank && Field<System.Collections.Generic.List<Vector2Int>>(child.Actor, "Route").Count == 0),
                "the child physically walks off the path to the selected reachable bank");
            Check(Field<CreatureAgent>(child.Actor, "CapturedBy") == null && child.Actor.Alive && !f.Grid.Habitat.IsWater(child.Actor.Cell),
                "arriving at the bank does not itself capture, submerge or damage a visitor");
            Check(f.Until(() => Field<CreatureAgent>(child.Actor, "CapturedBy") == f.Hunter), "ordinary aquatic predation approaches the lured visitor and physically captures it");
            Check(f.Until(() => f.Hunter.Activity == CreatureActivity.DrowningPrey), "the captor visibly drags its prey to real water before the struggle");
            Check(f.Grid.Habitat.IsWater(child.Actor.Cell) && child.Actor.Alive, "the visitor remains alive until the existing timed water attack completes");
        }
        using (var f = new Fixture(true))
        {
            var child = f.Guest(VisitorType.Children, new Vector2Int(15, 6));
            Check(f.Until(() => child.IsLured), "the sedation fixture establishes a call");
            var position = child.Actor.transform.position; float health = child.Actor.Health.Current;
            float remaining = f.World.CaptureLureCooldown(f.Hunter);
            Check(remaining > 0 && f.World.Sedate(f.Hunter, 5), "a keeper can sedate a luring source without bypassing its cooldown");
            f.Step();
            Check(!child.IsLured && child.Actor.Alive && child.Actor.Health.Current == health && Vector3.Distance(position, child.Actor.transform.position) < .3f,
                "source sedation releases the victim at its current physical location without harm");
            f.World.RestoreLureCooldown(f.Hunter, remaining);
            Check(Mathf.Abs(f.World.CaptureLureCooldown(f.Hunter) - remaining) < .01f, "cooldown restoration retains the saved remaining delay and no live lure");
        }
        using (var f = new Fixture(true))
        {
            var child = f.Guest(VisitorType.Children, new Vector2Int(15, 6));
            Check(f.Until(() => child.IsLured), "the drained-pool fixture establishes a call");
            f.Grid.Habitat.Restore(null); f.Step();
            Check(!child.IsLured && child.Actor.Alive && Field<CreatureAgent>(child.Actor, "CapturedBy") == null && f.Hunter.Vitals.Hunger == 90,
                "loss of water cancels the call without capture or a food reward");
        }
        using (var f = new Fixture(true))
        {
            f.AquariumRing(); var child = f.Guest(VisitorType.Children, new Vector2Int(15, 6));
            for (int i = 0; i < 120; i++) f.Step();
            Check(!child.IsLured && child.Actor.Alive && f.Grid.Habitat.IsWater(f.Hunter.Cell),
                "a completed aquarium's glass boundary prevents luring a child through the wall");
        }
        return checks;
    }
}

using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Call from a prepared Play-mode fixture. Exercises live water movement, shelter and graph invalidation.</summary>
public static class AquaticNavigationNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name).SetValue(owner, value);
    }
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Aquatic navigation: " + reason); checks++; }
        Check(Application.isPlaying, "native movement uses normal Play-mode lifetimes");
        var random = UnityEngine.Random.state;
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var profile = ScriptableObject.CreateInstance<CreatureDefinition>();
        var deep = ScriptableObject.CreateInstance<CreatureDefinition>();
        var land = ScriptableObject.CreateInstance<CreatureDefinition>();
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>();
        CreatureWorld world = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(104000, 0, 104000), 2, 12, 10);
            var fences = new FenceLayout(grid);
            for (int z = 0; z < grid.Depth; z++) for (int x = 5; x < 7; x++) grid.Habitat.TrySetWater(V(x, z), .7f);
            profile.name = "Native amphibious species"; profile.needsFood = false; profile.diet = CreatureDiet.None;
            profile.thirst.needsWater = false; profile.minimumSpace = profile.minimumOpenGroundFraction = 0;
            profile.threat = profile.damage = 0; profile.walkSpeed = 10; profile.decisionInterval = .05f;
            profile.pauseMinSeconds = profile.pauseMaxSeconds = .05f;
            profile.initialEnergy = 100; profile.energyDrainPerSecond = 0;
            profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
            profile.aquatic = new CreatureAquaticSettings { canSwim = true, minimumSwimDepth = .4f, swimSpeed = 10,
                preferWater = true, waterSearchRadius = 40, preferredWaterArea = 48 };
            profile.shelter = new CreatureShelterSettings { mode = CreatureShelterMode.Water, requiredForSleep = true, retreatWhenAfraid = true };
            deep.name = "Native deep swimmer"; deep.aquatic.canSwim = true; deep.aquatic.minimumSwimDepth = .9f;
            deep.aquatic.canWadeShallows=false;
            deep.threat=deep.damage=0;deep.needsFood=false;deep.Thirst.needsWater=false;deep.energyDrainPerSecond=0;
            land.name = "Native land species";
            simulation.placementSpacingTiles = 0;
            world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material,
                new[] { profile, deep, land }, new EnvironmentGrid(grid), simulation);
            Check(world.CanOccupy(profile, V(5, 5)) && !world.CanOccupy(deep, V(5, 5)) && !world.CanOccupy(land, V(5, 5)),
                "live occupancy distinguishes swimming depth profiles and ordinary land creatures");
            Check(world.TrySpawn(profile, V(2, 3)), "an amphibious creature can initially be placed on land");
            var actor = world.Agents.Last();
            Check(world.TrySpawn(deep, V(2, 6)), "a deep-water species can also stand on land"); var deepActor = world.Agents.Last();
            Check(world.CanApproach(actor, V(9, 3)) && !world.CanApproach(deepActor, V(9, 3)),
                "cached connected regions remain distinct for different water-depth requirements");
            void Step(int count = 1) { for (int i = 0; i < count; i++) world.Tick(.05f); }
            bool Until(Func<bool> done, int maximum = 600)
            { for (int i = 0; i < maximum; i++) { if (done()) return true; Step(); } return done(); }
            Check(Until(() => grid.Habitat.IsWater(actor.Cell) && world.IsSwimming(actor)), "water preference makes the live creature walk off land and swim");
            Check(Mathf.Abs(actor.transform.position.y - (grid.Habitat.WaterLevel(actor.Cell) + profile.bodyScale.y + profile.aquatic.swimmingBodyOffset)) < .01f,
                "swimming uses the water surface instead of the basin bed");
            Check(world.WaterSatisfaction(actor) == 100 && Mathf.Abs(actor.ReachableWaterArea - 80) < .01f,
                "reachable water area contributes the configured habitat requirement");
            for (int step = 0; step < 100; step++) { Step(); Check(world.IsSwimming(actor), "ordinary idle movement remains within water"); }
            for (int z = 0; z < grid.Depth; z++) fences.TryAdd(new FenceEdge(V(5, z), false));
            Check(!world.CanApproach(actor, V(2, 3)), "new water-bank fencing invalidates the cached aquatic component");
            Check(fences.TryRemove(new FenceEdge(V(5, 3), false)) && world.CanApproach(actor, V(2, 3)),
                "a new breach immediately restores physical aquatic access");
            var next=V(actor.Cell.x==5?6:5,actor.Cell.y);
            var route=(System.Collections.Generic.List<Vector2Int>)typeof(CreatureAgent).GetField("Route",Hidden).GetValue(actor);
            route.Clear();route.Add(next);Set(actor,"Goal",next);Set(actor,"Activity",CreatureActivity.Wandering);
            actor.transform.position=Vector3.Lerp(actor.transform.position,world.ProjectAquaticPosition(actor,grid.CellCenter(next)),.25f);
            Set(actor, "Energy", new CreatureEnergy(0)); Set(actor, "DecisionTimer", 0f);
            Check(Until(() => actor.Activity == CreatureActivity.Sleeping), "an exhausted swimmer chooses water as its sleep shelter");
            Check(world.IsSwimming(actor) && world.CaptureValidShelterHome(actor).HasValue && !world.IsHiddenInShelter(actor),
                "water sleep is saveable and remains visible to other actors");
            Check(actor.Energy.Energy > 0 && Mathf.Abs(actor.transform.position.y - (grid.Habitat.WaterLevel(actor.Cell) + profile.bodyScale.y + profile.aquatic.restingBodyOffset)) < .01f,
                "resting in water restores energy and uses the configurable rest pose");
            var home = world.CaptureValidShelterHome(actor).Value;
            Check(world.RestoreShelterState(actor, home, true, false) && world.IsSwimming(actor), "a water shelter home restores its real surface pose");
            deep.threat=100;
            Check(world.FindThreat(actor,40)==null,"water shelters remain safe from a land-bound threat that cannot enter their depth");
            deep.aquatic.minimumSwimDepth=.3f;
            Check(world.FindThreat(actor,40)==deepActor,"a stronger predator that can reach the same water still causes fear");
            deep.threat=0;
            var empty = grid.Habitat.Capture(); empty.water.Clear(); grid.Habitat.Restore(empty);
            Step(4);
            Check(actor.Activity != CreatureActivity.Sleeping && !world.HasShelterAvailable(actor) && actor.Energy.Energy < profile.wakeThreshold,
                "draining a pond stops unsupported water sleep without granting full energy");
        }
        finally
        {
            world?.Dispose(); Object.Destroy(profile); Object.Destroy(deep); Object.Destroy(land); Object.Destroy(simulation); Object.Destroy(material);
            UnityEngine.Random.state = random;
        }
        return checks;
    }
}

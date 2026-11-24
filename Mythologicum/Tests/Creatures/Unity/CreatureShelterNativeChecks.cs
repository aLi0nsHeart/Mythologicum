using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Call from a prepared Play-mode fixture; exercises actual creature decisions and visible shelter objects.</summary>
public static class CreatureShelterNativeChecks
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string why) { if (!value) throw new Exception("Shelters: " + why); checks++; }
    private static void Set(object owner, string field, object value)
    {
        var member = owner.GetType().GetField(field, Flags);
        if (member != null) member.SetValue(owner, value); else owner.GetType().GetProperty(field, Flags).SetValue(owner, value);
    }
    private static T Field<T>(object owner, string field) => (T)owner.GetType().GetField(field, Flags).GetValue(owner);
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root;
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        private readonly ShelterDefinition shelterSettings;
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly ShelterConstruction Shelters;
        public readonly CreatureDefinition Profile;
        public CreatureAgent Actor { get; }
        public Fixture(CreatureShelterMode mode, int index, bool tired = true)
        {
            root = new GameObject("Native shelter fixture");
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            Grid = new SquareBuildGrid(new Vector3(81000 + index * 100, 0, 81000), 2, 16, 16);
            Fences = new FenceLayout(Grid); Environment = new EnvironmentGrid(Grid);
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            shelterSettings = ScriptableObject.CreateInstance<ShelterDefinition>();
            Shelters = new ShelterConstruction(Grid, Environment, root.transform, material, shelterSettings);
            Profile = ScriptableObject.CreateInstance<CreatureDefinition>(); Profile.name = "Native shelter species";
            Profile.diet = CreatureDiet.None; Profile.needsFood = false; Profile.thirst.needsWater = false;
            Profile.minimumSpace = Profile.minimumOpenGroundFraction = 0; Profile.requiredTrees = Profile.requiredGravestones = Profile.minimumCompanions = 0;
            Profile.initialEnergy = tired ? 0 : 100; Profile.energyDrainPerSecond = 0;
            Profile.pauseMinSeconds = Profile.pauseMaxSeconds = 10000; Profile.decisionInterval = .05f;
            Profile.walkSpeed = Profile.runSpeed = 12; Profile.threat = 1; Profile.fearRange = 6;
            Profile.treeClimbSeconds = Profile.burrowSeconds = .2f;
            Profile.randomEscapeChance = Profile.hungryEscapeChance = Profile.crowdedEscapeChance = Profile.agitatedEscapeChance = 0;
            Profile.shelter = new CreatureShelterSettings { mode = mode, requiredForSleep = true, retreatWhenAfraid = true, searchRadius = 32 };
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material, new[] { Profile }, Environment, simulation);
            World.ConfigureShelters(Shelters);
            Field<GameObject>(World, "root").transform.SetParent(root.transform, false);
            Check(World.TrySpawn(Profile, V(3, 3)), "the fixture spawns an actual creature"); Actor = World.Agents.Last();
        }
        public void Tick(int count = 1) { for (int i = 0; i < count; i++) World.Tick(.1f); }
        public bool Until(Func<bool> predicate, int maximum = 300)
        { for (int i = 0; i < maximum; i++) { if (predicate()) return true; Tick(); } return predicate(); }
        public void PlaceActor(Vector2Int cell, float energy)
        {
            Set(Actor, "Cell", cell); Set(Actor, "Energy", new CreatureEnergy(energy));
            Set(Actor, "Activity", CreatureActivity.Resting); Set(Actor, "DecisionTimer", 0f); Set(Actor, "WantsSleep", false);
            Set(Actor, "SleepTree", null); Set(Actor, "ShelterCell", null); Set(Actor, "InBurrow", false); Set(Actor, "Elevation", 0f);
            Field<System.Collections.Generic.List<Vector2Int>>(Actor, "Route").Clear(); Set(Actor, "Goal", null);
            Actor.transform.position = Grid.ActorPosition(cell, Profile.bodyScale.y);
        }
        public void Dispose()
        {
            root.SetActive(false); World.Dispose(); Shelters.Dispose(); Object.Destroy(root);
            Object.Destroy(Profile); Object.Destroy(simulation); Object.Destroy(shelterSettings); Object.Destroy(material);
        }
    }

    public static int RunChecks()
    {
        checks = 0; Check(Application.isPlaying, "the fixture uses normal Play-mode object lifetime");
        int index = 0;
        using (var f = new Fixture(CreatureShelterMode.Cave, index++))
        {
            f.Tick(20);
            Check(f.Actor.Energy.Energy == 0 && f.World.ShelterSatisfaction(f.Actor) == 0,
                "a cave-dependent creature cannot recover energy without a cave");
            var home = V(5, 3); Check(f.Shelters.TryPlaceCave(home), "a cave has a real placeable object and registry entry");
            Check(f.Shelters.Occupies(home) && f.Grid.IsAvailable(home), "the cave reserves enrichment while leaving its entrance walkable");
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping && f.Actor.Cell == home), "the creature walks into a reachable cave before sleeping");
            Check(f.World.IsHiddenInShelter(f.Actor) && f.World.IsShelterProtected(f.Actor) && f.Actor.Energy.Energy > 0,
                "sleep inside a cave provides shelter protection and restores energy");
            Check(f.World.CaptureValidShelterHome(f.Actor) == home, "the actual cave home can be captured for a save");
            Check(f.World.RestoreShelterState(f.Actor, home, true, true) && f.World.IsHiddenInShelter(f.Actor),
                "restoring an asleep cave resident rebinds the real hidden shelter pose");
            Check(f.Shelters.TryRemove(home), "the cave can be demolished as an enclosure feature");
            f.Tick(3);
            Check(!f.World.IsHiddenInShelter(f.Actor) && f.Actor.Activity != CreatureActivity.Sleeping,
                "a removed cave wakes its resident and stops unsupported sleep");
        }
        using (var f = new Fixture(CreatureShelterMode.TreeCanopy, index++))
        {
            var tree = V(5, 3); f.Environment.AddTree(tree);
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping && Field<float>(f.Actor, "Elevation") > 0),
                "a canopy species reaches and climbs its tree before sleeping");
            Check(Mathf.Abs(Field<float>(f.Actor, "Elevation") - f.Profile.Shelter.treeHeight) < .01f && f.World.IsShelterProtected(f.Actor),
                "the new species profile controls actual canopy sleep height");
            Check(f.World.RestoreShelterState(f.Actor, tree, true, false)
                && f.Actor.transform.position.y > f.Grid.CellCenter(tree).y + f.Profile.Shelter.treeHeight,
                "restoring tree sleep keeps the creature above the ground");
            f.Environment.RemoveTree(tree);
            Check(f.Until(() => Mathf.Abs(Field<float>(f.Actor, "Elevation")) < .01f && f.Actor.Activity != CreatureActivity.Sleeping),
                "a removed sleep tree forces a safe descent");
        }
        using (var f = new Fixture(CreatureShelterMode.TreeShade, index++))
        {
            var tree = V(5, 3); f.Environment.AddTree(tree);
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping), "a shade-preferring species reaches a real tree");
            Check(ConstructionRouteGraph.CellDistance(f.Actor.Cell, tree) == 1 && Field<float>(f.Actor, "Elevation") == 0,
                "the creature stands beside its shade tree rather than climbing or overlapping it");
            Check(f.World.RestoreShelterState(f.Actor, tree, true, false), "shade sleep restores on its adjacent ground tile");
        }
        using (var f = new Fixture(CreatureShelterMode.Burrow, index++))
        {
            Check(f.Until(() => f.Actor.InBurrow), "a burrowing species digs its own home when none is reachable");
            var home = f.Actor.Cell;
            Check(f.Shelters.Registry.Contains(home, ConstructedShelterKind.Burrow) && f.Shelters.Capture().Count == 1,
                "digging creates a persistent, saveable dirt mound");
            Check(f.World.RestoreShelterState(f.Actor, home, true, false) && f.Actor.IsUnderground,
                "a restored burrow resident is actually underground");
            Set(f.Actor, "Energy", new CreatureEnergy(100)); f.Tick(5); f.PlaceActor(V(7, 3), 0);
            Check(f.Until(() => f.Actor.InBurrow) && f.Actor.Cell == home && f.Shelters.Capture().Count == 1,
                "the creature returns to its reachable existing home instead of digging wherever it gets tired");
            Set(f.Actor, "Energy", new CreatureEnergy(100)); f.Tick(5); f.PlaceActor(V(7, 3), 0);
            for (int y = 0; y < f.Grid.Depth; y++) f.Fences.TryAdd(new FenceEdge(V(6, y), false));
            Check(f.Until(() => f.Actor.InBurrow) && f.Actor.Cell.x >= 6 && f.Shelters.Capture().Count == 2,
                "an unreachable old home makes a new reachable burrow necessary");
            f.Grid.Habitat.PaintBiome(new Vector2(f.Actor.Cell.x + .5f, f.Actor.Cell.y + .5f), .75f, HabitatBiome.Stone, null, out _);
            f.Tick();
            Check(!f.Actor.InBurrow && f.Actor.Activity == CreatureActivity.LeavingBurrow,
                "stone-painted soil lets the animal emerge rather than trapping it in an invalid burrow");
        }
        using (var f = new Fixture(CreatureShelterMode.Cave, index++, tired: false))
        {
            var cave = V(5, 3); f.Shelters.TryPlaceCave(cave);
            var predator = Object.Instantiate(f.Profile); predator.name = "Native shelter threat"; predator.threat = 100;
            predator.shelter = new CreatureShelterSettings { mode = CreatureShelterMode.Ground, retreatWhenAfraid = false };
            try
            {
                Check(f.World.TrySpawn(predator, V(4, 4)), "a real nearby high-threat animal triggers fear");
                var threat = f.World.Agents.Last();
                Check(f.Until(() => f.Actor.Cell == cave && f.World.IsHiddenInShelter(f.Actor)), "a frightened creature retreats to its reachable cave");
                f.Tick(30);
                Check(f.World.IsHiddenInShelter(f.Actor) && f.Actor.Vitals.Fear < 1,
                    "shelter calms fear while the creature stays protected from the nearby threat");
                Set(threat, "Cell", V(13, 13)); threat.transform.position = f.Grid.ActorPosition(V(13, 13), predator.bodyScale.y);
                Check(f.Until(() => !f.World.IsHiddenInShelter(f.Actor)), "a calm rested creature leaves shelter when the threat is gone");
            }
            finally { Object.Destroy(predator); }
        }
        return checks;
    }
}

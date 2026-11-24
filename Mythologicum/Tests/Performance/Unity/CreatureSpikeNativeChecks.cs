using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Prepared Play-mode helper. Verifies performance shortcuts preserve actual routes and habitat measurements.</summary>
public static class CreatureSpikeNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Type[] RegionArguments = { typeof(CreatureAgent), typeof(Vector2Int) };
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private static void Set(object owner, string name, object value)
    {
        var type = owner.GetType(); var field = type.GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value);
        else type.GetProperty(name).SetValue(owner, value);
    }
    private static object Call(object owner, string name, params object[] arguments)
        => owner.GetType().GetMethod(name, Hidden).Invoke(owner, arguments);
    private static HashSet<Vector2Int> Region(CreatureWorld world, CreatureAgent actor, Vector2Int start)
        => (HashSet<Vector2Int>)typeof(CreatureWorld).GetMethod("AbilityRegion", Hidden, null, RegionArguments, null)
            .Invoke(world, new object[] { actor, start });
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static float Distance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Creature spike checks: " + reason); checks++; }
        Check(Application.isPlaying, "native checks use normal Play-mode object lifetimes");
        var random = UnityEngine.Random.state;
        var owner = new GameObject("Creature spike regression fixture");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var profile = ScriptableObject.CreateInstance<CreatureDefinition>();
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>();
        CreatureWorld world = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(94000, 0, 94000), 2, 36, 24);
            var fences = new FenceLayout(grid); var environment = new EnvironmentGrid(grid);
            for (int z = 0; z < grid.Depth; z++) fences.TryAdd(new FenceEdge(V(10, z), false), FenceKind.Spiked);
            profile.name = "Creature spike regression"; profile.needsFood = false; profile.diet = CreatureDiet.None;
            profile.threat = profile.damage = profile.fearRange = profile.minimumSpace = 0;
            profile.thirst.needsWater = false; profile.energyDrainPerSecond = 0; profile.initialEnergy = 100;
            profile.minimumCompanions = 0; profile.minimumOpenGroundFraction = .5f; profile.environmentRadius = 12;
            profile.shelter = new CreatureShelterSettings { mode = CreatureShelterMode.Ground };
            profile.canClimbWoodenFences = true;
            simulation.placementSpacingTiles = 0;
            world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material,
                new[] { profile }, environment, simulation);
            Field<GameObject>(world, "root").transform.SetParent(owner.transform, false);
            Check(world.TrySpawn(profile, V(4, 10)), "a real creature spawns on the near side");
            var actor = world.Agents.Single();
            Set(actor, "EscapeIntent", true); Set(actor, "EscapeUntil", 100f);
            var near = Region(world, actor, actor.Cell);
            var far = Region(world, actor, V(11, 10));
            Check(!ReferenceEquals(near, far) && !near.Contains(V(11, 10)) && far.Contains(V(30, 10)),
                "the same ability keeps an unscalable fence's two connectivity regions separate");
            Check(ReferenceEquals(far, Region(world, actor, V(30, 10))),
                "different far-side boundary queries reuse the same flood result");
            Check(world.TrySpawn(profile, V(15, 10)), "a real target spawns across the blocked fence");
            var target = world.Agents.Last();
            Check((bool)Call(world, "TryBlockedFence", actor, new List<CreatureAgent> { target }),
                "the blocked-fence search retains a real attempt toward visible prey");
            var groups = Field<Dictionary<int, Dictionary<Vector2Int, HashSet<Vector2Int>>>>(world, "abilityRegions");
            Check(groups.Count == 1 && groups.Values.Single().Values.Distinct().Count() == 2,
                "all boundary edges use only the two distinct cached connected regions");
            Check(Field<FenceEdge?>(actor, "AttemptFence").HasValue
                && Field<FenceEdge?>(actor, "AttemptFence").Value.Equals(new FenceEdge(V(10, 10), false)),
                "the nearest fence selection preserves the original distance ordering");

            // A topology change must discard both near and arbitrary-start cached regions.
            for (int z = 0; z < grid.Depth; z++) fences.TryAdd(new FenceEdge(V(20, z), false), FenceKind.Spiked);
            Call(world, "RefreshNavigation");
            var smallerFar = Region(world, actor, V(11, 10));
            Check(!ReferenceEquals(far, smallerFar) && !smallerFar.Contains(V(30, 10)),
                "fence edits invalidate far-side caches rather than serving stale connectivity");
            var route = Field<List<Vector2Int>>(actor, "Route"); route.Clear(); Set(actor, "Goal", null);
            Check(!(bool)Call(world, "SetGoal", actor, V(15, 10), true),
                "ordinary movement still rejects a known disconnected destination");
            fences.TrySetKind(new FenceEdge(V(10, 10), false), FenceKind.Wooden); Call(world, "RefreshNavigation");
            route.Clear(); Set(actor, "Goal", null);
            var ordinary = (HashSet<Vector2Int>)Call(world, "ReachableRegion", actor.Cell, false);
            Check(!ordinary.Contains(V(15, 10)), "a climbable fence remains disconnected for ordinary ground movement");
            var expected = new CreatureNavigation(grid, fences, world.IsClear, true).FindPath(actor.Cell, V(15, 10));
            Check((bool)Call(world, "SetGoal", actor, V(15, 10), false)
                && route.SequenceEqual(expected.Skip(1)), "skipping impossible ordinary A-star preserves the exact ability route and its ties");
            grid.TryOccupy(V(35, 23), BuildCellContent.Path); Call(world, "RefreshNavigation");
            route.Clear(); Set(actor, "Goal", null);
            Check((bool)Call(world, "SetGoal", actor, V(5, 10), true)
                && Field<Dictionary<Vector2Int, HashSet<Vector2Int>>>(world, "reachableRegions").Count == 0,
                "a cheap uncached short walk never creates a whole-map connectivity query");

            // Compare production measurements against the former full-component reference.
            for (int z = 0; z < 24; z++) for (int x = 0; x < 10; x++)
            {
                var cell = V(x, z);
                if ((x + z) % 9 == 0) environment.AddTree(cell);
                else if ((2 * x + z) % 11 == 0) environment.AddGravestone(cell);
                environment.SetForestCoverage(cell, (x + z) % 4 * .125f);
                if ((x + 2 * z) % 5 != 0)
                    grid.Habitat.PaintBiome(new Vector2(x + .5f, z + .5f), .55f,
                        (HabitatBiome)((x + 2 * z) % 5), _ => true, out _);
            }
            grid.TryOccupy(V(3, 10), BuildCellContent.Path);
            grid.Habitat.TrySetWater(V(8, 10), 1);
            Call(world, "RefreshNavigation");
            actor.transform.position += new Vector3(.37f, 0, -.29f);
            var accessible = (HashSet<Vector2Int>)Call(world, "ReachableRegion", actor.Cell, false);
            int trees = 0, graves = 0, ground = 0, localTiles = 0;
            var fractions = new float[5]; var meadow = new List<Vector2Int>();
            foreach (var cell in accessible)
            {
                if (Distance(grid.CellCenter(cell), actor.transform.position) > profile.environmentRadius) continue;
                trees += environment.TreesAt(cell); graves += environment.GravestonesAt(cell); localTiles++;
                if (grid.ContentAt(cell) == BuildCellContent.Empty && !grid.Habitat.IsWater(cell))
                { fractions[(int)grid.Habitat.BiomeAt(cell)]++; ground++; }
                if (grid.ContentAt(cell) == BuildCellContent.Empty && grid.Habitat.BiomeAt(cell) == HabitatBiome.Grass) meadow.Add(cell);
            }
            if (ground > 0) for (int i = 0; i < fractions.Length; i++) fractions[i] /= ground;
            var sampled = environment.Sample(meadow);
            float open = Mathf.Clamp01((sampled.Area - sampled.ForestArea) / (localTiles * grid.CellSize * grid.CellSize));
            Set(actor, "NeedsTimer", 0f); Call(world, "UpdateWellbeing", actor, .01f);
            Check(actor.NearbyTrees == trees && actor.NearbyGravestones == graves,
                "bounded needs scans retain exact local tree and grave counts");
            Check(Math.Abs(actor.AccessibleArea - accessible.Count * grid.CellSize * grid.CellSize) < .001f,
                "local optimization retains whole reachable-area space requirements");
            Check(actor.NearbyBiomeFractions.Zip(fractions, (actual, reference) => Math.Abs(actual - reference)).All(delta => delta < .00001f),
                "bounded needs scans retain all biome fractions including path and water exclusions");
            Check(Math.Abs(actor.NearbyOpenGroundFraction - open) < .00001f,
                "bounded meadow scanning matches the former whole-region reference");
        }
        finally
        {
            world?.Dispose(); owner.SetActive(false); Object.Destroy(owner);
            Object.Destroy(profile); Object.Destroy(simulation); Object.Destroy(material);
            UnityEngine.Random.state = random;
        }
        return checks;
    }
}

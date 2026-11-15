using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real meadow measurements and ground-shade sleep; disposable validation project only.</summary>
public static class CreatureHabitatNativeChecks
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("HABITAT PASS: " + message); }
    private static void Equal(float expected, float actual, string message)
        => Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .001f, message + ": " + actual);
    private static T Field<T>(CreatureAgent actor, string name)
        => (T)typeof(CreatureAgent).GetField(name, Hidden).GetValue(actor);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int count = RunChecks();
            File.WriteAllText("CreatureHabitatValidationResults.txt", "PASS: " + count + " meadow, shade sleeping and safe fallback checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("CreatureHabitatValidationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene original, scene;
        private readonly bool dirty;
        private readonly UnityEngine.Random.State random;
        private readonly Material material;
        private readonly CreatureSimulationDefinition settings;
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition Profile;
        public CreatureAgent Actor;

        public Fixture()
        {
            original = SceneManager.GetActiveScene(); dirty = original.isDirty; random = UnityEngine.Random.state;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene); UnityEngine.Random.InitState(4501);
            Grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2, 6, 5);
            Fences = new FenceLayout(Grid); Environment = new EnvironmentGrid(Grid);
            Profile = ScriptableObject.CreateInstance<CreatureDefinition>();
            Profile.name = "Ground shade fixture"; Profile.displayName = "Ground shade fixture";
            Profile.diet = CreatureDiet.None; Profile.tags = CreatureTags.None;
            Profile.initialEnergy = 0; Profile.energyDrainPerSecond = 0; Profile.hungerPerSecond = 0;
            Profile.walkSpeed = Profile.runSpeed = 8; Profile.sleepRecoveryPerSecond = 10;
            Profile.minimumSpace = 0; Profile.requiredTrees = 1; Profile.minimumOpenGroundFraction = .7f;
            Profile.environmentRadius = 100; Profile.prefersTreeShadeForSleep = true;
            Profile.decisionInterval = .05f; Profile.happinessChangePerSecond = 0;
            Profile.randomEscapeChance = Profile.hungryEscapeChance = Profile.agitatedEscapeChance = Profile.crowdedEscapeChance = 0;
            settings = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); settings.placementSpacingTiles = 0;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material, new[] { Profile }, Environment, settings);
        }
        public void Spawn(Vector2Int cell)
        {
            Check(World.TrySpawn(Profile, cell), "Habitat fixture places a real creature");
            Actor = World.Agents[0]; typeof(CreatureAgent).GetField("DecisionTimer", Hidden).SetValue(Actor, 0f);
        }
        public void Tick(float seconds)
        { for (float elapsed = 0; elapsed < seconds; elapsed += .05f) World.Tick(.05f); }
        public bool Until(Func<bool> predicate, float seconds)
        {
            for (float elapsed = 0; elapsed < seconds; elapsed += .05f)
            { if (predicate()) return true; World.Tick(.05f); }
            return predicate();
        }
        public float Measure()
        {
            var accessible = new HashSet<Vector2Int>(new CreatureNavigation(Grid, Fences, World.IsClear).Reachable(Actor.Cell, Grid.Width * Grid.Depth));
            return (float)typeof(CreatureWorld).GetMethod("MeasureOpenGround", Hidden).Invoke(World, new object[] { Actor, accessible });
        }
        public void Dispose()
        {
            EditorSceneManager.CloseScene(scene, true);
            Object.DestroyImmediate(Profile); Object.DestroyImmediate(settings); Object.DestroyImmediate(material);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
            Check(original.isDirty == dirty, "Habitat fixture preserves the original scene state");
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        using (var f = new Fixture())
        {
            var shade = V(4, 2); f.Environment.AddTree(shade); f.Spawn(V(1, 2));
            Equal(29.5f / 30f, f.Measure(), "Tree coverage reduces meadow fraction without removing all nearby meadow");
            for (int x = 0; x < f.Grid.Width; x++) f.Grid.TryOccupy(V(x, 0), BuildCellContent.Path);
            Equal(23.5f / 30f, f.Measure(), "Paved paths cannot satisfy open meadow needs");
            for (int y = 0; y < f.Grid.Depth; y++)
            for (int x = 0; x < f.Grid.Width; x++) f.Environment.SetForestCoverage(V(x, y), 1);
            Equal(0, f.Measure(), "Dense forest cannot count as meadow");
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping, 4), "A tired shade-preferring creature reaches a tree and sleeps");
            Check(f.Actor.Cell == shade && Field<float>(f.Actor, "Elevation") == 0,
                "Tree shade sleep takes place on the ground without a climb");
            Check(!f.Profile.requiresTreeForSleep, "Shade preference does not silently acquire tree-climbing requirements");
            float energy = f.Actor.Energy.Energy; f.Tick(.3f);
            Check(f.Actor.Energy.Energy > energy, "Shaded ground sleep restores energy");
        }
        using (var f = new Fixture())
        {
            f.Spawn(V(1, 2));
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping, 1), "Missing shade permits ordinary ground sleep");
            Check(!Field<Vector2Int?>(f.Actor, "SleepTree").HasValue, "Missing shade does not create a fictitious sleeping tree");
            f.Tick(.3f); Check(f.Actor.Energy.Energy > 0, "Basic sleep remains restorative without optional shade");
        }
        using (var f = new Fixture())
        {
            for (int y = 0; y < f.Grid.Depth; y++) f.Fences.TryAdd(new FenceEdge(V(2, y), false), FenceKind.Wooden);
            f.Environment.AddTree(V(4, 2)); f.Spawn(V(1, 2));
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Sleeping, 1), "Unreachable shade permits safe ground fallback");
            Check(f.Actor.Cell == V(1, 2) && !Field<Vector2Int?>(f.Actor, "SleepTree").HasValue,
                "A shy ground sleeper does not climb or cross a fence just to find shade");
            f.Tick(.3f); Check(f.Actor.Energy.Energy > 0, "Blocked shade does not leave the creature permanently exhausted");
        }
        return checks;
    }
}

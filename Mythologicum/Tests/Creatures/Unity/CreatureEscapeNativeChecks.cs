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

/// <summary>Real simulation checks for one-attempt escape budgets; disposable project only.</summary>
public static class CreatureEscapeNativeChecks
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Vector2Int V(int x) => new Vector2Int(x, 0);
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); Debug.Log("ESCAPE CHECK PASS: " + message); }
    private static T Field<T>(CreatureAgent actor, string name) => (T)typeof(CreatureAgent).GetField(name, Hidden).GetValue(actor);
    private static void Set(CreatureAgent actor, string name, object value) => typeof(CreatureAgent).GetField(name, Hidden).SetValue(actor, value);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int count = RunChecks();
            File.WriteAllText("CreatureEscapeValidationResults.txt", "PASS: " + count + " native one-attempt escape and fence checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("CreatureEscapeValidationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Scene original, scene;
        private readonly bool originalDirty;
        private readonly UnityEngine.Random.State random;
        private readonly Material material;
        private readonly CreatureSimulationDefinition settings;
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly CreatureDefinition Profile;
        public readonly CreatureWorld World;
        public readonly FenceEdge Edge = new FenceEdge(V(1), false);
        public CreatureAgent Actor;

        public Fixture(FenceKind kind, bool agitated, bool climb = false, bool phase = false, bool breaker = false)
        {
            original = SceneManager.GetActiveScene(); originalDirty = original.isDirty;
            random = UnityEngine.Random.state;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene); UnityEngine.Random.InitState(71131);
            Grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2, 3, 1);
            Fences = new FenceLayout(Grid); Fences.TryAdd(Edge, kind);
            Profile = ScriptableObject.CreateInstance<CreatureDefinition>();
            Profile.name = "Escape test"; Profile.displayName = "Escape test";
            Profile.diet = CreatureDiet.None; Profile.tags = CreatureTags.Predator;
            Profile.initialHunger = 100; Profile.hungerPerSecond = Profile.energyDrainPerSecond = 0;
            Profile.walkSpeed = 8; Profile.runSpeed = 8; Profile.decisionInterval = .05f;
            Profile.settlingSeconds = 0; Profile.minimumSpace = 0; Profile.happinessChangePerSecond = 0;
            Profile.pauseMinSeconds = Profile.pauseMaxSeconds = 60;
            Profile.randomEscapeChance = Profile.hungryEscapeChance = Profile.agitatedEscapeChance = Profile.crowdedEscapeChance = 0;
            Profile.canClimbWoodenFences = climb; Profile.canPhaseFences = phase; Profile.canBreakFences = breaker;
            Profile.damage = 10; Profile.fenceClimbSeconds = .3f; Profile.failedClimbSeconds = .3f;
            Profile.attackSeconds = .3f; Profile.Behaviour.phaseSeconds = .3f;
            Profile.escapeRetrySeconds = 30;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            settings = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); settings.placementSpacingTiles = 0;
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material, new[] { Profile }, null, settings);
            Check(World.TrySpawn(Profile, V(0)), "Escape fixture spawns a real creature"); Actor = World.Agents[0];
            Actor.Wellbeing.Restore(agitated ? 0 : 100, agitated ? 1 : 0);
        }
        public void Desire()
        {
            Set(Actor, "EscapeIntent", true); Set(Actor, "EscapeUntil", World.SimulationTime + 30);
            Set(Actor, "DecisionTimer", 0f);
        }
        public void Advance(float seconds) { for (float t = 0; t < seconds; t += .05f) World.Tick(.05f); }
        public bool Until(Func<bool> predicate, float seconds = 4)
        {
            for (float t = 0; t < seconds; t += .05f) { if (predicate()) return true; World.Tick(.05f); }
            return predicate();
        }
        public void Dispose()
        {
            EditorSceneManager.CloseScene(scene, true);
            Object.DestroyImmediate(Profile); Object.DestroyImmediate(settings); Object.DestroyImmediate(material);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            UnityEngine.Random.state = random;
            Check(original.isDirty == originalDirty, "Escape fixture preserves original scene state");
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        using (var f = new Fixture(FenceKind.Wooden, true, climb: true))
        {
            f.Fences.TryAdd(new FenceEdge(V(2), false), FenceKind.Wooden);
            f.Desire(); Check(f.Until(() => f.Actor.Activity == CreatureActivity.ClimbingFence), "A climber uses its normal method before attacking");
            Check(f.Fences.HealthAt(f.Edge) == f.Fences.MaximumHealthAt(f.Edge), "Starting a climb does not damage the fence");
            Check(f.Until(() => f.Actor.Cell == V(1)), "One desire completes one successful climb");
            Check(!Field<bool>(f.Actor, "EscapeIntent"), "A successful climb consumes its escape desire immediately");
            f.Advance(3); Check(f.Actor.Cell == V(1), "The same desire cannot cross a second barrier");
        }
        foreach (bool phase in new[] { false, true })
        foreach (bool agitated in new[] { false, true })
        using (var f = new Fixture(phase ? FenceKind.Sanctified : FenceKind.Spiked, agitated, climb: !phase, phase: phase))
        {
            float initial = f.Fences.HealthAt(f.Edge);
            f.Desire();
            Check(f.Until(() => f.Actor.Activity == (phase ? CreatureActivity.FailedPhase : CreatureActivity.FailedClimb)),
                "The blocked normal method is visible even when agitation is " + agitated);
            Check(f.Fences.HealthAt(f.Edge) == initial, "A blocked climb or phase has not secretly dealt damage");
            if (agitated)
            {
                Check(f.Until(() => f.Actor.Activity == CreatureActivity.AttackingFence), "A still-agitated creature follows failure with one attack");
                Check(f.Fences.HealthAt(f.Edge) == initial, "The follow-up strike respects its animation wind-up");
                Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "The single follow-up strike consumes the desire");
                Check(f.Fences.HealthAt(f.Edge) == initial - f.Profile.damage, "Exactly one damage increment reaches the fence");
            }
            else
            {
                Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "A calm creature finishes after its normal method fails");
                Check(f.Fences.HealthAt(f.Edge) == initial, "Hunger and a desire alone cannot cause fence damage");
            }
            GridChange(f);
            f.Advance(10);
            Check(f.Fences.HealthAt(f.Edge) == initial - (agitated ? f.Profile.damage : 0), "Navigation changes cannot re-arm fence damage");
            Check(f.Actor.Cell == V(0), "A rejected method never crosses the blocked edge");
            Check(Field<float>(f.Actor, "EscapeAfter") > f.World.SimulationTime, "A consumed attempt has a retry cooldown");
        }
        foreach (FenceKind kind in new[] { FenceKind.Wooden, FenceKind.Spiked, FenceKind.Sanctified, FenceKind.Heavy })
        using (var f = new Fixture(kind, true, breaker: true))
        {
            float initial = f.Fences.HealthAt(f.Edge);
            f.Desire(); Check(f.Until(() => f.Actor.Activity == CreatureActivity.AttackingFence), "An agitated breaker tries its native method against " + kind);
            Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "A breaker finishes after one strike against " + kind);
            f.Advance(6);
            Check(f.Fences.HealthAt(f.Edge) == initial - (kind == FenceKind.Heavy ? 0 : f.Profile.damage), "One strike obeys resistance and does not grind down " + kind);
        }
        using (var f = new Fixture(FenceKind.Wooden, false, breaker: true))
        {
            float initial = f.Fences.HealthAt(f.Edge); f.Desire(); f.Advance(4);
            Check(!Field<bool>(f.Actor, "EscapeIntent") && f.Fences.HealthAt(f.Edge) == initial,
                "A calm breaker cannot damage a fence even at maximum hunger");
        }
        using (var f = new Fixture(FenceKind.Spiked, true, climb: true))
        {
            float initial = f.Fences.HealthAt(f.Edge); f.Desire();
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.AttackingFence), "Agitation-change test reaches the follow-up wind-up");
            f.Actor.Wellbeing.Restore(100, 0); f.Advance(1);
            Check(f.Fences.HealthAt(f.Edge) == initial && !Field<bool>(f.Actor, "EscapeIntent"), "Calming down before impact cancels the strike");
        }
        using (var f = new Fixture(FenceKind.Wooden, true, breaker: true))
        {
            f.Profile.diet = CreatureDiet.Carnivore; f.Profile.preyTags = CreatureTags.Predator;
            Check(f.World.TrySpawn(f.Profile, V(2)), "A hungry-pursuit target exists beyond the fence");
            float initial = f.Fences.HealthAt(f.Edge); f.Advance(8);
            Check(f.Fences.HealthAt(f.Edge) == initial,
                "Agitation and persistent hungry pursuit cannot attack without a fresh escape desire");
        }
        using (var f = new Fixture(FenceKind.Wooden, true, breaker: true))
        {
            float initial = f.Fences.HealthAt(f.Edge); f.Desire();
            Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "A first breaker attempt finishes");
            f.Advance(31); f.Desire();
            Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "A fresh desire after cooldown permits another attempt");
            Check(f.Fences.HealthAt(f.Edge) == initial - 2 * f.Profile.damage,
                "Two separately authorized desires produce two individual strikes");
        }
        return checks;
    }

    private static void GridChange(Fixture f) => f.Grid.NotifyPhysicalChange();
}

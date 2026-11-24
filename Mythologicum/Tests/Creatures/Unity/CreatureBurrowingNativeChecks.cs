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

/// <summary>Actual sleep, escape and interruption checks in the disposable validation project.</summary>
public static class CreatureBurrowingNativeChecks
{
    private static int checks;
    private static Vector2Int V(int x) => new Vector2Int(x, 0);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("BURROW CHECK PASS: " + message); }
    private static T Field<T>(CreatureAgent actor, string name) => (T)typeof(CreatureAgent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
    private static void Set(CreatureAgent actor, string name, object value) => typeof(CreatureAgent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, value);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int result = RunChecks();
            File.WriteAllText("CreatureBurrowingValidationResults.txt", "PASS: " + result + " native burrow sleep, escape, interruption and visibility checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("CreatureBurrowingValidationResults.txt", "FAIL: " + error);
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
        public readonly CreatureAgent Actor;
        public readonly List<Object> Extras = new List<Object>();

        public Fixture(bool sleeping, FenceKind? fence = null)
        {
            original = SceneManager.GetActiveScene(); originalDirty = original.isDirty; random = UnityEngine.Random.state;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            UnityEngine.Random.InitState(17113);
            Grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2, 3, 1);
            Fences = new FenceLayout(Grid); if (fence.HasValue) Fences.TryAdd(Edge, fence.Value);
            Profile = ScriptableObject.CreateInstance<CreatureDefinition>(); Profile.name = "Burrow fixture"; Profile.displayName = "Burrow fixture";
            Profile.diet = CreatureDiet.None; Profile.tags = CreatureTags.Herbivore; Profile.damage = 0; Profile.threat = 0;
            Profile.initialHunger = 0; Profile.hungerPerSecond = Profile.energyDrainPerSecond = 0;
            Profile.initialEnergy = sleeping ? 0 : 100; Profile.sleepThreshold = 20; Profile.wakeThreshold = 80; Profile.sleepRecoveryPerSecond = 20;
            Profile.requiresBurrowForSleep = sleeping; Profile.canBurrowFences = true; Profile.burrowSeconds = .3f; Profile.burrowDepth = 1; Profile.burrowEnergyCost = 0;
            Profile.walkSpeed = Profile.runSpeed = 8; Profile.decisionInterval = .05f; Profile.minimumSpace = 0;
            Profile.minimumCompanions = 0; Profile.maximumCompanions = -1; Profile.happinessChangePerSecond = 0;
            Profile.pauseMinSeconds = Profile.pauseMaxSeconds = 60;
            Profile.randomEscapeChance = Profile.hungryEscapeChance = Profile.agitatedEscapeChance = Profile.crowdedEscapeChance = 0;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            settings = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); settings.placementSpacingTiles = 0;
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material, new[] { Profile }, null, settings);
            Check(World.TrySpawn(Profile, V(0)), "Burrow fixture spawns on clear natural ground"); Actor = World.Agents[0];
            Set(Actor, "DecisionTimer", 0f);
        }
        public void Desire() { Set(Actor, "EscapeIntent", true); Set(Actor, "EscapeUntil", World.SimulationTime + 30); Set(Actor, "DecisionTimer", 0f); }
        public void Advance(float seconds) { for (float t = 0; t < seconds; t += .05f) World.Tick(.05f); }
        public bool Until(Func<bool> predicate, float seconds = 6)
        { for (float t = 0; t < seconds; t += .05f) { if (predicate()) return true; World.Tick(.05f); } return predicate(); }
        public void Dispose()
        {
            EditorSceneManager.CloseScene(scene, true); Object.DestroyImmediate(Profile); Object.DestroyImmediate(settings); Object.DestroyImmediate(material);
            foreach (var extra in Extras) if (extra != null) Object.DestroyImmediate(extra);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            UnityEngine.Random.state = random;
            Check(original.isDirty == originalDirty, "Burrow fixture preserves the original scene state");
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        using (var f = new Fixture(true))
        {
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.EnteringBurrow), "Exhausted burrow sleeper visibly digs in");
            Check(!f.World.Sedate(f.Actor, 5), "A digging animal cannot be tranquilized through the ground");
            Check(f.Until(() => f.Actor.InBurrow), "The creature completes entry into an underground sleeping site");
            var body = Field<Transform>(f.Actor, "Body");
            Check(f.Actor.Activity == CreatureActivity.Sleeping && !body.gameObject.activeSelf && f.Actor.IsUnderground,
                "Underground sleeping hides the body and exposes its hidden state");
            Check(f.World.ApplyDamage(f.Actor, 100) == 0 && !f.World.Sedate(f.Actor, 5), "Surface strikes and sedation cannot reach an underground sleeper");
            var hunterProfile = Object.Instantiate(f.Profile);
            f.Extras.Add(hunterProfile);
            {
                hunterProfile.requiresBurrowForSleep = false; hunterProfile.canBurrowFences = false;
                hunterProfile.initialEnergy = 100; hunterProfile.diet = CreatureDiet.Carnivore;
                hunterProfile.initialHunger = 100; hunterProfile.preyTags = CreatureTags.Herbivore; hunterProfile.threat = 100;
                Check(f.World.TrySpawn(hunterProfile, V(2)), "An observer can stand above the sleeping region");
                var hunter = f.World.Agents[f.World.Agents.Count - 1];
                Check(!f.World.CanSee(hunter, f.Actor, 100), "An underground sleeper is hidden from shared perception");
                Set(hunter, "DecisionTimer", 0f); f.Advance(.05f);
                Check(Field<CreatureAgent>(hunter, "Prey") == null, "Predation discovery cannot see through the ground");
                Set(hunter, "Prey", f.Actor); Set(hunter, "PreyLastSeen", f.World.SimulationTime);
                Set(hunter, "LastSeenPreyCell", (Vector2Int?)f.Actor.Cell); Set(hunter, "DecisionTimer", 0f);
                f.Advance(.05f);
                Check(hunter.Activity == CreatureActivity.Searching && Field<float>(hunter, "PreyLastSeen") < f.World.SimulationTime,
                    "A hunter may search its last memory but cannot refresh sight through the ground");
                // Remove the observer before wake-up so normal threat fleeing does not
                // interfere with the independent sleep recovery assertions below.
                typeof(CreatureAgent).GetProperty("Alive").SetValue(hunter, false);
                Set(hunter, "CorpseTimer", 1000f);
            }
            float before = f.Actor.Energy.Energy; f.Advance(.3f);
            Check(f.Actor.Energy.Energy > before, "Burrow sleep restores energy");
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.LeavingBurrow), "A rested creature visibly emerges");
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.Resting), "Emerging returns to ordinary ground rest");
            Check(!f.Actor.InBurrow && body.gameObject.activeSelf && Mathf.Approximately(Field<float>(f.Actor, "Elevation"), 0), "Wake-up restores body and surface position");
            Check(f.World.Sedate(f.Actor, 5), "A fully emerged animal can be tranquilized normally");
        }
        using (var f = new Fixture(true))
        {
            Check(f.Until(() => f.Actor.InBurrow), "Paving test reaches burrow sleep");
            f.Grid.TryOccupy(f.Actor.Cell, BuildCellContent.Path);
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.LeavingBurrow), "Paving a sleeping site wakes its occupant");
            Check(f.Until(() => Field<float>(f.Actor, "Elevation") == 0), "Invalidated sleeping site does not trap its occupant underground");
        }
        using (var f = new Fixture(true))
        {
            f.Grid.TryOccupy(V(0), BuildCellContent.Path); f.Grid.TryOccupy(V(1), BuildCellContent.Path); f.Grid.TryOccupy(V(2), BuildCellContent.Path);
            f.Advance(1);
            Check(!f.Actor.InBurrow && f.Actor.Activity == CreatureActivity.SeekingSleep && f.Actor.Energy.Energy == 0,
                "A paved-only area supplies no substitute burrow or free rest");
        }
        using (var f = new Fixture(true))
        {
            f.Advance(.05f); Check(f.Actor.Activity == CreatureActivity.EnteringBurrow, "Interruption test begins burrow entry");
            f.Grid.TryOccupy(V(0), BuildCellContent.Path); f.Advance(.05f);
            Check(!f.Actor.InBurrow && Field<float>(f.Actor, "Elevation") == 0, "Paving during entry cancels safely on the same logical cell");
        }
        foreach (var kind in new[] { FenceKind.Wooden, FenceKind.Spiked, FenceKind.Sanctified })
        using (var f = new Fixture(false, kind))
        {
            f.Fences.TryAdd(new FenceEdge(V(2), false)); f.Desire();
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.BurrowingFence), "Burrower begins its normal method under " + kind);
            Check(f.Actor.Cell == V(0) && f.Actor.IsUnderground, "Logical crossing waits until digging completes");
            Check(f.Until(() => f.Actor.Cell == V(1)), "Burrower emerges beyond " + kind);
            Check(!Field<bool>(f.Actor, "EscapeIntent") && f.Fences.HealthAt(f.Edge) == f.Fences.MaximumHealthAt(f.Edge), "Digging consumes one desire without fence damage");
            f.Advance(2); Check(f.Actor.Cell == V(1), "The same desire cannot dig under a second fence");
        }
        foreach (var kind in new[] { FenceKind.Heavy, FenceKind.Deep })
        foreach (bool gate in new[] { false, true })
        using (var f = new Fixture(false, kind))
        {
            if (gate) Check(f.Fences.TrySetGate(f.Edge), kind + " gate retains its underlying fence kind");
            f.Desire(); Check(f.Until(() => f.Actor.Activity == CreatureActivity.FailedBurrow), kind + " foundations still show the failed normal dig attempt");
            Check(f.Until(() => !Field<bool>(f.Actor, "EscapeIntent")), "Foundation rejection consumes the desire");
            Check(f.Actor.Cell == V(0) && Field<float>(f.Actor, "EscapeAfter") > f.World.SimulationTime, "Rejected digging retains its safe side and cooldown");
        }
        using (var f = new Fixture(false, FenceKind.Wooden))
        {
            f.Desire(); Check(f.Until(() => f.Actor.Activity == CreatureActivity.BurrowingFence), "Upgrade interruption starts digging");
            f.Fences.TrySetKind(f.Edge, FenceKind.Deep); f.Advance(.05f);
            Check(f.Actor.Activity == CreatureActivity.FailedBurrow && f.Actor.Cell == V(0), "Adding foundations during a tunnel rejects crossing before logical movement");
        }
        using (var f = new Fixture(false, FenceKind.Wooden))
        {
            f.Grid.TryOccupy(V(1), BuildCellContent.Path); f.Desire();
            Check(f.Until(() => f.Actor.Activity == CreatureActivity.FailedBurrow), "A paved exit still shows attempted digging without crossing");
            f.Advance(1); Check(f.Actor.Cell == V(0), "A tunnel cannot emerge through a paved exit");
        }
        using (var f = new Fixture(true))
        {
            f.Advance(.05f); f.World.ApplyDamage(f.Actor, 10000);
            Check(!f.Actor.Alive && !f.Actor.InBurrow && Field<float>(f.Actor, "Elevation") == 0
                && Field<Transform>(f.Actor, "Body").gameObject.activeSelf, "Death at the surface during initial entry leaves a visible surface corpse");
        }
        return checks;
    }
}

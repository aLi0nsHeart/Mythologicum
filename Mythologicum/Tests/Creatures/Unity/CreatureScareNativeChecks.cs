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

/// <summary>Actual simulation feeding and visual-pose checks in a disposable editor project.</summary>
public static class CreatureScareNativeChecks
{
    private static int checks;
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Set(object actor, string name, object value)
    {
        var property = actor.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(actor, value);
        else actor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(actor, value);
    }
    private static T Get<T>(object actor, string name)
        => (T)actor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
    private static T Invoke<T>(CreatureWorld world, string name, CreatureAgent actor)
        => (T)typeof(CreatureWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(world, new object[] { actor });

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            int count = RunChecks();
            File.WriteAllText("CreatureScareValidationResults.txt", "PASS: " + count + " native scare feeding, staff, fence, jump and food-free checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("CreatureScareValidationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        var original = SceneManager.GetActiveScene(); bool originalDirty = original.isDirty;
        int actorsBefore = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var oldRandom = UnityEngine.Random.state;
        var owned = new List<Object>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            UnityEngine.Random.InitState(8427);
            var grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2f, 14, 14);
            var fences = new FenceLayout(grid); var network = new ParkBuildingNetwork(grid, fences);
            var environment = new EnvironmentGrid(grid);
            var species = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(species);
            species.name = "Scare fixture"; species.displayName = "Scare fixture";
            species.diet = CreatureDiet.Fear; species.feedsOnFear = true; species.damage = 0;
            species.tags = CreatureTags.Predator; species.threat = 20; species.initialHunger = 90;
            species.hungerPerSecond = species.energyDrainPerSecond = 0;
            species.scareSeconds = .6f; species.scareCooldownSeconds = 10; species.scareApproachDistance = 2.5f;
            species.scareRange = 3; species.foodRelief = 60; species.minimumSpace = 0;
            species.decisionInterval = .05f; species.randomEscapeChance = species.hungryEscapeChance = 0;
            species.agitatedEscapeChance = species.crowdedEscapeChance = 0;
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.placementSpacingTiles = 0; simulation.populationLimit = 100;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var world = new CreatureWorld(grid, fences, network, null, material, new[] { species }, environment, simulation);
            Check(world.TrySpawn(species, V(3, 3)), "The fear-feeding animal can be placed");
            var actor = world.Agents[0];
            void ResetActor()
            {
                Invoke<bool>(world, "AnimateScare", actor);
                Set(actor, "Activity", CreatureActivity.Resting); Set(actor, "Cell", V(3, 3));
                actor.transform.position = grid.ActorPosition(actor.Cell, species.bodyScale.y);
                Get<List<Vector2Int>>(actor, "Route").Clear(); Set(actor, "Goal", null);
                Set(actor, "ScareAfter", 0f); Set(actor, "ScareSucceeded", false); Set(actor, "ScarecrowTarget", null);
                Set(actor, "DecisionTimer", 0f); Set(actor, "StateTimer", 0f); Set(actor, "HasDanger", false);
                Set(actor, "Prey", null); actor.Vitals.Restore(90, 0);
            }
            void Advance(int ticks) { for (int i = 0; i < ticks; i++) world.Tick(.1f); }
            void FarAway(CreatureAgent person)
            { Set(person, "Cell", V(12, 12)); person.transform.position = grid.ActorPosition(person.Cell, person.Definition.bodyScale.y); }

            Check(environment.AddScarecrow(actor.Cell), "A scarecrow can provide enclosure enrichment");
            Set(actor, "DecisionTimer", 0f);
            world.Tick(.1f);
            Check(actor.Activity == CreatureActivity.Scaring && Get<Vector2Int?>(actor, "ScarecrowTarget") == actor.Cell,
                "A hungry contained fear-feeder starts practising on the reachable scarecrow");
            var rootPosition = actor.transform.position; var body = Get<Transform>(actor, "Body");
            var baseScale = Get<Vector3>(actor, "ScareBodyScale");
            Advance(3);
            Check(actor.Cell == V(3, 3) && actor.transform.position == rootPosition,
                "The exaggerated jump leaves the simulation position and cell unchanged");
            Check(body.localPosition.y > species.scareJumpHeight * .9f && body.localScale.y > baseScale.y,
                "The placeholder visibly jumps and stretches at the scare apex");
            Advance(5);
            Check(Mathf.Abs(actor.Vitals.Hunger - 30f) < .001f && actor.Activity != CreatureActivity.Scaring,
                "Completing one practice scare relieves hunger exactly once");
            Check(body.localScale == baseScale && !Get<bool>(actor, "ScarePoseActive"), "Landing restores the original body scale");
            actor.Vitals.Restore(90, 0);
            Check(!Invoke<bool>(world, "SeekScare", actor), "Practice feeding respects the shared scare cooldown");

            ResetActor(); world.Tick(.1f); environment.RemoveScarecrow(actor.Cell); Advance(8);
            Check(actor.Vitals.Hunger == 90, "Demolishing an active scarecrow prevents completion credit");

            var personDefinition = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(personDefinition);
            personDefinition.name = "Staff-only scare target"; personDefinition.tags = CreatureTags.Staff;
            personDefinition.threat = 5; personDefinition.damage = 0; personDefinition.diet = CreatureDiet.None;
            personDefinition.hungerPerSecond = personDefinition.energyDrainPerSecond = 0;
            var staff = world.SpawnExternal(personDefinition, V(4, 3));
            Check(staff != null, "A staff-only actor can be placed without the Visitor tag");
            ResetActor(); environment.AddScarecrow(actor.Cell); float health = staff.Health.Current;
            world.Tick(.1f);
            Check(actor.Activity == CreatureActivity.Scaring && Get<CreatureAgent>(actor, "Prey") == staff
                && !Get<Vector2Int?>(actor, "ScarecrowTarget").HasValue,
                "Nearby staff take priority over the practice dummy inside the same enclosure");
            Check(staff.Vitals.Fear > 0 && staff.Health.Current == health && Get<CreatureAgent>(staff, "CapturedBy") == null,
                "The staff scare raises fear without damage or capture");
            FarAway(staff); Advance(8);
            Check(actor.Vitals.Hunger == 30, "A staff member moving away still counts as a successful scare");

            var visitorDefinition = Object.Instantiate(personDefinition); owned.Add(visitorDefinition);
            visitorDefinition.name = "Visitor scare target"; visitorDefinition.tags = CreatureTags.Visitor;
            var visitor = world.SpawnExternal(visitorDefinition, V(4, 3));
            ResetActor(); world.Tick(.1f);
            Check(actor.Activity == CreatureActivity.Scaring && Get<CreatureAgent>(actor, "Prey") == visitor,
                "Visitors remain valid non-damaging fear-feeding targets");
            FarAway(visitor); Advance(8);
            Check(actor.Vitals.Hunger == 30 && visitor.Health.Current == visitor.Health.Maximum,
                "Visitor feeding grants one meal and never damages the target");

            ResetActor(); environment.RemoveScarecrow(actor.Cell); environment.AddScarecrow(V(4, 3));
            var enclosure = new[] { new FenceEdge(V(3, 3), true), new FenceEdge(V(3, 4), true),
                new FenceEdge(V(3, 3), false), new FenceEdge(V(4, 3), false) };
            foreach (var edge in enclosure) Check(fences.TryAdd(edge, FenceKind.Spiked), "Test enclosure barrier can be placed");
            Advance(8);
            Check(actor.Vitals.Hunger == 90 && actor.Activity != CreatureActivity.Scaring,
                "A scarecrow beyond a closed fence does not feed a contained animal through the barrier");
            foreach (var edge in enclosure) fences.TryRemove(edge);
            environment.RemoveScarecrow(V(4, 3));

            var modelSpecies = Object.Instantiate(species); owned.Add(modelSpecies);
            modelSpecies.visualPrefab = Resources.Load<CreatureDefinition>("Creatures/Hitotsume").visualPrefab;
            Check(modelSpecies.visualPrefab != null && world.TrySpawn(modelSpecies, V(7, 7)), "The actual imported Hitotsume model is available for animation");
            var modeled = world.Agents[world.Agents.Count - 1];
            environment.AddScarecrow(modeled.Cell); Set(modeled, "DecisionTimer", 0f); world.Tick(.1f);
            var modeledRoot = modeled.transform.position; var modeledBody = Get<Transform>(modeled, "Body");
            var modeledScale = Get<Vector3>(modeled, "ScareBodyScale"); Advance(3);
            Check(modeled.Activity == CreatureActivity.Scaring && modeledBody.localPosition.y > -.1f
                && modeled.transform.position == modeledRoot && modeledBody.localScale.y > modeledScale.y,
                "The imported model also jumps visibly while its collider remains grounded");
            Advance(5);
            Check(modeledBody.localScale == modeledScale && modeled.Vitals.Hunger == 30,
                "The imported model lands at its authored scale and receives one meal");

            var foodFree = Object.Instantiate(species); owned.Add(foodFree);
            foodFree.needsFood = false; foodFree.initialHunger = 90; foodFree.hungerPerSecond = 10;
            Check(world.TrySpawn(foodFree, V(9, 9)), "A food-free profile can be spawned");
            var undead = world.Agents[world.Agents.Count - 1];
            Check(undead.Vitals.Hunger == 0 && foodFree.HungerRate == 0 && foodFree.StartingHunger == 0,
                "Food-free species start full even with legacy hunger values");
            undead.Vitals.Restore(90, 0); world.Tick(.1f);
            Check(undead.Vitals.Hunger == 0, "Normal simulation clears legacy hunger for food-free species");
            Set(undead, "CapturedBy", actor); undead.Vitals.Restore(90, 0); world.Tick(.1f);
            Check(undead.Vitals.Hunger == 0, "Captured food-free creatures do not accumulate hunger");
            Set(undead, "CapturedBy", null); Check(world.Sedate(undead, 2), "The food-free fixture can be sedated");
            Advance(10);
            Check(undead.Vitals.Hunger == 0, "Sedated food-free creatures remain full");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = oldRandom;
        }
        Check(original.isLoaded && original.isDirty == originalDirty
            && Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == actorsBefore,
            "Scare fixture restores the previous scene and actor count");
        return checks;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Actual visitor choices, completed visits, arrival mix and danger responses; disposable validation project only.</summary>
public static class VisitorTypesNativeChecks
{
    private static int checks;
    private static Vector2Int V(int x, int z) => new Vector2Int(x, z);
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("VISITOR TYPES PASS: " + message); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static object Call(object owner, string name, params object[] arguments) => owner.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(owner, arguments);
    private static void Set(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(owner, value);
        else owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity", OpenSceneMode.Single);
            int count = RunChecks();
            File.WriteAllText("VisitorTypesValidationResults.txt", "PASS: " + count + " native visitor type, interests, novelty, needs and safety checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorTypesValidationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        var original = SceneManager.GetActiveScene(); bool originalDirty = original.isDirty;
        int actorsBefore = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var oldRandom = UnityEngine.Random.state;
        var owned = new HashSet<Object>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        VisitorWorld visitors = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(20000, 0, 20000), 2, 15, 10);
            var fences = new FenceLayout(grid);
            var buildings = new ParkBuildingNetwork(grid, fences);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1, admissionPrice = 0 }, V(1, 3), 0));
            for (int x = 0; x < grid.Width; x++) grid.TryOccupy(V(x, 2), BuildCellContent.Path);
            buildings.Refresh();
            Check(entrance != null && entrance.IsOpen, "The visitor fixture has an operating free entrance and a connected public path");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); owned.Add(simulation);
            simulation.placementSpacingTiles = 0; simulation.populationLimit = 200;
            var species = new CreatureDefinition[4];
            for (int i = 0; i < species.Length; i++)
            {
                species[i] = ScriptableObject.CreateInstance<CreatureDefinition>(); owned.Add(species[i]);
                species[i].name = "Type-exhibit-" + i; species[i].displayName = "Test exhibit " + i;
                species[i].origin = "Type-origin-" + i; species[i].minimumSpace = 0;
                species[i].diet = CreatureDiet.None; species[i].tags = CreatureTags.Goat; species[i].threat = 0; species[i].rating = 5;
                species[i].visitorAppeal = new CreatureVisitorAppeal { thrill = 0, wonder = 0, adventure = 0, children = 0 };
            }
            species[0].visitorAppeal.thrill = 1; species[1].visitorAppeal.wonder = 1;
            species[2].visitorAppeal.adventure = 1; species[3].visitorAppeal.children = 1;
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, species, null, simulation);
            foreach (var profile in species) Check(creatures.TrySpawn(profile, V(6, 4)), "An equally distant exhibit can be placed for " + profile.name);
            var definition = ScriptableObject.CreateInstance<VisitorDefinition>(); owned.Add(definition);
            definition.people = null; definition.maximumVisitors = 100; definition.spawnInterval = 100000;
            definition.minimumStaySeconds = 10000; definition.maximumStaySeconds = 20000;
            definition.viewingDistancePenalty = 0; definition.viewingTravelPenalty = 0;
            var economy = new ParkEconomy(10000);
            visitors = new VisitorWorld(grid, fences, buildings, creatures, definition, economy);
            var types = new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children };
            var guests = new List<VisitorAgent>();
            var paths = new VisitorNavigation(grid, fences, creatures.IsClear).Reachable(V(6, 2));
            Call(Field<object>(visitors, "viewing"), "BeginTick", definition.viewRange);
            for (int i = 0; i < types.Length; i++)
            {
                var guest = (VisitorAgent)Call(visitors, "RestoreGuestWithType", V(6, 2), types[i]); guests.Add(guest);
                Check(guest != null && guest.Type == types[i], "Restoration keeps visitor identity " + types[i]);
                Check((bool)Call(visitors, "TryViewingSpot", guest, paths), "A visitor can find a public viewing spot: " + types[i]);
                Check(Field<CreatureAgent>(guest, "Watching").Definition == species[i], "Equal travel costs expose the intended exhibit preference: " + types[i]);
                Check(guest.LastExhibitInterest > .9f, "The preferred exhibit has high interest: " + types[i]);
                Check(guest.ObservedSpecies.Count == 0 && guest.ObservedOrigins.Count == 0, "Choosing a target does not count it as seen: " + types[i]);
            }

            var adventure = guests[2];
            Field<List<Vector2Int>>(adventure.Actor, "Route").Clear();
            Set(adventure, "Activity", VisitorActivity.Watching); Set(adventure, "StateTimer", 0f);
            float before = adventure.Mood.Satisfaction;
            Call(visitors, "CompleteActivity", adventure);
            Check(adventure.ObservedSpecies.SetEquals(new[] { species[2].name }) && adventure.ObservedOrigins.SetEquals(new[] { species[2].origin }),
                "Only a completed visible exhibit records its stable species and origin IDs");
            Check(adventure.Mood.Satisfaction > before, "Completing an interesting exhibit rewards satisfaction");
            float remembered = (float)Call(visitors, "Interest", adventure, species[2]);
            adventure.RestoreInterests(null, null);
            float novel = (float)Call(visitors, "Interest", adventure, species[2]);
            Check(novel > remembered, "An Adventurer values a new species and origin more than a completed familiar exhibit");
            adventure.RestoreInterests(new[] { species[2].name, species[2].name, "", null }, new[] { species[2].origin, species[2].origin });
            Check(adventure.ObservedSpecies.Count == 1 && adventure.ObservedOrigins.Count == 1, "Restored novelty history drops duplicate and empty IDs");
            // Equal appeal and rarity isolate an Adventurer's preference for a novel origin and species.
            species[0].visitorAppeal.adventure = species[2].visitorAppeal.adventure;
            Field<Dictionary<CreatureAgent, float>>(adventure, "ViewedAt").Clear();
            Check((bool)Call(visitors, "TryViewingSpot", adventure, paths) && Field<CreatureAgent>(adventure, "Watching").Definition == species[0],
                "An Adventurer chooses the novel species when appeal, rarity and travel are equal");

            var child = guests[3]; var adult = guests[0];
            var childAgain = (VisitorAgent)Call(visitors, "RestoreGuestWithType", V(6, 2), VisitorType.Children);
            Check(childAgain.Actor.Definition == child.Actor.Definition, "Visitors of one type reuse their cached actor profile");
            Check(guests.Select(guest => guest.Actor.Definition).Distinct().Count() == 4, "The four types have separate cached profiles");
            Check(child.Actor.Definition != adult.Actor.Definition && child.Actor.Definition.bodyScale.y < adult.Actor.Definition.bodyScale.y,
                "Children have a smaller collider without modifying an adult profile");
            Check(child.Actor.Definition.hungerPerSecond > adult.Actor.Definition.hungerPerSecond
                && child.Actor.Definition.energyDrainPerSecond > adult.Actor.Definition.energyDrainPerSecond,
                "Children need food and rest sooner in the shared creature actor model");
            Check(child.Actor.Definition.walkSpeed < adult.Actor.Definition.walkSpeed, "Children use their editable movement speed multiplier");
            Field<List<Vector2Int>>(child.Actor, "Route").Clear(); Field<List<Vector2Int>>(child.Actor, "Route").Add(V(7, 2));
            Set(child, "Activity", VisitorActivity.Walking); Call(visitors, "Move", child, 10f);
            Check(child.Actor.Cell == V(7, 2) && Vector3.Distance(child.Actor.transform.position, grid.ActorPosition(V(7, 2), child.Actor.Definition.bodyScale.y)) < .001f,
                "A child remains correctly grounded when reaching a public path tile");

            foreach (var type in types)
            {
                foreach (var settings in definition.typeSettings) settings.arrivalWeight = settings.type == type ? 1 : 0;
                var guest = visitors.TrySpawn();
                Check(guest != null && guest.Type == type, "Actual paid admission obeys a forced arrival mix: " + type);
            }
            foreach (var settings in definition.typeSettings) settings.arrivalWeight = 0;
            var fallback = visitors.TrySpawn();
            Check(fallback != null && fallback.Type == VisitorType.Legacy, "Disabling all arrival weights safely falls back to a neutral visitor");

            // Visitors like the exhibit from a public path but still fear actual accessible danger.
            species[0].threat = 1000;
            var ring = new[] { new FenceEdge(V(6, 4), true), new FenceEdge(V(6, 5), true),
                new FenceEdge(V(6, 4), false), new FenceEdge(V(7, 4), false) };
            foreach (var edge in ring) Check(fences.TryAdd(edge, FenceKind.Heavy), "The danger test installs a real enclosing barrier");
            Call(visitors, "RefreshNavigation");
            Check(Call(visitors, "FindThreat", adult) == null && Call(visitors, "FindThreat", childAgain) == null,
                "An enclosed dangerous exhibit is safe to observe for both adults and children");
            Check(creatures.CanSee(adult.Actor, creatures.Agents[0], definition.fearRange), "The safe dangerous exhibit is visible through the containing fence");
            fences.TryRemove(ring[0]);
            visitors.Tick(.1f);
            Check(adult.Activity == VisitorActivity.Fleeing && Field<bool>(adult, "LeavingPark"),
                "A Thrill seeker still flees and abandons the visit when a dangerous creature becomes accessible");
            Check(childAgain.Activity == VisitorActivity.Fleeing && Field<bool>(childAgain, "LeavingPark"),
                "A child also flees actual accessible danger");
            Check(childAgain.Actor.Vitals.Fear > adult.Actor.Vitals.Fear && adult.Actor.Vitals.Fear > 0,
                "Real fear rises for both types, with the child's greater sensitivity");
        }
        finally
        {
            if (visitors != null) foreach (var profile in Field<Dictionary<VisitorType, CreatureDefinition>>(visitors, "actorProfiles").Values) owned.Add(profile);
            EditorSceneManager.CloseScene(scene, true);
            foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            UnityEngine.Random.state = oldRandom;
        }
        Check(SceneManager.GetActiveScene() == original && original.isDirty == originalDirty, "Native visitor checks restore the original scene and dirty flag");
        Check(Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == actorsBefore, "Native visitor checks clean up every temporary actor");
        return checks;
    }
}

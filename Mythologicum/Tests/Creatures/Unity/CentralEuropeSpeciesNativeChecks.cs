using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Actual profile imports, expedition availability and procedural bodies in a disposable project.</summary>
public static class CentralEuropeSpeciesNativeChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("CENTRAL EUROPE PASS: " + message); }
    private static T Field<T>(CreatureAgent actor, string name)
        => (T)typeof(CreatureAgent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);

    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int total = RunChecks();
            File.WriteAllText("CentralEuropeSpeciesResults.txt", "PASS: " + total + " species, expedition, visual and material checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("CentralEuropeSpeciesResults.txt", "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        string[] ids = { "Wolpertinger", "Unicorn", "Waldschrat" };
        var profiles = ids.Select(id => Resources.Load<CreatureDefinition>("Creatures/" + id)).ToArray();
        for (int i = 0; i < ids.Length; i++)
        {
            var species = profiles[i];
            Check(species != null && species.name == ids[i], "Stable species asset ID resolves: " + ids[i]);
            Check(species.diet == CreatureDiet.Herbivore && (species.tags & CreatureTags.Herbivore) != 0
                && !species.IsDomestic, "Mythical herbivore uses specimen stock rather than domestic placement: " + ids[i]);
            Check(species.origin == "creature.origin.central_europe" && species.rating == i + 1,
                "Origin and expedition tier are configured: " + ids[i]);
            Check(species.visualPrefab == null && species.placeholderKind == (CreaturePlaceholderKind)(i + 1),
                "Species selects its distinct replaceable placeholder: " + ids[i]);
        }
        var wolpertinger = profiles[0]; var unicorn = profiles[1]; var waldschrat = profiles[2];
        Check(wolpertinger.requiresBurrowForSleep && wolpertinger.canBurrowFences && wolpertinger.damage == 0,
            "Wolpertinger sleeps and escapes by digging, with no attack damage");
        Check(unicorn.minimumCompanions >= 1 && unicorn.maximumCompanions >= unicorn.minimumCompanions
            && unicorn.minimumOpenGroundFraction > .5f && unicorn.prefersTreeShadeForSleep
            && !unicorn.canClimbWoodenFences && !unicorn.canBurrowFences && !unicorn.canPhaseFences,
            "Unicorn needs meadow, company and shaded ground rest without a special fence-crossing ability");
        Check(waldschrat.minimumSpace >= 500 && waldschrat.requiredTrees >= 10 && waldschrat.canBreakFences
            && waldschrat.hungerPerSecond >= 3 && waldschrat.damage >= 50,
            "Waldschrat is a large hungry forest herbivore with an agitated physical strike");
        var settings = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
        Check(settings != null && settings.rules.regions.Single(region => region.id == "central-europe").species.SequenceEqual(ids),
            "The actual Central Europe expedition region contains all three stable species IDs");
        Check(new ProgressionRules().regions.Single(region => region.id == "central-europe").species.SequenceEqual(ids),
            "The code fallback matches the expedition asset");
        foreach (var species in profiles)
        {
            Check(!settings.rules.startingCreatures.Any(stock => stock.species == species.name && stock.count > 0),
                "New parks must obtain the new species through expeditions: " + species.name);
            Check(settings.rules.difficulties.Any(difficulty => difficulty.minimumRating <= species.rating
                && difficulty.maximumRating >= species.rating), "A configured expedition difficulty can award " + species.name);
        }
        var goat = Resources.Load<CreatureDefinition>("Creatures/Goat");
        Check(goat.IsDomestic && (goat.tags & CreatureTags.Herbivore) != 0, "Goats retain domestic identity and share the herbivore prey category");
        foreach (string predatorId in new[] { "Chupacabra", "Oni" })
            Check((Resources.Load<CreatureDefinition>("Creatures/" + predatorId).preyTags & CreatureTags.Herbivore) != 0,
                "Existing carnivore recognizes mythical herbivores as potential prey: " + predatorId);

        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        var random = UnityEngine.Random.state;
        int before = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2, 12, 8);
            var fences = new FenceLayout(grid);
            var world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material, profiles, null, simulation);
            for (int i = 0; i < profiles.Length; i++)
            {
                var species = profiles[i];
                Check(world.TrySpawn(species, new Vector2Int(2 + i * 3, 3)), "Actual simulation spawns " + species.name);
                var actor = world.Agents[world.Agents.Count - 1]; var body = Field<Transform>(actor, "Body");
                var primary = Field<MeshRenderer>(actor, "BodyRenderer");
                var renderers = body.GetComponentsInChildren<MeshRenderer>();
                Check(body.name == species.placeholderKind + " placeholder" && body.localScale == species.bodyScale
                    && !Field<bool>(actor, "HasVisualModel"), "Composite placeholder remains compatible with ordinary animation: " + species.name);
                Check(primary != null && primary.transform.IsChildOf(body) && renderers.Length >= 10,
                    "Placeholder has a primary agitation renderer and a recognizable composite silhouette: " + species.name);
                Check(body.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled)
                    && actor.GetComponent<CapsuleCollider>().isTrigger,
                    "Only the shared creature controller owns collision and selection: " + species.name);
                Check(renderers.All(renderer => renderer.sharedMaterial == material), "Visual parts reuse the supplied world material: " + species.name);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.min.y - grid.CellCenter(actor.Cell).y) < .08f && bounds.size.y > .5f,
                    "Placeholder feet align with the terrain: " + species.name);
                if (species.placeholderKind == CreaturePlaceholderKind.Wolpertinger)
                    Check(body.Find("Left long ear") != null && body.Find("Antler stem") != null,
                        "Wolpertinger is visually identifiable by rabbit ears and antlers");
                else if (species.placeholderKind == CreaturePlaceholderKind.Unicorn)
                    Check(body.Find("Unicorn horn tip") != null && body.Find("Horse neck") != null,
                        "Unicorn is visually identifiable by horse proportions and its horn");
                else Check(body.Find("Leaf crown") != null && body.Find("Bark torso") != null,
                    "Waldschrat is visually identifiable by its woody body and foliage crown");
                // Placeholder animation must preserve its authored scale and never create material instances.
                typeof(CreatureWorld).GetMethod("Animate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(world, new object[] { actor });
                Check(body.localScale == species.bodyScale && renderers.All(renderer => renderer.sharedMaterial == material),
                    "Animating the new species retains its scale and shared materials: " + species.name);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true); Object.DestroyImmediate(material); Object.DestroyImmediate(simulation);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
        }
        Check(original.isLoaded && original.isDirty == dirty
            && Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == before,
            "Species fixture restores the original scene and actor population");
        return checks;
    }
}

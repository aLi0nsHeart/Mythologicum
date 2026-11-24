using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Call from a prepared Play-mode fixture; imports both burrowing profiles and their replaceable bodies.</summary>
public static class BurrowingSpeciesNativeChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("Burrowing species: " + message); checks++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

    public static int RunChecks()
    {
        checks = 0;
        Check(Application.isPlaying, "the visual fixture uses normal Play-mode object lifetime");
        var tatzelwurm = Resources.Load<CreatureDefinition>("Creatures/Tatzelwurm");
        var deathworm = Resources.Load<CreatureDefinition>("Creatures/AllghoiKhorkhoi");
        Check(tatzelwurm != null && deathworm != null && tatzelwurm != deathworm,
            "both stable resource IDs resolve to distinct imported species assets");
        Check(tatzelwurm.name == "Tatzelwurm" && deathworm.name == "AllghoiKhorkhoi",
            "serialized species identities retain their specimen and save IDs");
        Check(tatzelwurm.origin == "creature.origin.central_europe" && deathworm.origin == "creature.origin.mongolia",
            "the two species retain their different expedition origins");
        Check(tatzelwurm.rating < deathworm.rating && tatzelwurm.damage < deathworm.damage
            && tatzelwurm.maximumHealth < deathworm.maximumHealth && tatzelwurm.threat < deathworm.threat,
            "the larger deathworm has the higher rarity, damage, health and threat");
        Check(tatzelwurm.minimumCompanions == 0 && tatzelwurm.maximumCompanions == 1
            && deathworm.minimumCompanions == 0 && deathworm.maximumCompanions == 0,
            "Tatzelwurm accepts solitude or a pair while Allghoi Khorkhoi is solitary");
        Check(tatzelwurm.placeholderKind == CreaturePlaceholderKind.Tatzelwurm
            && deathworm.placeholderKind == CreaturePlaceholderKind.AllghoiKhorkhoi
            && (int)CreaturePlaceholderKind.Nokk == 5,
            "new placeholder types are distinct and preserve previous serialized enum values");

        var profiles = new[] { tatzelwurm, deathworm };
        foreach (var profile in profiles)
        {
            Check(profile.manuallyPlaceable && !profile.pettable && !profile.IsDomestic
                && profile.diet == CreatureDiet.Carnivore && profile.needsFood && profile.Thirst.needsWater,
                "normal specimen placement, carnivore hunger and drinking apply: " + profile.name);
            var prey = CreatureTags.Goat | CreatureTags.Visitor | CreatureTags.Staff | CreatureTags.Herbivore;
            Check(profile.tags == CreatureTags.Predator && (profile.preyTags & prey) == prey
                && !profile.feedsOnFear && !profile.Aquatic.canSwim,
                "ordinary land hunting recognizes people and herbivores: " + profile.name);
            Check(profile.maximumHealth > 0 && profile.damage > 0 && profile.threat > 0
                && profile.walkSpeed > 0 && profile.runSpeed >= profile.walkSpeed
                && profile.HungerRate > 0 && profile.Thirst.thirstPerSecond > 0
                && profile.minimumSpace > 0 && profile.Habitat.HasPreferences,
                "editable health, movement, need and habitat values survive import: " + profile.name);
            Check(profile.requiresBurrowForSleep && profile.canBurrowFences
                && profile.Shelter.mode == CreatureShelterMode.Burrow && profile.Shelter.requiredForSleep
                && profile.Shelter.retreatWhenAfraid && profile.burrowSeconds > 0
                && profile.burrowDepth > profile.bodyScale.y && profile.burrowEnergyCost > 0,
                "sleep, fear retreat and escape use the existing burrow system: " + profile.name);
            Check(!profile.requiresTreeForSleep && !profile.requiresGravestoneForSleep
                && !profile.canClimbWoodenFences && !profile.canPhaseFences && !profile.canBreakFences,
                "the profile selects digging as its special fence traversal: " + profile.name);
            var research = new SpeciesResearch(new[] { profile }); research.Study(profile, 100);
            string description = research.Description(profile);
            Check(description.Contains(Loc.Text("research.creature.food.carnivore").Trim())
                && description.Contains(Loc.Text("research.creature.sleep.burrow").Trim())
                && description.Contains(Loc.Text("research.creature.containment.burrow").Trim()),
                "completed research describes carnivore food, burrow sleep and containment: " + profile.name);
        }
        var rockyDirt = new[] { .30f, .35f, .20f, .15f, 0f };
        var desert = new[] { 0f, .30f, 0f, 0f, .70f };
        Check(tatzelwurm.Habitat.Evaluate(rockyDirt) >= 99
            && tatzelwurm.Habitat.Evaluate(rockyDirt) > tatzelwurm.Habitat.Evaluate(desert),
            "Tatzelwurm prefers a usable rocky, dirt and thicket mix");
        Check(deathworm.Habitat.Evaluate(desert) >= 99
            && deathworm.Habitat.Evaluate(desert) > deathworm.Habitat.Evaluate(rockyDirt),
            "Allghoi Khorkhoi prefers predominantly sand with diggable dirt");

        var originalRandom = UnityEngine.Random.state;
        var root = new GameObject("Burrowing species visual fixture");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
        CreatureWorld world = null;
        try
        {
            UnityEngine.Random.InitState(71803);
            var grid = new SquareBuildGrid(new Vector3(124000, 0, 124000), 2, 12, 8);
            var fences = new FenceLayout(grid);
            world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material, profiles, null, simulation);
            Field<GameObject>(world, "root").transform.SetParent(root.transform, false);
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                Check(profile.visualPrefab == null && world.TrySpawn(profile, new Vector2Int(3 + i * 5, 3)),
                    "the production simulation creates the procedural species body: " + profile.name);
                var actor = world.Agents.Last();
                var body = Field<Transform>(actor, "Body");
                var renderers = body.GetComponentsInChildren<MeshRenderer>();
                Check(body.name == profile.placeholderKind + " placeholder" && body.localScale == profile.bodyScale
                    && Field<MeshRenderer>(actor, "BodyRenderer") != null && !Field<bool>(actor, "HasVisualModel"),
                    "the body uses the normal controller and animation contract: " + profile.name);
                Check(renderers.Length >= 15 && renderers.All(renderer => renderer.sharedMaterial == material)
                    && body.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled)
                    && actor.GetComponent<CapsuleCollider>().isTrigger,
                    "composite visuals share one material and leave collision to the creature: " + profile.name);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.min.y - grid.CellCenter(actor.Cell).y) < .06f && bounds.size.z > bounds.size.y,
                    "the horizontal body rests on the terrain rather than floating: " + profile.name);
                if (i == 0)
                    Check(body.Find("Cat head") != null && body.Find("Clawed forepaw") != null
                        && body.Find("Serpent tail segment 3") != null,
                        "Tatzelwurm has a cat head, front paws and serpentine tail");
                else
                    Check(body.Find("Worm segment 7") != null && body.Find("Dark circular mouth") != null
                        && body.Find("Mouth tooth") != null,
                        "Allghoi Khorkhoi has a segmented worm silhouette and circular mouth");
                typeof(CreatureWorld).GetMethod("Animate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(world, new object[] { actor });
                Check(body.localScale == profile.bodyScale && renderers.All(renderer => renderer.sharedMaterial == material),
                    "normal animation keeps the configured scale and shared material: " + profile.name);
            }
        }
        finally
        {
            root.SetActive(false); world?.Dispose(); Object.Destroy(root);
            Object.Destroy(material); Object.Destroy(simulation); UnityEngine.Random.state = originalRandom;
        }
        return checks;
    }
}

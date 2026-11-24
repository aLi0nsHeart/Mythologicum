using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imported North American profiles and their normal procedural-body contracts.</summary>
public static class NorthAmericanSpeciesNativeChecks
{
    private static int checks;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception("North American species: " + reason); checks++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);

    public static int RunChecks()
    {
        checks = 0;
        Check(Application.isPlaying, "the fixture uses Play-mode lifetime and imported resources");
        string[] ids = { "Wendigo", "Thunderbird", "Kalupelik" };
        var profiles = ids.Select(id => Resources.Load<CreatureDefinition>("Creatures/" + id)).ToArray();
        Check(profiles.All(profile => profile != null) && profiles.Distinct().Count() == 3,
            "all three stable resource IDs resolve to separate species");
        var origins = new[] { "algonquian", "indigenous_north_america", "inuit" };
        var shelters = new[] { CreatureShelterMode.Cave, CreatureShelterMode.TreeCanopy, CreatureShelterMode.Water };
        for (int i = 0; i < profiles.Length; i++)
        {
            var profile = profiles[i];
            Check(profile.name == ids[i] && (int)profile.placeholderKind == 18 + i && profile.visualPrefab == null,
                "the imported specimen identity and appended placeholder survive serialization: " + ids[i]);
            Check(profile.origin == "creature.origin." + origins[i] && profile.manuallyPlaceable && !profile.IsDomestic && !profile.pettable,
                "the chosen origin and ordinary expedition placement apply: " + ids[i]);
            Check(profile.diet == CreatureDiet.Carnivore && profile.tags == CreatureTags.Predator
                && (profile.preyTags & (CreatureTags.Goat | CreatureTags.Herbivore | CreatureTags.Visitor | CreatureTags.Staff))
                    == (CreatureTags.Goat | CreatureTags.Herbivore | CreatureTags.Visitor | CreatureTags.Staff)
                && profile.needsFood && profile.HungerRate > 0 && profile.Thirst.needsWater && profile.Thirst.thirstPerSecond > 0,
                "ordinary feeding, predation and drinking remain usable: " + ids[i]);
            Check(profile.maximumHealth > 0 && profile.damage > 0 && profile.threat > 0 && profile.minimumSpace > 0
                && profile.Habitat.HasPreferences && profile.walkSpeed > 0 && profile.runSpeed >= profile.walkSpeed,
                "editable combat, space, biome and locomotion values import: " + ids[i]);
            Check(profile.Shelter.mode == shelters[i] && profile.Shelter.requiredForSleep
                && profile.Shelter.searchRadius > 0 && profile.sleepRecoveryPerSecond > 0,
                "sleep needs the intended physical shelter: " + ids[i]);
            Check(profile.minimumCompanions == 0 && profile.maximumCompanions == (i == 2 ? 1 : 0),
                "Wendigo and Thunderbird are solitary while Kalupelik accepts a pair: " + ids[i]);
            var appeal = profile.VisitorAppeal;
            Check(new[] { appeal.thrill, appeal.wonder, appeal.adventure, appeal.children }.All(value => value >= 0 && value <= 1)
                && new[] { appeal.thrill, appeal.wonder, appeal.adventure, appeal.children }.Distinct().Count() > 1,
                "visitor interest is differentiated and normalized: " + ids[i]);
            Check(Loc.Table.Contains(profile.displayName) && Loc.Table.Contains(profile.origin)
                && !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.displayName, GameLanguage.ENGLISH))
                && !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.origin, GameLanguage.ENGLISH)),
                "names and origins have English source IDs: " + ids[i]);
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                Check(!string.IsNullOrWhiteSpace(Loc.Table.Get(profile.displayName, language))
                    && !string.IsNullOrWhiteSpace(Loc.Table.Get(profile.origin, language)),
                    "untranslated names and origins retain the normal English fallback: " + ids[i] + "/" + language);
        }
        var wendigo = profiles[0]; var bird = profiles[1]; var kalupelik = profiles[2];
        Check(wendigo.Stalking.enabled && !wendigo.Flight.canFly && !wendigo.Lure.enabled && !wendigo.Aquatic.canSwim
            && wendigo.canClimbWoodenFences && wendigo.requiredTrees >= 4
            && wendigo.Stalking.maximumStalkSeconds >= wendigo.Stalking.minimumStalkSeconds && wendigo.Stalking.ambushRange > 0,
            "Wendigo selects bounded forest stalking and climbing instead of unrelated abilities");
        Check(bird.Flight.canFly && bird.Flight.lightningAttack && !bird.Stalking.enabled && !bird.Lure.enabled
            && !bird.Aquatic.canSwim && bird.requiresTreeForSleep && bird.requiredTrees >= 2
            && bird.Flight.cruiseHeight > 0 && bird.Flight.maximumHeight >= bird.Flight.cruiseHeight
            && bird.Flight.bodyHeadroom > 0 && bird.Flight.lightningSeconds > 0 && bird.Flight.lightningWidth > 0,
            "Thunderbird selects airborne motion, a tree roost and visible eye lightning");
        Check(kalupelik.Aquatic.canSwim && kalupelik.Aquatic.canWadeShallows && kalupelik.Aquatic.preferWater
            && kalupelik.Aquatic.dragPreyToWater && kalupelik.Aquatic.minimumSwimDepth > 0 && kalupelik.Aquatic.preferredWaterArea > 0
            && kalupelik.Lure.enabled && kalupelik.Lure.Permits(VisitorType.Children)
            && !kalupelik.Lure.Permits(VisitorType.ThrillSeekers) && !kalupelik.Lure.Permits(VisitorType.Wonderlust)
            && !kalupelik.Lure.Permits(VisitorType.Adventurers) && !kalupelik.Lure.Permits(VisitorType.Legacy)
            && kalupelik.Lure.durationSeconds > 0 && kalupelik.Lure.cooldownSeconds > 0
            && !kalupelik.Flight.canFly && !kalupelik.Stalking.enabled,
            "Kalupelik selects amphibious capture with child-only, time-limited luring");
        Check(wendigo.Habitat.Evaluate(new[] { .2f, .15f, .55f, .1f, 0f }) > 99
            && bird.Habitat.Evaluate(new[] { .4f, 0f, .4f, .2f, 0f }) > 99
            && kalupelik.Habitat.Evaluate(new[] { .3f, .2f, .2f, .3f, 0f }) > 99,
            "each authored biome preference has a feasible satisfying habitat");
        var progression = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
        Check(progression != null && ids.All(id => progression.rules.regions.Single(region => region.id == "north-america").species.Contains(id)),
            "the actual North America pool offers all three imported species");
        Check(progression.rules.regions.All(region => !region.species.Any(id => id == "Bigfoot" || id == "Yeti" || id == "LochNess" || id == "LochNessMonster")),
            "reserved DLC creatures are absent from the base expedition pools");
        var research = new SpeciesResearch(profiles);
        string[] sleep = { "cave", "tree", "water" };
        string[] ability = { "research.creature.behaviour.stalking", "research.creature.containment.flight", "research.creature.behaviour.luring" };
        for (int i = 0; i < profiles.Length; i++)
        {
            Check(Loc.Table.Contains(ability[i]), "research guidance has a source localization ID: " + ids[i]);
            string unknown = research.Description(profiles[i]);
            research.Study(profiles[i], 100);
            string completed = research.Description(profiles[i]);
            Check(completed.Contains(Loc.Text("research.creature.sleep." + sleep[i]).Trim())
                && completed.Contains(Loc.Text(ability[i]).Trim()) && completed.Length > unknown.Length,
                "completed research reveals real shelter and ability guidance: " + ids[i]);
        }
        ValidateBodies(profiles);
        return checks;
    }

    private static void ValidateBodies(CreatureDefinition[] profiles)
    {
        var root = new GameObject("North American species body fixture");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
        var randomState = UnityEngine.Random.state;
        CreatureWorld world = null;
        try
        {
            UnityEngine.Random.InitState(272641);
            var grid = new SquareBuildGrid(new Vector3(126000, 0, 126000), 2, 28, 12);
            var fences = new FenceLayout(grid);
            world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material, profiles, null, simulation);
            Field<GameObject>(world, "root").transform.SetParent(root.transform, false);
            var features = new[] {
                new[] { "Gaunt humanoid torso", "Wendigo narrow head", "Wendigo long forearm" },
                new[] { "Left wing", "Right wing", "Left lightning eye", "Right lightning eye" },
                new[] { "Amauti back pouch", "Kalupelik webbed foot", "Kalupelik sea-green parka" }
            };
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                Check(world.TrySpawn(profile, new Vector2Int(4 + i * 8, 4)), "ordinary species placement creates the procedural body: " + profile.name);
                var actor = world.Agents.Last();
                var body = Field<Transform>(actor, "Body");
                var renderers = body.GetComponentsInChildren<MeshRenderer>();
                Check(body.name == profile.placeholderKind + " placeholder" && body.localScale == profile.bodyScale
                    && !Field<bool>(actor, "HasVisualModel") && Field<MeshRenderer>(actor, "BodyRenderer") != null,
                    "the composed body uses normal creature control and animation: " + profile.name);
                Check(renderers.Length >= 15 && renderers.All(item => item.sharedMaterial == material && item.GetComponent<MeshFilter>()?.sharedMesh != null)
                    && body.GetComponentsInChildren<Collider>(true).All(item => !item.enabled)
                    && actor.GetComponent<CapsuleCollider>().enabled && actor.GetComponent<CapsuleCollider>().isTrigger,
                    "body pieces share material and leave interaction to the main capsule: " + profile.name);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.min.y - grid.CellCenter(actor.Cell).y) < .12f && bounds.size.y > .8f,
                    "the resting body meets the ground without an unintended offset: " + profile.name);
                Check(features[i].All(name => body.Find(name) != null), "the actual mesh hierarchy contains the species' identifying anatomy: " + profile.name);
                typeof(CreatureWorld).GetMethod("Animate", Hidden).Invoke(world, new object[] { actor });
                Check(body.localScale == profile.bodyScale && renderers.All(item => item.sharedMaterial == material),
                    "ordinary animation preserves shared materials and configured scale: " + profile.name);
            }
        }
        finally
        {
            root.SetActive(false); world?.Dispose(); Object.Destroy(root);
            Object.Destroy(material); Object.Destroy(simulation); UnityEngine.Random.state = randomState;
        }
    }
}

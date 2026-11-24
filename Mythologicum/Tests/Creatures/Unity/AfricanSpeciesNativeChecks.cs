using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Imported African/Middle Eastern profiles, procedural bodies, and a few species-specific production AI journeys.</summary>
public static class AfricanSpeciesNativeChecks
{
    public static readonly string[] Ids = { "MokeleMbembe", "NinkiNanka", "Abada", "NandiBear", "Dingonek",
        "Marozi", "Grootslang", "Inkanyamba", "Aigamuxa", "Manticore" };
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception("African species: " + reason); checks++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Members).GetValue(owner);
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Members);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name, Members).SetValue(owner, value);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject root = new GameObject("African species native fixture");
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        private readonly ShelterDefinition shelterSettings;
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly ShelterConstruction Shelters;
        public readonly CreatureDefinition Profile;
        private CreatureDefinition preyProfile;
        public Fixture(string id, int index, bool tired = false)
        {
            Grid = new SquareBuildGrid(new Vector3(133000 + index * 100, 0, 133000), 2, 26, 16);
            Fences = new FenceLayout(Grid); Environment = new EnvironmentGrid(Grid);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            shelterSettings = ScriptableObject.CreateInstance<ShelterDefinition>();
            Shelters = new ShelterConstruction(Grid, Environment, root.transform, material, shelterSettings);
            Profile = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/" + id)); Profile.name = id;
            // Keep the imported diet, movement abilities and shelter rules. Only unrelated
            // needs, random intentions and observation timings are made deterministic.
            Profile.initialHunger = 0; Profile.hungerPerSecond = 0; Profile.Thirst.needsWater = false;
            Profile.initialEnergy = tired ? 0 : 100; Profile.energyDrainPerSecond = Profile.sleepRecoveryPerSecond = 0;
            Profile.minimumSpace = Profile.minimumOpenGroundFraction = 0;
            Profile.requiredTrees = Profile.requiredGravestones = Profile.minimumCompanions = 0;
            Profile.maximumCompanions = -1; Profile.fearRange = 0;
            Profile.happinessThreshold = Profile.happinessChangePerSecond = 0;
            Profile.decisionInterval = .05f; Profile.walkSpeed = Profile.runSpeed = Profile.Aquatic.swimSpeed = 10;
            Profile.pauseMinSeconds = Profile.pauseMaxSeconds = 10000; Profile.settlingSeconds = 0;
            Profile.attackSeconds = Profile.feedingSeconds = Profile.grazeSeconds = Profile.fenceClimbSeconds = .3f;
            Profile.Aquatic.captureSeconds = .2f; Profile.Aquatic.drowningSeconds = .5f;
            Profile.randomEscapeChance = Profile.hungryEscapeChance = Profile.crowdedEscapeChance = Profile.agitatedEscapeChance = 0;
            World = new CreatureWorld(Grid, Fences, new ParkBuildingNetwork(Grid, Fences), null, material,
                new[] { Profile }, Environment, simulation);
            World.ConfigureShelters(Shelters);
            Field<GameObject>(World, "root").transform.SetParent(root.transform, false);
        }
        public void Pond(int fromX, int toX)
        {
            bool placed = true;
            for (int x = fromX; x <= toX; x++) for (int z = 3; z <= 11; z++) placed &= Grid.Habitat.TrySetWater(new Vector2Int(x, z), 1);
            Check(placed, "fixture creates a real contiguous swimming pond");
        }
        public CreatureAgent Spawn(Vector2Int cell)
        {
            Check(World.TrySpawn(Profile, cell), Profile.name + " spawns through the ordinary production world");
            var actor = World.Agents.Last(); Set(actor, "DecisionTimer", 0f); return actor;
        }
        public CreatureAgent Prey(Vector2Int cell)
        {
            preyProfile = Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Goat")); preyProfile.name = "Goat";
            preyProfile.needsFood = preyProfile.Thirst.needsWater = false;
            preyProfile.damage = preyProfile.threat = preyProfile.fearRange = preyProfile.energyDrainPerSecond = 0;
            preyProfile.initialEnergy = 100; preyProfile.minimumSpace = 0;
            preyProfile.randomEscapeChance = preyProfile.hungryEscapeChance = preyProfile.crowdedEscapeChance = preyProfile.agitatedEscapeChance = 0;
            Check(World.TrySpawn(preyProfile, cell), "a real goat supplies an ordinary herbivore prey target");
            var actor = World.Agents.Last(); Set(actor, "DecisionTimer", 10000f); return actor;
        }
        public bool Until(Func<bool> done, int maximum = 900)
        { for (int i = 0; i < maximum; i++) { if (done()) return true; World.Tick(.05f); } return done(); }
        public void Dispose()
        {
            root.SetActive(false); World.Dispose(); Shelters.Dispose(); Object.Destroy(root);
            Object.Destroy(Profile); if (preyProfile != null) Object.Destroy(preyProfile);
            Object.Destroy(simulation); Object.Destroy(shelterSettings); Object.Destroy(material);
        }
    }

    public static int RunChecks()
    {
        checks = 0; Check(Application.isPlaying, "fixtures use ordinary Play-mode object lifetimes");
        var all = Resources.LoadAll<CreatureDefinition>("Creatures");
        Check(all.Length == 25 && all.Count(item => !item.IsDomestic) == 24
            && all.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() == all.Length,
            "the shipped catalog contains 24 mythical species plus the domestic goat, each with a unique save identity");
        var profiles = Ids.Select(id => Resources.Load<CreatureDefinition>("Creatures/" + id)).ToArray();
        Check(profiles.All(item => item != null) && profiles.Distinct().Count() == 10, "all ten stable resource IDs import as distinct profiles");
        var waterIds = new[] { "MokeleMbembe", "NinkiNanka", "Dingonek", "Inkanyamba" };
        var caveIds = new[] { "NandiBear", "Grootslang", "Manticore" };
        var shadeIds = new[] { "Abada", "Marozi" };
        var minimumCompanions = new[] { 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
        var maximumCompanions = new[] { 1, 0, 5, 0, 0, 4, 0, 0, 0, 1 };
        var origins = new[] { "central_africa", "western_africa", "central_africa", "eastern_africa", "eastern_africa",
            "eastern_africa", "southern_africa", "southern_africa", "southern_africa", "persia" };
        foreach (var profile in profiles)
        {
            int index = Array.IndexOf(Ids, profile.name);
            Check(profile.name == Ids[index] && (int)profile.placeholderKind == index + 8 && profile.visualPrefab == null,
                profile.name + " preserves the appended serialized placeholder and species identities"
                + " (index=" + index + ", kind=" + (int)profile.placeholderKind + "/" + profile.placeholderKind
                + ", prefab=" + (profile.visualPrefab == null ? "null" : profile.visualPrefab.name)
                + ", resource=" + UnityEditor.AssetDatabase.GetAssetPath(profile) + ")");
            Check(profile.manuallyPlaceable && !profile.IsDomestic && profile.pettable == (profile.name == "Abada") && profile.rating >= 1 && profile.rating <= 5,
                profile.name + " uses normal mythical specimen placement and expedition rarity");
            Check(profile.origin == "creature.origin." + origins[index] && profile.minimumCompanions == minimumCompanions[index]
                && profile.maximumCompanions == maximumCompanions[index], profile.name + " retains the chosen origin and solitary, pair or herd rules");
            Check(Loc.Table.Contains(profile.displayName) && Loc.Table.Contains(profile.origin)
                && !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.displayName, GameLanguage.ENGLISH))
                && !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.origin, GameLanguage.ENGLISH)),
                profile.name + " has stable localization keys with nonblank English source text");
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                Check(!string.IsNullOrWhiteSpace(Loc.Table.Get(profile.displayName, language))
                    && !string.IsNullOrWhiteSpace(Loc.Table.Get(profile.origin, language))
                    && (language == GameLanguage.ENGLISH || !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.displayName, language))
                        || Loc.Table.Get(profile.displayName, language) == Loc.English(profile.displayName))
                    && (language == GameLanguage.ENGLISH || !string.IsNullOrWhiteSpace(Loc.Table.Raw(profile.origin, language))
                        || Loc.Table.Get(profile.origin, language) == Loc.English(profile.origin)),
                    profile.name + " resolves its name and origin with the normal English fallback in " + language);
            bool herbivore = profile.name == "MokeleMbembe" || profile.name == "Abada";
            Check(profile.diet == (herbivore ? CreatureDiet.Herbivore : CreatureDiet.Carnivore)
                && (profile.tags & (herbivore ? CreatureTags.Herbivore : CreatureTags.Predator)) != 0
                && !profile.feedsOnFear && profile.needsFood && profile.HungerRate > 0 && profile.Thirst.needsWater
                && profile.Thirst.thirstPerSecond > 0 && profile.energyDrainPerSecond > 0 && profile.sleepRecoveryPerSecond > 0,
                profile.name + " imports its ordinary diet, prey category, hunger, thirst and sleep needs");
            if (!herbivore)
                Check((profile.preyTags & (CreatureTags.Goat | CreatureTags.Herbivore | CreatureTags.Visitor | CreatureTags.Staff))
                    == (CreatureTags.Goat | CreatureTags.Herbivore | CreatureTags.Visitor | CreatureTags.Staff),
                    profile.name + " recognizes existing herbivore and human prey");
            Check(profile.maximumHealth > 0 && profile.damage > 0 && profile.threat > 0
                && profile.walkSpeed > 0 && profile.runSpeed >= profile.walkSpeed
                && profile.minimumSpace > 0 && profile.Habitat.HasPreferences
                && profile.minimumCompanions >= 0 && (profile.maximumCompanions < 0 || profile.maximumCompanions >= profile.minimumCompanions),
                profile.name + " retains editable combat, locomotion, habitat and feasible group settings");
            var appeal = profile.VisitorAppeal;
            Check(new[] { appeal.thrill, appeal.wonder, appeal.adventure, appeal.children }.All(value => value >= 0 && value <= 1)
                && new[] { appeal.thrill, appeal.wonder, appeal.adventure, appeal.children }.Distinct().Count() > 1,
                profile.name + " has a deliberately differentiated normalized visitor appeal");
            var mode = waterIds.Contains(profile.name) ? CreatureShelterMode.Water : caveIds.Contains(profile.name)
                ? CreatureShelterMode.Cave : shadeIds.Contains(profile.name) ? CreatureShelterMode.TreeShade : CreatureShelterMode.Ground;
            Check(profile.Shelter.mode == mode && (!waterIds.Contains(profile.name) || profile.Shelter.requiredForSleep)
                && profile.Aquatic.canSwim == waterIds.Contains(profile.name), profile.name + " imports its intended water, cave, shade or ground rest");
            Check(!profile.canPhaseFences && !profile.canBurrowFences && !profile.requiresTreeForSleep
                && !profile.requiresBurrowForSleep && !profile.requiresGravestoneForSleep,
                profile.name + " uses only supported configured movement abilities");
            var research = new SpeciesResearch(new[] { profile }); research.Study(profile, 100);
            string food = herbivore ? "herbivore" : profile.Aquatic.dragPreyToWater ? "water_hunter" : "carnivore";
            string sleep = mode == CreatureShelterMode.Water ? "water" : mode == CreatureShelterMode.Cave ? "cave"
                : mode == CreatureShelterMode.TreeShade ? "shade" : "ground";
            Check(research.Description(profile).Contains(Loc.Text("research.creature.food." + food).Trim())
                && research.Description(profile).Contains(Loc.Text("research.creature.sleep." + sleep).Trim()),
                profile.name + " exposes its actual diet and rest requirements through completed research");
        }
        Check(profiles[0].Behaviour.grazingSearchSteps == 24 && !profiles[0].Aquatic.dragPreyToWater,
            "Mokele-mbembe searches far enough through its pond for edible shoreline without predator behavior");
        Check(new[] { profiles[1], profiles[4], profiles[7] }.All(item => item.Aquatic.dragPreyToWater),
            "Ninki Nanka, Dingonek and Inkanyamba select the existing capture-and-drag water hunt");
        Check(profiles[6].canBreakFences && profiles[9].canClimbWoodenFences
            && profiles.Where(item => item.name != "Manticore").All(item => !item.canClimbWoodenFences),
            "Grootslang selects physical fence breaking and Manticore selects supported fence climbing");
        Check(!typeof(CreatureDefinition).GetFields().Any(item => item.Name.IndexOf("fly", StringComparison.OrdinalIgnoreCase) >= 0
            || item.Name.IndexOf("flight", StringComparison.OrdinalIgnoreCase) >= 0), "no unsupported flight ability is introduced for these species");

        var random = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(528133);
            CheckBodies(profiles);
            using (var f = new Fixture("MokeleMbembe", 1))
            {
                f.Pond(7, 17); f.Profile.initialHunger = 90;
                var actor = f.Spawn(new Vector2Int(12, 7));
                Check(f.Until(() => actor.Activity == CreatureActivity.Grazing && !f.Grid.Habitat.IsWater(actor.Cell)),
                    "a hungry Mokele-mbembe swims out of the pond and reaches edible shoreline");
                Check(f.Until(() => actor.Vitals.Hunger < 90), "shoreline grazing actually relieves Mokele-mbembe hunger");
                actor.Vitals.Restore(0, 0); actor.Energy.Spend(100); Set(actor, "DecisionTimer", 0f);
                Check(f.Until(() => actor.Activity == CreatureActivity.Sleeping && f.Grid.Habitat.IsWater(actor.Cell))
                    && f.World.CaptureValidShelterHome(actor).HasValue && f.World.IsShelterProtected(actor),
                    "after eating, exhausted Mokele-mbembe returns to a real water home to sleep");
            }
            using (var f = new Fixture("Abada", 2))
            {
                f.Profile.initialHunger = 90; var tree = new Vector2Int(9, 6); f.Environment.AddTree(tree);
                var actor = f.Spawn(new Vector2Int(7, 5));
                Check(f.Until(() => actor.Vitals.Hunger < 90), "Abada satisfies hunger by ordinary ground grazing");
                actor.Vitals.Restore(0, 0); actor.Energy.Spend(100); Set(actor, "DecisionTimer", 0f);
                Check(f.Until(() => actor.Activity == CreatureActivity.Sleeping)
                    && ConstructionRouteGraph.CellDistance(actor.Cell, tree) == 1 && Field<float>(actor, "Elevation") == 0,
                    "Abada seeks tree shade and rests on the ground without tree climbing");
            }
            int index = 3;
            foreach (string id in new[] { "NinkiNanka", "Dingonek", "Inkanyamba" })
                using (var f = new Fixture(id, index++))
                {
                    f.Pond(10, 15); f.Profile.initialHunger = 90;
                    var hunter = f.Spawn(new Vector2Int(7, 5)); var prey = f.Prey(new Vector2Int(8, 5));
                    Check(f.Until(() => hunter.Activity == CreatureActivity.DraggingPrey) && prey.Alive
                        && prey.Health.Current == prey.Health.Maximum && Field<CreatureAgent>(prey, "CapturedBy") == hunter,
                        id + " captures ordinary prey alive on the bank before dragging it");
                    Check(f.Until(() => hunter.Activity == CreatureActivity.DrowningPrey)
                        && f.Grid.Habitat.IsWater(hunter.Cell) && f.Grid.Habitat.IsWater(prey.Cell) && prey.Alive,
                        id + " physically reaches water with its living captive before the drowning phase");
                    Check(f.Until(() => !prey.Alive && hunter.Vitals.Hunger < 90), id + " receives food relief only after its completed water meal");
                }
            using (var f = new Fixture("Grootslang", 6, true))
            {
                var cave = new Vector2Int(6, 5); Check(f.Shelters.TryPlaceCave(cave), "a real cave can house Grootslang");
                var actor = f.Spawn(new Vector2Int(4, 5));
                Check(f.Until(() => actor.Activity == CreatureActivity.Sleeping && actor.Cell == cave)
                    && f.World.IsHiddenInShelter(actor) && f.World.CaptureValidShelterHome(actor) == cave,
                    "Grootslang walks into an actual cave and binds its persistent sleeping home");
            }
            foreach (string id in new[] { "Grootslang", "Manticore" })
                using (var f = new Fixture(id, index++))
                {
                    var start = new Vector2Int(4, 5);
                    var edges = new[] { new FenceEdge(start, true), new FenceEdge(start, false),
                        new FenceEdge(start + Vector2Int.up, true), new FenceEdge(start + Vector2Int.right, false) };
                    foreach (var edge in edges) Check(f.Fences.TryAdd(edge), "an ordinary wooden fence closes the species fixture");
                    var actor = f.Spawn(start);
                    if (id == "Grootslang") { f.Profile.happinessThreshold = 100; actor.Wellbeing.Restore(0, 1); }
                    else actor.Wellbeing.Restore(100, 0);
                    Set(actor, "EscapeIntent", true); Set(actor, "EscapeUntil", f.World.SimulationTime + 30);
                    if (id == "Grootslang")
                    {
                        float health = edges.Sum(edge => f.Fences.HealthAt(edge));
                        Check(f.Until(() => actor.Activity == CreatureActivity.AttackingFence), "agitated Grootslang selects an actual physical fence strike");
                        Check(f.Until(() => !Field<bool>(actor, "EscapeIntent"))
                            && edges.Sum(edge => f.Fences.HealthAt(edge)) < health && actor.Cell == start,
                            "one Grootslang escape intention damages its real barrier and returns to rest");
                    }
                    else
                    {
                        Check(f.Until(() => actor.Activity == CreatureActivity.ClimbingFence), "Manticore uses the existing visible wooden fence climb");
                        Check(f.Until(() => actor.Cell != start) && !Field<bool>(actor, "EscapeIntent")
                            && edges.All(edge => Mathf.Approximately(f.Fences.HealthAt(edge), f.Fences.MaximumHealthAt(edge))),
                            "Manticore completes a ground-to-ground climb while preserving the wooden fence");
                    }
                }
        }
        finally { UnityEngine.Random.state = random; }
        return checks;
    }

    private static void CheckBodies(CreatureDefinition[] profiles)
    {
        var root = new GameObject("African imported body fixture");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
        CreatureWorld world = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(131000, 0, 131000), 2, 28, 16); var fences = new FenceLayout(grid);
            world = new CreatureWorld(grid, fences, new ParkBuildingNetwork(grid, fences), null, material, profiles, null, simulation);
            Field<GameObject>(world, "root").transform.SetParent(root.transform, false);
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i]; Check(world.TrySpawn(profile, new Vector2Int(3 + 5 * (i % 5), 3 + 8 * (i / 5))), "actual imported body spawns for " + profile.name);
                var actor = world.Agents.Last(); var body = Field<Transform>(actor, "Body");
                var renderers = body.GetComponentsInChildren<MeshRenderer>();
                Check(body.name == profile.placeholderKind + " placeholder" && body.localScale == profile.bodyScale
                    && !Field<bool>(actor, "HasVisualModel") && Field<MeshRenderer>(actor, "BodyRenderer") != null && renderers.Length >= 8,
                    profile.name + " supplies a recognizable composite body through the normal animation contract");
                var capsule = actor.GetComponent<CapsuleCollider>();
                Check(capsule != null && capsule.isTrigger && capsule.enabled
                    && body.GetComponentsInChildren<Collider>(true).All(item => !item.enabled)
                    && renderers.All(item => item.sharedMaterial == material && item.GetComponent<MeshFilter>()?.sharedMesh != null),
                    profile.name + " uses the supplied world material and meshes while the shared creature capsule owns interaction");
                Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Check(Mathf.Abs(bounds.min.y - grid.CellCenter(actor.Cell).y) < .08f && bounds.size.y > .3f,
                    profile.name + " rests its feet or body on the ground without floating");
                var features = new[] {
                    new[] { "Mokele-Mbembe lower neck", "Mokele-Mbembe upper neck" },
                    new[] { "Ninki Nanka crocodile snout", "Ninki Nanka horn" },
                    new[] { "Abada forehead horn", "Abada nose horn" },
                    new[] { "Nandi Bear shoulder hump", "Nandi Bear round ear" },
                    new[] { "Dingonek walrus head", "Dingonek saber tusk" },
                    new[] { "Marozi flank spot", "Marozi lion head" },
                    new[] { "Grootslang elephant head", "Grootslang serpent tail segment 0" },
                    new[] { "Inkanyamba horse head", "Inkanyamba serpent tail segment 0" },
                    new[] { "Aigamuxa foot eye", "Aigamuxa eyeless head" },
                    new[] { "Manticore human face", "Manticore scorpion stinger" } };
                Check(features[i].All(part => body.Find(part) != null), profile.name + " has its identifying anatomy in the actual generated mesh hierarchy");
                typeof(CreatureWorld).GetMethod("Animate", Members).Invoke(world, new object[] { actor });
                Check(body.localScale == profile.bodyScale && renderers.All(item => item.sharedMaterial == material),
                    profile.name + " retains its configured scale and shared materials during ordinary animation");
            }
            CaptureOverview(root, world, grid);
        }
        finally
        {
            root.SetActive(false); world?.Dispose(); Object.Destroy(root); Object.Destroy(material); Object.Destroy(simulation);
        }
    }

    private static void CaptureOverview(GameObject root, CreatureWorld world, SquareBuildGrid grid)
    {
        // One bounded render makes every authored silhouette reviewable; it has no
        // dependency on the game camera, generated landscape or user's editor layout.
        var cameraObject = new GameObject("African body overview camera"); cameraObject.transform.SetParent(root.transform, false);
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.orthographic = true;
        camera.orthographicSize = 18; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .19f);
        camera.nearClipPlane = .1f; camera.farClipPlane = 160; camera.cullingMask = 1 << 31;
        var focus = grid.CellCenter(new Vector2Int(13, 7));
        camera.transform.position = focus + new Vector3(0, 32, -29); camera.transform.LookAt(focus + Vector3.up);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var actor in world.Agents)
        {
            actor.transform.rotation = Quaternion.Euler(0, 180, 0);
            var label = new GameObject(actor.Definition.name + " label"); label.transform.SetParent(root.transform, false);
            label.transform.position = grid.CellCenter(actor.Cell) + new Vector3(0, .15f, -2.5f); label.transform.rotation = camera.transform.rotation;
            var text = label.AddComponent<TextMesh>(); text.text = Loc.English(actor.Definition.displayName);
            text.font = font; text.fontSize = 48; text.characterSize = .25f; text.anchor = TextAnchor.MiddleCenter; text.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        var target = new RenderTexture(1800, 1000, 24, RenderTextureFormat.ARGB32); target.Create();
        var active = RenderTexture.active; Texture2D pixels = null;
        try
        {
            camera.targetTexture = target;
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int i = 0; i < 2; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target; pixels = new Texture2D(1800, 1000, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1800, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "AfricanSpeciesOverview.png"), pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = active; target.Release();
            Object.Destroy(target); if (pixels != null) Object.Destroy(pixels);
        }
    }
}

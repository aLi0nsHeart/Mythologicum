using System;
using System.IO;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SpeciesCombatValidation
{
    private const string Running = "SpeciesCombatValidation.running";
    private static int checks;
    static SpeciesCombatValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); Run(); }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/SpeciesValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static Vector2Int V(int x, int y) => new Vector2Int(x, y);
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); Debug.Log("SPECIES CHECK PASS: " + message); }
    private static bool Captured(CreatureAgent agent) => typeof(CreatureAgent)
        .GetField("CapturedBy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent) != null;

    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly ParkBuildingNetwork Network;
        public readonly EnvironmentGrid Environment;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition Goat, Hunter;
        public VisitorWorld Visitors;
        public Fixture(int width = 8, int depth = 6)
        {
            foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects())
                UnityEngine.Object.DestroyImmediate(old);
            UnityEngine.Random.InitState(7351);
            Grid = new SquareBuildGrid(Vector3.zero, 2f, width, depth);
            Fences = new FenceLayout(Grid); Network = new ParkBuildingNetwork(Grid, Fences);
            Environment = new EnvironmentGrid(Grid);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(width, -.5f, depth);
            ground.transform.localScale = new Vector3(width * 2f, 1f, depth * 2f);
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(width, 20f, -5f);
            camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            Goat = Profile("Test goat"); Goat.tags = CreatureTags.Goat; Goat.diet = CreatureDiet.Herbivore;
            Goat.maximumHealth = 40f; Goat.damage = 0f; Goat.threat = 0f;
            Goat.initialHunger = 0f; Goat.walkSpeed = Goat.runSpeed = 0f;
            Hunter = Profile("Test hunter"); Hunter.tags = CreatureTags.Predator;
            Hunter.diet = CreatureDiet.Carnivore; Hunter.preyTags = CreatureTags.Goat;
            Hunter.maximumHealth = 120f; Hunter.damage = 40f; Hunter.threat = 50f;
            Hunter.initialHunger = 100f; Hunter.runSpeed = 8f; Hunter.attackSeconds = .5f;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            World = new CreatureWorld(Grid, Fences, Network, camera, new Material(shader), new[] { Goat, Hunter }, Environment);
        }
        public static CreatureDefinition Profile(string name)
        {
            var profile = ScriptableObject.CreateInstance<CreatureDefinition>();
            profile.displayName = name; profile.hungerPerSecond = 0f; profile.energyDrainPerSecond = 0f;
            profile.initialEnergy = 100f; profile.visionRange = profile.fearRange = 100f;
            profile.decisionInterval = .1f; profile.randomEscapeChance = profile.hungryEscapeChance = 0f;
            profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0f; profile.settlingSeconds = 0f;
            profile.minimumSpace = 0f; profile.requiredTrees = 0; profile.minimumCompanions = 0;
            return profile;
        }
        public CreatureAgent Spawn(CreatureDefinition profile, Vector2Int cell)
        {
            if (!World.TrySpawn(profile, cell)) throw new Exception("Fixture could not spawn " + profile.displayName);
            return World.Agents[World.Agents.Count - 1];
        }
        public void Tick(float seconds)
        {
            for (int i = 0; i < Mathf.CeilToInt(seconds / .05f); i++)
            { World.Tick(.05f); Visitors?.Tick(.05f); }
        }
        public bool Until(Func<bool> condition, float timeout)
        {
            for (float elapsed = 0; elapsed < timeout; elapsed += .05f)
            { if (condition()) return true; Tick(.05f); }
            return condition();
        }
        public void Wall(FenceKind kind, bool gate = false)
        {
            for (int y = 0; y < Grid.Depth; y++) Fences.TryAdd(new FenceEdge(V(3,y), false), kind);
            if (gate && !Fences.TrySetGate(new FenceEdge(V(3,2), false))) throw new Exception("Fixture gate failed");
        }
        public VisitorAgent AdmitVisitor()
        {
            Network.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, V(1,2), 0));
            Grid.TryOccupy(V(1,1), BuildCellContent.Path); Grid.TryOccupy(V(2,1), BuildCellContent.Path);
            Network.Refresh();
            var settings = ScriptableObject.CreateInstance<VisitorDefinition>();
            settings.maximumVisitors = 1; settings.spawnInterval = 10000f;
            settings.hungerPerSecond = settings.energyDrainPerSecond = 0f;
            settings.walkSpeed = settings.runSpeed = .1f; settings.minimumStaySeconds = 1000f;
            settings.maximumStaySeconds = 10000f;
            Visitors = new VisitorWorld(Grid, Fences, Network, World, settings, new ParkEconomy());
            return Visitors.TrySpawn();
        }
    }

    private static void Combat()
    {
        var f = new Fixture();
        var hunter = f.Spawn(f.Hunter, V(1,1)); var goat = f.Spawn(f.Goat, V(4,1));
        Check(f.Until(() => hunter.Activity == CreatureActivity.Attacking, 5f), "Hunter approaches prey and begins a timed strike");
        Check(goat.Health.Current == 40f && goat.Alive, "Starting an attack does not apply premature damage");
        Check(f.Until(() => !goat.Alive, 2f), "One Chupacabra-strength strike kills a goat");
        Check(goat.Health.Current == 0f && hunter.Activity == CreatureActivity.Feeding, "A lethal strike drains health then starts feeding");
        float hunger = hunter.Vitals.Hunger; f.Tick(.25f);
        Check(hunter.Vitals.Hunger == hunger, "Predation hunger relief still waits for feeding completion");
        Check(f.Until(() => hunter.Vitals.Hunger < hunger, 5f), "Completed feeding relieves hunger after the kill");

        f = new Fixture(); f.Goat.maximumHealth = 120f; f.Hunter.damage = 25f; f.Hunter.attackSeconds = 1f;
        hunter = f.Spawn(f.Hunter, V(1,1)); goat = f.Spawn(f.Goat, V(4,1));
        Check(f.Until(() => goat.Health.Current < 120f, 7f), "Nonlethal combat applies an individual strike");
        Check(goat.Alive && goat.Health.Current == 95f, "Healthy prey survives damage below its health");
        Check(!Captured(goat), "Nonlethal strike releases the prey instead of permanent captivity");
        Check(hunter.Activity != CreatureActivity.Feeding && hunter.Vitals.Hunger == 100f,
            "A surviving opponent does not become food or relieve hunger");
    }

    private static void Threat()
    {
        var f = new Fixture(); f.Hunter.preyTags = CreatureTags.None; f.Hunter.initialHunger = 0f;
        f.Hunter.walkSpeed = f.Hunter.runSpeed = 0f; f.Goat.fearRange = 4f;
        var goat = f.Spawn(f.Goat, V(1,1)); f.Spawn(f.Hunter, V(2,1)); f.Tick(.2f);
        Check(goat.Vitals.Fear > 0f && goat.Activity == CreatureActivity.Fleeing, "Higher threat produces fear at close range");

        f = new Fixture(); f.Hunter.preyTags = CreatureTags.None; f.Hunter.initialHunger = 0f;
        f.Hunter.threat = f.Goat.threat = 5f; f.Hunter.walkSpeed = f.Hunter.runSpeed = 0f;
        goat = f.Spawn(f.Goat, V(1,1)); f.Spawn(f.Hunter, V(2,1)); f.Tick(.5f);
        Check(goat.Vitals.Fear == 0f, "Equal threat does not cause automatic fear");

        f = new Fixture(); f.Hunter.preyTags = CreatureTags.None; f.Hunter.initialHunger = 0f;
        f.Hunter.walkSpeed = f.Hunter.runSpeed = 0f; f.Goat.fearRange = 3f;
        goat = f.Spawn(f.Goat, V(1,1)); f.Spawn(f.Hunter, V(5,1)); f.Tick(.5f);
        Check(goat.Vitals.Fear == 0f, "Higher threat outside fear range stays harmless to mood");

        f = new Fixture(); f.Wall(FenceKind.Heavy); f.Hunter.preyTags = CreatureTags.None;
        f.Hunter.initialHunger = 0f; f.Hunter.walkSpeed = f.Hunter.runSpeed = 0f;
        goat = f.Spawn(f.Goat, V(2,2)); f.Spawn(f.Hunter, V(3,2)); f.Tick(.5f);
        Check(goat.Vitals.Fear == 0f, "A complete safe barrier prevents threat panic through exhibits");
    }

    private static void FearFeeding()
    {
        var f = new Fixture();
        f.Hunter.diet = CreatureDiet.Fear; f.Hunter.feedsOnFear = true; f.Hunter.damage = 0f;
        f.Hunter.preyTags = CreatureTags.Visitor; f.Hunter.threat = 20f;
        f.Hunter.scareRange = 3f; f.Hunter.scareSeconds = .5f; f.Hunter.scareCooldownSeconds = 10f;
        var guest = f.AdmitVisitor(); Check(guest != null, "Actual visitor enters scare test through an open entrance");
        var trickster = f.Spawn(f.Hunter, V(2,1));
        float health = guest.Actor.Health.Current;
        Check(f.Until(() => trickster.Vitals.Hunger < 100f, 5f), "Hitotsume feeds by successfully frightening a visitor");
        Check(guest.Actor.Vitals.Fear > 0f, "Successful fear feeding actually raises visitor fear");
        Check(guest.Actor.Alive && guest.Actor.Health.Current == health, "Fear feeding deals no health damage");
        float hunger = trickster.Vitals.Hunger; f.Tick(.5f);
        Check(trickster.Vitals.Hunger == hunger, "Scare cooldown prevents repeated instant meals");

        f = new Fixture();
        f.Hunter.diet = CreatureDiet.Fear; f.Hunter.feedsOnFear = true; f.Hunter.damage = 0f;
        f.Hunter.preyTags = CreatureTags.Visitor; f.Hunter.threat = 20f;
        f.Hunter.scareRange = 3f; f.Hunter.scareSeconds = 1f;
        guest = f.AdmitVisitor(); trickster = f.Spawn(f.Hunter, V(2,1));
        Check(f.Until(() => trickster.Activity == CreatureActivity.Scaring, 3f), "Recovery regression begins a successful scare action");
        // After the scare lands, equalize threat and clear fear to represent a guest recovering before the animation ends.
        guest.Actor.Definition.threat = trickster.Definition.threat;
        guest.Actor.Vitals.Tick(1f, 0f, 0f, 1000f, false);
        Check(guest.Actor.Vitals.Fear == 0f, "Scare recovery scenario clears current fear before action completion");
        Check(f.Until(() => trickster.Vitals.Hunger < 100f, 3f), "An already successful scare still feeds after the guest recovers");
    }

    private static void BarrierDamage()
    {
        foreach (FenceKind kind in new[] { FenceKind.Wooden, FenceKind.Spiked, FenceKind.Sanctified })
        {
            var f = new Fixture(); f.Wall(kind); f.Hunter.damage = 90f; f.Hunter.canBreakFences = true;
            var oni = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2));
            var edge = new FenceEdge(V(3,2), false);
            Check(f.Until(() => f.Fences.HealthAt(edge) < f.Fences.MaximumHealthAt(edge) || !f.Fences.HasFence(edge), 7f),
                "Oni attacks and damages " + kind + " rather than teleporting through it");
            Check(f.Until(() => !f.Fences.HasFence(edge), 7f), "Enough physical damage destroys " + kind);
            Check(f.Until(() => oni.Cell.x >= 3, 4f), "Oni crosses the gap only after " + kind + " breaks");
        }
        {
            var f = new Fixture(); f.Wall(FenceKind.Heavy, true); f.Hunter.damage = 90f; f.Hunter.canBreakFences = true;
            var oni = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2)); f.Tick(8f);
            var edge = new FenceEdge(V(3,2), false);
            Check(f.Fences.HealthAt(edge) == 800f && f.Fences.IsGate(edge) && oni.Cell.x < 3,
                "Reinforced fence and gate resist Oni damage and contain it");
        }
        {
            var f = new Fixture(); f.Wall(FenceKind.Wooden); f.Hunter.canBreakFences = true;
            var oni = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2));
            Check(f.Until(() => oni.Activity == CreatureActivity.AttackingFence, 5f), "Exhaustion interruption scenario begins attacking a fence");
            oni.Energy.Spend(100f);
            Check(f.Until(() => oni.Activity == CreatureActivity.Sleeping, 3f), "Exhaustion interrupts fence attacks so the creature can sleep");
            Check(f.Fences.HealthAt(new FenceEdge(V(3,2), false)) == 120f, "Interrupted fence attack does not keep dealing damage while resting");
        }
        {
            var f = new Fixture(); f.Wall(FenceKind.Wooden); f.Hunter.canBreakFences = true;
            var oni = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2));
            Check(f.Until(() => oni.Activity == CreatureActivity.AttackingFence, 5f), "Threat interruption scenario begins attacking a fence");
            var danger = Fixture.Profile("Higher threat"); danger.threat = 200f; danger.initialHunger = 0f;
            danger.diet = CreatureDiet.None; danger.walkSpeed = danger.runSpeed = 0f;
            f.Spawn(danger, V(1,3));
            Check(f.Until(() => oni.Activity == CreatureActivity.Fleeing, 3f), "A higher nearby threat interrupts fence attacks and causes flight");
            Check(f.Fences.HealthAt(new FenceEdge(V(3,2), false)) == 120f, "Threat-interrupted strike does not apply hidden fence damage");
        }
    }

    private static void Phasing()
    {
        var f = new Fixture(); f.Wall(FenceKind.Heavy, true);
        f.Hunter.canPhaseFences = true; f.Hunter.phaseHungerThreshold = 80f;
        f.Hunter.damage = 0f; f.Hunter.initialHunger = 79f;
        var ghost = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2)); f.Tick(3f);
        Check(ghost.Cell.x < 3, "Yurei below severe hunger threshold cannot phase through a fence");
        ghost.Vitals.Tick(1f, 2f, 0f, 0f, false);
        Check(f.Until(() => ghost.Activity == CreatureActivity.PhasingFence, 5f), "Very hungry Yurei begins a visible phase crossing");
        Check(f.Until(() => ghost.Cell.x >= 3, 5f), "Hungry Yurei phases through reinforced fencing and its protected gate");
        Check(f.Fences.HealthAt(new FenceEdge(V(3,2), false)) == 800f, "Phasing crosses without damaging its barrier");

        f = new Fixture(); f.Wall(FenceKind.Sanctified, true);
        f.Hunter.canPhaseFences = true; f.Hunter.phaseHungerThreshold = 80f; f.Hunter.damage = 0f;
        ghost = f.Spawn(f.Hunter, V(1,2)); f.Spawn(f.Goat, V(5,2)); f.Tick(8f);
        Check(ghost.Cell.x < 3 && f.Fences.IsGate(new FenceEdge(V(3,2), false)), "Sanctified fencing and gates block even hungry phasing");
        Check(f.Fences.HealthAt(new FenceEdge(V(3,2), false)) == 300f, "Zero-damage phase test cannot disguise a barrier breach as an attack");
    }

    private static void GraveSleep()
    {
        var f = new Fixture(); f.Hunter.preyTags = CreatureTags.None; f.Hunter.initialHunger = 0f;
        f.Hunter.initialEnergy = 0f; f.Hunter.requiresGravestoneForSleep = true;
        f.Hunter.requiredGravestones = 1; f.Hunter.walkSpeed = 4f;
        f.Environment.AddTree(V(2,2)); var ghost = f.Spawn(f.Hunter, V(2,3)); f.Tick(2f);
        Check(ghost.Activity != CreatureActivity.Sleeping && ghost.Energy.Energy == 0f,
            "A tired Yurei cannot substitute a tree or bare ground for a gravestone");
        var grave = V(4,3); f.Environment.AddGravestone(grave);
        Check(f.Until(() => ghost.Activity == CreatureActivity.Sleeping, 8f), "Yurei seeks a newly placed gravestone and sleeps there");
        Check(ghost.Cell == grave, "Gravestone sleep occurs at the physical sleep site");
        Transform body = ghost.transform.Find("Animated placeholder");
        Check(body != null && !body.gameObject.activeSelf, "Sleeping Yurei disappears into its gravestone rather than lying beside it");
        var observer = f.Spawn(f.Goat, V(1,3));
        Check(!f.World.CanSee(observer, ghost, 100f), "A Yurei sleeping inside a gravestone is not visible to other agents");
        f.Tick(.25f); float energy = ghost.Energy.Energy;
        Check(energy > 0f && ghost.NearbyGravestones > 0, "Gravestone restores energy and contributes to environment needs");
        f.Environment.RemoveGravestone(grave); f.Tick(.3f);
        Check(ghost.Activity != CreatureActivity.Sleeping && ghost.Energy.Energy <= energy,
            "Removing an occupied gravestone interrupts sleep without further recovery");
        Check(body.gameObject.activeSelf, "Interrupted grave sleep restores the creature's visible body");
    }

    private static void FenceVisualDamage()
    {
        var f = new Fixture();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var root = new GameObject("Physical test fences");
        var visuals = Resources.Load<FenceVisualCatalog>("Fences/FenceVisuals");
        Check(visuals != null && visuals.heavy != null && visuals.sanctified != null,
            "Active fence catalog retains imported reinforced and sanctified art");
        var construction = new FenceConstruction(f.Grid, root.transform, new Material(shader), null, visuals);
        var edge = new FenceEdge(V(3,2), false);
        Check(construction.TryPlace(edge), "Damage display test creates an actual imported fence and solid barrier");
        construction.Layout.TryDamage(edge, 30f); construction.Refresh();
        TextMesh health = root.GetComponentInChildren<TextMesh>();
        Check(health != null && health.text.Contains("90/120"), "Partial fence damage appears as current and maximum health");
        construction.Layout.TryDamage(edge, 100f); construction.Refresh();
        bool enabledCollider = false;
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            if (collider.enabled) enabledCollider = true;
        Check(!construction.Layout.HasFence(edge) && !enabledCollider,
            "Destroyed logical fence immediately retires its visual selection and physical colliders");
        Check(construction.TryPlace(edge, FenceKind.Heavy) && construction.Layout.HealthAt(edge) == 800f,
            "Destroyed segment can be rebuilt using reinforced art and full new health");
    }

    private static void Profiles()
    {
        CreatureDefinition chupa = Resources.Load<CreatureDefinition>("Creatures/Chupacabra");
        CreatureDefinition goat = Resources.Load<CreatureDefinition>("Creatures/Goat");
        CreatureDefinition hitotsume = Resources.Load<CreatureDefinition>("Creatures/Hitotsume");
        CreatureDefinition yurei = Resources.Load<CreatureDefinition>("Creatures/Yurei");
        CreatureDefinition oni = Resources.Load<CreatureDefinition>("Creatures/Oni");
        Check(chupa != null && goat != null && hitotsume != null && yurei != null && oni != null, "All five authored creature profiles load");
        Check(chupa.visualPrefab == null && chupa.origin == "Southern New World", "Chupacabra returns to the capsule and retains its origin");
        Check(chupa.damage >= goat.maximumHealth && chupa.minimumCompanions == 1 && chupa.maximumCompanions == 2,
            "Chupacabra can kill goats in one hit and prefers two to three total animals");
        Check(hitotsume.visualPrefab != null && hitotsume.damage == 0f && hitotsume.feedsOnFear
            && hitotsume.minimumCompanions == 0 && hitotsume.maximumCompanions == 0, "Hitotsume has its own visual, fear feeding and solitary profile");
        Check(yurei.visualPrefab != null && yurei.origin == "Yokai" && yurei.canPhaseFences
            && yurei.requiresGravestoneForSleep && yurei.requiredGravestones > 0 && yurei.maximumCompanions == 19,
            "Yurei has grave needs, phasing and a maximum total group of twenty");
        Check(yurei.hungerPerSecond < chupa.hungerPerSecond, "Yurei accumulates hunger much more slowly");
        Check(oni.visualPrefab != null && oni.origin == "Yokai" && oni.canBreakFences
            && oni.maximumCompanions == 2 && oni.minimumSpace > chupa.minimumSpace && oni.hungerPerSecond > chupa.hungerPerSecond,
            "Oni has a large habitat, high appetite, fence breaking and a group limit of three");
        Check(Resources.Load<CreatureDefinition>("Creatures/Kuchisakeonna") == null, "Old creature identity does not remain as a duplicate profile");
        var f = new Fixture(); var actor = f.Spawn(chupa, V(2,2));
        Check(actor.GetComponentInChildren<Animator>() == null && actor.GetComponentInChildren<MeshRenderer>() != null,
            "Actual Chupacabra spawn uses the placeholder rather than the Hitotsume rig");
    }
    private static void Run()
    {
        try
        {
            Profiles(); Combat(); Threat(); FearFeeding(); BarrierDamage(); Phasing(); GraveSleep(); FenceVisualDamage();
            string result = "Species combat validation: " + checks + " checks passed.";
            File.WriteAllText("SpeciesCombatValidationResults.txt", result); Debug.Log(result); EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("SpeciesCombatValidationResults.txt", "FAILED after " + checks + " checks: " + exception);
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
}

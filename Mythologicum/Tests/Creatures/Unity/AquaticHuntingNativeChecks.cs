using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Real capture, physical drag, interruption and water-only meal regressions.</summary>
public static class AquaticHuntingNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Hidden);
        if (field != null) field.SetValue(owner, value); else owner.GetType().GetProperty(name).SetValue(owner, value);
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden).GetValue(owner);
    private sealed class Fixture : IDisposable
    {
        public readonly SquareBuildGrid Grid = new SquareBuildGrid(new Vector3(88000, 0, 88000), 2, 18, 12);
        public readonly CreatureDefinition HunterProfile, GoatProfile;
        public readonly CreatureWorld World;
        public readonly CreatureAgent Hunter, Goat;
        private readonly Material material;
        private readonly CreatureSimulationDefinition simulation;
        public Fixture(bool water = true)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
            simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>(); simulation.placementSpacingTiles = 0;
            HunterProfile = ScriptableObject.CreateInstance<CreatureDefinition>(); HunterProfile.name = "Native water hunter";
            HunterProfile.tags = CreatureTags.Predator; HunterProfile.preyTags = CreatureTags.Goat | CreatureTags.Visitor | CreatureTags.Staff;
            HunterProfile.diet = CreatureDiet.Carnivore; HunterProfile.initialHunger = 90; HunterProfile.hungerPerSecond = 0;
            HunterProfile.damage = 50; HunterProfile.threat = 100; HunterProfile.visionRange = 30; HunterProfile.fearRange = .1f;
            HunterProfile.initialEnergy = 100; HunterProfile.energyDrainPerSecond = 0; HunterProfile.minimumSpace = 0;
            HunterProfile.decisionInterval = .05f; HunterProfile.walkSpeed = HunterProfile.runSpeed = 8;
            HunterProfile.attackSeconds = HunterProfile.feedingSeconds = .3f; HunterProfile.Thirst.needsWater = false;
            HunterProfile.Aquatic.canSwim = HunterProfile.Aquatic.dragPreyToWater = true;
            HunterProfile.Aquatic.preferWater = false; HunterProfile.Aquatic.captureSeconds = .2f;
            HunterProfile.Aquatic.swimSpeed = 8; HunterProfile.Aquatic.drowningSeconds = .4f;
            HunterProfile.Aquatic.waterSearchRadius = 40; HunterProfile.Aquatic.dragSpeedMultiplier = .7f;
            HunterProfile.randomEscapeChance = HunterProfile.hungryEscapeChance = HunterProfile.agitatedEscapeChance = HunterProfile.crowdedEscapeChance = 0;
            GoatProfile = ScriptableObject.CreateInstance<CreatureDefinition>(); GoatProfile.name = "Native drag victim";
            GoatProfile.tags = CreatureTags.Goat; GoatProfile.diet = CreatureDiet.None; GoatProfile.needsFood = false;
            GoatProfile.maximumHealth = 30; GoatProfile.damage = GoatProfile.threat = GoatProfile.energyDrainPerSecond = 0;
            GoatProfile.fearRange = .1f; GoatProfile.Thirst.needsWater = false; GoatProfile.minimumSpace = 0;
            if (water) for (int x = 10; x < 14; x++) for (int z = 3; z < 9; z++) Grid.Habitat.TrySetWater(new Vector2Int(x, z), 1);
            var fences = new FenceLayout(Grid);
            World = new CreatureWorld(Grid, fences, new ParkBuildingNetwork(Grid, fences), null, material,
                new[] { HunterProfile, GoatProfile }, null, simulation);
            if (!World.TrySpawn(HunterProfile, new Vector2Int(7, 5)) || !World.TrySpawn(GoatProfile, new Vector2Int(8, 5)))
                throw new Exception("Aquatic fixture could not spawn its actors.");
            Hunter = World.Agents[0]; Goat = World.Agents[1]; Set(Goat, "DecisionTimer", 10000f);
        }
        public void Step(int count = 1) { for (int i = 0; i < count; i++) World.Tick(.05f); }
        public bool Until(Func<bool> done, int maximum = 800)
        { for (int i = 0; i < maximum; i++) { if (done()) return true; Step(); } return done(); }
        public void Dispose()
        { World.Dispose(); Object.Destroy(HunterProfile); Object.Destroy(GoatProfile); Object.Destroy(simulation); Object.Destroy(material); }
    }
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Aquatic hunt: " + reason); checks++; }
        Check(Application.isPlaying, "the fixture uses ordinary Play-mode lifetimes");
        var random = UnityEngine.Random.state;
        try
        {
            using (var f = new Fixture())
            {
                Check(f.Until(() => f.Hunter.Activity == CreatureActivity.DraggingPrey), "the hunter captures prey alive and starts returning to water");
                Check(f.Goat.Alive && f.Goat.Health.Current == f.Goat.Health.Maximum && Field<CreatureAgent>(f.Goat, "CapturedBy") == f.Hunter,
                    "capture immobilizes prey without an instant land kill");
                Vector3 start = f.Hunter.transform.position; f.Step();
                Check(Vector3.Distance(start, f.Hunter.transform.position) <= f.HunterProfile.walkSpeed * .05f + .01f,
                    "dragging uses visible bounded movement rather than a teleport");
                var saved = f.World.CaptureAquaticHunt(f.Hunter);
                Check(saved != null && saved.phase == CreatureActivity.DraggingPrey && CreatureWorld.ValidAquaticHuntSave(f.Grid, f.HunterProfile, saved),
                    "a mid-drag snapshot keeps its phase, water target and dry rescue bank");
                Check(f.Until(() => f.Hunter.Activity == CreatureActivity.DrowningPrey), "the hunter reaches real water before drowning begins");
                Check(f.Grid.Habitat.IsWater(f.Hunter.Cell) && f.Goat.Alive && f.Grid.Habitat.IsWater(f.Goat.Cell),
                    "both physical participants are in water while the prey is still alive");
                f.Step(2); Check(f.Goat.Alive, "the visible drowning struggle takes time");
                Check(f.Until(() => f.Hunter.Activity == CreatureActivity.Feeding) && !f.Goat.Alive,
                    "the prey dies only after the water struggle, then feeding starts");
                float hunger = f.Hunter.Vitals.Hunger;
                Check(f.Until(() => f.Hunter.Vitals.Hunger < hunger), "hunger is relieved after a completed meal");
            }
            using (var f = new Fixture(false))
            {
                f.Step(200);
                Check(f.Goat.Alive && f.Goat.Health.Current == f.Goat.Health.Maximum && f.Hunter.Vitals.Hunger == 90,
                    "without reachable water the predator cannot turn into an ordinary land killer");
            }
            using(var f=new Fixture())
            {
                f.GoatProfile.Shelter.mode=CreatureShelterMode.TreeShade;
                var tree=f.Goat.Cell+Vector2Int.right;
                Field<EnvironmentGrid>(f.World,"environment").AddTree(tree);
                Set(f.Goat,"SleepTree",tree);Set(f.Goat,"ShelterCell",tree);
                Set(f.Goat,"ShelterRetreat",true);Set(f.Goat,"WantsSleep",true);
                Set(f.Goat,"Activity",CreatureActivity.Sleeping);
                Check(f.Until(()=>f.Hunter.Activity==CreatureActivity.DraggingPrey),"a visible ground sleeper can be captured");
                Check(f.World.CaptureValidShelterHome(f.Goat)==null && !Field<bool>(f.Goat,"ShelterRetreat")
                    && f.Goat.Activity!=CreatureActivity.Sleeping,"capture releases sleeping and retreat claims before saving the live prey");
            }
            using (var f = new Fixture())
            {
                Check(f.Until(() => f.Hunter.Activity == CreatureActivity.DrowningPrey), "the interruption fixture reaches a real water capture");
                float hunger = f.Hunter.Vitals.Hunger;
                Check(f.World.Sedate(f.Hunter, 10), "a keeper can interrupt the water hunter with sedation");
                Check(f.Goat.Alive && Field<CreatureAgent>(f.Goat, "CapturedBy") == null && f.Grid.GroundWalkable(f.Goat.Cell)
                    && f.Hunter.Vitals.Hunger == hunger, "sedation releases living prey onto its dry bank without a meal reward");
            }
            using (var f = new Fixture())
            {
                Check(f.Until(() => f.Hunter.Activity == CreatureActivity.DrowningPrey), "the drain fixture reaches a real water capture");
                f.Grid.Habitat.Restore(null); f.Step();
                Check(f.Goat.Alive && Field<CreatureAgent>(f.Goat, "CapturedBy") == null && f.Hunter.Vitals.Hunger == 90,
                    "draining the pond safely interrupts predation without damage or a reward");
            }
            using(var f=new Fixture())
            {
                Check(f.Until(()=>f.Hunter.Activity==CreatureActivity.DraggingPrey),"the paused-construction fixture begins a live drag");
                var cell=f.Hunter.Cell;var fences=Field<FenceLayout>(f.World,"fences");
                fences.TryAdd(new FenceEdge(cell,true));fences.TryAdd(new FenceEdge(cell+Vector2Int.up,true));
                fences.TryAdd(new FenceEdge(cell,false));fences.TryAdd(new FenceEdge(cell+Vector2Int.right,false));
                f.World.PrepareAquaticSnapshot();
                Check(f.World.CaptureAquaticHunt(f.Hunter)==null && f.Goat.Alive && Field<CreatureAgent>(f.Goat,"CapturedBy")==null,
                    "saving before another simulation tick resolves a newly blocked hunt without breaking the load");
            }
        }
        finally { UnityEngine.Random.state = random; }
        return checks;
    }
}

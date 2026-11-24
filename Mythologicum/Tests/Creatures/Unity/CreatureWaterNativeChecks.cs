using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Prepared Play-mode helper; actual drinking, stock, fences and keeper travel.</summary>
public static class CreatureWaterNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Creature water: " + reason); checks++; }
        Check(Application.isPlaying, "the native water fixture uses ordinary Play-mode object lifetimes");
        var random = UnityEngine.Random.state;
        var owner = new GameObject("Creature water native fixture");
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var profile = ScriptableObject.CreateInstance<CreatureDefinition>();
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>();
        var waterSettings = ScriptableObject.CreateInstance<WaterTroughDefinition>();
        var staffSettings = ScriptableObject.CreateInstance<StaffDefinition>();
        CreatureWorld world = null; StaffWorld staff = null; WaterTroughConstruction troughs = null;
        try
        {
            var terrain = new GridHeightField(20, 14, 2);
            var grid = new SquareBuildGrid(new Vector3(48000, 0, 48000), 2, 20, 14, terrain);
            var fences = new FenceLayout(grid); var buildings = new ParkBuildingNetwork(grid, fences);
            var environment = new EnvironmentGrid(grid);
            var entrance = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(2, 2), 0));
            var keeperHome = buildings.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ZookeeperHut, width = 1, depth = 1, hireCost = 0, staffCapacity = 2 }, new Vector2Int(4, 2), 0));
            for (int x = 1; x < 18; x++) grid.TryOccupy(new Vector2Int(x, 1), BuildCellContent.Path);
            for (int x = 5; x < 12; x++) { fences.TryAdd(new FenceEdge(new Vector2Int(x, 4), true)); fences.TryAdd(new FenceEdge(new Vector2Int(x, 10), true)); }
            for (int y = 4; y < 10; y++) { fences.TryAdd(new FenceEdge(new Vector2Int(5, y), false)); fences.TryAdd(new FenceEdge(new Vector2Int(12, y), false)); }
            Check(fences.TrySetGate(new FenceEdge(new Vector2Int(5, 5), false)), "the enclosure has actual staff access");
            buildings.Refresh(); Check(entrance.IsOpen && keeperHome.IsOpen, "the keeper's home has connected public access");
            profile.name = "Water native species"; profile.displayName = "creature.name.unidentified";
            profile.diet = CreatureDiet.None; profile.needsFood = false; profile.threat = profile.damage = 0;
            profile.energyDrainPerSecond = 0; profile.initialEnergy = 100; profile.minimumSpace = 0;
            profile.fearRange = 0; profile.Thirst.initialThirst = 80; profile.Thirst.thirstPerSecond = 0;
            profile.Thirst.drinkThreshold = 40; profile.Thirst.criticalThirst = 75; profile.Thirst.drinkReliefPerSecond = 20;
            profile.Thirst.drinkSeconds = 2; profile.walkSpeed = profile.runSpeed = 8; profile.decisionInterval = .1f;
            profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
            simulation.placementSpacingTiles = 0;
            world = new CreatureWorld(grid, fences, buildings, null, material, new[] { profile }, environment, simulation);
            waterSettings.capacity = 20; waterSettings.refillThreshold = 5; waterSettings.refillSeconds = .2f;
            waterSettings.refillCost = 7; waterSettings.waterUnitsPerThirstPoint = .1f;
            troughs = new WaterTroughConstruction(grid, environment, owner.transform, material, waterSettings, buildings);
            world.ConfigureDrinking(troughs, grid.Habitat.IsWater);
            var troughCell = new Vector2Int(8, 6);
            Check(troughs.TryPlace(troughCell) && !troughs.CanPlace(troughCell), "a trough is a distinct enclosure object with collision and duplicate protection");
            var trough = troughs.RecordAt(troughCell); trough.Restore(2, 0);
            Check(!world.IsClear(troughCell) && !world.CanSpawn(troughCell), "animals cannot stand inside the solid trough itself");
            Check(world.TrySpawn(profile, new Vector2Int(6, 6)), "a living animal begins beside the trough");
            var animal = world.Agents.Last();
            void Step(bool care = false, int count = 1)
            { for (int i = 0; i < count; i++) { world.Tick(.05f); if (care) staff.Tick(.05f); troughs.Refresh(); } }
            bool Until(Func<bool> done, bool care = false, int limit = 600)
            { for (int i = 0; i < limit; i++) { if (done()) return true; Step(care); } return done(); }
            Check(Until(() => trough.Stock <= .0001f), "a creature approaches and consumes finite trough water");
            Check(animal.Vitals.Thirst < 80 && animal.Vitals.Thirst >= 59.9f,
                "the water charged matches actual thirst relief when the trough runs dry");
            Check(ConstructionRouteGraph.CellDistance(animal.Cell, troughCell) == 1,
                "drinking occurs from a reachable bank beside the trough");
            staffSettings.walkSpeed = staffSettings.runSpeed = 8;
            staffSettings.decisionInterval = .1f; staffSettings.threatFleeRange = 0;
            var economy = new ParkEconomy(1000);
            staff = new StaffWorld(grid, fences, buildings, world, economy, null, new[] { profile }, staffSettings);
            staff.ConfigureWaterTroughs(troughs);
            Check(staff.TryHire(keeperHome), "a paid keeper can be hired for real refill work");
            Check(Until(() => trough.Expenses == waterSettings.refillCost && trough.Stock > 0, true),
                "the keeper travels through staff access and refills the empty trough");
            Check(economy.Expenses == waterSettings.refillCost && trough.Expenses == waterSettings.refillCost,
                "the refill is charged once to park supplies and the trough's account");
            var worker = staff.Staff.Single();
            Check(worker.Actor.Cell.x >= 5 && worker.Actor.Cell.x < 12 && worker.Actor.Cell.y >= 4 && worker.Actor.Cell.y < 10,
                "refill work requires the keeper to physically enter the enclosure");
            Step(true, 80);
            Check(trough.Expenses == waterSettings.refillCost && animal.Vitals.Thirst <= 40,
                "the completed keeper job releases its claim without duplicate deliveries");

            var pond = new Vector2Int(15, 7);
            var savedHabitat = grid.Habitat.Capture(); savedHabitat.water.Add(new SavedWaterCell { cell = pond, level = .6f });
            grid.Habitat.Restore(savedHabitat);
            Check(grid.Habitat.IsWater(pond) && !grid.GroundWalkable(pond), "a natural pond has water while remaining blocked to ordinary land travel");
            var boundary = new[] { new FenceEdge(pond, true), new FenceEdge(pond + Vector2Int.up, true),
                new FenceEdge(pond, false), new FenceEdge(pond + Vector2Int.right, false) };
            foreach (var edge in boundary) fences.TryAdd(edge);
            Check(world.TrySpawn(profile, pond + Vector2Int.left * 2), "another thirsty creature can live outside all owned enclosures");
            var pondAnimal = world.Agents.Last(); Step(false, 80);
            Check(pondAnimal.Vitals.Thirst == 80, "a full fence prevents drinking through an inaccessible pond boundary");
            Check(fences.TryRemove(new FenceEdge(pond, false)), "opening one real pond bank changes access");
            Check(Until(() => pondAnimal.Vitals.Thirst < 60), "the land creature discovers and drinks from reachable natural water");
            Check(pondAnimal.Cell != pond && ConstructionRouteGraph.CellDistance(pondAnimal.Cell, pond) == 1,
                "natural drinking stays on the land bank and does not grant swimming");

            var captured = troughs.Capture().Single();
            var copy = JsonUtility.FromJson<WaterTroughSave>(JsonUtility.ToJson(captured)); copy.cell = new Vector2Int(10, 8);
            Check(troughs.Restore(copy), "a saved trough restores its own geometry and stock");
            Check(Mathf.Approximately(troughs.RecordAt(copy.cell).Stock, captured.stock)
                && troughs.RecordAt(copy.cell).Expenses == captured.expenses, "trough stock and expenses survive a real Unity JSON round trip");
            var beforeWaterLevel = owner.GetComponentsInChildren<Transform>(true).Where(item => item.name == "Water level").First();
            trough.Restore(0, trough.Expenses); troughs.Refresh();
            Check(!beforeWaterLevel.gameObject.activeSelf, "an empty trough visibly loses its water surface");
            Check(troughs.TryRemove(copy.cell) && troughs.RecordAt(copy.cell) == null, "ordinary demolition removes a trough and its refill target");
        }
        finally
        {
            staff?.Dispose(); troughs?.Dispose(); world?.Dispose(); owner.SetActive(false); Object.Destroy(owner);
            Object.Destroy(profile); Object.Destroy(simulation); Object.Destroy(waterSettings); Object.Destroy(staffSettings); Object.Destroy(material);
            UnityEngine.Random.state = random;
        }
        checks += CheckShelterDrinking(CreatureShelterMode.Burrow, 0);
        checks += CheckShelterDrinking(CreatureShelterMode.TreeCanopy, 1);
        checks += CheckShelterDrinking(CreatureShelterMode.Gravestone, 2);
        return checks;
    }

    private static int CheckShelterDrinking(CreatureShelterMode mode, int index)
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Sheltered drinking (" + mode + "): " + reason); checks++; }
        var owner = new GameObject("Sheltered drinking native fixture " + mode);
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader"));
        var profile = ScriptableObject.CreateInstance<CreatureDefinition>();
        var simulation = ScriptableObject.CreateInstance<CreatureSimulationDefinition>();
        var waterSettings = ScriptableObject.CreateInstance<WaterTroughDefinition>();
        var shelterSettings = ScriptableObject.CreateInstance<ShelterDefinition>();
        CreatureWorld world = null; WaterTroughConstruction troughs = null; ShelterConstruction shelters = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(56000 + index * 100, 0, 56000), 2, 14, 10);
            var fences = new FenceLayout(grid); var buildings = new ParkBuildingNetwork(grid, fences);
            var environment = new EnvironmentGrid(grid); var home = new Vector2Int(3, 3);
            profile.name = "Sheltered drinking " + mode; profile.needsFood = false; profile.diet = CreatureDiet.None;
            profile.threat = profile.damage = profile.fearRange = 0;
            profile.minimumSpace = profile.minimumOpenGroundFraction = profile.requiredTrees = profile.requiredGravestones = 0;
            profile.initialEnergy = profile.energyDrainPerSecond = profile.sleepRecoveryPerSecond = 0;
            profile.walkSpeed = profile.runSpeed = 8; profile.decisionInterval = .05f;
            profile.treeClimbSeconds = profile.burrowSeconds = .6f;
            profile.randomEscapeChance = profile.hungryEscapeChance = profile.agitatedEscapeChance = profile.crowdedEscapeChance = 0;
            profile.shelter = new CreatureShelterSettings { mode = mode, requiredForSleep = true, searchRadius = 24 };
            profile.Thirst.initialThirst = profile.Thirst.thirstPerSecond = 0;
            profile.Thirst.drinkThreshold = 40; profile.Thirst.criticalThirst = 75;
            profile.Thirst.satisfiedThirst = 5; profile.Thirst.drinkReliefPerSecond = 20; profile.Thirst.drinkSeconds = 8;
            simulation.placementSpacingTiles = 0;
            world = new CreatureWorld(grid, fences, buildings, null, material, new[] { profile }, environment, simulation);
            shelters = new ShelterConstruction(grid, environment, owner.transform, material, shelterSettings);
            world.ConfigureShelters(shelters);
            waterSettings.capacity = 20; waterSettings.waterUnitsPerThirstPoint = .1f;
            troughs = new WaterTroughConstruction(grid, environment, owner.transform, material, waterSettings, buildings);
            world.ConfigureDrinking(troughs, null);
            Check(world.TrySpawn(profile, home), "a tired creature spawns on reachable ground");
            var actor = world.Agents.Single();
            if (mode == CreatureShelterMode.TreeCanopy) environment.AddTree(home);
            if (mode == CreatureShelterMode.Gravestone) environment.AddGravestone(home);
            float Elevation() => (float)typeof(CreatureAgent).GetField("Elevation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(actor);
            void Step(int count = 1) { for (int i = 0; i < count; i++) world.Tick(.05f); }
            bool Until(Func<bool> done, int limit = 400)
            { for (int i = 0; i < limit; i++) { if (done()) return true; Step(); } return done(); }
            Check(Until(() => actor.Activity == CreatureActivity.Sleeping), "it physically enters its required shelter before sleeping");
            Check(Mathf.Abs(Elevation()) > .1f && (mode != CreatureShelterMode.Burrow || actor.InBurrow),
                "sleep uses a real elevated or underground pose");
            actor.Vitals.Restore(actor.Vitals.Hunger, 0, 90);
            Step(5);
            Check(actor.Activity == CreatureActivity.Sleeping && actor.Cell == home && Mathf.Abs(Elevation()) > .1f,
                "critical thirst without reachable water leaves the sleeper in its shelter");
            var troughCell = new Vector2Int(7, 3);
            Check(troughs.TryPlace(troughCell), "a stocked reachable source can be added while the creature sleeps");
            var trough = troughs.RecordAt(troughCell); float beforeStock = trough.Stock;
            float sleepingElevation = Elevation(); var sleepingPosition = actor.transform.position;
            Step();
            var exit = mode == CreatureShelterMode.Burrow ? CreatureActivity.LeavingBurrow
                : mode == CreatureShelterMode.Gravestone ? CreatureActivity.LeavingGrave : CreatureActivity.DescendingTree;
            Check(actor.Activity == exit && actor.Cell == home && actor.transform.position == sleepingPosition,
                "critical thirst starts the matching exit animation without teleporting");
            Step();
            Check(actor.Activity == exit && Mathf.Abs(Elevation()) > .01f && Mathf.Abs(Elevation()) < Mathf.Abs(sleepingElevation)
                && actor.Cell == home && Mathf.Approximately(trough.Stock, beforeStock),
                "emerging takes nonzero time and cannot consume water from the shelter");
            Check(Until(() => actor.Activity == CreatureActivity.SeekingWater || actor.Activity == CreatureActivity.Drinking),
                "the completed exit resumes the selected water journey");
            Check(!actor.InBurrow && Mathf.Abs(Elevation()) < .01f && !world.IsHiddenInShelter(actor),
                "a walking drinker has no stale hiding state or elevation");
            Check(world.CaptureValidShelterHome(actor) == home, "the valid remembered shelter home survives emerging");
            Check(Until(() => actor.Activity == CreatureActivity.Drinking && actor.Vitals.Thirst < profile.Thirst.criticalThirst
                && actor.Vitals.Thirst > profile.Thirst.satisfiedThirst), "the low-energy creature starts a real finite drink");
            Check(Until(() => actor.Vitals.Thirst <= profile.Thirst.satisfiedThirst),
                "wanting sleep cannot interrupt the drink as thirst falls below the critical threshold");
            Check(actor.Energy.Energy <= profile.sleepThreshold && beforeStock > trough.Stock
                && Mathf.Abs((beforeStock - trough.Stock) - (90 - actor.Vitals.Thirst) * waterSettings.waterUnitsPerThirstPoint) < .001f,
                "the exhausted drinker consumes exactly the finite stock matching its thirst relief");
            Check(!actor.InBurrow && Mathf.Abs(Elevation()) < .01f && !world.IsHiddenInShelter(actor),
                "completed drinking remains on the ground before sleep resumes");
            Check(Until(() => actor.Activity == CreatureActivity.Sleeping && actor.Cell == home),
                "after satisfying thirst the creature returns to sleep at the same home");
            if (mode == CreatureShelterMode.Burrow)
                Check(shelters.Registry.Contains(home, ConstructedShelterKind.Burrow) && shelters.Capture().Count == 1,
                    "drinking preserves and reuses the persistent burrow instead of digging a replacement");
        }
        finally
        {
            world?.Dispose(); troughs?.Dispose(); shelters?.Dispose(); owner.SetActive(false); Object.Destroy(owner);
            Object.Destroy(profile); Object.Destroy(simulation); Object.Destroy(waterSettings); Object.Destroy(shelterSettings); Object.Destroy(material);
        }
        return checks;
    }
}

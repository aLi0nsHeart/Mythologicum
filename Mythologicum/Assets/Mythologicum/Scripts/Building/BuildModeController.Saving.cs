using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        public ParkSaveData CapturePark()
        {
            var data = new ParkSaveData { width = grid.Width, depth = grid.Depth, cellSize = grid.CellSize,
                balance = economy.Balance, income = economy.Income, expenses = economy.Expenses,
                unlimited = economy.UnlimitedFunds, savedAt = DateTime.UtcNow.ToString("O"),
                departedVisitors = visitorWorld.DepartedCount, lostVisitors = visitorWorld.LostCount };
            data.terrain=grid.Terrain?.Capture();
            data.hasHabitat = true; data.habitat = grid.Habitat.Capture();
            data.waterTroughs = waterTroughs?.Capture(); data.shelters = shelters?.Capture();
            data.treeVisuals=trees.CaptureVisuals();
            data.generation=mapOptions?.Clone();data.rocks=rocks.Capture();
            data.finance=economy.CaptureFinance();data.reputation=visitorWorld.Reputation.Capture();
            data.rejectedAdmissions=visitorWorld.RejectedAdmissions;data.expensiveServiceRefusals=visitorWorld.ExpensiveServiceRefusals;
            var camera = GetComponent<IsometricCameraController>();
            if (camera != null) { data.focus = camera.SavedFocus; data.yaw = camera.SavedYaw; data.zoom = camera.SavedZoom; }
            for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
            {
                var cell = new Vector2Int(x,y);
                if (grid.ContentAt(cell) == BuildCellContent.Path) data.paths.Add(cell);
                if (Environment.TreesAt(cell) > 0) data.trees.Add(cell);
                if (Environment.GravestonesAt(cell) > 0) data.graves.Add(cell);
                if (Environment.ScarecrowsAt(cell) > 0) data.scarecrows.Add(cell);
            }
            var records = buildingConstruction.Network.Records;
            data.progression = Progression.Capture(records);
            foreach (var record in records)
            {
                var f = record.Footprint; var d = f.Definition;
                var ride = visitorWorld.CaptureRide(record);
                data.buildings.Add(new SavedBuilding { kind = d.kind, cell = f.Anchor, rotation = f.Rotation,
                    fenceMounted = f.IsFenceMounted,
                    ride = ride, hasRide = ride != null,
                    width = d.width, depth = d.depth, height = d.height, door = d.entranceColumn,
                    price = record.Price, revenue = record.Revenue, expenses = record.Expenses, stock = record.FeedStock,
                    research = staffWorld.ResearchTarget(record)?.name });
            }
            foreach (var edge in fences.Layout.Edges) data.fences.Add(CaptureFence(edge));
            foreach (var actor in creatureWorld.Agents)
            {
                if (!actor.Alive || actor.ExternallyControlled) continue;
                var saved = CaptureActor(actor); saved.building = IndexOf(records, actor.SourceDispenser);
                var home = creatureWorld.CaptureValidShelterHome(actor);
                saved.hasShelterHome = home.HasValue; saved.shelterHome = home ?? default;
                saved.shelterSleeping = home.HasValue && actor.Activity == CreatureActivity.Sleeping && !actor.IsSedated && actor.CarriedBy == null;
                saved.shelterRetreat = home.HasValue && actor.ShelterRetreat;
                saved.retire = actor.RetirementRequested; saved.sedate = actor.SedationRequested;
                data.creatures.Add(saved);
            }
            foreach (var worker in staffWorld.Staff)
                if (worker.Actor.Alive) { var saved = CaptureActor(worker.Actor); saved.building = IndexOf(records, worker.Home); data.staff.Add(saved); }
            foreach (var guest in visitorWorld.Visitors)
                if (guest.Actor.Alive)
                {
                    var saved = CaptureActor(guest.Actor); saved.age = guest.Mood.Age; saved.satisfaction = guest.Mood.Satisfaction;
                    saved.patience = guest.Mood.VisitProgress; saved.leaving = guest.LeavingPark || guest.Mood.ShouldLeave;
                    saved.visitorType = guest.Type;
                    saved.observedSpecies = new List<string>(guest.ObservedSpecies);
                    saved.observedOrigins = new List<string>(guest.ObservedOrigins);
                    saved.observedSpecies.Sort(StringComparer.Ordinal); saved.observedOrigins.Sort(StringComparer.Ordinal);
                    saved.attraction = visitorWorld.CaptureAttraction(guest, records);
                    data.visitors.Add(saved);
                }
            foreach (var species in creatureCatalog) data.research.Add(new SavedResearch { species = species.name, progress = staffWorld.Research.Progress(species) });
            return data;
        }
        private static int IndexOf(IReadOnlyList<ParkBuildingRecord> records, ParkBuildingRecord record)
        { for (int i = 0; i < records.Count; i++) if (records[i] == record) return i; return -1; }
        private SavedFence CaptureFence(FenceEdge edge)
        {
            var window = fences.Layout.WindowAt(edge);
            return new SavedFence { cell = edge.Start, alongX = edge.AlongX, kind = fences.Layout.KindAt(edge),
                gate = fences.Layout.IsGate(edge), health = fences.Layout.HealthAt(edge), window = window != null,
                windowPrice = window?.Price ?? 0, windowRevenue = window?.Revenue ?? 0, windowExpenses = window?.Expenses ?? 0 };
        }
        private void RestoreFenceWindow(FenceEdge edge, SavedFence saved, float minimumExpenses = 0)
        {
            if (!saved.window) return;
            if (!fences.Layout.TrySetWindow(edge, saved.windowPrice)) throw new InvalidDataException(Loc.Text("attraction.window.invalid_save"));
            fences.Layout.WindowAt(edge).RestoreAccounts(saved.windowPrice, saved.windowRevenue, Mathf.Max(saved.windowExpenses, minimumExpenses));
        }
        private SavedActor CaptureActor(CreatureAgent actor) => new SavedActor { species = actor.Definition.name,
            cell = actor.CarriedBy != null ? grid.WorldToCell(actor.transform.position) : actor.Cell,
            hunger = actor.Vitals.Hunger, thirst = actor.Vitals.Thirst, fear = actor.Vitals.Fear, energy = actor.Energy.Energy, health = actor.Health.Current,
            happiness = actor.Wellbeing.Happiness, agitation = actor.Wellbeing.Agitation,
            sedation = actor.SedationRemaining, explored = actor.Exploration.AwakeSeconds,
            // In-flight attempts are consumed on load, so saving cannot grant another
            // immediate fence strike. Ordinary cooldowns keep their remaining duration.
            escapeCooldown = actor.EscapeIntent || actor.Activity == CreatureActivity.AttackingFence
                || actor.Activity == CreatureActivity.ClimbingFence || actor.Activity == CreatureActivity.PhasingFence
                || actor.Activity == CreatureActivity.FailedClimb || actor.Activity == CreatureActivity.FailedPhase
                || actor.Activity == CreatureActivity.BurrowingFence || actor.Activity == CreatureActivity.FailedBurrow
                ? Mathf.Max(actor.Definition.escapeRetrySeconds, actor.EscapeAfter - creatureWorld.SimulationTime)
                : Mathf.Max(0f, actor.EscapeAfter - creatureWorld.SimulationTime),
            fenceRetryCooldown = Mathf.Max(0f, actor.AttemptAfter - creatureWorld.SimulationTime),
            scareCooldown = Mathf.Max(actor.Activity == CreatureActivity.Scaring ? actor.Definition.scareCooldownSeconds : 0f,
                actor.ScareAfter - creatureWorld.SimulationTime) };
        private void RestoreActor(CreatureAgent actor, SavedActor saved)
        {
            // Traversals resume on their saved logical safe-side cell. Persistent
            // shelter homes and settled sleep poses are restored after all features.
            actor.Cell = saved.cell;
            actor.transform.position = grid.ActorPosition(saved.cell,actor.Definition.bodyScale.y);
            actor.Vitals.Restore(actor.Definition.needsFood ? saved.hunger : actor.Definition.StartingHunger, saved.fear,
                actor.Definition.Thirst.needsWater ? saved.thirst : 0);
            actor.Energy = new CreatureEnergy(saved.energy);
            actor.Health.Damage(Mathf.Max(0f, actor.Health.Maximum - Mathf.Max(1f, saved.health)));
            actor.Wellbeing.Restore(saved.happiness, saved.agitation); actor.Exploration.Tick(saved.explored);
            if (!actor.ExternallyControlled && saved.sedation > 0f) creatureWorld.Sedate(actor, saved.sedation);
            actor.EscapeAfter = creatureWorld.SimulationTime + Mathf.Max(0f, saved.escapeCooldown);
            actor.AttemptAfter = creatureWorld.SimulationTime + Mathf.Max(0f, saved.fenceRetryCooldown);
            actor.ScareAfter = creatureWorld.SimulationTime + Mathf.Max(0f, saved.scareCooldown);
        }
        public void ValidateSave(ParkSaveData data)
        {
            ParkEconomy.ValidateSnapshot(data.finance);ParkReputation.ValidateSnapshot(data.reputation);
            if(data.rejectedAdmissions<0||data.expensiveServiceRefusals<0)throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_visitor_pricing_feedback"));
            if(data.width<1||data.depth<1||data.width>128||data.depth>128||!Finite(data.cellSize)||data.cellSize<.25f||data.cellSize>16f)
                throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.unsupported_saved_map_dimensions"));
            if(data.generation!=null&&(!data.generation.Validate(out _)||data.generation.width!=data.width||data.generation.depth!=data.depth))
                throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_generation_settings"));
            GridHeightField checkTerrain=null;
            if(data.version>=3){checkTerrain=new GridHeightField(data.width,data.depth,data.cellSize);checkTerrain.Restore(data.terrain);}
            var checkGrid = new SquareBuildGrid(grid.Origin, data.cellSize, data.width, data.depth,checkTerrain);
            if (data.hasHabitat && data.habitat == null) throw new InvalidDataException(Loc.Text("habitat.terrain.invalid_save"));
            checkGrid.Habitat.Restore(data.hasHabitat ? data.habitat : null);
            var checkFences = new FenceLayout(checkGrid, fences.Layout.Rules);
            var network = new ParkBuildingNetwork(checkGrid, checkFences);
            network.RestoringSave = true;
            foreach (var saved in data.buildings)
            {
                var definition = SavedDefinition(saved,data.width,data.depth);
                if (saved.hasRide && (!VisitorWorld.IsBatchRide(definition) || saved.ride == null))
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                if (saved.hasRide) AttractionRideCycle.ValidateSave(saved.ride, definition.serviceSeconds, definition.attraction.boardingWindowSeconds);
                var footprint=new BuildingFootprint(definition,saved.cell,saved.rotation,saved.fenceMounted);
                if (checkGrid.Habitat.IsWater(footprint.AccessCell) || footprint.IsFenceMounted && checkGrid.Habitat.IsWater(footprint.RearAccessCell))
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_has_an_invalid_footprint"));
                var record=network.TryPlace(footprint);
                if (record == null)
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_has_an_invalid_footprint"));
                if(checkTerrain!=null&&footprint.RequiresFoundation&&!checkTerrain.RegisterFoundation(record,footprint))throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_foundation_is_not_flat"));
            }
            foreach (var cell in data.paths) if (!checkGrid.GroundWalkable(cell,true)||!checkGrid.TryOccupy(cell, BuildCellContent.Path)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_path"));
            foreach (var fence in data.fences)
            {
                if (!Enum.IsDefined(typeof(FenceKind), fence.kind) || fence.health <= 0f || !Finite(fence.health)
                    || !checkFences.TryAdd(new FenceEdge(fence.cell, fence.alongX), fence.kind))
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_fence"));
                var edge = new FenceEdge(fence.cell, fence.alongX);
                if (fence.gate && !checkFences.TrySetGate(edge)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_fence"));
                if (fence.window && (fence.gate || !Finite(fence.windowPrice) || fence.windowPrice < 0
                    || !Finite(fence.windowRevenue) || fence.windowRevenue < 0 || !Finite(fence.windowExpenses) || fence.windowExpenses < 0
                    || !checkFences.TrySetWindow(edge, fence.windowPrice))) throw new InvalidDataException(Loc.Text("attraction.window.invalid_save"));
            }
            foreach (var record in network.Records)
                if (record.Footprint.IsFenceMounted)
                {
                    foreach (var edge in record.Footprint.AttachmentEdges())
                        if (checkFences.IsGate(edge) || checkFences.HasWindow(edge))
                            throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_has_an_invalid_footprint"));
                    foreach (var cell in record.Footprint.Cells())
                        if (record.Footprint.Contains(cell + Vector2Int.right) && checkFences.HasFence(new FenceEdge(cell + Vector2Int.right, false))
                            || record.Footprint.Contains(cell + Vector2Int.up) && checkFences.HasFence(new FenceEdge(cell + Vector2Int.up, true)))
                            throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_has_an_invalid_footprint"));
                }
            var enrichment = new HashSet<Vector2Int>();
            foreach (var cell in data.trees)
                if (!checkGrid.IsAvailable(cell) || !enrichment.Add(cell)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_tree"));
            foreach (var cell in data.graves)
                if (!checkGrid.IsAvailable(cell) || !enrichment.Add(cell)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_gravestone"));
            if (data.scarecrows != null) foreach (var cell in data.scarecrows)
            {
                if (!checkGrid.IsAvailable(cell) || !enrichment.Add(cell)) throw new InvalidDataException(Loc.Text("park.save.invalid_scarecrow"));
                foreach (var record in network.Records)
                    if (record.Footprint.AccessCell == cell) throw new InvalidDataException(Loc.Text("park.save.scarecrow_blocks_entrance"));
            }
            var rockCells=new HashSet<Vector2Int>();
            if(data.rocks!=null)foreach(var cell in data.rocks)
            {
                if(!checkGrid.IsAvailable(cell)||!rockCells.Add(cell)||!enrichment.Add(cell))throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_rock"));
                foreach(var record in network.Records)if(record.Footprint.AccessCell==cell)throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.rock_blocks_a_saved_entrance"));
            }
            if(data.treeVisuals!=null)
            {
                var ids=new HashSet<Vector2Int>();var treeCells=new HashSet<Vector2Int>(data.trees);
                foreach(var item in data.treeVisuals)if(item==null||!treeCells.Contains(item.cell)||!ids.Add(item.cell)||!Finite(item.height)||item.height<.1f||item.height>100||!Finite(item.yaw))
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_tree_appearance"));
            }
            var troughCells = new HashSet<Vector2Int>();
            if (data.waterTroughs != null) foreach (var trough in data.waterTroughs)
            {
                if (trough == null || !checkGrid.IsAvailable(trough.cell) || !checkGrid.GroundWalkable(trough.cell)
                    || !enrichment.Add(trough.cell) || !troughCells.Add(trough.cell)
                    || !Finite(trough.stock) || trough.stock < 0 || !Finite(trough.expenses) || trough.expenses < 0
                    || SavedAccessReserved(network.Records, trough.cell))
                    throw new InvalidDataException(Loc.Text("habitat.save.invalid_water_trough"));
            }
            CreatureShelterRegistry.ValidateSaved(data.shelters, checkGrid);
            var shelterKinds = new Dictionary<Vector2Int, ConstructedShelterKind>();
            if (data.shelters != null) foreach (var shelter in data.shelters)
            {
                if (!enrichment.Add(shelter.cell) || SavedAccessReserved(network.Records, shelter.cell))
                    throw new InvalidDataException(Loc.Text("habitat.shelter.invalid_save"));
                shelterKinds.Add(shelter.cell, shelter.kind);
            }
            foreach (var saved in data.creatures)
            {
                var species = saved == null ? null : Species(saved.species);
                if (species == null || !ValidActor(saved, checkGrid) || rockCells.Contains(saved.cell) || troughCells.Contains(saved.cell))
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_creature_is_unavailable_or_invalid"));
                ValidateShelterHome(saved, species, checkGrid, shelterKinds, data.trees, data.graves);
            }
            var hired = new Dictionary<int,int>();
            foreach (var saved in data.staff)
            {
                if (!ValidActor(saved, checkGrid) || saved.building < 0 || saved.building >= data.buildings.Count
                    ||rockCells.Contains(saved.cell)||troughCells.Contains(saved.cell)|| saved.hasShelterHome || saved.shelterSleeping || saved.shelterRetreat
                    || !SavedDefinition(data.buildings[saved.building],data.width,data.depth).IsEmployeeBuilding) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_staff_home_or_employee"));
                hired.TryGetValue(saved.building, out int count); hired[saved.building] = count + 1;
                // A lowered hiring limit must not discard employees already hired in an older balance.
                // This is a corrupt-file guard, not the live hiring limit.
                if (count >= 1000000) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.too_many_saved_employees"));
            }
            var passengerSeats = new HashSet<string>();
            foreach (var saved in data.visitors)
            {
                if (saved == null) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_visitor"));
                VisitorWorld.ValidateAttractionSave(saved.attraction, network.Records, checkGrid, checkFences);
                bool insideRoom = (saved.attraction?.insideViewingRoom == true || saved.attraction?.insideInterior == true) && saved.attraction.building >= 0;
                if (insideRoom && saved.cell != saved.attraction.interior
                    || !ValidActor(saved, checkGrid, insideRoom) || rockCells.Contains(saved.cell) || troughCells.Contains(saved.cell)
                    || saved.hasShelterHome || saved.shelterSleeping || saved.shelterRetreat
                    || !Enum.IsDefined(typeof(VisitorType), saved.visitorType)
                    || !ValidInterestHistory(saved.observedSpecies) || !ValidInterestHistory(saved.observedOrigins))
                    throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_visitor"));
                if (saved.attraction?.hasInterior == true && saved.attraction.building >= 0
                    && !passengerSeats.Add(saved.attraction.building + ":" + saved.attraction.seat))
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
            }
            foreach (var saved in data.research) if (Species(saved.species) == null || !Finite(saved.progress)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_species_research"));
            if (!Finite(data.balance) || !Finite(data.income) || !Finite(data.expenses) || !Finite(data.zoom) || !Finite(data.yaw)
                || !Finite(data.focus.x) || !Finite(data.focus.y) || !Finite(data.focus.z)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_saved_finances_or_camera"));
            if(data.version>=2)Progression.Validate(data.progression,network.Records);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool SavedAccessReserved(IReadOnlyList<ParkBuildingRecord> records, Vector2Int cell)
        {
            foreach (var record in records)
                if (record.Footprint.AccessCell == cell || record.Footprint.IsFenceMounted && record.Footprint.RearAccessCell == cell) return true;
            return false;
        }
        private static void ValidateShelterHome(SavedActor actor, CreatureDefinition species, SquareBuildGrid checkGrid,
            Dictionary<Vector2Int, ConstructedShelterKind> constructed, List<Vector2Int> trees, List<Vector2Int> graves)
        {
            if (!actor.hasShelterHome)
            {
                if (actor.shelterSleeping || actor.shelterRetreat) throw new InvalidDataException(Loc.Text("habitat.save.invalid_shelter_home"));
                return;
            }
            var home = actor.shelterHome; var mode = species.Shelter.mode;
            bool valid = checkGrid.Contains(home) && !checkGrid.Habitat.IsWater(home)
                && (mode == CreatureShelterMode.TreeCanopy || mode == CreatureShelterMode.TreeShade ? trees.Contains(home)
                    : mode == CreatureShelterMode.Gravestone ? graves.Contains(home)
                    : mode == CreatureShelterMode.Cave ? constructed.TryGetValue(home, out var cave) && cave == ConstructedShelterKind.Cave
                    : mode == CreatureShelterMode.Burrow && constructed.TryGetValue(home, out var burrow) && burrow == ConstructedShelterKind.Burrow
                        && CreatureBurrowing.CanDigGround(checkGrid, home));
            if (actor.shelterSleeping)
            {
                bool legacyShade = mode == CreatureShelterMode.TreeShade && (species.shelter == null || species.shelter.mode == CreatureShelterMode.Legacy);
                valid &= actor.sedation <= 0 && (mode == CreatureShelterMode.TreeShade
                    ? ConstructionRouteGraph.CellDistance(actor.cell, home) == 1 || legacyShade && actor.cell == home : actor.cell == home);
            }
            if (!valid) throw new InvalidDataException(Loc.Text("habitat.save.invalid_shelter_home"));
        }
        private static bool ValidInterestHistory(List<string> values)
        {
            if (values == null) return true; // Older saves have no observation history.
            if (values.Count > 10000) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || value.Length > 256 || !unique.Add(value)) return false;
                foreach (char c in value) if (char.IsControl(c)) return false;
            }
            return true;
        }
        private static bool ValidActor(SavedActor saved, SquareBuildGrid checkGrid, bool allowBuilding = false) => saved != null && checkGrid.Contains(saved.cell)
            && !checkGrid.Habitat.IsWater(saved.cell) && Finite(saved.thirst) && saved.thirst >= 0f && saved.thirst <= 100f
            && (allowBuilding || checkGrid.ContentAt(saved.cell) != BuildCellContent.Building) && Finite(saved.health) && saved.health > 0f
            && Finite(saved.hunger) && Finite(saved.fear) && Finite(saved.energy) && Finite(saved.sedation)
            && Finite(saved.happiness) && Finite(saved.agitation) && Finite(saved.age) && Finite(saved.satisfaction) && Finite(saved.patience) && Finite(saved.explored)
            && Finite(saved.escapeCooldown) && saved.escapeCooldown >= 0f
            && Finite(saved.fenceRetryCooldown) && saved.fenceRetryCooldown >= 0f
            && Finite(saved.scareCooldown) && saved.scareCooldown >= 0f;
        private CreatureDefinition Species(string name) => Array.Find(creatureCatalog, d => d.name == name);
        private BuildingDefinition SavedDefinition(SavedBuilding saved,int mapWidth=-1,int mapDepth=-1)
        {
            var source = Array.Find(buildingCatalog, d => d.kind == saved.kind);
            if (source == null || saved.width < 1 || saved.depth < 1 || saved.width > (mapWidth<0?grid.Width:mapWidth) || saved.depth > (mapDepth<0?grid.Depth:mapDepth)
                || saved.fenceMounted && saved.kind != BuildingKind.PettingZoo && saved.kind != BuildingKind.ViewingWindow
                || !Finite(saved.height) || saved.height <= 0f || !Finite(saved.price) || !Finite(saved.revenue) || !Finite(saved.expenses))
                throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.a_saved_building_type_is_unavailable_or_invalid"));
            // Clone settings so loading a footprint never changes the player's catalogue.
            var result = JsonUtility.FromJson<BuildingDefinition>(JsonUtility.ToJson(source));
            result.visualPrefab = source.visualPrefab;
            result.width = saved.width; result.depth = saved.depth; result.height = saved.height; result.entranceColumn = saved.door;
            return result;
        }
        private void RestorePark(ParkSaveData data)
        {
            try
            {
                ValidateSave(data);
                creatureWorld.RestoringSave = true;
                buildingConstruction.Network.RestoringSave = true;
                if(grid.Terrain!=null)
                {
                    if(data.version>=3)grid.Terrain.Restore(data.terrain);
                    else grid.Terrain.ImportHeights(new float[(grid.Width+1)*(grid.Depth+1)]);
                }
                grid.Habitat.Restore(data.hasHabitat ? data.habitat : null);
                SelectTool(Tool.None);
                var restored = new List<ParkBuildingRecord>();
                foreach (var saved in data.buildings)
                {
                    if (!buildingConstruction.TryPlace(new BuildingFootprint(SavedDefinition(saved), saved.cell, saved.rotation, saved.fenceMounted))) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_building"));
                    restored.Add(buildingConstruction.Network.Records[restored.Count]);
                    if (saved.hasRide) visitorWorld.RestoreRide(restored[restored.Count - 1], saved.ride);
                }
                foreach (var cell in data.paths) if (!paths.TryPlace(cell)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_path"));
                foreach (var saved in data.fences)
                {
                    var edge = new FenceEdge(saved.cell, saved.alongX);
                    if (!fences.RestoreSegment(edge, saved.kind, saved.gate, saved.health)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_fence"));
                    RestoreFenceWindow(edge, saved);
                }
                foreach (var cell in data.trees) if (!trees.RestoreTree(cell,data.treeVisuals?.Find(item=>item.cell==cell))) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_tree"));
                foreach (var cell in data.graves) if (!graves.TryPlace(cell)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_gravestone"));
                if (data.scarecrows != null) foreach (var cell in data.scarecrows)
                    if (!scarecrows.TryPlace(cell)) throw new InvalidDataException(Loc.Text("park.save.restore_scarecrow_failed"));
                if(data.rocks!=null)foreach(var cell in data.rocks)if(!rocks.TryPlace(cell))throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_rock"));
                if (data.shelters != null) foreach (var shelter in data.shelters)
                    if (!shelters.Restore(shelter)) throw new InvalidDataException(Loc.Text("habitat.save.restore_shelter_failed"));
                if (data.waterTroughs != null) foreach (var trough in data.waterTroughs)
                    if (!waterTroughs.Restore(trough)) throw new InvalidDataException(Loc.Text("habitat.save.restore_trough_failed"));
                buildingConstruction.Network.Refresh();
                // Restore all saved actors even if the designer has since lowered the shared population cap.
                creatureWorld.PopulationLimit = Math.Max(creatureWorld.PopulationLimit, data.creatures.Count + data.staff.Count + data.visitors.Count);
                // Restore staff without charging hiring fees, including temporarily disconnected homes.
                foreach (var saved in data.staff)
                {
                    if (saved.building < 0 || saved.building >= restored.Count) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.invalid_staff_home"));
                    var home = restored[saved.building];
                    var worker = staffWorld.RestoreEmployee(home, saved.cell);
                    if (worker == null) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_employee"));
                    RestoreActor(worker.Actor, saved);
                }
                foreach (var saved in data.creatures)
                {
                    var dispenser = saved.building >= 0 && saved.building < restored.Count ? restored[saved.building] : null;
                    if (!creatureWorld.TrySpawn(Species(saved.species), saved.cell, dispenser)) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_creature"));
                    var actor = creatureWorld.Agents[creatureWorld.Agents.Count - 1]; RestoreActor(actor, saved);
                    if (!creatureWorld.RestoreShelterState(actor, saved.hasShelterHome ? saved.shelterHome : (Vector2Int?)null, saved.shelterSleeping, saved.shelterRetreat))
                        throw new InvalidDataException(Loc.Text("habitat.save.restore_home_failed"));
                    if (saved.retire) staffWorld.RequestRetire(actor);
                    if (saved.sedate) staffWorld.RequestSedate(actor);
                }
                foreach (var saved in data.visitors)
                {
                    // Ordinary spawning keeps building interiors blocked. Restore an authorized
                    // room guest at the public door first, then restore the saved indoor position.
                    var spawnCell = (saved.attraction?.insideViewingRoom == true || saved.attraction?.insideInterior == true)
                        && saved.attraction.building >= 0 ? saved.attraction.access : saved.cell;
                    var guest = visitorWorld.RestoreGuestWithType(spawnCell, saved.visitorType);
                    if (guest == null) throw new InvalidDataException(Loc.Text("park.build_mode_controller__saving.could_not_restore_visitor"));
                    RestoreActor(guest.Actor, saved); guest.Mood.Restore(saved.satisfaction, saved.age, saved.patience, saved.leaving);
                    guest.LeavingPark = saved.leaving;
                    guest.RestoreInterests(saved.observedSpecies, saved.observedOrigins);
                    visitorWorld.RestoreAttraction(guest, saved.attraction, restored);
                }
                foreach (var saved in data.research) staffWorld.Research.Study(Species(saved.species), saved.progress);
                for (int i = 0; i < restored.Count; i++)
                {
                    var saved = data.buildings[i]; restored[i].RestoreAccounts(saved.price, saved.revenue, saved.expenses, saved.stock);
                    var species = Species(saved.research); if (species != null) staffWorld.SetResearchTarget(restored[i], species);
                }
                economy.Restore(data.balance, data.income, data.expenses, data.unlimited);
                economy.RestoreFinance(data.finance);
                visitorWorld.Reputation.Restore(data.reputation);visitorWorld.RefreshReputation();
                visitorWorld.RestoreFeedbackCounts(data.rejectedAdmissions,data.expensiveServiceRefusals);
                visitorWorld.RestoreCounts(data.departedVisitors, data.lostVisitors);
                if(data.version>=2) Progression.Restore(data.progression,restored);
                else Progression.AdoptLegacyPark(restored,fences.Layout.Edges,fences.Layout);
                GetComponent<IsometricCameraController>()?.RestorePose(data.focus, data.yaw, data.zoom);
                RefreshTerrainAttachments();groundVegetation.Refresh();
                alerts?.ResetBaseline();
                ResetConstructionHistory("park.build_mode_controller__saving.game_loaded_construction_history_starts_here");
                GameSaveMenu.ShowMessage(Loc.Text("park.build_mode_controller__saving.game_loaded"));
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                // Never allow play or saving a partially reconstructed park after a failed load.
                ParkSaveStore.Pending = null; ParkSaveStore.LoadError = Loc.Format("park.build_mode_controller__saving.load_failed", error.Message);
                preparationFailed=true;ready=false;enabled=false;
                SceneTransition.FailPreparation(error);
            }
            finally { creatureWorld.RestoringSave = false; buildingConstruction.Network.RestoringSave = false; }
        }
    }
}

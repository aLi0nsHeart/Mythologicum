using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            data.treeVisuals=trees.CaptureVisuals();
            data.generation=mapOptions?.Clone();data.rocks=rocks.Capture();
            var camera = GetComponent<IsometricCameraController>();
            if (camera != null) { data.focus = camera.SavedFocus; data.yaw = camera.SavedYaw; data.zoom = camera.SavedZoom; }
            for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
            {
                var cell = new Vector2Int(x,y);
                if (grid.ContentAt(cell) == BuildCellContent.Path) data.paths.Add(cell);
                if (Environment.TreesAt(cell) > 0) data.trees.Add(cell);
                if (Environment.GravestonesAt(cell) > 0) data.graves.Add(cell);
            }
            var records = buildingConstruction.Network.Records;
            data.progression = Progression.Capture(records);
            foreach (var record in records)
            {
                var f = record.Footprint; var d = f.Definition;
                data.buildings.Add(new SavedBuilding { kind = d.kind, cell = f.Anchor, rotation = f.Rotation,
                    width = d.width, depth = d.depth, height = d.height, door = d.entranceColumn,
                    price = record.Price, revenue = record.Revenue, expenses = record.Expenses, stock = record.FeedStock,
                    research = staffWorld.ResearchTarget(record)?.name });
            }
            foreach (var edge in fences.Layout.Edges) data.fences.Add(new SavedFence { cell = edge.Start,
                alongX = edge.AlongX, kind = fences.Layout.KindAt(edge), gate = fences.Layout.IsGate(edge), health = fences.Layout.HealthAt(edge) });
            foreach (var actor in creatureWorld.Agents)
            {
                if (!actor.Alive || actor.ExternallyControlled) continue;
                var saved = CaptureActor(actor); saved.building = IndexOf(records, actor.SourceDispenser);
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
                    data.visitors.Add(saved);
                }
            foreach (var species in creatureCatalog) data.research.Add(new SavedResearch { species = species.name, progress = staffWorld.Research.Progress(species) });
            return data;
        }
        private static int IndexOf(IReadOnlyList<ParkBuildingRecord> records, ParkBuildingRecord record)
        { for (int i = 0; i < records.Count; i++) if (records[i] == record) return i; return -1; }
        private SavedActor CaptureActor(CreatureAgent actor) => new SavedActor { species = actor.Definition.name,
            cell = actor.CarriedBy != null ? grid.WorldToCell(actor.transform.position) : actor.Cell,
            hunger = actor.Vitals.Hunger, fear = actor.Vitals.Fear, energy = actor.Energy.Energy, health = actor.Health.Current,
            happiness = actor.Wellbeing.Happiness, agitation = actor.Wellbeing.Agitation,
            sedation = actor.SedationRemaining, explored = actor.Exploration.AwakeSeconds };
        private void RestoreActor(CreatureAgent actor, SavedActor saved)
        {
            actor.Cell = saved.cell;
            actor.transform.position = grid.ActorPosition(saved.cell,actor.Definition.bodyScale.y);
            actor.Vitals.Restore(saved.hunger, saved.fear); actor.Energy = new CreatureEnergy(saved.energy);
            actor.Health.Damage(Mathf.Max(0f, actor.Health.Maximum - Mathf.Max(1f, saved.health)));
            actor.Wellbeing.Restore(saved.happiness, saved.agitation); actor.Exploration.Tick(saved.explored);
            if (!actor.ExternallyControlled && saved.sedation > 0f) creatureWorld.Sedate(actor, saved.sedation);
        }
        public void ValidateSave(ParkSaveData data)
        {
            if(data.width<1||data.depth<1||data.width>128||data.depth>128||!Finite(data.cellSize)||data.cellSize<.25f||data.cellSize>16f)
                throw new InvalidDataException("Unsupported saved map dimensions.");
            if(data.generation!=null&&(!data.generation.Validate(out _)||data.generation.width!=data.width||data.generation.depth!=data.depth))
                throw new InvalidDataException("Invalid saved generation settings.");
            GridHeightField checkTerrain=null;
            if(data.version>=3){checkTerrain=new GridHeightField(data.width,data.depth,data.cellSize);checkTerrain.Restore(data.terrain);}
            var checkGrid = new SquareBuildGrid(grid.Origin, data.cellSize, data.width, data.depth,checkTerrain);
            var checkFences = new FenceLayout(checkGrid);
            var network = new ParkBuildingNetwork(checkGrid, checkFences);
            network.RestoringSave = true;
            foreach (var saved in data.buildings)
            {
                var definition = SavedDefinition(saved,data.width,data.depth);
                var footprint=new BuildingFootprint(definition,saved.cell,saved.rotation);
                var record=network.TryPlace(footprint);
                if (record == null)
                    throw new InvalidDataException("A saved building has an invalid footprint.");
                if(checkTerrain!=null&&!checkTerrain.RegisterFoundation(record,footprint))throw new InvalidDataException("A saved building foundation is not flat.");
            }
            foreach (var cell in data.paths) if (!checkGrid.GroundWalkable(cell,true)||!checkGrid.TryOccupy(cell, BuildCellContent.Path)) throw new InvalidDataException("Invalid saved path.");
            foreach (var fence in data.fences)
                if (!Enum.IsDefined(typeof(FenceKind), fence.kind) || fence.health <= 0f || !Finite(fence.health)
                    || !checkFences.TryAdd(new FenceEdge(fence.cell, fence.alongX), fence.kind))
                    throw new InvalidDataException("Invalid saved fence.");
            var enrichment = new HashSet<Vector2Int>();
            foreach (var cell in data.trees)
                if (!checkGrid.IsAvailable(cell) || !enrichment.Add(cell)) throw new InvalidDataException("Invalid saved tree.");
            foreach (var cell in data.graves)
                if (!checkGrid.IsAvailable(cell) || !enrichment.Add(cell)) throw new InvalidDataException("Invalid saved gravestone.");
            var rockCells=new HashSet<Vector2Int>();
            if(data.rocks!=null)foreach(var cell in data.rocks)
            {
                if(!checkGrid.IsAvailable(cell)||!rockCells.Add(cell)||!enrichment.Add(cell))throw new InvalidDataException("Invalid saved rock.");
                foreach(var record in network.Records)if(record.Footprint.AccessCell==cell)throw new InvalidDataException("Rock blocks a saved entrance.");
            }
            if(data.treeVisuals!=null)
            {
                var ids=new HashSet<Vector2Int>();var treeCells=new HashSet<Vector2Int>(data.trees);
                foreach(var item in data.treeVisuals)if(item==null||!treeCells.Contains(item.cell)||!ids.Add(item.cell)||!Finite(item.height)||item.height<.1f||item.height>100||!Finite(item.yaw))
                    throw new InvalidDataException("Invalid saved tree appearance.");
            }
            foreach (var saved in data.creatures)
                if (Species(saved.species) == null || !ValidActor(saved, checkGrid)||rockCells.Contains(saved.cell)) throw new InvalidDataException("A saved creature is unavailable or invalid.");
            var hired = new Dictionary<int,int>();
            foreach (var saved in data.staff)
            {
                if (!ValidActor(saved, checkGrid) || saved.building < 0 || saved.building >= data.buildings.Count
                    ||rockCells.Contains(saved.cell)|| !SavedDefinition(data.buildings[saved.building],data.width,data.depth).IsEmployeeBuilding) throw new InvalidDataException("Invalid staff home or employee.");
                hired.TryGetValue(saved.building, out int count); hired[saved.building] = count + 1;
                if (count >= ParkBuildingRecord.EmployeeCapacity) throw new InvalidDataException("Too many saved employees.");
            }
            foreach (var saved in data.visitors) if (!ValidActor(saved, checkGrid)||rockCells.Contains(saved.cell)) throw new InvalidDataException("Invalid saved visitor.");
            foreach (var saved in data.research) if (Species(saved.species) == null || !Finite(saved.progress)) throw new InvalidDataException("Invalid species research.");
            if (!Finite(data.balance) || !Finite(data.income) || !Finite(data.expenses) || !Finite(data.zoom) || !Finite(data.yaw)
                || !Finite(data.focus.x) || !Finite(data.focus.y) || !Finite(data.focus.z)) throw new InvalidDataException("Invalid saved finances or camera.");
            if(data.version>=2)Progression.Validate(data.progression,network.Records);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidActor(SavedActor saved, SquareBuildGrid checkGrid) => saved != null && checkGrid.Contains(saved.cell)
            && checkGrid.ContentAt(saved.cell) != BuildCellContent.Building && Finite(saved.health) && saved.health > 0f
            && Finite(saved.hunger) && Finite(saved.fear) && Finite(saved.energy) && Finite(saved.sedation)
            && Finite(saved.happiness) && Finite(saved.agitation) && Finite(saved.age) && Finite(saved.satisfaction) && Finite(saved.patience) && Finite(saved.explored);
        private CreatureDefinition Species(string name) => Array.Find(creatureCatalog, d => d.name == name);
        private BuildingDefinition SavedDefinition(SavedBuilding saved,int mapWidth=-1,int mapDepth=-1)
        {
            var source = Array.Find(buildingCatalog, d => d.kind == saved.kind);
            if (source == null || saved.width < 1 || saved.depth < 1 || saved.width > (mapWidth<0?grid.Width:mapWidth) || saved.depth > (mapDepth<0?grid.Depth:mapDepth)
                || !Finite(saved.height) || saved.height <= 0f || !Finite(saved.price) || !Finite(saved.revenue) || !Finite(saved.expenses))
                throw new InvalidDataException("A saved building type is unavailable or invalid.");
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
                SelectTool(Tool.None);
                var restored = new List<ParkBuildingRecord>();
                foreach (var saved in data.buildings)
                {
                    if (!buildingConstruction.TryPlace(new BuildingFootprint(SavedDefinition(saved), saved.cell, saved.rotation))) throw new InvalidDataException("Could not restore building.");
                    restored.Add(buildingConstruction.Network.Records[restored.Count]);
                }
                foreach (var cell in data.paths) if (!paths.TryPlace(cell)) throw new InvalidDataException("Could not restore path.");
                foreach (var saved in data.fences)
                {
                    var edge = new FenceEdge(saved.cell, saved.alongX);
                    if (!fences.RestoreSegment(edge, saved.kind, saved.gate, saved.health)) throw new InvalidDataException("Could not restore fence.");
                }
                foreach (var cell in data.trees) if (!trees.RestoreTree(cell,data.treeVisuals?.Find(item=>item.cell==cell))) throw new InvalidDataException("Could not restore tree.");
                foreach (var cell in data.graves) if (!graves.TryPlace(cell)) throw new InvalidDataException("Could not restore gravestone.");
                if(data.rocks!=null)foreach(var cell in data.rocks)if(!rocks.TryPlace(cell))throw new InvalidDataException("Could not restore rock.");
                buildingConstruction.Network.Refresh();
                // Restore staff without charging hiring fees, including temporarily disconnected homes.
                foreach (var saved in data.staff)
                {
                    if (saved.building < 0 || saved.building >= restored.Count) throw new InvalidDataException("Invalid staff home.");
                    var home = restored[saved.building];
                    var worker = staffWorld.RestoreEmployee(home, saved.cell);
                    if (worker == null) throw new InvalidDataException("Could not restore employee.");
                    RestoreActor(worker.Actor, saved);
                }
                creatureWorld.PopulationLimit = Math.Max(creatureWorld.PopulationLimit, data.creatures.Count + data.staff.Count + data.visitors.Count);
                foreach (var saved in data.creatures)
                {
                    var dispenser = saved.building >= 0 && saved.building < restored.Count ? restored[saved.building] : null;
                    if (!creatureWorld.TrySpawn(Species(saved.species), saved.cell, dispenser)) throw new InvalidDataException("Could not restore creature.");
                    var actor = creatureWorld.Agents[creatureWorld.Agents.Count - 1]; RestoreActor(actor, saved);
                    if (saved.retire) staffWorld.RequestRetire(actor);
                    if (saved.sedate) staffWorld.RequestSedate(actor);
                }
                foreach (var saved in data.visitors)
                {
                    var guest = visitorWorld.RestoreGuest(saved.cell);
                    if (guest == null) throw new InvalidDataException("Could not restore visitor.");
                    RestoreActor(guest.Actor, saved); guest.Mood.Restore(saved.satisfaction, saved.age, saved.patience, saved.leaving);
                    guest.LeavingPark = saved.leaving;
                }
                foreach (var saved in data.research) staffWorld.Research.Study(Species(saved.species), saved.progress);
                for (int i = 0; i < restored.Count; i++)
                {
                    var saved = data.buildings[i]; restored[i].RestoreAccounts(saved.price, saved.revenue, saved.expenses, saved.stock);
                    var species = Species(saved.research); if (species != null) staffWorld.SetResearchTarget(restored[i], species);
                }
                economy.Restore(data.balance, data.income, data.expenses, data.unlimited);
                visitorWorld.RestoreCounts(data.departedVisitors, data.lostVisitors);
                if(data.version>=2) Progression.Restore(data.progression,restored);
                else Progression.AdoptLegacyPark(restored,fences.Layout.Edges,fences.Layout);
                GetComponent<IsometricCameraController>()?.RestorePose(data.focus, data.yaw, data.zoom);
                RefreshTerrainAttachments();groundVegetation.Refresh();
                GameSaveMenu.ShowMessage("Game loaded.");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                // Never allow play or saving a partially reconstructed park after a failed load.
                ParkSaveStore.Pending = null; ParkSaveStore.LoadError = "Load failed: " + error.Message;
                Time.timeScale = 1f; SceneManager.LoadScene("MainMenuScene");
            }
            finally { creatureWorld.RestoringSave = false; buildingConstruction.Network.RestoringSave = false; }
        }
    }
}

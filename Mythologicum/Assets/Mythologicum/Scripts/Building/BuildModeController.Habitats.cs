using UnityEngine;
using UnityEngine.InputSystem;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private WaterTroughConstruction waterTroughs;
        private ShelterConstruction shelters;
        private HabitatTerrainSurface habitatSurface;
        private int habitatAttachmentRevision = -1;

        private bool HabitatFeatureAt(Vector2Int cell) => waterTroughs?.Occupies(cell) == true || shelters?.Occupies(cell) == true;
        private bool ReservedBuildingAccess(Vector2Int cell)
        {
            if (buildingConstruction == null) return false;
            foreach (var record in buildingConstruction.Network.Records)
                if (record.Footprint.AccessCell == cell || record.Footprint.IsFenceMounted && record.Footprint.RearAccessCell == cell) return true;
            return false;
        }
        private void CreateHabitatFeatures()
        {
            var shelterRoot = new GameObject("Creature shelters"); shelterRoot.transform.SetParent(buildings.transform, false);
            shelters = new ShelterConstruction(grid, Environment, shelterRoot.transform, buildingMaterial,
                foreignOccupancy: cell => rocks.Occupies(cell) || waterTroughs?.Occupies(cell) == true || ReservedBuildingAccess(cell));
            var troughRoot = new GameObject("Water troughs"); troughRoot.transform.SetParent(buildings.transform, false);
            waterTroughs = new WaterTroughConstruction(grid, Environment, troughRoot.transform, buildingMaterial,
                buildings: buildingConstruction.Network, blockedCell: cell => rocks.Occupies(cell) || shelters.Occupies(cell));
            creatureWorld.ConfigureShelters(shelters);
            creatureWorld.ConfigureDrinking(waterTroughs, grid.Habitat.IsWater);
            staffWorld.ConfigureWaterTroughs(waterTroughs);
            habitatSurface = new HabitatTerrainSurface(grid, terrainArt, buildings.transform);
        }

        private bool HabitatGroundEditable(Vector2Int cell)
            => grid.Contains(cell) && grid.ContentAt(cell) != BuildCellContent.Building;
        private bool HabitatActorAt(Vector2Int cell)
        {
            foreach (var actor in creatureWorld.Agents)
                if (actor.Alive && (actor.Cell == cell || actor.Route.Count > 0 && actor.Route[0] == cell)) return true;
            return false;
        }
        private bool HabitatSculptSafe(Vector2 center, float radius)
        {
            float reach = radius + 1.5f;
            foreach (var cell in grid.Habitat.WaterSurfaceCells)
                if ((new Vector2(cell.x + .5f, cell.y + .5f) - center).sqrMagnitude < reach * reach && !HabitatWaterEditable(cell)) return false;
            return true;
        }
        private bool HabitatWaterEditable(Vector2Int cell)
        {
            if (!grid.Contains(cell) || grid.ContentAt(cell) != BuildCellContent.Empty || ReservedBuildingAccess(cell)
                || trees.Occupies(cell) || graves.Occupies(cell) || scarecrows.Occupies(cell) || rocks.Occupies(cell) || HabitatFeatureAt(cell)) return false;
            foreach (var actor in creatureWorld.Agents)
                if (actor.Alive && (actor.Cell == cell || actor.Route.Contains(cell))) return false;
            return true;
        }
        private void ApplyWaterTrough(Mouse mouse, Vector2Int cell)
        {
            bool valid = waterTroughs.CanPlace(cell) && !HabitatActorAt(cell), affordable = economy.CanAfford(waterTroughs.Cost);
            previewMaterial.color = valid && affordable ? ValidPreviewColor : new Color(.95f, .15f, .12f);
            ShowModelPreview(grid.CellCenter(cell), Quaternion.identity, parent => waterTroughs.CreateVisual(parent));
            status.text = Loc.Format("habitat.trough.controls", waterTroughs.Cost);
            if (!valid) status.text = Loc.Text("habitat.feature.blocked");
            else if (!affordable) status.text = Loc.Format("park.build_mode_controller.not_enough_funds_need_enable_infinite_funds_to_build_freel", waterTroughs.Cost);
            if (!valid || !affordable || !mouse.leftButton.wasPressedThisFrame) return;
            BeginConstructionAction("habitat.trough.place");
            if (waterTroughs.TryPlace(cell))
            { economy.TrySpend(waterTroughs.Cost, ParkTransactionCategory.Construction); CommitConstructionAction(waterTroughs.Cost); SelectTool(Tool.None); }
            else CancelConstructionAction();
        }
        private void ApplyCave(Mouse mouse, Vector2Int cell)
        {
            bool valid = shelters.CanPlaceCave(cell), affordable = economy.CanAfford(shelters.CaveCost);
            previewMaterial.color = valid && affordable ? ValidPreviewColor : new Color(.95f, .15f, .12f);
            ShowModelPreview(grid.CellCenter(cell), Quaternion.identity, shelters.CreateCaveVisual);
            status.text = Loc.Format("habitat.cave.controls", shelters.CaveCost);
            if (!valid) status.text = Loc.Text("habitat.feature.blocked");
            else if (!affordable) status.text = Loc.Format("park.build_mode_controller.not_enough_funds_need_enable_infinite_funds_to_build_freel", shelters.CaveCost);
            if (!valid || !affordable || !mouse.leftButton.wasPressedThisFrame) return;
            BeginConstructionAction("habitat.cave.place");
            if (shelters.TryPlaceCave(cell))
            { economy.TrySpend(shelters.CaveCost, ParkTransactionCategory.Construction); CommitConstructionAction(shelters.CaveCost); SelectTool(Tool.None); }
            else CancelConstructionAction();
        }
    }
}

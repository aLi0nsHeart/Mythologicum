using UnityEngine;
using UnityEngine.InputSystem;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private BuildingDefinition ViewingBuilding => System.Array.Find(buildingCatalog, item => item.kind == BuildingKind.ViewingWindow)
            ?? ViewingWindowDefinition.Current.CreateBuildingDefinition();

        private void SelectViewingWindow()
        {
            if (Progression?.Completed("viewing-window") != true) return;
            selectedBuilding = ViewingBuilding;
            buildingRotation = 0;
            SelectTool(Tool.ViewingWindow);
        }

        private bool TrySnapViewingWindow(FenceEdge edge, out BuildingFootprint footprint)
            => TrySnapFenceBuilding(ViewingBuilding, edge, true, out footprint);

        private bool ViewingFootprintValid(BuildingFootprint footprint)
        {
            if (!grid.Contains(footprint.AccessCell) || !creatureWorld.IsClear(footprint.AccessCell)) return false;
            RefreshPettingGeometry();
            var pen = enclosureMap?.At(footprint.RearAccessCell);
            if (pen == null || !pen.IsSecured || pen.Cells.Contains(footprint.AccessCell)) return false;
            foreach (var edge in footprint.AttachmentEdges())
                if (!fences.CanPlaceWindow(edge) || FenceAttachedBuildingAt(edge) != null) return false;
            foreach (var cell in footprint.Cells())
                if (!grid.IsAvailable(cell) || !creatureWorld.IsClear(cell) || !pen.Cells.Contains(cell) || HabitatFeatureAt(cell)) return false;
            return true;
        }

        private void ApplyViewingWindow(Mouse mouse, Vector3 point)
        {
            if (Progression?.Completed("viewing-window") != true) { SelectTool(Tool.None); return; }
            FenceEdge edge = fences.Layout.NearestEdge(point);
            if (Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var hit,
                view.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var segment = hit.collider.GetComponentInParent<FenceSegment>();
                if (segment != null) edge = segment.Edge;
            }
            bool valid = TrySnapViewingWindow(edge, out var footprint);
            valid = valid && buildingConstruction.CanPlace(footprint) && !creatureWorld.Occupies(footprint);
            var definition = ViewingBuilding;
            bool affordable = economy.CanAfford(definition.constructionCost);
            previewMaterial.color = valid && affordable ? ValidPreviewColor : new Color(.95f, .15f, .12f);
            ShowModelPreview(buildingConstruction.PreviewPosition(footprint), Quaternion.Euler(0, -90f * footprint.Rotation, 0),
                parent => buildingConstruction.CreateVisual(parent, definition));
            if (grid.Contains(footprint.AccessCell))
            {
                entrancePreview.transform.position = grid.CellCenter(footprint.AccessCell) + Vector3.up * .14f;
                entrancePreview.transform.localScale = new Vector3(cellSize * .65f, .08f, cellSize * .65f);
                entrancePreview.SetActive(true);
            }
            status.text = Loc.Format("attraction.window.building_controls", GameInput.KeyCaption(GameplayAction.RotateBuilding));
            if (!valid) status.text = Loc.Text("attraction.window.building_invalid");
            else if (!affordable) status.text = Loc.Text("attraction.window.funds");
            else status.text += Loc.Format("park.build_mode_controller.cost", definition.constructionCost);
            if (!valid || !affordable || !mouse.leftButton.wasPressedThisFrame) return;
            BeginConstructionAction("attraction.window.place");
            if (buildingConstruction.TryPlace(footprint))
            {
                economy.TrySpend(definition.constructionCost, ParkTransactionCategory.Construction);
                CommitConstructionAction(definition.constructionCost); fences.Refresh(); SelectTool(Tool.None);
            }
            else CancelConstructionAction();
        }
    }
}

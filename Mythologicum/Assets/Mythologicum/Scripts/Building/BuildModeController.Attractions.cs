using UnityEngine;
using UnityEngine.InputSystem;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private int pettingFenceRevision = -1;

        private ParkBuildingRecord FenceAttachedBuildingAt(FenceEdge edge)
        {
            foreach (var record in buildingConstruction.Network.Records)
                if (record.Footprint.IsFenceMounted)
                    foreach (var attached in record.Footprint.AttachmentEdges()) if (attached.Equals(edge)) return record;
            return null;
        }

        private bool TrySnapPettingEntrance(FenceEdge edge, out BuildingFootprint footprint)
            => TrySnapFenceBuilding(selectedBuilding, edge, false, out footprint);

        private bool TrySnapFenceBuilding(BuildingDefinition definition, FenceEdge edge, bool viewingRoom, out BuildingFootprint footprint)
        {
            footprint = default;
            bool first = true;
            for (int step = 0; step < 4; step++)
            {
                int rotation = (buildingRotation + step) % 4;
                if (edge.AlongX != (rotation % 2 == 0)) continue;
                Vector2Int outside = rotation == 0 || rotation == 3 ? edge.CellB : edge.CellA;
                var basis = new BuildingFootprint(definition, outside, rotation, fenceMounted: true);
                // The pointed-at segment can be either end of a wider building.
                for (int column = 0; column < (viewingRoom ? definition.width : 1); column++)
                {
                    var candidate = new BuildingFootprint(definition, outside - basis.AlongFenceDirection * column, rotation, fenceMounted: true);
                    if (first) { footprint = candidate; first = false; }
                    if (!(viewingRoom ? ViewingFootprintValid(candidate) : PettingFootprintValid(candidate))
                        || !buildingConstruction.Network.CanPlace(candidate)) continue;
                    footprint = candidate;
                    return true;
                }
            }
            return false;
        }

        private void ApplyPettingEntrance(Mouse mouse, Vector3 point)
        {
            if (!Progression.BuildingUnlocked(selectedBuilding.kind)) { SelectTool(Tool.None); return; }
            var edge = fences.Layout.NearestEdge(point);
            if (Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var hit,
                view.farClipPlane, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                var segment = hit.collider.GetComponentInParent<FenceSegment>();
                if (segment != null) edge = segment.Edge;
            }
            bool valid = TrySnapPettingEntrance(edge, out var footprint);
            valid = valid && buildingConstruction.CanPlace(footprint);
            bool affordable = economy.CanAfford(selectedBuilding.constructionCost);
            previewMaterial.color = valid && affordable ? ValidPreviewColor : new Color(.95f, .15f, .12f);
            ShowModelPreview(buildingConstruction.PreviewPosition(footprint), Quaternion.Euler(0, -90f * footprint.Rotation, 0),
                parent => buildingConstruction.CreateVisual(parent, selectedBuilding));
            if (grid.Contains(footprint.AccessCell))
            {
                entrancePreview.transform.position = grid.CellCenter(footprint.AccessCell) + Vector3.up * .14f;
                entrancePreview.transform.localScale = new Vector3(cellSize * .65f, .08f, cellSize * .65f);
                entrancePreview.SetActive(true);
            }
            status.text = Loc.Format("attraction.petting.controls", GameInput.KeyCaption(GameplayAction.RotateBuilding));
            if (!valid) status.text = Loc.Text("attraction.petting.invalid");
            else if (!affordable) status.text = Loc.Format("park.build_mode_controller.not_enough_funds_need_enable_infinite_funds_to_build_freel", selectedBuilding.constructionCost);
            else status.text += Loc.Format("park.build_mode_controller.cost", selectedBuilding.constructionCost);
            if (!valid || !affordable || !mouse.leftButton.wasPressedThisFrame) return;
            BeginLocalizedConstructionAction("park.build_mode_controller.place", selectedBuilding.displayName);
            if (buildingConstruction.TryPlace(footprint))
            {
                economy.TrySpend(selectedBuilding.constructionCost, ParkTransactionCategory.Construction);
                CommitConstructionAction(selectedBuilding.constructionCost); fences.Refresh(); SelectTool(Tool.None);
            }
            else CancelConstructionAction();
        }

        private void TickWindowEconomy(float dt)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0) return;
            float cost = ViewingWindowDefinition.Current.operatingCostPerSecond * dt;
            foreach (var edge in fences.Layout.Windows)
            {
                var window = fences.Layout.WindowAt(edge);
                economy.Tick(dt, ViewingWindowDefinition.Current.operatingCostPerSecond, ParkTransactionCategory.BuildingUpkeep); window.RecordExpense(cost);
            }
        }

        private bool PettingFootprintValid(BuildingFootprint footprint)
        {
            if (!grid.Contains(footprint.AccessCell) || !grid.Contains(footprint.RearAccessCell)
                || !creatureWorld.IsClear(footprint.AccessCell) || !creatureWorld.IsClear(footprint.RearAccessCell)
                || !fences.Layout.HasFence(footprint.RearEdge) || fences.Layout.IsGate(footprint.RearEdge)
                || fences.Layout.HasWindow(footprint.RearEdge)) return false;
            RefreshPettingGeometry();
            var pen = enclosureMap?.At(footprint.RearAccessCell);
            return pen != null && pen.IsSecured && !pen.Cells.Contains(footprint.AccessCell);
        }

        private void RefreshPettingGeometry()
        {
            if (enclosureMap == null || pettingFenceRevision == fences.Layout.Revision) return;
            enclosureMap.Refresh(buildingConstruction.Network.IsPathConnected);
            pettingFenceRevision = fences.Layout.Revision;
        }

        private Vector2Int? PettingEntrance(ParkBuildingRecord record)
        {
            if (record == null || !record.IsOpen || !buildingConstruction.Network.Contains(record)
                || record.Footprint.Definition.kind != BuildingKind.PettingZoo || !PettingFootprintValid(record.Footprint)) return null;
            var pen = enclosureMap.At(record.Footprint.RearAccessCell);
            bool hasAnimal = false;
            foreach (var animal in creatureWorld.Agents)
            {
                if (!animal.Alive || animal.ExternallyControlled || !pen.Cells.Contains(animal.Cell)) continue;
                if (!animal.Definition.pettable || animal.Definition.threat > record.Footprint.Definition.attraction.maximumCreatureThreat) return null;
                hasAnimal = true;
            }
            return hasAnimal ? record.Footprint.RearAccessCell : (Vector2Int?)null;
        }

        private bool IsSafePettingCreature(CreatureAgent animal)
        {
            if (animal == null || !animal.Alive || !animal.Definition.pettable) return false;
            foreach (var record in buildingConstruction.Network.Records)
                if (record.Footprint.Definition.kind == BuildingKind.PettingZoo && PettingEntrance(record).HasValue
                    && enclosureMap.At(record.Footprint.RearAccessCell).Cells.Contains(animal.Cell)) return true;
            return false;
        }
    }
}

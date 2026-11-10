using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private sealed class ConstructionSnapshot
        {
            public readonly HashSet<Vector2Int> paths = new HashSet<Vector2Int>(), trees = new HashSet<Vector2Int>(), graves = new HashSet<Vector2Int>(), rocks = new HashSet<Vector2Int>();
            public readonly Dictionary<FenceEdge, SavedFence> fences = new Dictionary<FenceEdge, SavedFence>();
            public readonly HashSet<ParkBuildingRecord> buildings = new HashSet<ParkBuildingRecord>();
            public SavedTerrain terrain;
        }
        private sealed class ConstructionUndo
        {
            public StatusMessage label;
            public override string ToString() => label?.Value??"";
            public ConstructionSnapshot before, after;
            public TerrainUndoPatch terrain;
            public List<Vector2Int> paths, trees, graves, rocks;
            public List<FenceEdge> fences;
            public List<ParkBuildingRecord> buildings;
            public float cost;
            public bool charged;
            public double purchasedAt;
            public FinanceWindowBucket receipt;
            public bool Changed => terrain.Count > 0 || paths.Count + trees.Count + graves.Count + rocks.Count + fences.Count + buildings.Count > 0;
        }
        private readonly List<ConstructionUndo> constructionHistory = new List<ConstructionUndo>();
        private ConstructionSnapshot pendingConstruction;
        private StatusMessage pendingConstructionLabel;
        private bool pendingConstructionCharged;
        private double pendingConstructionTime;
        private Button undoConstructionButton;
        public int UndoCount => constructionHistory.Count;
        private readonly StatusMessage undoMessage=new StatusMessage("park.build_mode_controller__history.build_something_to_enable_construction_undo");
        public string UndoMessage { get=>undoMessage.Value;private set=>undoMessage.Set(value); }

        private void InitializeConstructionHistory(Canvas canvas)
        {
            if (undoConstructionButton != null) return;
            undoConstructionButton = ManagementWidgets.Button(canvas.transform, "park.build_mode_controller__history.undo_construction", Vector2.zero, new Vector2(205, 32), () => UndoLastConstruction(), 16);
            var rect = (RectTransform)undoConstructionButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 62);
            UpdateConstructionHistoryUI();
        }
        private void BeginConstructionAction(string label)
        {
            if (pendingConstruction != null || grid == null || creatureWorld.RestoringSave) return;
            pendingConstruction = CaptureConstruction(); pendingConstructionLabel = new StatusMessage(label);
            pendingConstructionCharged = !economy.UnlimitedFunds; pendingConstructionTime = economy.ClockSeconds;
        }
        private void BeginLocalizedConstructionAction(string labelId,params object[] arguments)
        {
            if(pendingConstruction!=null)return;
            BeginConstructionAction(labelId);
            if(pendingConstruction!=null)pendingConstructionLabel.Set(labelId,arguments);
        }
        private void CancelConstructionAction() => pendingConstruction = null;
        private void CommitConstructionAction(float constructionCost = 0f)
        {
            if (pendingConstruction == null) return;
            var before = pendingConstruction; pendingConstruction = null;
            var after = CaptureConstruction();
            // This journal reverses placements, not destructive demolition or simulation damage.
            if (before.buildings.Any(item => !after.buildings.Contains(item)) || before.paths.Any(cell => !after.paths.Contains(cell))
                || before.trees.Any(cell => !after.trees.Contains(cell)) || before.graves.Any(cell => !after.graves.Contains(cell))
                || before.rocks.Any(cell => !after.rocks.Contains(cell)) || before.fences.Keys.Any(edge => !after.fences.ContainsKey(edge)))
            { ResetConstructionHistory("park.build_mode_controller__history.destructive_changes_clear_construction_undo_history"); return; }
            var action = new ConstructionUndo
            {
                label = pendingConstructionLabel, before = before, after = after,
                terrain = new TerrainUndoPatch(before.terrain?.heights, after.terrain?.heights),
                paths = after.paths.Except(before.paths).ToList(), trees = after.trees.Except(before.trees).ToList(),
                graves = after.graves.Except(before.graves).ToList(), rocks = after.rocks.Except(before.rocks).ToList(),
                buildings = after.buildings.Except(before.buildings).ToList(),
                // Combat damage during a multi-frame terrain stroke is simulation state, not construction.
                fences = after.fences.Keys.Where(edge => !before.fences.TryGetValue(edge, out var previous)
                    || previous.kind != after.fences[edge].kind || previous.gate != after.fences[edge].gate).ToList(),
                cost = Math.Max(0f, constructionCost), charged = pendingConstructionCharged, purchasedAt = pendingConstructionTime,
                receipt = constructionCost > 0 ? economy.CurrentExpenseBucket : null
            };
            if (!action.Changed) return;
            constructionHistory.Add(action);
            int limit = ParkOperationsDefinition.Current.undoCapacity;
            while (constructionHistory.Count > limit) constructionHistory.RemoveAt(0);
            undoMessage.Set("park.build_mode_controller__history.undo_creature_activity_visitors_and_operating_expenses_con", action);
            UpdateConstructionHistoryUI();
        }
        private void ResetConstructionHistory(string reason = "park.build_mode_controller__history.loaded_history")
        { pendingConstruction = null; constructionHistory.Clear(); UndoMessage = reason; UpdateConstructionHistoryUI(); }

        private ConstructionSnapshot CaptureConstruction()
        {
            var result = new ConstructionSnapshot { terrain = grid.Terrain?.Capture() };
            for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
            {
                var cell = new Vector2Int(x, y);
                if (grid.ContentAt(cell) == BuildCellContent.Path) result.paths.Add(cell);
                if (trees.Occupies(cell)) result.trees.Add(cell);
                if (graves.Occupies(cell)) result.graves.Add(cell);
            }
            if (rocks != null) result.rocks.UnionWith(rocks.Capture());
            foreach (var record in buildingConstruction.Network.Records) result.buildings.Add(record);
            foreach (var edge in fences.Layout.Edges) result.fences.Add(edge, new SavedFence
                { cell = edge.Start, alongX = edge.AlongX, kind = fences.Layout.KindAt(edge), gate = fences.Layout.IsGate(edge), health = fences.Layout.HealthAt(edge) });
            return result;
        }
        private static bool SameFence(SavedFence a, SavedFence b) => a.kind == b.kind && a.gate == b.gate && Math.Abs(a.health - b.health) < .0001f;

        public bool CanUndoLastConstruction(out string reason)
        {
            bool allowed=CanUndoLastConstructionKey(out reason);
            reason=string.IsNullOrEmpty(reason)?UndoMessage:Loc.Resolve(reason);
            return allowed;
        }
        private bool CanUndoLastConstructionKey(out string reason)
        {
            reason = "";
            if (constructionHistory.Count == 0 || pendingConstruction != null) return false;
            if (GameSaveMenu.IsOpen || SettingsPresentation.IsOpen || GameInput.BlocksGameplayInput)
            { reason = "park.build_mode_controller__history.close_the_menu_or_finish_changing_a_shortcut_before_undoin"; return false; }
            return ValidateConstructionUndo(constructionHistory[constructionHistory.Count - 1], out _, out reason);
        }
        private bool ValidateConstructionUndo(ConstructionUndo action, out SavedTerrain targetTerrain, out string reason)
        {
            targetTerrain = grid.Terrain?.Capture(); reason = "";
            foreach (var cell in action.paths) if (grid.ContentAt(cell) != BuildCellContent.Path)
            { reason = "park.build_mode_controller__history.a_path_in_this_action_has_changed_undo_cannot_replace_newe"; return false; }
            foreach (var cell in action.trees) if (!trees.Occupies(cell)) { reason = "park.build_mode_controller__history.a_tree_in_this_action_was_removed"; return false; }
            foreach (var cell in action.graves) if (!graves.Occupies(cell)) { reason = "park.build_mode_controller__history.a_gravestone_in_this_action_was_removed"; return false; }
            foreach (var cell in action.rocks) if (!rocks.Occupies(cell)) { reason = "park.build_mode_controller__history.a_rock_in_this_action_was_removed"; return false; }
            foreach (var edge in action.fences)
            {
                if (!fences.Layout.HasFence(edge) || !SameFence(action.after.fences[edge], new SavedFence
                    { kind = fences.Layout.KindAt(edge), gate = fences.Layout.IsGate(edge), health = fences.Layout.HealthAt(edge) }))
                { reason = "park.build_mode_controller__history.a_fence_in_this_action_was_damaged_repaired_or_changed_und"; return false; }
            }
            foreach (var building in action.buildings)
            {
                if (!buildingConstruction.Network.Contains(building)) { reason = "park.build_mode_controller__history.a_building_in_this_action_was_demolished"; return false; }
                if (building.HiredCount > 0 || building.Revenue > 0 || Progression?.ActiveProject(building) != null
                    || visitorWorld.Visitors.Any(guest => guest.Service == building)
                    || creatureWorld.Agents.Any(actor => actor.Alive && actor.SourceDispenser == building))
                { reason = "park.build_mode_controller__history.this_building_has_personnel_customers_animals_or_active_re"; return false; }
                if (building == buildingConstruction.Network.Entrance && buildingConstruction.Network.Records.Any(other => other != building))
                { reason = "park.build_mode_controller__history.remove_later_construction_before_undoing_the_park_entrance"; return false; }
            }
            if (action.cost > economy.Expenses) { reason = "park.build_mode_controller__history.the_original_construction_transaction_is_no_longer_availab"; return false; }
            var protectedCells = new HashSet<Vector2Int>(action.trees.Concat(action.graves).Concat(action.paths).Concat(action.rocks));
            foreach (var edge in action.fences) { protectedCells.Add(edge.CellA); protectedCells.Add(edge.CellB); }
            foreach (var building in action.buildings) protectedCells.UnionWith(building.Footprint.Cells());
            foreach (var actor in creatureWorld.Agents)
                if (actor.Alive && (protectedCells.Contains(actor.Cell)
                    || actor.SleepTree.HasValue && protectedCells.Contains(actor.SleepTree.Value)))
                { reason = "park.build_mode_controller__history.a_creature_or_person_is_using_this_construction_wait_until"; return false; }
            if (action.terrain.Count > 0)
            {
                if (targetTerrain == null || !action.terrain.TryRevert(targetTerrain.heights, out var heights))
                { reason = "park.build_mode_controller__history.the_edited_terrain_has_changed_again_undo_will_not_overwri"; return false; }
                targetTerrain.heights = heights;
                var affected = new HashSet<Vector2Int>();
                foreach (int index in action.terrain.Nodes)
                {
                    int x = index % (grid.Width + 1), y = index / (grid.Width + 1);
                    for (int dx = -1; dx <= 0; dx++) for (int dy = -1; dy <= 0; dy++) affected.Add(new Vector2Int(x + dx, y + dy));
                }
                foreach (var actor in creatureWorld.Agents) if (actor.Alive && affected.Contains(actor.Cell))
                { reason = "park.build_mode_controller__history.a_creature_or_person_is_standing_on_the_edited_ground_wait"; return false; }
                var check = new GridHeightField(grid.Width, grid.Depth, grid.CellSize); check.Restore(targetTerrain);
                foreach (var building in buildingConstruction.Network.Records)
                    if (!action.buildings.Contains(building) && !check.RegisterFoundation(building, building.Footprint))
                    { reason = "park.build_mode_controller__history.undo_would_disturb_an_existing_building_foundation"; return false; }
                for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (grid.ContentAt(cell) == BuildCellContent.Path && !action.paths.Contains(cell) && !check.Walkable(cell, true))
                    { reason = "park.build_mode_controller__history.undo_would_make_an_existing_path_too_steep"; return false; }
                }
            }
            return true;
        }

        public bool UndoLastConstruction()
        {
            if (!CanUndoLastConstructionKey(out string reason)) { if(!string.IsNullOrEmpty(reason))UndoMessage = reason; UpdateConstructionHistoryUI(); return false; }
            var action = constructionHistory[constructionHistory.Count - 1];
            if (!ValidateConstructionUndo(action, out var targetTerrain, out reason)) { UndoMessage = reason; return false; }
            SelectTool(Tool.None);
            bool wasRestoring = creatureWorld.RestoringSave; creatureWorld.RestoringSave = true;
            try
            {
                foreach (var building in action.buildings) buildingConstruction.TryRemove(building);
                foreach (var cell in action.paths) paths.TryRemove(cell);
                foreach (var cell in action.trees) trees.TryRemove(cell);
                foreach (var cell in action.graves) graves.TryRemove(cell);
                foreach (var cell in action.rocks) rocks.TryRemove(cell);
                foreach (var edge in action.fences) fences.Layout.TryRemove(edge);
                fences.Refresh(view);
                foreach (var edge in action.fences)
                    if (action.before.fences.TryGetValue(edge, out var previous)) fences.RestoreSegment(edge, previous.kind, previous.gate, previous.health);
                if (action.terrain.Count > 0)
                {
                    grid.Terrain.Restore(targetTerrain);
                    foreach (var building in buildingConstruction.Network.Records) grid.Terrain.RegisterFoundation(building, building.Footprint);
                }
                if (action.cost > 0) economy.RefundPurchase(action.cost, ParkTransactionCategory.Construction, action.charged, action.purchasedAt, action.receipt);
                constructionHistory.RemoveAt(constructionHistory.Count - 1);
                grid.NotifyPhysicalChange(); buildingConstruction.Network.Refresh(); RefreshTerrainAttachments();
                undoMessage.Set("park.build_mode_controller__history.undid", action); UpdateConstructionHistoryUI(); return true;
            }
            finally { creatureWorld.RestoringSave = wasRestoring; }
        }
        private bool HandleConstructionUndoInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || GameInput.BlocksGameplayInput || GameSaveMenu.IsOpen || SettingsPresentation.IsOpen) return false;
            if (EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused == true) return false;
            if (!(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) || !keyboard.zKey.wasPressedThisFrame) return false;
            UndoLastConstruction(); return true;
        }
        private void UpdateConstructionHistoryUI()
        {
            if (undoConstructionButton == null) return;
            bool enabled = constructionHistory.Count > 0 && pendingConstruction == null && !GameSaveMenu.IsOpen && !SettingsPresentation.IsOpen && !GameInput.BlocksGameplayInput;
            undoConstructionButton.interactable = enabled;
            undoConstructionButton.GetComponentInChildren<Text>().text = Loc.Text("park.build_mode_controller__history.undo_construction_ctrl_z");
            UIHoverTooltip.Attach(undoConstructionButton, constructionHistory.Count > 0
                ? Loc.Format("park.build_mode_controller__history.undo", constructionHistory[constructionHistory.Count - 1], UndoMessage) : UndoMessage);
        }
    }
}

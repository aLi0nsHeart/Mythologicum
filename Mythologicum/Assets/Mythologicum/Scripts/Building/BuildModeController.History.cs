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
            public string label;
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
        private string pendingConstructionLabel;
        private bool pendingConstructionCharged;
        private double pendingConstructionTime;
        private Button undoConstructionButton;
        public int UndoCount => constructionHistory.Count;
        public string UndoMessage { get; private set; } = "Build something to enable construction undo.";

        private void InitializeConstructionHistory(Canvas canvas)
        {
            if (undoConstructionButton != null) return;
            undoConstructionButton = ManagementWidgets.Button(canvas.transform, "Undo construction", Vector2.zero, new Vector2(205, 32), () => UndoLastConstruction(), 16);
            var rect = (RectTransform)undoConstructionButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 62);
            UpdateConstructionHistoryUI();
        }
        private void BeginConstructionAction(string label)
        {
            if (pendingConstruction != null || grid == null || creatureWorld.RestoringSave) return;
            pendingConstruction = CaptureConstruction(); pendingConstructionLabel = label;
            pendingConstructionCharged = !economy.UnlimitedFunds; pendingConstructionTime = economy.ClockSeconds;
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
            { ResetConstructionHistory("Destructive changes clear construction undo history."); return; }
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
            UndoMessage = "Undo " + action.label + ". Creature activity, visitors and operating expenses continue normally.";
            UpdateConstructionHistoryUI();
        }
        private void ResetConstructionHistory(string reason = "Construction undo history starts fresh after loading a park.")
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
            reason = UndoMessage;
            if (constructionHistory.Count == 0 || pendingConstruction != null) return false;
            if (GameSaveMenu.IsOpen || SettingsPresentation.IsOpen || GameInput.BlocksGameplayInput)
            { reason = "Close the menu or finish changing a shortcut before undoing construction."; return false; }
            return ValidateConstructionUndo(constructionHistory[constructionHistory.Count - 1], out _, out reason);
        }
        private bool ValidateConstructionUndo(ConstructionUndo action, out SavedTerrain targetTerrain, out string reason)
        {
            targetTerrain = grid.Terrain?.Capture(); reason = "";
            foreach (var cell in action.paths) if (grid.ContentAt(cell) != BuildCellContent.Path)
            { reason = "A path in this action has changed. Undo cannot replace newer construction."; return false; }
            foreach (var cell in action.trees) if (!trees.Occupies(cell)) { reason = "A tree in this action was removed."; return false; }
            foreach (var cell in action.graves) if (!graves.Occupies(cell)) { reason = "A gravestone in this action was removed."; return false; }
            foreach (var cell in action.rocks) if (!rocks.Occupies(cell)) { reason = "A rock in this action was removed."; return false; }
            foreach (var edge in action.fences)
            {
                if (!fences.Layout.HasFence(edge) || !SameFence(action.after.fences[edge], new SavedFence
                    { kind = fences.Layout.KindAt(edge), gate = fences.Layout.IsGate(edge), health = fences.Layout.HealthAt(edge) }))
                { reason = "A fence in this action was damaged, repaired or changed. Undo leaves its current state intact."; return false; }
            }
            foreach (var building in action.buildings)
            {
                if (!buildingConstruction.Network.Contains(building)) { reason = "A building in this action was demolished."; return false; }
                if (building.HiredCount > 0 || building.Revenue > 0 || Progression?.ActiveProject(building) != null
                    || visitorWorld.Visitors.Any(guest => guest.Service == building)
                    || creatureWorld.Agents.Any(actor => actor.Alive && actor.SourceDispenser == building))
                { reason = "This building has personnel, customers, animals or active research. Use demolition when it is no longer in use."; return false; }
                if (building == buildingConstruction.Network.Entrance && buildingConstruction.Network.Records.Any(other => other != building))
                { reason = "Remove later construction before undoing the park entrance."; return false; }
            }
            if (action.cost > economy.Expenses) { reason = "The original construction transaction is no longer available."; return false; }
            var protectedCells = new HashSet<Vector2Int>(action.trees.Concat(action.graves).Concat(action.paths).Concat(action.rocks));
            foreach (var edge in action.fences) { protectedCells.Add(edge.CellA); protectedCells.Add(edge.CellB); }
            foreach (var building in action.buildings) protectedCells.UnionWith(building.Footprint.Cells());
            foreach (var actor in creatureWorld.Agents)
                if (actor.Alive && (protectedCells.Contains(actor.Cell)
                    || actor.SleepTree.HasValue && protectedCells.Contains(actor.SleepTree.Value)))
                { reason = "A creature or person is using this construction. Wait until the area is clear."; return false; }
            if (action.terrain.Count > 0)
            {
                if (targetTerrain == null || !action.terrain.TryRevert(targetTerrain.heights, out var heights))
                { reason = "The edited terrain has changed again. Undo will not overwrite the newer edit."; return false; }
                targetTerrain.heights = heights;
                var affected = new HashSet<Vector2Int>();
                foreach (int index in action.terrain.Nodes)
                {
                    int x = index % (grid.Width + 1), y = index / (grid.Width + 1);
                    for (int dx = -1; dx <= 0; dx++) for (int dy = -1; dy <= 0; dy++) affected.Add(new Vector2Int(x + dx, y + dy));
                }
                foreach (var actor in creatureWorld.Agents) if (actor.Alive && affected.Contains(actor.Cell))
                { reason = "A creature or person is standing on the edited ground. Wait until it is clear."; return false; }
                var check = new GridHeightField(grid.Width, grid.Depth, grid.CellSize); check.Restore(targetTerrain);
                foreach (var building in buildingConstruction.Network.Records)
                    if (!action.buildings.Contains(building) && !check.RegisterFoundation(building, building.Footprint))
                    { reason = "Undo would disturb an existing building foundation."; return false; }
                for (int x = 0; x < grid.Width; x++) for (int y = 0; y < grid.Depth; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (grid.ContentAt(cell) == BuildCellContent.Path && !action.paths.Contains(cell) && !check.Walkable(cell, true))
                    { reason = "Undo would make an existing path too steep."; return false; }
                }
            }
            return true;
        }

        public bool UndoLastConstruction()
        {
            if (!CanUndoLastConstruction(out string reason)) { UndoMessage = reason; UpdateConstructionHistoryUI(); return false; }
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
                UndoMessage = "Undid " + action.label + "."; UpdateConstructionHistoryUI(); return true;
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
            undoConstructionButton.GetComponentInChildren<Text>().text = "Undo construction (Ctrl+Z)";
            UIHoverTooltip.Attach(undoConstructionButton, constructionHistory.Count > 0
                ? "Undo " + constructionHistory[constructionHistory.Count - 1].label + ". " + UndoMessage : UndoMessage);
        }
    }
}

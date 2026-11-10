using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;

public static class ConstructionUndoNativeChecks
{
    public static int Run(BuildModeController controller)
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception("Construction undo: " + message); }
        T Field<T>(string name) => (T)typeof(BuildModeController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        void Invoke(string name, params object[] args) => typeof(BuildModeController).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, args);
        var grid = Field<SquareBuildGrid>("grid");
        var paths = Field<PathConstruction>("paths");
        var fences = Field<FenceConstruction>("fences");
        var building = Field<BuildingConstruction>("buildingConstruction");
        var creatures = Field<CreatureWorld>("creatureWorld");
        var economy = controller.Economy;
        var actor = creatures.Agents.FirstOrDefault(item => item.Alive);
        float hunger = actor?.Vitals.Hunger ?? 0;
        float beforeBalance = economy.Balance, beforeIncome = economy.Income;
        int beforePopulation = creatures.Population;
        Invoke("ResetConstructionHistory", "Native undo fixture.");
        Vector2Int cell = default;
        bool found = false;
        for (int y = grid.Depth - 4; y >= 20 && !found; y--)
            for (int x = grid.Width - 4; x >= 10 && !found; x--)
            {
                var candidate = new Vector2Int(x, y);
                if (!grid.IsAvailable(candidate) || !grid.GroundWalkable(candidate, true)
                    || creatures.Agents.Any(item => item.Alive && (item.Cell - candidate).sqrMagnitude < 25)) continue;
                cell = candidate; found = true;
            }
        Check(found, "fixture has a clear distant patch of ground");
        Invoke("BeginConstructionAction", "park.build_mode_controller.build_path_route");
        Check(paths.TryPlace(cell), "path placement succeeds");
        Invoke("CommitConstructionAction", 0f);
        var localeEntries = (System.Collections.Generic.Dictionary<string, string[]>)typeof(LocalizationTable)
            .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Loc.Table);
        string oldGerman = localeEntries["park.build_mode_controller.build_path_route"][(int)GameLanguage.GERMAN];
        var originalLanguage = Loc.Language;
        try
        {
            localeEntries["park.build_mode_controller.build_path_route"][(int)GameLanguage.GERMAN] = "Pfad bauen";
            Loc.SetLanguage(GameLanguage.GERMAN);
            Check(controller.UndoMessage.Contains("Pfad bauen"), "a previously recorded Undo action changes language without rebuilding its history");
        }
        finally
        {
            localeEntries["park.build_mode_controller.build_path_route"][(int)GameLanguage.GERMAN] = oldGerman;
            Loc.SetLanguage(originalLanguage);
        }
        Check(controller.UndoCount == 1 && controller.UndoLastConstruction(), "recorded path can be undone");
        Check(grid.ContentAt(cell) == BuildCellContent.Empty, "undo removes only the new path");
        Check(creatures.Population == beforePopulation && (actor == null || Math.Abs(actor.Vitals.Hunger - hunger) < .001f),
            "undo does not restore or reset creature simulation state");

        var edge = new FenceEdge(new Vector2Int(cell.x - 2, cell.y - 2), true);
        Check(fences.TryPlace(edge), "fixture creates an existing fence");
        Invoke("BeginConstructionAction", "native terrain stroke");
        float healthBefore = fences.Layout.HealthAt(edge);
        fences.Layout.TryDamage(edge, 1);
        Check(grid.Terrain.TryEdit(grid, new Vector2(cell.x + .5f, cell.y + .5f), 1f, .2f,
            TerrainBrushMode.Raise, 0, out _), "terrain stroke changes clear ground");
        Invoke("CommitConstructionAction", 0f);
        Check(controller.UndoLastConstruction(), "terrain stroke can be undone");
        Check(Math.Abs(fences.Layout.HealthAt(edge) - (healthBefore - 1)) < .001f,
            "combat damage occurring during the terrain stroke is preserved");
        Invoke("BeginConstructionAction", "native gate");
        Check(fences.TryPlaceGate(edge), "gate installation succeeds");
        Invoke("CommitConstructionAction", 0f);
        Check(controller.UndoLastConstruction() && fences.Layout.HasFence(edge) && !fences.Layout.IsGate(edge),
            "gate undo restores the preceding fence without deleting it");
        Check(Math.Abs(fences.Layout.HealthAt(edge) - (healthBefore - 1)) < .001f, "gate undo retains its preceding fence damage");

        var addedEdge = new FenceEdge(new Vector2Int(cell.x - 3, cell.y - 3), false);
        Invoke("BeginConstructionAction", "native new fence");
        Check(fences.TryPlace(addedEdge), "new fence placement succeeds");
        Invoke("CommitConstructionAction", 0f);
        fences.Layout.TryDamage(addedEdge, 1);
        Check(!controller.UndoLastConstruction() && fences.Layout.HasFence(addedEdge), "undo refuses to erase a subsequently damaged fence");
        fences.Layout.TryRepair(addedEdge, 1);
        Check(controller.UndoLastConstruction() && !fences.Layout.HasFence(addedEdge), "the unchanged fence can be undone after repair");

        var definition = BuildingCatalogDefinition.Current.buildings.First(item => item.kind == BuildingKind.Kiosk).Copy();
        definition.width = definition.depth = 1; definition.height = 1; definition.entranceColumn = 0;
        definition.visualPrefab = null; definition.useVisualCatalogFallback = false; definition.constructionCost = 100;
        var footprint = new BuildingFootprint(definition, new Vector2Int(cell.x - 1, cell.y + 1), 0);
        Check(building.CanPlace(footprint), "unused building has an available test footprint");
        Invoke("BeginConstructionAction", "native paid building");
        Check(building.TryPlace(footprint) && economy.TrySpend(100, ParkTransactionCategory.Construction), "paid building placement and purchase succeed");
        Invoke("CommitConstructionAction", 100f);
        Check(controller.UndoLastConstruction(), "unused paid building can be undone");
        Check(Math.Abs(economy.Balance - beforeBalance) < .01f && Math.Abs(economy.Income - beforeIncome) < .01f,
            "purchase reversal returns only the paid capital without recording income");
        Check(!building.Network.Records.Any(item => item.Footprint.Anchor == footprint.Anchor && ReferenceEquals(item.Footprint.Definition, definition)),
            "paid building undo removes its footprint and scene object");
        var restoringField = typeof(CreatureWorld).GetProperty("RestoringSave", BindingFlags.Instance | BindingFlags.NonPublic);
        bool restoring = (bool)restoringField.GetValue(creatures); restoringField.SetValue(creatures, true);
        try { fences.Layout.TryRemove(edge); fences.Refresh(); }
        finally { restoringField.SetValue(creatures, restoring); }
        Invoke("ResetConstructionHistory", "Native undo fixture complete.");
        return checks;
    }
}

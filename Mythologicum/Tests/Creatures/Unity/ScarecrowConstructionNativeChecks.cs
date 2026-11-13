using System;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Run in a disposable, prepared Play-mode park with a park entrance.</summary>
public static class ScarecrowConstructionNativeChecks
{
    public static int Run(BuildModeController park)
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new Exception("Scarecrow construction: " + reason); }
        T Field<T>(string name) => (T)typeof(BuildModeController).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(park);
        object Invoke(string name, params object[] args) => typeof(BuildModeController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(park, args);
        Check(Application.isPlaying && park != null && park.IsReady, "fixture is an actual prepared Play-mode park");
        var grid = Field<SquareBuildGrid>("grid");
        var construction = Field<ScarecrowConstruction>("scarecrows");
        var buildings = Field<BuildingConstruction>("buildingConstruction");
        var creatures = Field<CreatureWorld>("creatureWorld");
        var trees = Field<TreeConstruction>("trees");
        var graves = Field<GravestoneConstruction>("graves");
        var palette = Field<ConstructionPalette>("palette");
        Check(buildings.Network.HasEntrance, "fixture has its required main entrance");
        Vector2Int? selected = null;
        for (int z = 1; z < grid.Depth - 1 && !selected.HasValue; z++)
        for (int x = 1; x < grid.Width - 1 && !selected.HasValue; x++)
        {
            var cell = new Vector2Int(x, z);
            if (construction.CanPlace(cell) && grid.GroundWalkable(cell)
                && !creatures.Agents.Any(actor => actor.Alive && (actor.Cell - cell).sqrMagnitude < 16)) selected = cell;
        }
        Check(selected.HasValue, "fixture has clear enrichment ground away from actors");
        var target = selected.Value;
        Invoke("ResetConstructionHistory", "Scarecrow native fixture");
        float beforeBalance = park.Economy.Balance, beforeExpenses = park.Economy.Expenses;
        float cost = construction.Cost;
        bool unlimited = park.Economy.UnlimitedFunds;
        Check(park.Economy.CanAfford(cost), "fixture can afford the configured scarecrow price");
        palette.Refresh(true); palette.Toggle(ConstructionCategory.Enclosures);
        var button = palette.Root.GetComponentsInChildren<Button>(true)
            .Single(item => item.name == Loc.InternalName("enclosure.scarecrow.place"));
        Check(button.interactable, "Enclosures exposes the scarecrow construction tool");
        button.onClick.Invoke();
        Check(Field<object>("tool").ToString() == "Scarecrow", "the enclosure button selects the placement tool");
        Check((bool)Invoke("TryPlaceScarecrow", target), "normal construction action places the scarecrow");
        Check(construction.Occupies(target) && park.Environment.ScarecrowsAt(target) == 1 && grid.IsAvailable(target),
            "construction registers a walkable feeding target without blocking ground navigation");
        Check(!construction.CanPlace(target) && !trees.CanPlace(target) && !graves.CanPlace(target)
            && !(bool)Invoke("CanPlace", target), "all overlapping enrichment and path previews reject the occupied tile");
        Check(Mathf.Abs(park.Economy.Expenses - beforeExpenses - cost) < .01f
            && Mathf.Abs(park.Economy.Balance - beforeBalance + (unlimited ? 0 : cost)) < .01f,
            "construction records exactly one configured purchase");
        var saved = park.CapturePark();
        Check(saved.scarecrows.Contains(target), "save capture retains placed scarecrows");
        park.ValidateSave(saved);
        Check(park.UndoCount == 1, "placement records one construction Undo action");
        // End the placement tool before checking the normal Undo input guards.
        Invoke("SelectTool", Enum.Parse(typeof(BuildModeController).GetNestedType("Tool", BindingFlags.NonPublic), "None"));
        Check(park.UndoLastConstruction() && !construction.Occupies(target) && park.Environment.ScarecrowsAt(target) == 0,
            "Undo removes the placed feeding target through shared demolition");
        Check(Mathf.Abs(park.Economy.Balance - beforeBalance) < .01f && Mathf.Abs(park.Economy.Expenses - beforeExpenses) < .01f,
            "Undo refunds the original purchase and expense receipt");
        return checks;
    }
}

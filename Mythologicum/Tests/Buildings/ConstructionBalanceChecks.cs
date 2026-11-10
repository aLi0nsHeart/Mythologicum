using System;
using Mythologicum;
using UnityEngine;

public static class ConstructionBalanceChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Equal(float expected, float actual, string message)
        => Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .001f, message + ": " + actual);

    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 8, 8);
        var rules = new FenceRules();
        rules.wooden.maximumHealth = 45f;
        rules.wooden.allowsClimbing = false;
        rules.wooden.blocksPhasing = true;
        rules.heavy.maximumHealth = 25f;
        rules.heavy.resistsCreatureDamage = false;
        rules.heavy.allowsClimbing = true;
        var layout = new FenceLayout(grid, rules);
        var wood = new FenceEdge(new Vector2Int(2, 1), false);
        var heavy = new FenceEdge(new Vector2Int(3, 1), false);
        Check(layout.TryAdd(wood) && layout.TryAdd(heavy, FenceKind.Heavy), "Both tuned fence kinds build");
        Equal(45f, layout.MaximumHealthAt(wood), "Wood uses the configured health rather than its old constant");
        Check(!layout.CanClimb(wood) && !layout.CanPhase(wood), "Wood protection follows tunable capabilities");
        Check(layout.CanBreak(heavy) && layout.CanClimb(heavy), "Heavy type can be rebalanced independently of its enum name");
        Equal(10f, layout.TryDamage(heavy, 10f), "Tunable damage immunity changes actual damage");
        Equal(15f, layout.HealthAt(heavy), "Damage subtracts from configured health");
        Equal(10f, layout.TryRepair(heavy, 200f), "Repair clamps to the configured maximum");
        rules.heavy.resistsCreatureDamage = true;
        Equal(0f, layout.TryDamage(heavy, 100f), "Re-enabled protection stops creature attacks");
        Check(layout.RestoreHealth(heavy, 12f), "Restoring saved damage bypasses creature damage protection");
        Equal(12f, layout.HealthAt(heavy), "Immunity cannot erase saved damage");
        Check(layout.RestoreHealth(heavy, 500f), "Old saved health can exceed rebalanced maximum");
        Equal(25f, layout.HealthAt(heavy), "Restored health clamps safely to the new maximum");

        var entrance = new BuildingDefinition { kind = BuildingKind.ParkEntrance, admissionPrice = 23.5f };
        var kiosk = new BuildingDefinition { kind = BuildingKind.Kiosk, foodPrice = 11f };
        var rest = new BuildingDefinition { kind = BuildingKind.RestArea, restPrice = 4.5f };
        Equal(23.5f, Record(entrance).Price, "New entrance uses the catalogued starting price");
        Equal(11f, Record(kiosk).Price, "New kiosk still uses its catalogued meal price");
        Equal(4.5f, Record(rest).Price, "Rest facilities support a catalogued starting price");
        var staff = new BuildingDefinition { kind = BuildingKind.ZookeeperHut, staffCapacity = 2, operatingCostPerSecond = .2f,
            staffSalaryPerSecond = .3f };
        var employees = Record(staff); employees.HiredCount = 9;
        Check(employees.Capacity == 2 && employees.HiredCount == 2, "Employee capacity is specific to the building definition");
        Equal(.8f, employees.UpkeepPerSecond, "Configured capacity controls salaries through actual hired count");
        employees.RestoreEmployeeCount(6);
        Check(employees.HiredCount == 6 && employees.Capacity == 2, "Existing saved personnel remain counted above a reduced hiring limit");
        Equal(2f, employees.UpkeepPerSecond, "Existing staff above the new limit still receive their full salary");
        employees.HiredCount--;
        Check(employees.HiredCount == 5, "Dismissing one restored worker does not erase other staff above the limit");
        var copy = staff.Copy(); copy.staffCapacity = 7; copy.width = 4;
        Check(staff.staffCapacity == 2 && staff.width == 2, "Runtime catalog copies cannot overwrite source settings");
        copy.serviceSeconds = float.NaN; copy.staffCapacity = -1; copy.dispenseSearchSteps = 0; copy.Validate();
        Check(copy.serviceSeconds > 0f && copy.staffCapacity == 0 && copy.dispenseSearchSteps == 1,
            "Invalid inspector values sanitize without producing broken timing or search");

        var environment = new EnvironmentGrid(grid, .8f);
        var treeCell = new Vector2Int(1, 1); environment.AddTree(treeCell);
        Equal(3.2f, environment.Sample(new[] { treeCell }).ForestArea, "Environment statistics use configured forest contribution");
        environment.SetForestCoverage(treeCell, .4f);
        Equal(4f, environment.Sample(new[] { treeCell }).ForestArea, "Combined tree and ground coverage remains bounded to one tile");
        environment.RemoveTree(treeCell);
        Equal(1.6f, environment.Sample(new[] { treeCell }).ForestArea, "Demolition restores underlying forest coverage");

        var terrain = new GridHeightField(2, 2, 2f, uphillCostMultiplier: 2f);
        terrain.ImportHeights(new[] { 0f, 1f, 2f, 0f, 1f, 2f, 0f, 1f, 2f });
        var slopes = new SquareBuildGrid(Vector3.zero, 2f, 2, 2, terrain);
        float up = slopes.MovementCost(Vector2Int.zero, Vector2Int.right);
        float down = slopes.MovementCost(Vector2Int.right, Vector2Int.zero);
        Equal(1f, up - down, "Configured uphill penalty affects actual traversal cost");
        var restoredTerrain = new GridHeightField(2, 2, 2f); restoredTerrain.Restore(terrain.Capture());
        Equal(2f, restoredTerrain.UphillCostMultiplier, "Terrain save restoration preserves the chosen uphill cost");
        var legacyTerrain = terrain.Capture(); legacyTerrain.uphillCostVersion = 0; legacyTerrain.uphillCostMultiplier = 0;
        restoredTerrain.Restore(legacyTerrain);
        Equal(.25f, restoredTerrain.UphillCostMultiplier, "Old terrain saves retain the historical uphill penalty even when missing fields deserialize to zero");
        var zeroCostTerrain = new GridHeightField(2, 2, 2f, uphillCostMultiplier: 0);
        restoredTerrain.Restore(zeroCostTerrain.Capture());
        Equal(0f, restoredTerrain.UphillCostMultiplier, "An explicitly configured zero uphill penalty survives new save restoration");
        var brush = new TerrainBrushSettings { initialRadius = 8, minimumRadius = 2, maximumRadius = 5,
            initialStrength = 0, minimumStrength = .1f, maximumStrength = 3, strengthStep = float.NaN };
        brush.Validate();
        Equal(5f, brush.initialRadius, "Brush start radius is constrained to the configured range");
        Equal(.1f, brush.initialStrength, "Brush strength starts inside its configured range");
        Check(brush.strengthStep > 0f, "Brush increment cannot become invalid");
        Console.WriteLine("PASS: " + checks + " construction, fence and environment balance checks.");
    }

    private static ParkBuildingRecord Record(BuildingDefinition definition)
        => new ParkBuildingRecord(new BuildingFootprint(definition, Vector2Int.zero, 0));
}

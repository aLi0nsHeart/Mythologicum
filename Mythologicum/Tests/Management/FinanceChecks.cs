using System;
using System.IO;
using Mythologicum;
using UnityEngine;

public static class FinanceChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Equal(float expected, float actual, string message)
    { Check(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(expected - actual) < .002f, message + ": " + actual); }
    private static void Reject(Action action, string message)
    { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, message); }

    public static void Main()
    {
        var park = new ParkEconomy(5000);
        Equal(0, park.IncomePerMinute, "Starting funds do not masquerade as admission income");
        Equal(0, park.ExpensesPerMinute, "A new park has no fictional running expenses");
        park.Earn(30, ParkTransactionCategory.Admission);
        park.Earn(8, ParkTransactionCategory.Food);
        park.Earn(2, ParkTransactionCategory.Rest);
        Check(park.TrySpend(250, ParkTransactionCategory.Construction), "Construction investment is affordable");
        Check(park.TrySpend(50, ParkTransactionCategory.Hiring), "Hiring investment is affordable");
        Check(park.TrySpend(12, ParkTransactionCategory.Supplies), "Supply delivery is affordable");
        park.Tick(10, .1f, ParkTransactionCategory.BuildingUpkeep);
        park.Tick(10, .5f, ParkTransactionCategory.Salary);
        Equal(40, park.IncomePerMinute, "Recent income includes admission and completed services");
        Equal(318, park.ExpensesPerMinute, "Actual cash expenses include investments and operations");
        Equal(-278, park.ProfitPerMinute, "Cash profit includes all money spent");
        Equal(18, park.OperatingExpensesPerMinute, "Operating expenses isolate upkeep, salaries and supplies");
        Equal(22, park.OperatingProfitPerMinute, "Operating profit reveals whether the park itself covers ongoing costs");
        Equal(300, park.InvestmentExpensesPerMinute, "Building and hiring are separately visible investments");
        Equal(30, park.IncomeInWindow(ParkTransactionCategory.Admission), "Admission breakdown is independent of service revenue");
        Equal(8, park.IncomeInWindow(ParkTransactionCategory.Food), "Food breakdown records the actual meal price");
        Equal(1, park.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep), "Building base upkeep is attributed once");
        Equal(5, park.ExpensesInWindow(ParkTransactionCategory.Salary), "Wages are attributed once");
        Equal(4722, park.Balance, "Additional statistics do not alter actual cash arithmetic");
        Equal(40, park.Income, "Lifetime income stays cumulative");
        Equal(318, park.Expenses, "Lifetime expense semantics remain unchanged");

        park.Advance(30);
        park.Earn(15, ParkTransactionCategory.Admission);
        Equal(55, park.IncomePerMinute, "Purchases in separate seconds remain in the rolling window");
        park.Advance(29);
        Equal(55, park.IncomePerMinute, "The first transaction is retained before its full minute expires");
        park.Advance(1);
        Equal(15, park.IncomePerMinute, "Old admission leaves the window after sixty simulation seconds");
        Equal(0, park.ExpensesPerMinute, "Old expenses expire independently of lifetime accounts");
        Equal(55, park.Income, "A rolling-window expiry never erases lifetime revenue");
        Equal(318, park.Expenses, "A rolling-window expiry never refunds expenses");
        Equal(60, park.TrackedSeconds, "A full financial window is reported after a minute");
        park.Advance(30);
        Equal(0, park.IncomePerMinute, "Inactive parks eventually display zero recent revenue");

        var paused = new ParkEconomy();
        paused.Advance(12);
        paused.Earn(50, ParkTransactionCategory.Admission);
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            paused.Advance(invalid);
            Equal(12, paused.TrackedSeconds, "Paused or invalid time does not advance accounting");
            Equal(50, paused.IncomePerMinute, "Paused or invalid time does not expire transactions");
        }
        var warmup = new ParkEconomy();
        warmup.Advance(.01f); warmup.Earn(15, ParkTransactionCategory.Admission);
        Equal(15, warmup.IncomePerMinute, "First-frame admission is not extrapolated into an enormous per-minute estimate");
        Equal(.01f, warmup.TrackedSeconds, "First-minute reporting exposes its partial observation period");

        var busy = new ParkEconomy { UnlimitedFunds = true };
        for (int i = 0; i < 36000; i++)
        {
            busy.Advance(.01f);
            busy.Earn(.01f, ParkTransactionCategory.Food);
            busy.Tick(.01f, .1f, ParkTransactionCategory.Salary);
        }
        var history = busy.CaptureFinance();
        Check(history.buckets.Count <= 61, "Long sessions retain a bounded number of history buckets");
        Check(busy.IncomePerMinute > 58 && busy.IncomePerMinute <= 61, "Continuous small transactions represent approximately the last minute");
        Check(busy.ExpensesPerMinute > 5.8f && busy.ExpensesPerMinute <= 6.1f, "Frame-by-frame salaries remain visible in the recent window");
        ParkEconomy.ValidateSnapshot(history);
        Check(true, "A naturally produced long-session snapshot validates");
        Equal(5000, busy.Balance, "Infinite funds preserve the bank balance while reporting finances");

        var restored = new ParkEconomy();
        restored.Restore(busy.Balance, busy.Income, busy.Expenses, busy.UnlimitedFunds);
        Equal(0, restored.IncomePerMinute, "Loading an old save starts a fresh window without inventing income");
        restored.RestoreFinance(history);
        Equal(busy.IncomePerMinute, restored.IncomePerMinute, "Saving and loading preserves recent revenue");
        Equal(busy.ExpensesPerMinute, restored.ExpensesPerMinute, "Saving and loading preserves recent expenditure");
        Equal(busy.Income, restored.Income, "Restoring financial history does not credit old sales again");
        Equal(busy.Expenses, restored.Expenses, "Restoring financial history does not charge old costs again");
        history.buckets[0].income[0] = 10000;
        Equal(busy.IncomePerMinute, restored.IncomePerMinute, "Restored financial history owns a deep copy");
        var isolated = restored.CaptureFinance(); isolated.buckets[0].expenses[0] = 20000;
        Equal(busy.ExpensesPerMinute, restored.ExpensesPerMinute, "Captured snapshots cannot mutate live financial history");
        restored.RestoreFinance(null);
        Equal(0, restored.IncomePerMinute, "Null financial history remains compatible with legacy saves");

        Reject(() => ParkEconomy.ValidateSnapshot(new FinanceSnapshot { trackedSeconds = float.NaN }), "Non-finite saved window age is rejected");
        Reject(() => ParkEconomy.ValidateSnapshot(new FinanceSnapshot { trackedSeconds = 61 }), "Financial windows longer than sixty seconds are rejected");
        Reject(() => ParkEconomy.ValidateSnapshot(new FinanceSnapshot { buckets = null }), "A missing saved bucket list is rejected");
        var bad = new FinanceSnapshot { trackedSeconds = 10 };
        bad.buckets.Add(new FinanceWindowBucket { age = 3, income = new double[1] });
        Reject(() => ParkEconomy.ValidateSnapshot(bad), "Malformed saved category arrays are rejected");
        bad.buckets[0].income = new double[ParkEconomy.CategoryCount]; bad.buckets[0].income[0] = double.NaN;
        Reject(() => ParkEconomy.ValidateSnapshot(bad), "Non-finite saved transaction values are rejected");
        bad.buckets[0].income[0] = -1;
        Reject(() => ParkEconomy.ValidateSnapshot(bad), "Negative saved transaction values are rejected");
        bad.buckets[0].income[0] = 1; bad.buckets[0].age = 60;
        Reject(() => ParkEconomy.ValidateSnapshot(bad), "Expired buckets cannot be restored into current figures");
        bad.buckets[0].age = 1; bad.buckets.Add(new FinanceWindowBucket { age = 2 });
        Reject(() => ParkEconomy.ValidateSnapshot(bad), "Out-of-order financial buckets are rejected");
        restored.Earn(20, ParkTransactionCategory.Admission);
        Reject(() => restored.RestoreFinance(bad), "A malformed restore rejects before altering live history");
        Equal(20, restored.IncomePerMinute, "A failed financial restore leaves the original window intact");

        var extreme = new ParkEconomy(float.MaxValue) { UnlimitedFunds = true };
        extreme.Earn(float.MaxValue, ParkTransactionCategory.Admission);
        extreme.Earn(float.MaxValue, ParkTransactionCategory.Admission);
        extreme.Tick(float.MaxValue, float.MaxValue, ParkTransactionCategory.Salary);
        Equal(float.MaxValue, extreme.IncomePerMinute, "Extreme recent revenue remains finite");
        Equal(float.MaxValue, extreme.ExpensesPerMinute, "Extreme recent costs remain finite");
        ParkEconomy.ValidateSnapshot(extreme.CaptureFinance());
        Check(true, "Extreme bounded history is still a valid save");
        extreme.Advance(float.MaxValue);
        Equal(0, extreme.IncomePerMinute, "A large simulation jump correctly clears expired accounting history");
        extreme.Earn(10, (ParkTransactionCategory)999);
        Equal(10, extreme.IncomeInWindow(ParkTransactionCategory.Other), "Unknown categories safely retain money in the Other breakdown");

        var grid = new SquareBuildGrid(Vector3.zero, 2, 12, 12);
        var network = new ParkBuildingNetwork(grid, new FenceLayout(grid));
        var entrance = network.TryPlace(new BuildingFootprint(new BuildingDefinition
            { kind = BuildingKind.ParkEntrance, width = 1, depth = 1, operatingCostPerSecond = .2f }, new Vector2Int(1, 1), 0));
        var workplace = network.TryPlace(new BuildingFootprint(new BuildingDefinition
            { kind = BuildingKind.ResearchCentre, width = 1, depth = 1, operatingCostPerSecond = .3f, staffSalaryPerSecond = .5f }, new Vector2Int(4, 1), 0));
        workplace.HiredCount = 2;
        var finances = new ParkEconomy(100);
        finances.Advance(10); BuildingEconomy.Tick(network, finances, 10);
        Equal(5, finances.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep), "Each building's base upkeep enters the dedicated breakdown");
        Equal(10, finances.ExpensesInWindow(ParkTransactionCategory.Salary), "Employee salaries enter their dedicated breakdown");
        Equal(15, finances.ExpensesPerMinute, "Splitting cost categories does not double-charge the park");
        Equal(85, finances.Balance, "Categorized building accounts preserve expected bank balance");
        Equal(13, workplace.Expenses, "Workplace history still records its complete local expense");
        Equal(10, finances.TrackedSeconds, "Building count never advances the financial clock more than once");
        Console.WriteLine("PASS: " + checks + " rolling finance, categorization, save validation and building accounting checks.");
    }
}

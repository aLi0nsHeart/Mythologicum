using System;
using Mythologicum;

public static class ConstructionUndoChecks
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Equal(float expected, float actual, string message) => Check(Math.Abs(expected - actual) < .001f, message + ": " + actual);
    public static void Main()
    {
        var patch = new TerrainUndoPatch(new[] { 0f, 1f, 2f, 3f }, new[] { 0f, 4f, 2f, 5f });
        Check(patch.Count == 2, "Only modified terrain nodes enter the journal");
        Check(patch.TryRevert(new[] { 8f, 4f, 9f, 5f }, out var terrain), "Unchanged edited nodes can be reverted");
        Check(terrain[0] == 8 && terrain[1] == 1 && terrain[2] == 9 && terrain[3] == 3,
            "Undo restores edited nodes while preserving unrelated later terrain changes");
        Check(!patch.TryRevert(new[] { 0f, 6f, 2f, 5f }, out _), "A newer change to a touched node blocks undo");
        var cash = new ParkEconomy(1000);
        double purchase = cash.ClockSeconds;
        Check(cash.TrySpend(200, ParkTransactionCategory.Construction), "Original construction purchase succeeds");
        cash.Advance(3); cash.Earn(50, ParkTransactionCategory.Admission); cash.Tick(3, 2);
        Check(cash.RefundPurchase(200, ParkTransactionCategory.Construction, true, purchase), "Undo can reverse its own purchase");
        Equal(1044, cash.Balance, "Only construction capital returns; admission and upkeep remain");
        Equal(50, cash.Income, "Returned purchase capital is not classified as income");
        Equal(6, cash.Expenses, "Only the cancelled purchase leaves lifetime expenses");
        Equal(0, cash.ExpensesInWindow(ParkTransactionCategory.Construction), "Undo removes the purchase from its original rolling bucket");
        Equal(6, cash.ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep), "Operating expenses remain recorded");
        var aged = new ParkEconomy(1000); purchase = aged.ClockSeconds;
        aged.TrySpend(100, ParkTransactionCategory.Construction); aged.Advance(65); aged.TrySpend(30, ParkTransactionCategory.Construction);
        aged.RefundPurchase(100, ParkTransactionCategory.Construction, true, purchase);
        Equal(30, aged.ExpensesInWindow(ParkTransactionCategory.Construction), "Expired purchase refunds never erase a newer purchase from reports");
        var unlimited = new ParkEconomy(500) { UnlimitedFunds = true };
        purchase = unlimited.ClockSeconds; unlimited.TrySpend(100, ParkTransactionCategory.Construction); unlimited.UnlimitedFunds = false;
        unlimited.RefundPurchase(100, ParkTransactionCategory.Construction, false, purchase);
        Equal(500, unlimited.Balance, "An originally unlimited-funds purchase does not mint cash after changing funds mode");
        Equal(0, unlimited.Expenses, "Unlimited-funds undo still reverses purchase accounting");
        var buckets = new ParkEconomy(500);
        buckets.TrySpend(10, ParkTransactionCategory.Construction); buckets.Advance(1);
        buckets.TrySpend(20, ParkTransactionCategory.Construction);
        var receipt = buckets.CurrentExpenseBucket;
        Check(buckets.RefundPurchase(20, ParkTransactionCategory.Construction, true, buckets.ClockSeconds, receipt),
            "A purchase receipt can be reversed at an exact bucket boundary");
        Equal(10, buckets.ExpensesInWindow(ParkTransactionCategory.Construction), "Exact receipts preserve earlier purchases in adjacent buckets");
        Console.WriteLine("PASS: " + checks + " construction undo and purchase reversal checks.");
    }
}

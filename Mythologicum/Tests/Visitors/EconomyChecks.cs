using System;
using Mythologicum;

public static class EconomyChecks
{
    private static int checks;
    private static void Equal(float expected, float actual, string message)
    {
        checks++;
        if (float.IsNaN(actual) || float.IsInfinity(actual) || Math.Abs(expected - actual) > .001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    private static void Assert(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        var economy = new ParkEconomy();
        Equal(5000, economy.Balance, "Starting funds");
        Equal(0, economy.Income, "Starting money is not income");
        Assert(economy.CanAfford(5000), "Exact balance affordable");
        Assert(!economy.CanAfford(5001), "Over balance unaffordable");
        Assert(economy.TrySpend(250), "Building purchase succeeds");
        Equal(4750, economy.Balance, "Construction deducts balance");
        Equal(250, economy.Expenses, "Construction logged");
        Assert(!economy.TrySpend(5000), "Overspending rejected");
        Equal(4750, economy.Balance, "Rejected purchase leaves balance");
        Equal(250, economy.Expenses, "Rejected purchase leaves expenses");
        economy.Earn(8);
        Equal(4758, economy.Balance, "Kiosk income credited");
        Equal(8, economy.Income, "Kiosk income logged");
        economy.Tick(10, .1f);
        Equal(4757, economy.Balance, "Operating cost uses time");
        Equal(251, economy.Expenses, "Operating cost logged");

        var debt = new ParkEconomy(5);
        debt.Tick(10, 1);
        Equal(-5, debt.Balance, "Running costs permit debt");
        Assert(!debt.TrySpend(1), "Purchases blocked while in debt");
        Assert(debt.TrySpend(0), "Free actions permitted in debt");
        debt.Earn(10);
        Equal(5, debt.Balance, "Income repays debt");

        var sandbox = new ParkEconomy(100) { UnlimitedFunds = true };
        Assert(sandbox.TrySpend(1000), "Unlimited funds ignore affordability");
        sandbox.Earn(8); sandbox.Tick(100, 2);
        Equal(100, sandbox.Balance, "Unlimited transactions keep balance");
        Equal(1200, sandbox.Expenses, "Unlimited expenses still visible");
        Equal(8, sandbox.Income, "Unlimited income still visible");
        sandbox.UnlimitedFunds = false;
        Assert(!sandbox.TrySpend(101), "Disabling unlimited restores limit");

        var safe = new ParkEconomy(100);
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert(!safe.CanAfford(invalid), "Invalid amount not affordable");
            Assert(!safe.TrySpend(invalid), "Invalid spend rejected");
            safe.Earn(invalid); safe.Tick(invalid, 1); safe.Tick(1, invalid);
            Equal(100, safe.Balance, "Invalid operations leave balance");
            Equal(0, safe.Expenses, "Invalid operations leave expenses");
            Equal(0, safe.Income, "Invalid income ignored");
            Equal(0, new ParkEconomy(invalid).Balance, "Invalid starting funds sanitized");
        }
        safe.Tick(0, 100); safe.Earn(0); safe.TrySpend(0);
        Equal(100, safe.Balance, "Zero operations are no-ops");
        safe.UnlimitedFunds = true;
        Assert(!safe.TrySpend(float.NaN), "Unlimited does not accept invalid transactions");
        var one = new ParkEconomy(100);
        var many = new ParkEconomy(100);
        one.Tick(10, .1f);
        for (int i = 0; i < 100; i++) many.Tick(.1f, .1f);
        Equal(one.Balance, many.Balance, "Operating costs independent of frame frequency");
        var huge = new ParkEconomy(float.MaxValue);
        huge.Earn(float.MaxValue);
        Equal(float.MaxValue, huge.Balance, "Extreme income cannot overflow");
        huge.Tick(float.MaxValue, float.MaxValue);
        Equal(-float.MaxValue, huge.Balance, "Extreme debt remains finite");
        Equal(float.MaxValue, huge.Expenses, "Extreme expenses remain finite");
        Console.WriteLine("PASS: " + checks + " economy checks.");
    }
}

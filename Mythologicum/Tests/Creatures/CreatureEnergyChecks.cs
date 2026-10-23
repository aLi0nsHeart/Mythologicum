using System;
using Mythologicum;

public static class CreatureEnergyChecks
{
    private static int checks;

    private static void Equal(float expected, float actual, string message)
    {
        checks++;
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > .001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    public static void Main()
    {
        var energy = new CreatureEnergy(100);
        Equal(100, energy.Energy, "Starts rested");
        Equal(0, energy.Exhaustion, "No exhaustion at full reserve");
        energy.Tick(10, .35f, 1, false, 8);
        Equal(96.5f, energy.Energy, "Awake baseline drain");
        energy.Tick(10, .35f, 3, false, 8);
        Equal(86, energy.Energy, "Hunting uses three times baseline energy");
        energy.Tick(10, .35f, 2, false, 8);
        Equal(79, energy.Energy, "Socializing uses twice baseline energy");
        energy.Spend(5);
        Equal(74, energy.Energy, "Climb cost");
        Equal(26, energy.Exhaustion, "Exhaustion is inverse reserve");
        energy.Tick(2, .35f, 3, true, 8);
        Equal(90, energy.Energy, "Sleep restores without concurrent drain");
        energy.Tick(100, 100, 3, true, 8);
        Equal(100, energy.Energy, "Recovery capped");
        energy.Tick(100, 100, 3, false, 8);
        Equal(0, energy.Energy, "Drain capped");
        Equal(100, energy.Exhaustion, "Fully exhausted");
        energy.Tick(1, 1, 1, true, 8);
        Equal(8, energy.Energy, "Can recover from zero");
        energy.Spend(1000);
        Equal(0, energy.Energy, "Oversized cost clamped");
        Equal(0, new CreatureEnergy(-5).Energy, "Negative initial energy clamped");
        Equal(100, new CreatureEnergy(150).Energy, "Excess initial energy clamped");
        Equal(0, new CreatureEnergy(float.NaN).Energy, "Invalid initial energy safe");

        var safe = new CreatureEnergy(50);
        foreach (var dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            safe.Tick(dt, 10, 3, false, 8);
            Equal(50, safe.Energy, "Invalid or reversed time ignored");
        }
        foreach (var rate in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            safe.Tick(1, rate, 3, false, 8);
            Equal(50, safe.Energy, "Invalid drain ignored");
            safe.Tick(1, 3, rate, false, 8);
            Equal(50, safe.Energy, "Invalid multiplier ignored");
            safe.Tick(1, 3, 1, true, rate);
            Equal(50, safe.Energy, "Invalid recovery ignored");
            safe.Spend(rate);
            Equal(50, safe.Energy, "Invalid cost ignored");
        }
        safe.Tick(float.MaxValue, float.MaxValue, float.MaxValue, false, 8);
        Equal(0, safe.Energy, "Extreme finite drain cannot overflow reserve");
        safe.Tick(float.MaxValue, 1, 1, true, float.MaxValue);
        Equal(100, safe.Energy, "Extreme finite recovery cannot overflow reserve");

        var one = new CreatureEnergy(100);
        var many = new CreatureEnergy(100);
        one.Tick(10, .35f, 3, false, 8);
        for (int i = 0; i < 100; i++) many.Tick(.1f, .35f, 3, false, 8);
        Equal(one.Energy, many.Energy, "Drain independent of update frequency");
        one.Tick(1, .35f, 1, true, 8);
        for (int i = 0; i < 10; i++) many.Tick(.1f, .35f, 1, true, 8);
        Equal(one.Energy, many.Energy, "Recovery independent of update frequency");
        Console.WriteLine("PASS: " + checks + " creature energy checks.");
    }
}

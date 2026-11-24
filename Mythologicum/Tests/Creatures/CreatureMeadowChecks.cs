using System;
using Mythologicum;

public static class CreatureMeadowChecks
{
    private static int checks;
    private static void Equal(float expected, float value, string message)
    {
        checks++;
        if (float.IsNaN(value) || Math.Abs(expected - value) > .001f)
            throw new Exception(message + ": expected " + expected + ", got " + value);
    }

    private static CreatureNeedScores Meadow(float open, int trees = 1, float required = .7f)
        => CreatureWellbeing.Evaluate(0, 0, 100, trees, 1, 100, 1, 1, 4,
            nearbyOpenGroundFraction: open, minimumOpenGroundFraction: required);

    public static void Main()
    {
        Equal(100, Meadow(.7f).Environment, "Meeting meadow and tree needs completes habitat satisfaction");
        Equal(100, Meadow(1).Environment, "Extra open ground cannot exceed full satisfaction");
        Equal(75, Meadow(.35f).Environment, "Half the required meadow plus shade averages the two needs");
        Equal(50, Meadow(0).Environment, "Trees alone cannot replace missing meadow");
        Equal(50, Meadow(1, trees: 0).Environment, "Meadow alone cannot replace missing shade trees");
        Equal(0, Meadow(0, trees: 0).Environment, "Both missing habitat requirements are unsatisfied");
        Equal(50, Meadow(float.NaN).Environment, "Invalid measured meadow cannot corrupt need scores");
        Equal(100, Meadow(float.PositiveInfinity).Environment, "Positive overflow is clamped to full meadow");
        Equal(50, Meadow(float.NegativeInfinity).Environment, "Negative overflow is clamped to empty meadow");
        Equal(100, Meadow(1, required: float.PositiveInfinity).Environment, "Invalidly high requirement is capped at full ground");

        var random = new Random(45193);
        for (int i = 0; i < 1000; i++)
        {
            float hunger = random.Next(101), fear = random.Next(101), area = random.Next(300), neededArea = random.Next(300);
            int trees = random.Next(10), requiredTrees = random.Next(10), companions = random.Next(15), minimum = random.Next(5), maximum = random.Next(5, 10);
            var legacy = CreatureWellbeing.Evaluate(hunger, fear, area, trees, companions, neededArea, requiredTrees, minimum, maximum);
            foreach (float disabled in new[] { 0f, -1f, float.NaN })
            {
                var unchanged = CreatureWellbeing.Evaluate(hunger, fear, area, trees, companions, neededArea, requiredTrees, minimum, maximum,
                    nearbyOpenGroundFraction: 0, minimumOpenGroundFraction: disabled);
                Equal(legacy.Environment, unchanged.Environment, "Disabled meadow need preserves legacy habitat");
                Equal(legacy.Happiness, unchanged.Happiness, "Disabled meadow need preserves legacy happiness");
            }
        }
        Console.WriteLine("PASS: " + checks + " meadow need, habitat independence and legacy compatibility checks.");
    }
}

using System;
using Mythologicum;

public static class CreatureModelChecks
{
    static int checks;
    static void Equal(float expected, float actual, string message)
    {
        checks++;
        if (Math.Abs(expected - actual) > 0.0001f || float.IsNaN(actual))
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    public static void Main()
    {
        var vitals = new CreatureVitals(35f);
        Equal(35f, vitals.Hunger, "Initial hunger");
        Equal(0f, vitals.Fear, "New creatures calm");
        vitals.Tick(2f, 2f, 50f, 15f, true);
        Equal(39f, vitals.Hunger, "Elapsed-time hunger");
        Equal(100f, vitals.Fear, "Threat increases fear");
        vitals.Tick(2f, 2f, 50f, 15f, false);
        Equal(70f, vitals.Fear, "Threat gone fear recovers");
        vitals.Eat(60f);
        Equal(0f, vitals.Hunger, "Food clamps hunger at zero");
        vitals.Tick(100f, 2f, 50f, 15f, true);
        Equal(100f, vitals.Hunger, "Hunger capped");
        Equal(100f, vitals.Fear, "Fear capped");
        vitals.Tick(100f, 2f, 50f, 15f, false);
        Equal(0f, vitals.Fear, "Recovery capped at zero");
        vitals.Eat(-50f);
        Equal(100f, vitals.Hunger, "Negative food cannot increase hunger");
        vitals.Tick(-1f, 2f, 50f, 15f, true);
        Equal(0f, vitals.Fear, "Negative time ignored");
        vitals.Tick(float.NaN, 2f, 50f, 15f, true);
        Equal(100f, vitals.Hunger, "Invalid time cannot corrupt hunger");
        var oneStep = new CreatureVitals(0f);
        var manySteps = new CreatureVitals(0f);
        oneStep.Tick(1f, 2f, 50f, 15f, true);
        for (int i = 0; i < 10; i++) manySteps.Tick(.1f, 2f, 50f, 15f, true);
        Equal(oneStep.Hunger, manySteps.Hunger, "Hunger independent of update frequency");
        Equal(oneStep.Fear, manySteps.Fear, "Fear independent of update frequency");
        Equal(0f, new CreatureVitals(-1f).Hunger, "Initial lower bound");
        Equal(100f, new CreatureVitals(150f).Hunger, "Initial upper bound");
        Equal(0f, new CreatureVitals(float.NaN).Hunger, "Invalid initial hunger sanitized");
        if (((CreatureTags.Goat | CreatureTags.Visitor) & CreatureTags.Goat) == 0)
            throw new Exception("Prey masks must match individual categories");
        Console.WriteLine("PASS: " + checks + " creature vitals checks and prey tag mask.");
    }
}

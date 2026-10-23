using System;
using Mythologicum;

public static class CreatureWellbeingChecks
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
        var full = CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0);
        Equal(100, full.Happiness, "All needs met");
        var empty = CreatureWellbeing.Evaluate(100, 100, 0, 0, 0, 100, 2, 2, 8);
        Equal(0, empty.Happiness, "All needs unmet");
        var partial = CreatureWellbeing.Evaluate(50, 50, 50, 1, 1, 100, 2, 2, 8);
        Equal(50, partial.Environment, "Half trees");
        Equal(50, partial.Space, "Half space");
        Equal(50, partial.Herd, "Half companions");
        Equal(50, partial.Happiness, "Normalized weighted happiness");
        var alone = CreatureWellbeing.Evaluate(0, 0, 100, 2, 1, 100, 2, 0, 0);
        Equal(0, alone.Herd, "Solitary predator dislikes one companion");
        Equal(85, alone.Happiness, "Herd contributes fifteen percent");
        var crowded = CreatureWellbeing.Evaluate(0, 0, 48, 0, 12, 48, 0, 2, 8);
        Equal(50, crowded.Herd, "Overcrowding penalizes herd");
        Equal(100, CreatureWellbeing.Evaluate(0, 0, 0, 0, 999, 0, 0, 0, -1).Happiness,
            "No space, tree or herd restrictions");
        Equal(100, CreatureWellbeing.Evaluate(-10, -10, 500, 50, 0, 100, 2, 0, 0).Happiness,
            "Excess environment cannot exceed full happiness");
        var model = new CreatureWellbeing();
        Equal(75, model.Happiness, "Initial happiness");
        model.Update(empty, 2, 4, 50);
        Equal(67, model.Happiness, "Gradual happiness drop");
        Equal(0, model.Agitation, "Calm above threshold");
        model.Update(empty, 10, 4, 50);
        Equal(27, model.Happiness, "Continued deficit");
        Equal(.46f, model.Agitation, "Agitation scales with deficit");
        model.Update(empty, 100, 4, 50);
        Equal(0, model.Happiness, "No undershoot");
        Equal(1, model.Agitation, "Maximum agitation");
        model.Update(full, 100, 4, 50);
        Equal(100, model.Happiness, "No overshoot");
        Equal(0, model.Agitation, "Recovery calms creature");
        model.Update(empty, float.NaN, 4, 50);
        Equal(100, model.Happiness, "Invalid time ignored");
        model.Update(empty, -1, 4, 50);
        Equal(100, model.Happiness, "Negative time ignored");
        model.Update(empty, 1, -10, 50);
        Equal(100, model.Happiness, "Negative rate ignored");
        model.Update(empty, 100, 4, 0);
        Equal(0, model.Agitation, "Zero agitation threshold safe");
        var one = new CreatureWellbeing();
        var many = new CreatureWellbeing();
        one.Update(empty, 5, 4, 50);
        for (int i = 0; i < 50; i++) many.Update(empty, .1f, 4, 50);
        Equal(one.Happiness, many.Happiness, "Update frequency independent");
        Console.WriteLine("PASS: " + checks + " creature wellbeing checks.");
    }
}

using System;
using Mythologicum;

/// <summary>Changes data independently of defaults and verifies actual decisions/need results.</summary>
public static class CreatureBalanceChecks
{
    static int checks;
    static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Equal(float expected, float value, string message)
        => Check(!float.IsNaN(value) && Math.Abs(expected - value) < .001f, message + ": " + value);

    public static void Main()
    {
        var weights = new CreatureBehaviourSettings
        { hungerWeight = 4, fearWeight = 0, environmentWeight = 0, spaceWeight = 0, herdWeight = 0 };
        var scores = CreatureWellbeing.Evaluate(20, 100, 0, 0, 0, 100, 10, 3, 3, weights);
        Equal(80, scores.Happiness, "Hunger-only happiness ignores unmet habitat/herd/fear needs");
        weights.hungerWeight = 0; weights.herdWeight = 9;
        scores = CreatureWellbeing.Evaluate(0, 0, 100, 2, 1, 100, 2, 2, 5, weights);
        Equal(50, scores.Happiness, "Herd-only happiness uses adjustable relative weight");
        weights.hungerWeight = 9;
        scores = CreatureWellbeing.Evaluate(0, 100, 0, 0, 1, 100, 2, 2, 5, weights);
        Equal(75, scores.Happiness, "Relative weights normalize automatically");
        weights.hungerWeight = -1; weights.herdWeight = float.PositiveInfinity;
        Equal(100, CreatureWellbeing.Evaluate(100, 100, 0, 0, 0, 100, 2, 2, 5, weights).Happiness,
            "Invalid or all-disabled weights cannot corrupt happiness");
        Equal(23, new CreatureWellbeing(23).Happiness, "New creature initial happiness is adjustable");
        Equal(50, CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, -1,
            nearbyGravestones: 0, requiredGravestones: 4).Environment,
            "Meeting only the tree requirement leaves a combined habitat half satisfied");
        Equal(50, CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 100, 2, 0, -1,
            nearbyGravestones: 4, requiredGravestones: 4).Environment,
            "Meeting only the grave requirement does not ignore the trees");
        Equal(100, CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, -1,
            nearbyGravestones: 4, requiredGravestones: 4).Environment,
            "Meeting both requirements completes combined habitat satisfaction");
        Equal(50, CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 100, 0, 0, -1,
            nearbyGravestones: 2, requiredGravestones: 4).Environment,
            "Grave-only habitat keeps the previous proportional score");

        float escape = CreatureEscape.Chance(60, 0, 0, 0, .8f, 0, 0, 20, 1);
        Equal(.4f, escape, "Custom hunger start changes escape pressure");
        Equal(.2f, CreatureEscape.Chance(100, 1, 1, .5f, 1, 1, 1, 20, .2f),
            "Custom total escape chance cap applies after all pressure sources");
        Equal(0, CreatureEscape.Chance(99, 0, 0, 0, 1, 0, 0, 100, 1),
            "A full hunger threshold only activates at maximum hunger");
        Equal(1, CreatureEscape.Chance(100, 0, 0, 0, 1, 0, 0, 100, 1),
            "Full hunger threshold avoids division by zero");

        var exploration = new CreatureExploration();
        Check(exploration.WillClimb(45, 50, 90, false, 40, 5), "Lower urgent hunger overrides initial caution");
        Check(!exploration.WillClimb(45, 50, 90, false, 60, 5), "Higher urgent hunger preserves initial caution");
        Check(exploration.WillClimb(45, 0, 15, false, 80, 20), "Higher urgent energy seeks shelter sooner");
        Check(!exploration.WillClimb(45, 0, 15, false, 80, 10), "Lower urgent energy keeps a rested creature cautious");

        Console.WriteLine("PASS: " + checks + " creature balance data checks.");
    }
}

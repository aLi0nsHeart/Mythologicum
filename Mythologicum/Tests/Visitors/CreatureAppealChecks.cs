using System;
using Mythologicum;

/// <summary>Profile interest data is independent of Unity, real danger and visitor runtime state.</summary>
public static class CreatureAppealChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static void Equal(float expected, float actual, string message)
        => Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .00001f, message);

    public static void Main()
    {
        var appeal = new CreatureVisitorAppeal();
        Equal(.5f, appeal.thrill, "Existing or custom profiles have neutral thrill appeal");
        Equal(.5f, appeal.wonder, "Existing or custom profiles have neutral wonder appeal");
        Equal(.5f, appeal.adventure, "Existing or custom profiles have neutral adventure appeal");
        Equal(.5f, appeal.children, "Existing or custom profiles have neutral child appeal");
        foreach (float invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            Equal(.5f, CreatureVisitorAppeal.Normalize(invalid), "Unsupported appeal values fall back to neutral interest");
        Equal(0, CreatureVisitorAppeal.Normalize(-1), "Appeal below zero clamps to no interest");
        Equal(1, CreatureVisitorAppeal.Normalize(2), "Appeal above one clamps to strongest interest");
        var random = new Random(527231);
        for (int i = 0; i < 1500; i++)
        {
            float value = (float)(random.NextDouble() * 4 - 1.5);
            float normalized = CreatureVisitorAppeal.Normalize(value);
            Check(normalized >= 0 && normalized <= 1, "All finite data stays within the interest domain");
            Equal(normalized, CreatureVisitorAppeal.Normalize(normalized), "Validation is idempotent");
            if (value >= 0 && value <= 1) Equal(value, normalized, "Valid artist-assigned interest is preserved exactly");
        }
        appeal.thrill = float.NaN; appeal.wonder = 1.5f; appeal.adventure = -.5f; appeal.children = .37f;
        appeal.Validate();
        Equal(.5f, appeal.thrill, "Inspector validation fixes invalid thrill interest");
        Equal(1, appeal.wonder, "Inspector validation clamps wonder interest");
        Equal(0, appeal.adventure, "Inspector validation clamps adventure interest");
        Equal(.37f, appeal.children, "One visitor group's edits do not change another group's valid setting");
        Console.WriteLine("PASS: " + checks + " creature exhibit appeal defaults and numeric-safety checks.");
    }
}

using System;
using Mythologicum;

public static class VisitorModelChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); checks++; }
    private static void Equal(float expected, float actual, string message) =>
        Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .001f, message + ": " + actual);
    private static void Tick(VisitorMood mood, float seconds, float hunger = 20, float energy = 80,
        float fear = 0, bool watching = false, bool receivingService = false, float minimum = 30, float maximum = 300)
    {
        for (float time = 0; time < seconds; time += .5f)
            mood.Tick(.5f, hunger, energy, fear, watching, receivingService, minimum, maximum);
    }

    public static void Main()
    {
        var mood = new VisitorMood();
        Equal(60, mood.Satisfaction, "New visitors start reasonably satisfied");
        Equal(0, mood.Age, "Visit age starts at zero");
        Equal(0, mood.VisitProgress, "No patience spent at arrival");
        Equal(120, mood.RemainingPatience, "Initial patience budget");
        Check(!mood.ShouldLeave, "New visitor does not immediately leave");
        mood.Reward(1000); Equal(100, mood.Satisfaction, "Rewards clamp satisfaction");
        mood.Punish(1000); Equal(0, mood.Satisfaction, "Penalties clamp satisfaction");
        Tick(mood, 29);
        Check(!mood.ShouldLeave, "Poor satisfaction respects minimum stay");
        Tick(mood, 1);
        Check(mood.ShouldLeave, "Unhappy visitor leaves after minimum stay");
        mood.Reward(100); Tick(mood, 1, watching: true);
        Check(mood.ShouldLeave, "Leaving decision remains committed");

        var content = new VisitorMood(90);
        var bored = new VisitorMood(30);
        Tick(content, 90); Tick(bored, 90);
        Check(content.VisitProgress < bored.VisitProgress, "Satisfied visitors spend patience more slowly");
        Check(content.RemainingPatience > bored.RemainingPatience, "Satisfied visitors have more remaining patience");
        Check(content.Willingness > bored.Willingness, "Satisfaction and patience drive willingness to stay");
        Check(!content.ShouldLeave && bored.ShouldLeave, "Content visitors stay longer than disappointed visitors");

        var watching = new VisitorMood(); var neutral = new VisitorMood();
        Tick(watching, 20, watching: true); Tick(neutral, 20);
        Check(watching.Satisfaction > neutral.Satisfaction, "Watching creatures improves satisfaction");
        var served = new VisitorMood(); Tick(served, 20, receivingService: true);
        Check(served.Satisfaction > neutral.Satisfaction, "Receiving food or rest service improves satisfaction");
        var hungry = new VisitorMood(); Tick(hungry, 20, hunger: 100);
        Check(hungry.Satisfaction < neutral.Satisfaction, "Unmet hunger lowers satisfaction");
        var tired = new VisitorMood(); Tick(tired, 20, energy: 0);
        Check(tired.Satisfaction < neutral.Satisfaction, "Exhaustion lowers satisfaction");
        var frightened = new VisitorMood(); Tick(frightened, 20, fear: 100);
        Check(frightened.Satisfaction < tired.Satisfaction, "Terror strongly reduces satisfaction");

        var delighted = new VisitorMood(100);
        Tick(delighted, 299, watching: true, receivingService: true);
        Check(!delighted.ShouldLeave, "Excellent visit can continue almost to absolute stay limit");
        Tick(delighted, 1, watching: true, receivingService: true);
        Check(delighted.ShouldLeave && delighted.Age == 300, "Absolute visit cap applies even when delighted");
        Equal(1, delighted.VisitProgress, "Patience progress remains normalized");

        var safe = new VisitorMood();
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            safe.Tick(invalid, 100, 0, 100, false, false, 30, 300);
            Equal(0, safe.Age, "Invalid time does not age visitors");
            Equal(60, safe.Satisfaction, "Invalid time does not change satisfaction");
        }
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            safe.Reward(invalid); safe.Punish(invalid);
            Equal(60, safe.Satisfaction, "Invalid reward and punishment ignored");
        }
        safe.Tick(1, float.NaN, float.NaN, float.NaN, false, false, float.NaN, float.NaN);
        Check(!float.IsNaN(safe.Satisfaction) && !float.IsNaN(safe.VisitProgress), "Invalid needs and stay settings remain finite");
        safe.Tick(float.MaxValue, 0, 100, 0, false, false, 30, 300);
        Check(safe.ShouldLeave && safe.Satisfaction >= 0 && safe.Satisfaction <= 100,
            "Extreme finite time cannot overflow satisfaction or suppress departure");
        Equal(0, new VisitorMood(-10).Satisfaction, "Initial satisfaction lower clamp");
        Equal(100, new VisitorMood(110).Satisfaction, "Initial satisfaction upper clamp");
        Console.WriteLine("PASS: " + checks + " visitor model checks.");
    }
}

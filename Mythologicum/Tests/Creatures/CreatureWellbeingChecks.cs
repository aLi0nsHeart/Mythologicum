using System;
using Mythologicum;

public static class CreatureWellbeingChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Equal(float expected, float actual, string message)
        => Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .001f,
            message + ": expected " + expected + ", got " + actual);
    private static bool Has(CreatureNeedScores scores, CreatureUnmetNeeds need) => (scores.UnmetNeeds & need) != 0;
    private static void Crisis(CreatureNeedScores scores, CreatureUnmetNeeds cause, string message)
    {
        Check(scores.Happiness <= 20 && scores.Distress >= .8f, message + " cannot be cancelled by otherwise perfect care");
        Check(Has(scores, cause), message + " reports its individual cause");
    }
    public static void Main()
    {
        var full = CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, needsWater: true, needsRest: true);
        Equal(100, full.Happiness, "All needs met"); Equal(100, full.Energy, "Full rest satisfaction");
        Equal(0, full.Distress, "Healthy care has no direct distress");
        Check(full.UnmetNeeds == CreatureUnmetNeeds.None, "Healthy care lists no unmet need");
        var empty = CreatureWellbeing.Evaluate(100, 100, 0, 0, 0, 100, 2, 2, 8, thirst: 100, needsWater: true, energy: 0, needsRest: true);
        Equal(0, empty.Happiness, "All needs unmet");
        var partial = CreatureWellbeing.Evaluate(50, 50, 50, 1, 1, 100, 2, 2, 8);
        Equal(50, partial.Environment, "Half trees"); Equal(50, partial.Space, "Half space");
        Equal(50, partial.Herd, "Half companions"); Equal(50, partial.Happiness, "Moderate needs retain weighted scoring");
        var alone = CreatureWellbeing.Evaluate(0, 0, 100, 2, 1, 100, 2, 0, 0);
        Equal(0, alone.Herd, "Solitary creature dislikes one companion"); Crisis(alone, CreatureUnmetNeeds.Herd, "Severe herd mismatch");
        var weightsOnly = new CreatureBehaviourSettings { criticalNeedThreshold = 0 };
        Equal(85, CreatureWellbeing.Evaluate(0, 0, 100, 2, 1, 100, 2, 0, 0, weightsOnly).Happiness,
            "Disabling critical caps retains the authored fifteen-percent herd contribution");
        Equal(50, CreatureWellbeing.Evaluate(0, 0, 48, 0, 12, 48, 0, 2, 8).Herd, "Overcrowding penalizes herd");
        Equal(100, CreatureWellbeing.Evaluate(0, 0, 0, 0, 999, 0, 0, 0, -1).Happiness, "Unconfigured restrictions stay neutral");
        Equal(100, CreatureWellbeing.Evaluate(-10, -10, 500, 50, 0, 100, 2, 0, 0).Happiness, "Excess habitat cannot exceed full happiness");
        Crisis(CreatureWellbeing.Evaluate(100, 0, 100, 2, 0, 100, 2, 0, 0), CreatureUnmetNeeds.Hunger, "Starvation");
        Crisis(CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, energy: 0, needsRest: true), CreatureUnmetNeeds.Exhaustion, "Exhaustion");
        Crisis(CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, thirst: 100, needsWater: true), CreatureUnmetNeeds.Thirst, "Dehydration");
        Crisis(CreatureWellbeing.Evaluate(0, 100, 100, 2, 0, 100, 2, 0, 0), CreatureUnmetNeeds.Fear, "Extreme fear");
        Crisis(CreatureWellbeing.Evaluate(0, 0, 0, 2, 0, 100, 2, 0, 0), CreatureUnmetNeeds.Space, "No usable space");
        var ground = CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, biomeScore: 0, shelterScore: 100);
        Check(ground.Environment > 50, "Good trees and shelter still contribute to the environment summary");
        Crisis(ground, CreatureUnmetNeeds.Ground, "Completely unsuitable ground");
        Crisis(CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, biomeScore: 100, shelterScore: 0), CreatureUnmetNeeds.Shelter, "Missing required shelter");
        Crisis(CreatureWellbeing.Evaluate(0, 0, 100, 0, 0, 100, 2, 0, 0, nearbyGravestones: 4, requiredGravestones: 4), CreatureUnmetNeeds.Trees, "Missing trees despite adequate graves");

        // Screenshot regression: the previous average was 60.8 and energy was absent.
        var screenshot = CreatureWellbeing.Evaluate(100, 0, 100, 2, 0, 100, 2, 0, 0,
            thirst: 54, needsWater: true, biomeScore: 50, energy: 0, needsRest: true);
        Equal(75, screenshot.Environment, "Screenshot's environment stays seventy-five despite half-suitable ground");
        Check(screenshot.Happiness <= 20 && screenshot.Distress > .95f, "Starvation and exhaustion create compound distress");
        foreach (var need in new[] { CreatureUnmetNeeds.Hunger, CreatureUnmetNeeds.Exhaustion, CreatureUnmetNeeds.Thirst, CreatureUnmetNeeds.Ground })
            Check(Has(screenshot, need), "Screenshot reports " + need);
        var noFood = CreatureWellbeing.Evaluate(100, 0, 100, 2, 0, 100, 2, 0, 0, needsFood: false);
        Equal(100, noFood.Happiness, "Food-free species ignore hunger"); Check(!Has(noFood, CreatureUnmetNeeds.Hunger), "Food-free species have no false hunger diagnosis");
        Equal(100, CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, thirst: 100, needsWater: false).Happiness, "Water-free species ignore thirst");
        Equal(100, CreatureWellbeing.Evaluate(0, 0, 100, 2, 0, 100, 2, 0, 0, energy: 0, needsRest: false).Happiness, "Disabled rest is neutral");
        var disabled = new CreatureBehaviourSettings { hungerWeight = 0, thirstWeight = 0, energyWeight = 0, fearWeight = 0, environmentWeight = 0, spaceWeight = 0, herdWeight = 0 };
        var ignored = CreatureWellbeing.Evaluate(100, 100, 0, 0, 0, 100, 2, 2, 8, disabled,
            thirst: 100, needsWater: true, biomeScore: 0, shelterScore: 0, energy: 0, needsRest: true);
        Equal(100, ignored.Happiness, "All disabled weights are neutral"); Equal(0, ignored.Distress, "Disabled weights cannot produce hidden distress");
        Check(ignored.UnmetNeeds == CreatureUnmetNeeds.None, "Disabled effects have no misleading diagnosis");

        var settings = new CreatureBehaviourSettings(); var model = new CreatureWellbeing();
        model.Update(screenshot, 1, 4, 50, settings); Equal(71, model.Happiness, "Happiness retains gradual response");
        Check(model.Happiness > 50 && model.Agitation > 0, "Critical pressure acts before historical happiness falls below its threshold");
        model.Update(screenshot, 4, 4, 50, settings); Check(model.Agitation > .9f && model.Agitation <= 1, "A sustained crisis agitates within five seconds");
        model.Update(screenshot, 100, 4, 50, settings); Equal(screenshot.Happiness, model.Happiness, "No happiness undershoot");
        model.Update(full, 100, 4, 50, settings); Equal(100, model.Happiness, "No recovery overshoot"); Equal(0, model.Agitation, "Meeting needs restores calm");
        model.Restore(100, .8f); model.Update(full, 1, 4, 50, settings);
        Check(model.Agitation > 0 && model.Agitation < .8f, "Recovery does not instantly erase agitation");
        float happiness = model.Happiness, pressure = model.Agitation;
        foreach (float invalidTime in new[] { -1f, float.NaN, float.PositiveInfinity }) model.Update(empty, invalidTime, 4, 50, settings);
        Equal(happiness, model.Happiness, "Invalid time preserves happiness"); Equal(pressure, model.Agitation, "Invalid time preserves agitation");
        model.Update(empty, 1, -10, 50, settings); Equal(happiness, model.Happiness, "Negative happiness rate is ignored");
        var noCrisis = new CreatureBehaviourSettings { criticalNeedThreshold = 0 };
        var policyOff = CreatureWellbeing.Evaluate(100, 100, 0, 0, 0, 100, 2, 2, 8, noCrisis,
            thirst: 100, needsWater: true, energy: 0, needsRest: true);
        model.Restore(100, 0); model.Update(policyOff, 1, 4, 0, noCrisis);
        Equal(0, model.Agitation, "Explicitly disabled emotional and critical thresholds stay safe");
        var one = new CreatureWellbeing(); var many = new CreatureWellbeing(); one.Update(screenshot, 5, 4, 50, settings);
        for (int i = 0; i < 50; i++) many.Update(screenshot, .1f, 4, 50, settings);
        Equal(one.Happiness, many.Happiness, "Crisis happiness is frame-independent"); Equal(one.Agitation, many.Agitation, "Crisis agitation is frame-independent");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue, -float.MaxValue })
        {
            var unusual = CreatureWellbeing.Evaluate(invalid, invalid, invalid, 0, 0, 100, 2, 0, 0,
                thirst: invalid, needsWater: true, energy: invalid, needsRest: true);
            Check(!float.IsNaN(unusual.Happiness) && !float.IsInfinity(unusual.Happiness) && unusual.Happiness >= 0 && unusual.Happiness <= 100, "Extreme values keep bounded happiness");
            Check(!float.IsNaN(unusual.Distress) && !float.IsInfinity(unusual.Distress) && unusual.Distress >= 0 && unusual.Distress <= 1, "Extreme values keep bounded distress");
        }
        Console.WriteLine("PASS: " + checks + " wellbeing, critical needs, exhaustion, diagnosis, recovery and frame-invariance checks.");
    }
}

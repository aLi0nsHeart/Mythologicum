using System;
using System.Linq;
using Mythologicum;

public static class VisitorTypeChecks
{
    private static int checks;
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); checks++; }
    private static void Equal(float expected, float actual, string why)
        => Check(!float.IsNaN(actual) && Math.Abs(expected - actual) < .001f, why + ": " + actual);
    public static void Main()
    {
        var types = VisitorTypeSettings.Defaults();
        Check(types.Length == 4 && types.Select(item => item.type).Distinct().Count() == 4, "All four requested types have independent settings");
        for (int i = 0; i < 4; i++)
        {
            Check(VisitorInterests.Pick(types, (i + .5) / 4d) == types[i].type, "Equal weights divide arrivals evenly");
            Check(types[i].arrivalWeight > 0 && types[i].interestChoiceWeight > 0, "Each type arrives and expresses exhibit interests");
        }
        Check(VisitorInterests.Pick(types, 1) == VisitorType.Children, "The upper sampling endpoint remains defined");
        Check(VisitorInterests.Pick(types, double.NaN) == VisitorType.ThrillSeekers, "Invalid random samples cannot corrupt visitor types");
        types[0].arrivalWeight = 0; types[1].arrivalWeight = float.NaN; types[2].arrivalWeight = float.PositiveInfinity;
        for (int i = 0; i <= 100; i++) Check(VisitorInterests.Pick(types, i / 100d) == VisitorType.Children, "Zero and invalid weights are excluded");
        types[3].arrivalWeight = -1;
        Check(VisitorInterests.Pick(types, .5) == VisitorType.Legacy && VisitorInterests.Pick(null, .5) == VisitorType.Legacy,
            "An empty visitor mix falls back to the original neutral guest");
        var first = new VisitorTypeSettings { type = VisitorType.Wonderlust, arrivalWeight = 1 };
        Check(VisitorInterests.Pick(new[] { first, new VisitorTypeSettings { type = VisitorType.Wonderlust, arrivalWeight = 100 },
            new VisitorTypeSettings { type = (VisitorType)999, arrivalWeight = 100 } }, .9) == VisitorType.Wonderlust,
            "Duplicate and unknown configured types cannot create new identities");

        var adventure = VisitorTypeSettings.DefaultFor(VisitorType.Adventurers);
        float firstView = VisitorInterests.Interest(adventure, .5f, 4, false, false);
        float newOrigin = VisitorInterests.Interest(adventure, .5f, 4, true, false);
        float repeated = VisitorInterests.Interest(adventure, .5f, 4, true, true);
        Check(firstView > newOrigin && newOrigin > repeated, "Adventurers distinguish new species and new origins");
        Check(VisitorInterests.Interest(adventure, .5f, 5, true, true) > VisitorInterests.Interest(adventure, .5f, 1, true, true),
            "Expedition rarity attracts adventurers independently of novelty");
        var child = VisitorTypeSettings.DefaultFor(VisitorType.Children);
        Equal(VisitorInterests.Interest(child, .8f, 1, false, false), VisitorInterests.Interest(child, .8f, 5, false, false),
            "Children's default interests do not reward danger or rarity alone");
        Check(child.hungerMultiplier > 1 && child.energyMultiplier > 1 && child.hungerThresholdMultiplier < 1
            && child.restThresholdMultiplier > 1, "Children seek food and rest sooner through editable multipliers");
        var thrill = VisitorTypeSettings.DefaultFor(VisitorType.ThrillSeekers);
        Check(thrill.fearGainMultiplier < child.fearGainMultiplier && thrill.fearLossMultiplier < child.fearLossMultiplier,
            "Thrill seekers and children have different fear sensitivity");
        var neutral = VisitorTypeSettings.DefaultFor(VisitorType.Legacy);
        Equal(1, VisitorInterests.Interest(neutral, 0, 1, true, true), "Legacy visitors keep neutral interest");
        Equal(1, neutral.RewardMultiplier(0), "Legacy watching rewards stay exactly unchanged");
        Equal(0, neutral.interestChoiceWeight, "Legacy exhibit selection has no preference bonus");

        var liked = new VisitorMood(rules: new VisitorMoodRules(), patienceMultiplier: adventure.patienceMultiplier);
        var disliked = new VisitorMood(rules: new VisitorMoodRules(), patienceMultiplier: adventure.patienceMultiplier);
        liked.Tick(20, 0, 100, 0, true, false, 30, 300, adventure.RewardMultiplier(1));
        disliked.Tick(20, 0, 100, 0, true, false, 30, 300, adventure.RewardMultiplier(0));
        Check(liked.Satisfaction > disliked.Satisfaction && liked.VisitProgress < disliked.VisitProgress,
            "Preferred exhibits increase satisfaction and slow patience use");
        Check(new VisitorMood(patienceMultiplier: child.patienceMultiplier).RemainingPatience
            < new VisitorMood(patienceMultiplier: adventure.patienceMultiplier).RemainingPatience,
            "Type-specific patience affects the real visit budget");
        var brave = new VisitorMood(); var sensitive = new VisitorMood();
        brave.Tick(10, 0, 100, 50, false, false, 30, 300, fearLossMultiplier: thrill.fearLossMultiplier);
        sensitive.Tick(10, 0, 100, 50, false, false, 30, 300, fearLossMultiplier: child.fearLossMultiplier);
        Check(brave.Satisfaction > sensitive.Satisfaction, "Fear sensitivity affects real satisfaction without changing safety routing");
        var random = new Random(9172);
        foreach (var settings in VisitorTypeSettings.Defaults())
        {
            float previous = settings.RewardMultiplier(0);
            for (int i = 0; i <= 1000; i++)
            {
                float value = settings.RewardMultiplier(i / 1000f);
                Check(value >= previous && !float.IsNaN(value) && !float.IsInfinity(value), "Interest rewards stay finite and monotonic");
                previous = value;
                float interest = VisitorInterests.Interest(settings, (float)(random.NextDouble() * 4 - 2), random.Next(-3, 10), i % 2 == 0, i % 3 == 0);
                Check(interest >= 0 && interest <= 1 && !float.IsNaN(interest), "Scoring remains bounded with out-of-range inputs");
            }
        }
        var broken = new VisitorTypeSettings { arrivalWeight = float.NaN, hungerMultiplier = float.NaN,
            energyMultiplier = float.PositiveInfinity, patienceMultiplier = -10, minimumExhibitReward = float.NaN,
            maximumExhibitReward = -10, newOriginBonus = float.PositiveInfinity };
        broken.Validate();
        Check(broken.arrivalWeight == 0 && broken.hungerMultiplier == 1 && broken.energyMultiplier == 1
            && broken.patienceMultiplier > 0 && broken.maximumExhibitReward >= broken.minimumExhibitReward,
            "Invalid editable settings sanitize without corrupting simulation state");
        Console.WriteLine("PASS: " + checks + " visitor type mix, interests, novelty, mood and safety-tuning checks.");
    }
}

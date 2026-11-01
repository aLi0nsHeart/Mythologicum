using System;
using System.IO;
using Mythologicum;

public static class ReputationChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    private static void Equal(float expected, float actual, string message) => Check(
        !float.IsNaN(actual) && Math.Abs(expected - actual) < .001f, message + ": " + actual);
    private static void Rejected(ParkReputationSnapshot saved, string message)
    {
        bool threw = false;
        try { ParkReputation.ValidateSnapshot(saved); } catch (InvalidDataException) { threw = true; }
        Check(threw, message);
    }

    public static void Main()
    {
        var fresh = new ParkReputation();
        Equal(0f, fresh.Stars, "An empty new park has no exhibition stars");
        Equal(60f, fresh.GuestScore, "Guest reputation begins neutral");
        Equal(15f, fresh.RecommendedAdmissionPrice, "Starting price supports early park income");
        Equal(1f, fresh.DemandMultiplier, "Neutral new park preserves baseline visitor arrival rate");
        Equal(1f, fresh.PriceAcceptance(15f), "The default ticket is accepted before any creatures arrive");
        Equal(1f, fresh.PriceAcceptance(0f), "Free entry never excludes potential visitors");
        Equal(.5f, fresh.PriceAcceptance(21f), "Forty percent over recommended price loses half of prospective guests");
        Equal(0f, fresh.PriceAcceptance(27f), "Eighty percent over the fair price stops admission demand");
        Equal(0f, fresh.PriceAcceptance(float.MaxValue), "Extreme admission prices cannot create infinite revenue");
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Equal(0f, fresh.PriceAcceptance(invalid), "Invalid admission prices are rejected");
            Check(!fresh.ServiceAffordable(invalid, true), "Invalid service prices are rejected");
        }
        Check(fresh.ServiceAffordable(8f, true) && fresh.ServiceAffordable(0f, false), "Default meals and free rest remain accessible");
        Check(!fresh.ServiceAffordable(1000f, true) && !fresh.ServiceAffordable(1000f, false), "Guests refuse excessive meal and rest prices");

        fresh.UpdateExhibition(new[] { new ParkExhibit("Goat", 1, 1000, true) });
        Check(fresh.ExhibitionScore <= 4f && fresh.Stars < .25f, "Domestic goat spam cannot produce a highly rated exhibition");
        var one = new ParkReputation();
        one.UpdateExhibition(new[] { new ParkExhibit("Chupacabra", 2) });
        var duplicate = new ParkReputation();
        duplicate.UpdateExhibition(new[] { new ParkExhibit("Chupacabra", 2, 1000) });
        Check(duplicate.ExhibitionScore > one.ExhibitionScore && duplicate.ExhibitionScore <= one.ExhibitionScore * 1.151f,
            "Extra specimens make a small bounded contribution");
        var varied = new ParkReputation();
        varied.UpdateExhibition(new[] { new ParkExhibit("Chupacabra", 2), new ParkExhibit("Hitotsume", 1) });
        Check(varied.ExhibitionScore > duplicate.ExhibitionScore, "Species variety matters more than repeating one species");
        var oni = new ParkReputation(); oni.UpdateExhibition(new[] { new ParkExhibit("Oni", 5) });
        Check(oni.ExhibitionScore > one.ExhibitionScore && oni.RecommendedAdmissionPrice > one.RecommendedAdmissionPrice,
            "Higher-rated creatures support more valuable admission");

        var park = new ParkReputation();
        park.UpdateExhibition(new[] { new ParkExhibit("Chupacabra", 2, 3), new ParkExhibit("Hitotsume", 1),
            new ParkExhibit("Yurei", 3, 2), new ParkExhibit("Oni", 5, 3) });
        Equal(4f, park.DisplayedSpecies, "Only distinct species count toward variety");
        Equal(9f, park.DisplayedCreatures, "Exhibition creature count includes every displayed specimen");
        Check(park.Stars > 4f && park.Stars <= 5f, "Current mythical collection can support a highly rated park");
        float before = park.Stars, fairBefore = park.RecommendedAdmissionPrice;
        park.ReportDeparture(100f);
        Check(park.GuestScore > 60f && park.Stars > before && park.RecommendedAdmissionPrice > fairBefore,
            "Satisfied departures improve stars and acceptable ticket value");
        before = park.Stars;
        park.ReportDeparture(0f);
        Check(park.Stars < before && park.ReviewedVisitors == 2, "An unhappy departure reduces reputation once");
        before = park.Stars; float demandBefore = park.DemandMultiplier;
        park.ReportDeath();
        Check(park.Stars < before && park.DemandMultiplier < demandBefore && park.Fatalities == 1 && park.SafetyPenalty == 20f,
            "A visitor death meaningfully harms stars and future demand");
        float penalty = park.SafetyPenalty;
        foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) park.Tick(dt);
        Equal(penalty, park.SafetyPenalty, "Pausing or invalid time cannot erase accident penalties");
        park.Tick(60f);
        Check(park.SafetyPenalty < penalty && park.SafetyPenalty > penalty - 1f, "Accident penalties recover gradually over simulation time");
        penalty = park.SafetyPenalty; park.ReportDeparture(100f);
        Check(park.SafetyPenalty < penalty, "Excellent later visits help rebuild trust");
        for (int i = 0; i < 100; i++) park.ReportDeath();
        Equal(0f, park.Stars, "Severe safety failures cannot create negative stars");
        Check(park.DemandMultiplier > 0f, "A low reputation does not permanently lock sandbox parks out of visitors");
        for (int i = 0; i < 500; i++) park.ReportDeparture(100f);
        park.Tick(10000f);
        Check(park.Stars > 4.99f && park.Stars <= 5f, "Safe excellent operation can recover to five stars");
        Check(park.RecommendedAdmissionPrice >= 39.9f && park.RecommendedFoodPrice >= 12.9f
            && park.ServiceAffordable(18f, true), "High reputation supports higher entry and service prices");
        float score = park.GuestScore;
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }) park.ReportDeparture(invalid);
        Equal(score, park.GuestScore, "Invalid visitor feedback cannot poison the reputation model");

        var saved = park.Capture();
        var restored = new ParkReputation(); restored.Restore(saved);
        Equal(park.Stars, restored.Stars, "Reputation stars survive save restoration");
        Equal(park.DemandMultiplier, restored.DemandMultiplier, "Restored demand preserves reputation effects");
        Check(restored.Fatalities == park.Fatalities && restored.ReviewedVisitors == park.ReviewedVisitors,
            "Restoration preserves accident and review history");
        saved.exhibits[0].count = 999;
        Check(park.Capture().exhibits[0].count != 999 && restored.Capture().exhibits[0].count != 999,
            "Snapshots cannot mutate the live reputation or restored exhibits");
        restored.Restore(null);
        Equal(60f, restored.GuestScore, "Legacy saves without reputation start neutral");
        Equal(0f, restored.Stars, "Legacy reputation is rebuilt from actual exhibited creatures");

        Rejected(new ParkReputationSnapshot { guestScore = float.NaN }, "Malformed guest reputation is rejected");
        Rejected(new ParkReputationSnapshot { safetyPenalty = -1f }, "Negative safety penalties are rejected");
        Rejected(new ParkReputationSnapshot { fatalities = -1 }, "Negative fatality history is rejected");
        var invalidExhibit = new ParkReputationSnapshot(); invalidExhibit.exhibits.Add(new ParkExhibit("Oni", 6));
        Rejected(invalidExhibit, "Unsupported saved creature ratings are rejected");
        invalidExhibit = new ParkReputationSnapshot(); invalidExhibit.exhibits.Add(new ParkExhibit("Oni", 5)); invalidExhibit.exhibits.Add(new ParkExhibit("Oni", 5));
        Rejected(invalidExhibit, "Duplicate saved species cannot inflate appeal");
        var malformedRuntime = new ParkReputation();
        malformedRuntime.UpdateExhibition(new[] { null, new ParkExhibit(null, 5), new ParkExhibit("Oni", 5, -2), new ParkExhibit("Oni", 999, int.MaxValue) });
        Check(malformedRuntime.Stars >= 0f && malformedRuntime.Stars <= 5f && malformedRuntime.DisplayedSpecies == 1,
            "Runtime exhibit sanitization keeps all scores finite and bounded");
        Console.WriteLine("PASS: " + checks + " park reputation, pricing, feedback and save checks.");
    }
}

using System;
using Mythologicum;

public static class ParkTuningChecks
{
    private static int checks;
    private static void Equal(float expected, float actual, string message)
    {
        checks++;
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > .001f)
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }

    public static void Main()
    {
        var money = new ParkEconomy(new ParkEconomyRules { startingFunds = 1234, startWithUnlimitedFunds = true });
        Equal(1234, money.Balance, "Configured opening capital is used");
        Check(money.UnlimitedFunds && money.TrySpend(2000), "Configured sandbox funds mode is used");
        Equal(1234, money.Balance, "Unlimited mode leaves capital unchanged");
        money.Restore(321, 40, 20, false);
        Equal(321, money.Balance, "Loading preserves saved cash instead of applying new starting funds");
        Check(!money.UnlimitedFunds, "Loading preserves saved funds mode");

        var rules = new ParkReputationRules
        {
            initialGuestScore = 40, reviewWeight = .5f,
            appealPerCreatureRating = 10, extraSpecimenBonus = .25f, extraSpecimenLimit = 1,
            speciesVarietyBonus = 7, exhibitionQualityFloor = 1, guestQualityWeight = 0,
            qualityPointsPerStar = 10, domesticSpeciesAppeal = 3, domesticExtraSpecimenAppeal = 2,
            domesticExtraSpecimenLimit = 1, maximumDomesticAppeal = 5,
            baseAdmissionPrice = 20, admissionPricePerStar = 10,
            baseFoodPrice = 4, foodPricePerStar = 2, baseRestPrice = 1, restPricePerStar = 3,
            admissionRefusalPremium = .5f, servicePriceTolerance = 2,
            deathGuestPenalty = 5, deathSafetyPenalty = 10,
            safetyRecoveryPerSecond = .5f, safetyReviewThreshold = 50, safetyRecoveryPerReviewPoint = .1f,
            baseDemand = 1, guestDemandBonus = 0, starDemandBonus = .1f, safetyDemandLossPerPoint = .01f
        };
        var park = new ParkReputation(rules);
        Equal(40, park.GuestScore, "Initial reputation is configurable");
        park.UpdateExhibition(new[] { new ParkExhibit("One", 2, 10) });
        Equal(25, park.ExhibitionScore, "Specimen appeal and bounded duplicate bonus are configurable");
        Equal(2.5f, park.Stars, "Quality weights and points per star are configurable");
        Equal(45, park.RecommendedAdmissionPrice, "Admission base and star premium are configurable");
        Equal(9, park.RecommendedFoodPrice, "Meal base and star premium are configurable");
        Equal(8.5f, park.RecommendedRestPrice, "Rest base and star premium are configurable");
        Equal(.5f, park.PriceAcceptance(56.25f), "Configured admission price tolerance drives acceptance");
        Check(park.ServiceAffordable(18, true) && !park.ServiceAffordable(18.01f, true), "Configured service tolerance drives affordability");
        Equal(1.25f, park.DemandMultiplier, "Configured star demand contributes to arrivals");
        park.ReportDeparture(100);
        Equal(70, park.GuestScore, "Configured review weight changes reputation response");
        park.ReportDeath();
        Equal(65, park.GuestScore, "Configured death reputation loss applies");
        Equal(10, park.SafetyPenalty, "Configured accident penalty applies");
        Equal(1.15f * .9f, park.DemandMultiplier, "Configured safety demand loss applies");
        park.Tick(4);
        Equal(8, park.SafetyPenalty, "Configured passive safety recovery applies");
        park.ReportDeparture(70);
        Equal(6, park.SafetyPenalty, "Configured review recovery threshold and rate apply");

        park.UpdateExhibition(new[] { new ParkExhibit("One", 2), new ParkExhibit("Two", 1), new ParkExhibit("Goat", 1, 100, true) });
        Equal(42, park.ExhibitionScore, "Configured species variety and capped domestic appeal apply");
        var snapshot = park.Capture();
        var newBalance = new ParkReputation(new ParkReputationRules { initialGuestScore = 90, baseAdmissionPrice = 100 });
        newBalance.Restore(snapshot);
        Equal(park.GuestScore, newBalance.GuestScore, "Rebalancing does not reset saved reviews");
        Check(newBalance.RecommendedAdmissionPrice >= 100, "Loaded parks use current price rules with existing state");
        newBalance.Restore(null);
        Equal(90, newBalance.GuestScore, "Older parks without reputation use the configured initial score");

        var zeroPrice = new ParkReputation(new ParkReputationRules { baseAdmissionPrice = 0, admissionPricePerStar = 0 });
        Equal(1, zeroPrice.PriceAcceptance(0), "Free admission remains accepted when fair price is zero");
        Equal(0, zeroPrice.PriceAcceptance(1), "Positive prices at zero fair value avoid division by zero");
        rules.safetyDemandLossPerPoint = 1;
        park.ReportDeath();
        Equal(0, park.DemandMultiplier, "Extreme safety tuning cannot cause negative arrivals");
        rules.qualityPointsPerStar = 0; rules.admissionRefusalPremium = 0; rules.baseFoodPrice = float.NaN;
        rules.Validate();
        Check(rules.qualityPointsPerStar > 0 && rules.admissionRefusalPremium > 0 && !float.IsNaN(rules.baseFoodPrice),
            "Inspector validation keeps divisions finite and replaces invalid values");
        Console.WriteLine("PASS: " + checks + " park and economy tuning checks.");
    }
}

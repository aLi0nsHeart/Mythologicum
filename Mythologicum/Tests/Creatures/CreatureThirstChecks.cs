using System;
using Mythologicum;

public static class CreatureThirstChecks
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Close(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .0001f, message);
    public static void Main()
    {
        var settings = new CreatureThirstSettings();
        var vitals = new CreatureVitals(35, settings.StartingThirst);
        Close(vitals.Thirst, 15, "Living creatures begin hydrated enough to allow park preparation");
        vitals.TickThirst(20, settings); Close(vitals.Thirst, 18, "Thirst rises by simulation seconds and species rate");
        vitals.TickThirst(0, settings); vitals.TickThirst(-1, settings); vitals.TickThirst(float.NaN, settings);
        vitals.TickThirst(float.PositiveInfinity, settings); Close(vitals.Thirst, 18, "Invalid and paused time do not change thirst");
        vitals.Restore(50, 80); Close(vitals.Thirst, 18, "Fear and hunger restoration does not refill thirst");
        vitals.Restore(50, 80, 93); Close(vitals.Thirst, 93, "Saving can restore a specific hydration need");
        Close(vitals.Drink(23), 23, "Drinking reports the actual relief consumed"); Close(vitals.Thirst, 70, "Water reduces thirst independently of hunger");
        Close(vitals.Hunger, 50, "Drinking does not feed the creature"); Close(vitals.Fear, 80, "Drinking does not clear a real fear response");
        Close(vitals.Drink(-20), 0, "Negative water does not increase thirst"); Close(vitals.Drink(float.NaN), 0, "Invalid relief does not corrupt thirst");
        Close(vitals.Drink(1000), 70, "A trough is charged only for actual relief near full hydration"); Close(vitals.Thirst, 0, "Thirst never becomes negative");
        vitals.TickThirst(10000, settings); Close(vitals.Thirst, 100, "Long simulation steps clamp thirst at maximum");
        settings.needsWater = false; vitals.TickThirst(1, settings);
        Close(vitals.Thirst, 0, "Undead profiles can opt out of water needs"); Close(settings.StartingThirst, 0, "Water-free creatures start hydrated");
        settings.thirstPerSecond = float.NaN; settings.initialThirst = float.PositiveInfinity;
        settings.drinkThreshold = 70; settings.criticalThirst = 20; settings.satisfiedThirst = 90;
        settings.drinkSeconds = -5; settings.searchRadius = -3; settings.Validate();
        Close(settings.thirstPerSecond, 0, "Invalid species rate sanitizes to zero");
        Check(settings.criticalThirst >= settings.drinkThreshold && settings.satisfiedThirst <= settings.drinkThreshold,
            "Validated thirst thresholds retain sensible urgency and satiation ordering");
        Check(settings.drinkSeconds >= .1f && settings.searchRadius >= .1f, "Drinking duration and source search stay valid");
        vitals.Restore(0, 0, float.NaN); Close(vitals.Thirst, 0, "Invalid persisted thirst is contained");
        Console.WriteLine("PASS: " + checks + " creature thirst, drinking, restore and species tuning checks.");
    }
}

using System;
using System.Linq;
using Mythologicum;
using UnityEngine;

/// <summary>Imported species data, expedition resources and staged research descriptions.</summary>
public static class AquaticDataNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Aquatic data: " + reason); checks++; }
        var kappa = Resources.Load<CreatureDefinition>("Creatures/Kappa");
        var nokk = Resources.Load<CreatureDefinition>("Creatures/Nokk");
        Check(kappa != null && nokk != null && kappa != nokk, "Both stable species IDs load as distinct persistent profiles");
        Check(kappa.origin == "creature.origin.yokai" && nokk.origin == "creature.origin.nordic", "The confirmed origin tags are preserved");
        var rules = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression").rules;
        Check(rules.regions.Single(region => region.id == "east-asia").species.Contains("Kappa")
            && rules.regions.Single(region => region.id == "northern-europe").species.Contains("Nokk"),
            "The authored expedition resource includes both species in the correct regions");
        Check(kappa.rating == 2 && nokk.rating == 3 && kappa.minimumCompanions == 1 && kappa.maximumCompanions == 2
            && nokk.minimumCompanions == 0 && nokk.maximumCompanions == 0, "Rarity and total-group defaults match their distinct designs");
        foreach (var species in new[] { kappa, nokk })
        {
            Check(species.manuallyPlaceable && !species.pettable && species.diet == CreatureDiet.Carnivore,
                "The species belongs to the normal specimen-placement and carnivore systems");
            Check(species.Aquatic.canSwim && species.Aquatic.preferWater && species.Aquatic.dragPreyToWater
                && species.Aquatic.preferredWaterArea > 0 && species.Aquatic.minimumSwimDepth > 0,
                "Serialized water habitat and hunting fields survive Unity import");
            Check(species.Shelter.mode == CreatureShelterMode.Water && species.Shelter.requiredForSleep,
                "Water is the required sleep shelter");
            Check((species.preyTags & (CreatureTags.Goat | CreatureTags.Visitor | CreatureTags.Staff))
                == (CreatureTags.Goat | CreatureTags.Visitor | CreatureTags.Staff)
                && (species.Aquatic.preferredPreyTags & (CreatureTags.Visitor | CreatureTags.Staff)) != 0,
                "Both human prey and goat fallback are configured in editable data");
            var research = new SpeciesResearch(new[] { species });
            research.Study(species, research.Rules.Threshold(1));
            string food = research.Description(species);
            Check(food.Contains(Loc.Text("research.creature.food.water_hunter").Trim()), "The first discovery explains water hunting");
            research.Study(species, 100);
            string description = research.Description(species);
            Check(description.Contains(Loc.Text("research.creature.sleep.water").Trim())
                && description.Contains(Loc.Text("research.creature.containment.water").Trim()),
                "Completed research explains water sleep and aquatic containment");
        }
        return checks;
    }
}

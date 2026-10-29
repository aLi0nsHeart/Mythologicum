using System;
using System.Collections.Generic;
using System.Text;

namespace Mythologicum
{
    /// <summary>One species' discovery progress, independent of Unity and employee presentation.</summary>
    public sealed class ResearchProgress
    {
        public float Progress { get; private set; }
        public int UnlockedStages => Math.Min(SpeciesResearch.StageCount,(int)(Progress/25f));
        public bool Study(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || Progress >= 100f) return false;
            Progress = (float)Math.Min(100d,(double)Progress+amount);
            return true;
        }
    }

    /// <summary>Park-wide species knowledge earned through employee observation; separate from future technology research.</summary>
    public sealed class SpeciesResearch
    {
        public event Action<CreatureDefinition> Finished;
        public const int StageCount = 4;
        private readonly Dictionary<CreatureDefinition,ResearchProgress> progress = new Dictionary<CreatureDefinition,ResearchProgress>();

        public SpeciesResearch(IEnumerable<CreatureDefinition> knownSpecies)
        {
            if (knownSpecies == null) return;
            foreach (CreatureDefinition species in knownSpecies)
                if (species != null && !progress.ContainsKey(species)) progress.Add(species,new ResearchProgress());
        }

        public float Progress(CreatureDefinition species) => species != null && progress.TryGetValue(species,out ResearchProgress value) ? value.Progress : 0f;
        public int UnlockedStages(CreatureDefinition species) => Math.Min(StageCount,(int)(Progress(species)/25f));
        public bool Study(CreatureDefinition species,float amount)
        {
            if (species == null || !progress.TryGetValue(species,out ResearchProgress value)) return false;
            bool changed = value.Study(amount);
            if (changed && value.Progress >= 100f) Finished?.Invoke(species);
            return changed;
        }

        public string Description(CreatureDefinition species)
        {
            if (species == null || !progress.ContainsKey(species)) return "No creature research selected.";
            var text = new StringBuilder();
            int stages = UnlockedStages(species);
            text.Append(species.displayName).Append(" knowledge: ").Append(Progress(species).ToString("0")).Append("%\n");
            if (stages < 1) text.Append("Observe living creatures to reveal care information.\n");
            else
            {
                string food = species.feedsOnFear || species.diet == CreatureDiet.Fear ? "Feeds by frightening people without harming them."
                    : species.diet == CreatureDiet.Carnivore ? "Carnivore: provide suitable prey."
                    : species.diet == CreatureDiet.Herbivore ? "Herbivore: needs natural grazing ground." : "No ordinary food requirement.";
                text.Append("Food: ").Append(food).Append('\n');
            }
            if (stages >= 2)
            {
                if (species.requiredTrees > 0) text.Append("Habitat: ").Append(species.requiredTrees).Append(" nearby reachable trees.\n");
                else if (species.requiredGravestones > 0) text.Append("Habitat: ").Append(species.requiredGravestones).Append(" nearby reachable gravestones.\n");
                else text.Append("Habitat: no special enrichment requirement.\n");
                text.Append("Sleep: ").Append(species.requiresGravestoneForSleep ? "inside a gravestone." : species.requiresTreeForSleep ? "in a tree." : "on safe ground.").Append('\n');
            }
            if (stages >= 3)
            {
                text.Append("Space: at least ").Append(species.minimumSpace.ToString("0")).Append(" m² of ground.\n");
                text.Append("Group: ").Append(species.minimumCompanions+1);
                text.Append(species.maximumCompanions < 0 ? "+" : "-"+(species.maximumCompanions+1));
                text.Append(" including the animal itself.\n");
            }
            if (stages >= 4)
            {
                if (species.canPhaseFences) text.Append("Containment: sanctified barriers block phasing, which starts at hunger ").Append(species.phaseHungerThreshold.ToString("0")).Append(".\n");
                if (species.canBreakFences) text.Append("Containment: reinforced barriers resist its attacks.\n");
                if (species.canClimbWoodenFences) text.Append("Containment: can climb wood; spikes prevent climbing.\n");
                text.Append("Escape pressure rises with hunger, agitation and overcrowding. Keep food, rest and space available.");
            }
            else text.Append("Next discovery at ").Append((stages+1)*25).Append("%.");
            return text.ToString().TrimEnd();
        }
    }
}

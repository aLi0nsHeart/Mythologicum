using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class SpeciesResearchRules
    {
        [Tooltip("Knowledge percentage needed to reveal diet information.")]
        [Range(0, 100)] public float foodDiscovery = 25f;
        [Range(0, 100)] public float habitatDiscovery = 50f;
        [Range(0, 100)] public float socialDiscovery = 75f;
        [Range(0, 100)] public float containmentDiscovery = 100f;
        public float Threshold(int stage) => stage <= 1 ? foodDiscovery : stage == 2 ? habitatDiscovery
            : stage == 3 ? socialDiscovery : containmentDiscovery;
        public int Unlocked(float progress)
        {
            int count = 0;
            for (int stage = 1; stage <= SpeciesResearch.StageCount; stage++)
                if (progress >= Threshold(stage)) count++; else break;
            return count;
        }
        public void Validate()
        {
            foodDiscovery = Mathf.Clamp(foodDiscovery, 0, 100);
            habitatDiscovery = Mathf.Clamp(habitatDiscovery, foodDiscovery, 100);
            socialDiscovery = Mathf.Clamp(socialDiscovery, habitatDiscovery, 100);
            containmentDiscovery = Mathf.Clamp(containmentDiscovery, socialDiscovery, 100);
        }
    }

    /// <summary>One species' discovery progress, independent of Unity and employee presentation.</summary>
    public sealed class ResearchProgress
    {
        public float Progress { get; private set; }
        private readonly SpeciesResearchRules rules;
        public ResearchProgress(SpeciesResearchRules rules = null) => this.rules = rules ?? new SpeciesResearchRules();
        public int UnlockedStages => rules.Unlocked(Progress);
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
        public SpeciesResearchRules Rules { get; }
        private readonly Dictionary<CreatureDefinition,ResearchProgress> progress = new Dictionary<CreatureDefinition,ResearchProgress>();

        public SpeciesResearch(IEnumerable<CreatureDefinition> knownSpecies, SpeciesResearchRules rules = null)
        {
            Rules = rules ?? new SpeciesResearchRules();
            if (knownSpecies == null) return;
            foreach (CreatureDefinition species in knownSpecies)
                if (species != null && !progress.ContainsKey(species)) progress.Add(species,new ResearchProgress(Rules));
        }

        public float Progress(CreatureDefinition species) => species != null && progress.TryGetValue(species,out ResearchProgress value) ? value.Progress : 0f;
        public int UnlockedStages(CreatureDefinition species) => Rules.Unlocked(Progress(species));
        public float NextDiscovery(CreatureDefinition species) => Rules.Threshold(Math.Min(StageCount, UnlockedStages(species) + 1));
        public bool Study(CreatureDefinition species,float amount)
        {
            if (species == null || !progress.TryGetValue(species,out ResearchProgress value)) return false;
            bool changed = value.Study(amount);
            if (changed && value.Progress >= 100f) Finished?.Invoke(species);
            return changed;
        }

        public string Description(CreatureDefinition species)
        {
            if (species == null || !progress.ContainsKey(species)) return Loc.Text("research.creature.none");
            var text = new StringBuilder();
            int stages = UnlockedStages(species);
            text.Append(Loc.Format("research.creature.knowledge", species.DisplayName, Progress(species)));
            if (stages < 1) text.Append(Loc.Text("research.creature.observe"));
            else text.Append(Loc.Text(!species.needsFood ? "research.creature.food.none"
                : species.feedsOnFear || species.diet == CreatureDiet.Fear ? "research.creature.food.fear"
                : species.diet == CreatureDiet.Carnivore ? "research.creature.food.carnivore"
                : species.diet == CreatureDiet.Herbivore ? "research.creature.food.herbivore" : "research.creature.food.none"));
            if (stages >= 2)
            {
                if (species.requiredTrees > 0) text.Append(Loc.Format("research.creature.habitat.trees", species.requiredTrees));
                else if (species.requiredGravestones > 0) text.Append(Loc.Format("research.creature.habitat.graves", species.requiredGravestones));
                else text.Append(Loc.Text("research.creature.habitat.none"));
                text.Append(Loc.Text(species.requiresGravestoneForSleep ? "research.creature.sleep.grave"
                    : species.requiresTreeForSleep ? "research.creature.sleep.tree" : "research.creature.sleep.ground"));
            }
            if (stages >= 3)
            {
                text.Append(Loc.Format("research.creature.space", species.minimumSpace));
                string group = species.maximumCompanions < 0
                    ? Loc.Format("creature.group.unbounded", species.minimumCompanions + 1)
                    : Loc.Format("creature.group.range", species.minimumCompanions + 1, species.maximumCompanions + 1);
                text.Append(Loc.Format("research.creature.group", group));
            }
            if (stages >= 4)
            {
                if (species.canPhaseFences) text.Append(species.needsFood
                    ? Loc.Format("research.creature.containment.phase", species.phaseHungerThreshold)
                    : Loc.Text("research.creature.containment.phase_no_hunger"));
                if (species.canBreakFences) text.Append(Loc.Text("research.creature.containment.break"));
                if (species.canClimbWoodenFences) text.Append(Loc.Text("research.creature.containment.climb"));
                text.Append(Loc.Text("research.creature.escape_pressure"));
            }
            else text.Append(Loc.Format("research.creature.next", NextDiscovery(species)));
            return text.ToString().TrimEnd();
        }
    }
}

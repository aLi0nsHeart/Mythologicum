using System;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class ExpeditionRegion
    {
        public string id, name;
        [Min(0), Tooltip("One-time price to unlock this location.")] public float accessCost = 350f;
        public Vector2 mapPosition;
        public string[] species = Array.Empty<string>();
    }
    [Serializable] public sealed class ExpeditionDifficulty
    {
        public string id, name;
        [Min(0)] public float costPerTeam = 120f;
        [Min(.1f), Tooltip("Base mission duration in simulation seconds, before technology upgrades.")] public float seconds = 60f;
        [Range(0, 1)] public float successChance = .8f;
        [Range(1, 5)] public int minimumRating = 1, maximumRating = 2;
        [Min(1), Tooltip("Number of specimens awarded by a successful mission.")] public int creaturesFound = 2;
    }
    [Serializable] public sealed class TechnologyDefinition
    {
        public string id, name, description;
        [Min(0)] public float cost = 250f;
        [Min(.1f), Tooltip("Researcher work units needed. Multiple employees contribute together.")] public float workSeconds = 45f;
        public string[] prerequisites = Array.Empty<string>();
        public FenceKind[] fences = Array.Empty<FenceKind>();
        public BuildingKind[] buildings = Array.Empty<BuildingKind>();
        [Min(1)] public float staffEfficiency = 1f;
        [Range(0, 1)] public float expeditionSuccessBonus;
        [Min(.01f)] public float expeditionDurationMultiplier = 1f;
    }
    [Serializable] public sealed class ProgressionRules
    {
        [Header("Teams and success")]
        [Min(0), Tooltip("Price of the first team.")] public float teamCost = 250f;
        [Min(0), Tooltip("Extra price for every team already owned.")] public float additionalTeamCost = 100f;
        [Range(0, 1), Tooltip("Success probability added by each team beyond the first.")] public float extraTeamSuccess = .12f;
        [Range(0, 1)] public float maximumSuccess = .95f;
        [Min(1)] public int maximumTeams = 12;
        [Min(.1f)] public float minimumMissionSeconds = 1f;
        [Header("New park starting unlocks")]
        [Min(0)] public int startingTeams;
        [Tooltip("Stable region IDs to unlock for free in newly created parks.")]
        public string[] startingRegions = Array.Empty<string>();
        [Tooltip("Stable technology IDs already researched in newly created parks.")]
        public string[] startingTechnologies = Array.Empty<string>();
        public CreatureStock[] startingCreatures = Array.Empty<CreatureStock>();
        [Header("Basic fence access")]
        [Tooltip("Fence types always available without technology research, including in loaded parks.")]
        public FenceKind[] startingFences = { FenceKind.Wooden };
        [Header("Notifications")]
        [Min(0), Tooltip("Repeated notices with the same event key merge within this many simulation seconds.")]
        public float notificationMergeSeconds = 5f;
        [Header("Locations")]
        public ExpeditionRegion[] regions = DefaultRegions();
        [Header("Mission difficulties")]
        public ExpeditionDifficulty[] difficulties = {
            new ExpeditionDifficulty { id="survey", name="Survey", minimumRating=1, maximumRating=2, successChance=.8f, costPerTeam=120, seconds=60 },
            new ExpeditionDifficulty { id="capture", name="Capture", minimumRating=2, maximumRating=3, successChance=.6f, costPerTeam=200, seconds=120 },
            new ExpeditionDifficulty { id="dangerous", name="Dangerous", minimumRating=3, maximumRating=5, successChance=.35f, costPerTeam=350, seconds=180 }
        };
        [Header("Technology projects")]
        public TechnologyDefinition[] technologies = {
            new TechnologyDefinition { id="food-service", name="Food services", description="Unlock kiosks to sell food to hungry visitors.", cost=200,workSeconds=30,buildings=new[]{BuildingKind.Kiosk} },
            new TechnologyDefinition { id="visitor-rest", name="Visitor comfort", description="Unlock rest areas for tired visitors.", cost=150,workSeconds=25,buildings=new[]{BuildingKind.RestArea} },
            new TechnologyDefinition { id="spiked-fences", name="Spiked fences", description="Prevent creatures climbing out over wooden barriers.", cost=250,workSeconds=45,fences=new[]{FenceKind.Spiked} },
            new TechnologyDefinition { id="reinforced-fences", name="Reinforced fences", description="Heavy barriers resist creature attacks.", cost=600,workSeconds=90,prerequisites=new[]{"spiked-fences"},fences=new[]{FenceKind.Heavy} },
            new TechnologyDefinition { id="sanctified-fences", name="Sanctified fences", description="Block a spirit's ability to phase through barriers.", cost=700,workSeconds=100,prerequisites=new[]{"spiked-fences"},fences=new[]{FenceKind.Sanctified} },
            new TechnologyDefinition { id="staff-training", name="Staff training", description="Research, healing and fence repairs are 25% faster.",cost=450,workSeconds=60,staffEfficiency=1.25f },
            new TechnologyDefinition { id="field-equipment", name="Expedition equipment", description="Add 10 percentage points to expedition success chances.",cost=350,workSeconds=45,expeditionSuccessBonus=.1f },
            new TechnologyDefinition { id="field-logistics", name="Expedition logistics", description="Expeditions take 25% less time.",cost=500,workSeconds=60,prerequisites=new[]{"field-equipment"},expeditionDurationMultiplier=.75f }
        };
        private static ExpeditionRegion[] DefaultRegions() => new[] {
            new ExpeditionRegion {id="north-america",name="North America",mapPosition=new Vector2(.19f,.68f),species=new[]{"Chupacabra"}},
            new ExpeditionRegion {id="south-america",name="South America",mapPosition=new Vector2(.31f,.27f),species=new[]{"Chupacabra"}},
            new ExpeditionRegion {id="west-africa",name="West Africa",mapPosition=new Vector2(.49f,.43f)},
            new ExpeditionRegion {id="east-africa",name="East Africa",mapPosition=new Vector2(.59f,.36f)},
            new ExpeditionRegion {id="south-africa",name="Southern Africa",mapPosition=new Vector2(.55f,.14f)},
            new ExpeditionRegion {id="china",name="China",mapPosition=new Vector2(.77f,.65f)},
            new ExpeditionRegion {id="middle-east",name="Middle East",mapPosition=new Vector2(.62f,.52f)},
            new ExpeditionRegion {id="japan",name="Japan",mapPosition=new Vector2(.92f,.57f),species=new[]{"Hitotsume","Yurei","Oni"}},
            new ExpeditionRegion {id="central-europe",name="Central Europe",mapPosition=new Vector2(.49f,.73f)},
            new ExpeditionRegion {id="northern-europe",name="Northern Europe",mapPosition=new Vector2(.56f,.9f)}
        };
    }
    [CreateAssetMenu(menuName="Mythologicum/Progression settings",fileName="DefaultProgression")]
    public sealed class ProgressionDefinition : ScriptableObject
    {
        public ProgressionRules rules = new ProgressionRules();
        private void OnValidate()
        {
            rules ??= new ProgressionRules();
            rules.maximumTeams = Mathf.Max(1, rules.maximumTeams);
            rules.startingTeams = Mathf.Clamp(rules.startingTeams, 0, rules.maximumTeams);
            rules.minimumMissionSeconds = Mathf.Max(.1f, rules.minimumMissionSeconds);
            rules.notificationMergeSeconds = Mathf.Max(0, rules.notificationMergeSeconds);
            if (rules.difficulties != null) foreach (var difficulty in rules.difficulties)
            {
                if (difficulty == null) continue;
                difficulty.costPerTeam = Mathf.Max(0, difficulty.costPerTeam);
                difficulty.seconds = Mathf.Max(.1f, difficulty.seconds);
                difficulty.successChance = Mathf.Clamp01(difficulty.successChance);
                difficulty.minimumRating = Mathf.Clamp(difficulty.minimumRating, 1, 5);
                difficulty.maximumRating = Mathf.Clamp(difficulty.maximumRating, difficulty.minimumRating, 5);
                difficulty.creaturesFound = Mathf.Max(1, difficulty.creaturesFound);
            }
        }
    }
}

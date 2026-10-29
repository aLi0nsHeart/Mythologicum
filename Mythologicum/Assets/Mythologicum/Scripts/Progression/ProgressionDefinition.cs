using System;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class ExpeditionRegion
    {
        public string id, name;
        public float accessCost = 350f;
        public Vector2 mapPosition;
        public string[] species = Array.Empty<string>();
    }
    [Serializable] public sealed class ExpeditionDifficulty
    {
        public string id, name;
        public float costPerTeam = 120f, seconds = 60f, successChance = .8f;
        public int minimumRating = 1, maximumRating = 2, creaturesFound = 2;
    }
    [Serializable] public sealed class TechnologyDefinition
    {
        public string id, name, description;
        public float cost = 250f, workSeconds = 45f;
        public string[] prerequisites = Array.Empty<string>();
        public FenceKind[] fences = Array.Empty<FenceKind>();
        public BuildingKind[] buildings = Array.Empty<BuildingKind>();
        public float staffEfficiency = 1f, expeditionSuccessBonus, expeditionDurationMultiplier = 1f;
    }
    [Serializable] public sealed class ProgressionRules
    {
        public float teamCost = 250f, additionalTeamCost = 100f, extraTeamSuccess = .12f, maximumSuccess = .95f;
        public int maximumTeams = 12;
        public ExpeditionRegion[] regions = DefaultRegions();
        public ExpeditionDifficulty[] difficulties = {
            new ExpeditionDifficulty { id="survey", name="Survey", minimumRating=1, maximumRating=2, successChance=.8f, costPerTeam=120, seconds=60 },
            new ExpeditionDifficulty { id="capture", name="Capture", minimumRating=2, maximumRating=3, successChance=.6f, costPerTeam=200, seconds=120 },
            new ExpeditionDifficulty { id="dangerous", name="Dangerous", minimumRating=3, maximumRating=5, successChance=.35f, costPerTeam=350, seconds=180 }
        };
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
    public sealed class ProgressionDefinition : ScriptableObject { public ProgressionRules rules = new ProgressionRules(); }
}

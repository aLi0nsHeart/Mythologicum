using System;
using UnityEngine;

namespace Mythologicum
{
    public enum AttractionKind { None, PettingZoo, ViewingTower, BalloonRide, PredatorFeedingGallery,
        TricksterTheatre, CabinetOfCuriosities, ExpeditionPavilion, ClockworkCarousel, ViewingWindow }

    /// <summary>Shared attraction interests and experience tuning. Price, duration and capacity belong to its building.</summary>
    [Serializable]
    public sealed class AttractionDefinition
    {
        public AttractionKind kind;
        public CreatureVisitorAppeal appeal = new CreatureVisitorAppeal();
        [Min(0), Tooltip("Satisfaction gained by a completed visit, scaled by visitor interest and exhibit quality.")]
        public float satisfactionReward = 12f;
        [Min(0), Tooltip("Seconds before the same visitor considers this attraction again.")]
        public float revisitSeconds = 60f;
        [Min(0), Tooltip("Nearby creatures within this many metres contribute to the experience.")]
        public float exhibitRadius = 12f;
        [Min(0), Tooltip("Strength of the extra experience provided by actual nearby creatures.")]
        public float exhibitBonus = 1f;
        [Min(0), Tooltip("Extra exhibit quality for an origin this visitor has not discovered before.")]
        public float noveltyBonus = .5f;
        [Min(0), Tooltip("Extra exhibit quality while a displayed predator is actually feeding.")]
        public float feedingBonus = .5f;
        public bool requiresCreatures;
        public bool requiresPettableCreatures;
        public bool requiresDangerousCreatures;
        public bool requiresFearFeeder;
        [Min(0), Tooltip("Highest creature threat permitted for a safe-contact petting exhibit.")]
        public float maximumCreatureThreat = 10f;
        [Range(0, 1)] public float minimumInterest = .15f;
        [Min(0), Tooltip("Interest score awarded before visitor travel costs are considered.")]
        public float decisionWeight = 14f;
        [Min(0), Tooltip("Highest attraction ticket price visitors consider affordable before interest/reputation adjustments.")]
        public float maximumPrice = 20f;
        [Min(0), Tooltip("Height of the visible tower or balloon experience above its station.")]
        public float rideHeight = 8f;
        [Min(0), Tooltip("Horizontal radius of the balloon's visible route.")]
        public float rideRadius = 4f;
        [Min(.1f), Tooltip("Simulation seconds spent walking from the public entrance to the attraction's passenger position.")]
        public float entrySeconds = 1.5f;
        [Min(.1f), Tooltip("Simulation seconds spent returning from the passenger position to the public entrance.")]
        public float exitSeconds = 1.5f;
        [Min(0), Tooltip("Maximum boarding wait after the first passenger arrives. A full, ready group can depart sooner.")]
        public float boardingWindowSeconds = 5f;
        [Min(0), Tooltip("Spacing in metres between passenger positions. Positions are fitted to the attraction's usable area.")]
        public float passengerSpacing = .65f;
        [Range(.01f, .49f), Tooltip("Fraction of the balloon trip used for ascent, and again for its final descent. The remainder is the level flight.")]
        public float balloonAscentFraction = .2f;
        [Min(1), Tooltip("Complete carousel revolutions during one group ride. Whole turns return the seats to their boarding positions.")]
        public int carouselTurns = 1;
        [Range(0, 100), Tooltip("Hunger relieved by one successful controlled trickster performance.")]
        public float scareFoodRelief = 30f;
        [Min(0), Tooltip("Seconds between hunger rewards from the same theatre performer.")]
        public float scareCooldownSeconds = 10f;

        public AttractionDefinition Copy()
        {
            var copy = (AttractionDefinition)MemberwiseClone();
            copy.appeal = appeal == null ? new CreatureVisitorAppeal() : new CreatureVisitorAppeal
                { thrill = appeal.thrill, wonder = appeal.wonder, adventure = appeal.adventure, children = appeal.children };
            return copy;
        }
        public float AppealFor(VisitorType type)
        {
            var values = appeal ?? new CreatureVisitorAppeal();
            float value = type == VisitorType.ThrillSeekers ? values.thrill : type == VisitorType.Wonderlust ? values.wonder
                : type == VisitorType.Adventurers ? values.adventure : type == VisitorType.Children ? values.children
                : (values.thrill + values.wonder + values.adventure + values.children) * .25f;
            return CreatureVisitorAppeal.Normalize(value);
        }
        public void Validate()
        {
            appeal ??= new CreatureVisitorAppeal(); appeal.Validate();
            satisfactionReward = Amount(satisfactionReward); revisitSeconds = Amount(revisitSeconds);
            exhibitRadius = Amount(exhibitRadius); exhibitBonus = Amount(exhibitBonus);
            noveltyBonus = Amount(noveltyBonus); feedingBonus = Amount(feedingBonus);
            maximumCreatureThreat = Amount(maximumCreatureThreat); minimumInterest = Mathf.Clamp01(Amount(minimumInterest));
            decisionWeight = Amount(decisionWeight); maximumPrice = Amount(maximumPrice);
            rideHeight = Amount(rideHeight); rideRadius = Amount(rideRadius);
            entrySeconds = Mathf.Max(.1f, Amount(entrySeconds)); exitSeconds = Mathf.Max(.1f, Amount(exitSeconds));
            boardingWindowSeconds = Amount(boardingWindowSeconds); passengerSpacing = Amount(passengerSpacing);
            balloonAscentFraction = Mathf.Clamp(Amount(balloonAscentFraction), .01f, .49f); carouselTurns = Math.Max(1, carouselTurns);
            scareFoodRelief = Mathf.Min(100f, Amount(scareFoodRelief)); scareCooldownSeconds = Amount(scareCooldownSeconds);
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);

        public static BuildingDefinition Building(BuildingKind kind)
        {
            var result = new BuildingDefinition { kind = kind, useVisualCatalogFallback = false, foodPrice = 0,
                staffCapacity = 0, serviceCapacity = 4, serviceSeconds = 12, attractionPrice = 6, constructionCost = 350,
                operatingCostPerSecond = .06f, width = 3, depth = 3, height = 3 };
            var settings = result.attraction;
            switch (kind)
            {
                case BuildingKind.PettingZoo:
                    result.displayName = "attraction.petting_zoo.name"; result.width = 1; result.depth = 1; result.height = 2;
                    settings.kind = AttractionKind.PettingZoo; settings.requiresCreatures = settings.requiresPettableCreatures = true;
                    settings.appeal = Appeal(.15f, .5f, .35f, 1f); result.openColor = new Color(.55f, .75f, .4f); break;
                case BuildingKind.ViewingTower:
                    result.displayName = "attraction.viewing_tower.name"; result.height = 9; result.constructionCost = 550; result.attractionPrice = 8;
                    settings.kind = AttractionKind.ViewingTower; settings.exhibitRadius = 24; settings.rideHeight = 7;
                    settings.entrySeconds = 5; settings.exitSeconds = 4;
                    settings.appeal = Appeal(.45f, .85f, .9f, .45f); result.openColor = new Color(.55f, .47f, .3f); break;
                case BuildingKind.BalloonRide:
                    result.displayName = "attraction.balloon_ride.name"; result.width = result.depth = 4; result.height = 4;
                    result.constructionCost = 1400; result.operatingCostPerSecond = .18f; result.attractionPrice = 25; result.serviceSeconds = 35;
                    settings.kind = AttractionKind.BalloonRide; settings.exhibitRadius = 36; settings.maximumPrice = 40; settings.satisfactionReward = 20;
                    settings.entrySeconds = settings.exitSeconds = 3;
                    settings.rideHeight = 16; settings.rideRadius = 10; settings.revisitSeconds = 120;
                    settings.appeal = Appeal(.65f, 1f, .95f, .6f); result.openColor = new Color(.75f, .37f, .22f); break;
                case BuildingKind.PredatorFeedingGallery:
                    result.displayName = "attraction.predator_feeding_gallery.name"; result.width = 4; result.depth = 2;
                    result.constructionCost = 650; result.attractionPrice = 12; result.serviceSeconds = 16; result.serviceCapacity = 5;
                    settings.kind = AttractionKind.PredatorFeedingGallery; settings.requiresCreatures = settings.requiresDangerousCreatures = true;
                    settings.appeal = Appeal(1f, .25f, .7f, .15f); result.openColor = new Color(.64f, .38f, .25f); break;
                case BuildingKind.TricksterTheatre:
                    result.displayName = "attraction.trickster_theatre.name"; result.width = 4; result.depth = 3;
                    result.constructionCost = 500; result.attractionPrice = 8; result.serviceCapacity = 6; result.serviceSeconds = 15;
                    settings.kind = AttractionKind.TricksterTheatre; settings.requiresCreatures = settings.requiresFearFeeder = true;
                    settings.appeal = Appeal(.8f, .55f, .35f, .95f); result.openColor = new Color(.62f, .4f, .75f); break;
                case BuildingKind.CabinetOfCuriosities:
                    result.displayName = "attraction.cabinet_of_curiosities.name"; result.constructionCost = 450; result.attractionPrice = 7;
                    settings.kind = AttractionKind.CabinetOfCuriosities; settings.appeal = Appeal(.2f, 1f, .7f, .5f);
                    result.openColor = new Color(.35f, .65f, .68f); break;
                case BuildingKind.ExpeditionPavilion:
                    result.displayName = "attraction.expedition_pavilion.name"; result.width = 4; result.depth = 3;
                    result.constructionCost = 500; result.attractionPrice = 9; settings.kind = AttractionKind.ExpeditionPavilion;
                    settings.appeal = Appeal(.4f, .65f, 1f, .35f); result.openColor = new Color(.57f, .66f, .35f); break;
                case BuildingKind.ClockworkCarousel:
                    result.displayName = "attraction.clockwork_carousel.name"; result.constructionCost = 400; result.attractionPrice = 4;
                    settings.kind = AttractionKind.ClockworkCarousel; settings.appeal = Appeal(.25f, .65f, .2f, 1f);
                    result.openColor = new Color(.85f, .65f, .3f); break;
                case BuildingKind.ViewingWindow:
                    result.displayName = "attraction.viewing_window.name"; result.width = 2; result.depth = 1; result.height = 2.8f;
                    result.entranceColumn = 0; result.constructionCost = 120; result.attractionPrice = 5;
                    result.serviceCapacity = 2; result.serviceSeconds = 10; result.operatingCostPerSecond = .015f;
                    settings.kind = AttractionKind.ViewingWindow; settings.requiresCreatures = true;
                    settings.exhibitRadius = 8; settings.satisfactionReward = 14; settings.revisitSeconds = 75;
                    settings.decisionWeight = 18; settings.maximumPrice = 18; settings.rideHeight = settings.rideRadius = 0;
                    settings.appeal = Appeal(.9f, .7f, .6f, .45f); result.openColor = new Color(.31f, .22f, .1f); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), "Building is not an attraction.");
            }
            return result;
        }
        private static CreatureVisitorAppeal Appeal(float thrill, float wonder, float adventure, float children)
            => new CreatureVisitorAppeal { thrill = thrill, wonder = wonder, adventure = adventure, children = children };
    }
}

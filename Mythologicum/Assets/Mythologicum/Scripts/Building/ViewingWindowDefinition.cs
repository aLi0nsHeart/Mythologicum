using UnityEngine;

namespace Mythologicum
{
    /// <summary>Editable admission, operating and experience rules for a fence-mounted viewing attraction.</summary>
    [CreateAssetMenu(menuName = "Mythologicum/Attractions/Viewing window", fileName = "ViewingWindow")]
    public sealed class ViewingWindowDefinition : ScriptableObject
    {
        private static ViewingWindowDefinition current;
        public static ViewingWindowDefinition Current
        {
            get
            {
                if (current != null) return current;
                current = Resources.Load<ViewingWindowDefinition>("Attractions/ViewingWindow");
                if (current == null) { current = CreateInstance<ViewingWindowDefinition>(); current.hideFlags = HideFlags.HideAndDontSave; }
                return current;
            }
        }
        [Min(0)] public float constructionCost = 120f;
        [Min(0)] public float ticketPrice = 5f;
        [Min(.1f)] public float serviceSeconds = 10f;
        [Min(1)] public int serviceCapacity = 2;
        [Min(.5f), Tooltip("Height of the protected two-by-one viewing building, in metres.")]
        public float buildingHeight = 2.8f;
        [Min(0), Tooltip("Base upkeep per simulation second; multiply by sixty for the per-minute amount.")]
        public float operatingCostPerSecond = .015f;
        public AttractionDefinition attraction = Defaults();
        public Color frameColor = new Color(.31f, .22f, .1f);
        public Color glassColor = new Color(.35f, .8f, .93f, .22f);

        public CreatureVisitorAppeal visitorAppeal => attraction.appeal;
        public float visitSeconds => serviceSeconds;
        public int capacity => serviceCapacity;
        public float defaultPrice => ticketPrice;
        public float upkeepPerSecond => operatingCostPerSecond;
        public float viewRange => attraction.exhibitRadius;
        public float satisfactionReward => attraction.satisfactionReward;
        public float revisitSeconds => attraction.revisitSeconds;
        public float minimumInterest => attraction.minimumInterest;
        public float decisionWeight => attraction.decisionWeight;
        public float maximumPrice => attraction.maximumPrice;
        public float exhibitBonus => attraction.exhibitBonus;

        /// <summary>The same editable resource supplies new viewing buildings and existing fence-window accounts.</summary>
        public BuildingDefinition CreateBuildingDefinition()
        {
            var result = AttractionDefinition.Building(BuildingKind.ViewingWindow);
            result.height = Mathf.Max(.5f, Amount(buildingHeight));
            result.constructionCost = Amount(constructionCost); result.attractionPrice = Amount(ticketPrice);
            result.serviceSeconds = Mathf.Max(.1f, Amount(serviceSeconds)); result.serviceCapacity = Mathf.Max(1, serviceCapacity);
            result.operatingCostPerSecond = Amount(operatingCostPerSecond);
            result.attraction = (attraction ?? Defaults()).Copy(); result.attraction.kind = AttractionKind.ViewingWindow;
            result.openColor = frameColor; result.Validate();
            return result;
        }

        private static AttractionDefinition Defaults() => new AttractionDefinition
        {
            kind = AttractionKind.ViewingWindow,
            appeal = new CreatureVisitorAppeal { thrill = .9f, wonder = .7f, adventure = .6f, children = .45f },
            satisfactionReward = 14, revisitSeconds = 75, exhibitRadius = 8, exhibitBonus = 1,
            requiresCreatures = true, minimumInterest = .15f, decisionWeight = 18, maximumPrice = 18
        };
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
        private void OnValidate()
        {
            constructionCost = Amount(constructionCost); ticketPrice = Amount(ticketPrice);
            serviceSeconds = Mathf.Max(.1f, Amount(serviceSeconds)); serviceCapacity = Mathf.Max(1, serviceCapacity);
            buildingHeight = Mathf.Max(.5f, Amount(buildingHeight));
            operatingCostPerSecond = Amount(operatingCostPerSecond);
            attraction ??= Defaults(); attraction.kind = AttractionKind.ViewingWindow; attraction.Validate();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => current = null;
    }
}

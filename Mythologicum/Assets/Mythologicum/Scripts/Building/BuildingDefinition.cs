using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum BuildingKind { ParkEntrance, ExpeditionCentre, Kiosk, GoatDispenser, RestArea,
        ResearchCentre, MercenaryHQ, ZookeeperHut }

    [Serializable]
    public sealed class BuildingDefinition
    {
        public BuildingKind kind;
        public string displayName;
        [Tooltip("Footprint width in grid tiles before rotation.")]
        [Min(1)] public int width = 2;
        [Tooltip("Footprint depth in grid tiles before rotation.")]
        [Min(1)] public int depth = 2;
        [Tooltip("Placeholder/collider height in metres; the visual catalogue may supply a model separately.")]
        [Min(0.5f)] public float height = 3f;
        public Color openColor = new Color(0.45f, 0.7f, 0.45f);
        [Header("Appearance")]
        public GameObject visualPrefab;
        public float visualYaw;
        [Tooltip("Use a legacy visual-catalogue model only when Visual Prefab is empty. Disable to deliberately show the placeholder instead.")]
        public bool useVisualCatalogFallback = true;
        [Tooltip("Front-edge cell used for access; -1 selects the middle cell.")]
        public int entranceColumn = -1;
        public int DoorColumn => entranceColumn < 0 ? width / 2 : Math.Min(entranceColumn, width - 1);
        [Header("Economy and visitor services")]
        [Tooltip("One-time purchase cost in park currency.")]
        [Min(0f)] public float constructionCost = 100f;
        [Tooltip("Base running cost per simulation second, before employee salaries. Multiply by 60 for the per-minute cost.")]
        [Min(0f)] public float operatingCostPerSecond = 0.05f;
        [Tooltip("Starting admission ticket price for each new park entrance. Existing placed entrances retain their chosen price.")]
        [Min(0f)] public float admissionPrice = 15f;
        [Tooltip("Starting price for one visit to a rest area. Zero makes resting free.")]
        [Min(0f)] public float restPrice;
        [Tooltip("Starting price for a kiosk meal. Each placed building keeps its own editable price.")]
        [Min(0f)] public float foodPrice = 8f;
        [Tooltip("Visitor hunger points relieved by one completed kiosk meal (0-100 scale).")]
        [Range(0f, 100f)] public float foodRelief = 65f;
        [Tooltip("Simulation seconds spent purchasing food or completing a paid rest visit.")]
        [Min(0.1f)] public float serviceSeconds = 3f;
        [Tooltip("Visitors served simultaneously at this building.")]
        [Min(1)] public int serviceCapacity = 2;
        [Tooltip("Rest points recovered per simulation second while using a rest area (0-100 scale).")]
        [Min(0f)] public float restRecoveryPerSecond = 10f;
        [Tooltip("Optional creature profile released by a dispenser. Defaults to the goat profile.")]
        public CreatureDefinition dispensedCreature;
        [Tooltip("Simulation seconds between dispenser release attempts. One supply unit is consumed by each successful release.")]
        [Min(1f)] public float dispenseInterval = 20f;
        [Tooltip("Maximum living creatures originating from this individual dispenser. Releases resume when one dies.")]
        [Min(1)] public int maximumDispensed = 12;
        [Tooltip("Maximum grid steps searched around the dispenser for an available release position.")]
        [Min(1)] public int dispenseSearchSteps = 2;
        [Header("Personnel and supplies")]
        [Tooltip("Maximum employees hired by one employee building. Salaries are charged separately for each employee.")]
        [Min(0)] public int staffCapacity = 5;
        [Tooltip("Salary per hired employee per simulation second. Multiply by 60 for the per-minute amount.")]
        [Min(0f)] public float staffSalaryPerSecond = 0.12f;
        [Tooltip("One-time charge for hiring one employee.")]
        [Min(0f)] public float hireCost = 50f;
        [Tooltip("Supply units delivered by one complete keeper restock. One unit releases one creature.")]
        [Min(1)] public int feederCapacity = 6;
        [Tooltip("Cost of one keeper supply delivery, regardless of how many supply units were missing.")]
        [Min(0f)] public float restockCost = 12f;
        public bool RequiresPathConnection => kind != BuildingKind.GoatDispenser;
        public bool IsEmployeeBuilding => kind == BuildingKind.ResearchCentre || kind == BuildingKind.MercenaryHQ
            || kind == BuildingKind.ZookeeperHut;
        public bool CanSetPrice => kind == BuildingKind.ParkEntrance || kind == BuildingKind.Kiosk || kind == BuildingKind.RestArea;

        public BuildingDefinition Copy() => (BuildingDefinition)MemberwiseClone();

        public void Validate()
        {
            width = Math.Max(1, width); depth = Math.Max(1, depth);
            height = Positive(height, .5f);
            entranceColumn = Math.Max(-1, Math.Min(width - 1, entranceColumn));
            constructionCost = Amount(constructionCost); operatingCostPerSecond = Amount(operatingCostPerSecond);
            admissionPrice = Amount(admissionPrice); restPrice = Amount(restPrice); foodPrice = Amount(foodPrice);
            foodRelief = Math.Min(100f, Amount(foodRelief)); serviceSeconds = Positive(serviceSeconds, .1f);
            serviceCapacity = Math.Max(1, serviceCapacity); restRecoveryPerSecond = Amount(restRecoveryPerSecond);
            dispenseInterval = Positive(dispenseInterval, 1f); maximumDispensed = Math.Max(1, maximumDispensed);
            dispenseSearchSteps = Math.Max(1, dispenseSearchSteps); staffCapacity = Math.Max(0, staffCapacity);
            staffSalaryPerSecond = Amount(staffSalaryPerSecond); hireCost = Amount(hireCost);
            feederCapacity = Math.Max(1, feederCapacity); restockCost = Amount(restockCost);
        }

        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        private static float Positive(float value, float minimum) => Math.Max(minimum, Amount(value));

        public static BuildingDefinition[] Defaults() => new[]
        {
            new BuildingDefinition { kind = BuildingKind.ParkEntrance, displayName = "Park entrance", width = 5, depth = 5,
                height = 3.5f, openColor = new Color(0.8f, 0.67f, 0.38f), constructionCost = 0f,
                operatingCostPerSecond = .05f }, // 3 per minute.
            new BuildingDefinition { kind = BuildingKind.ExpeditionCentre, displayName = "Expedition centre", width = 8, depth = 6,
                height = 4f, openColor = new Color(0.38f, 0.58f, 0.74f), constructionCost = 500f,
                operatingCostPerSecond = .09f }, // 5.4 per minute.
            new BuildingDefinition { kind = BuildingKind.Kiosk, displayName = "Kiosk", width = 4, depth = 3,
                height = 2.5f, openColor = new Color(0.4f, 0.72f, 0.43f), constructionCost = 250f,
                operatingCostPerSecond = .05f }, // 3 per minute.
            new BuildingDefinition { kind = BuildingKind.GoatDispenser, displayName = "Goat dispenser", width = 1, depth = 1,
                height = 2.5f, openColor = new Color(0.65f, 0.5f, 0.3f), constructionCost = 150f,
                operatingCostPerSecond = .04f }, // 2.4 per minute, plus physical supply deliveries.
            new BuildingDefinition { kind = BuildingKind.RestArea, displayName = "Rest area", width = 4, depth = 3,
                height = 1.3f, openColor = new Color(0.56f, 0.38f, 0.22f), constructionCost = 120f, foodPrice = 0f,
                operatingCostPerSecond = .03f }, // 1.8 per minute; initially free visitor rest.
            new BuildingDefinition { kind = BuildingKind.ResearchCentre, displayName = "Research centre", width = 4, depth = 4,
                height = 10f, openColor = new Color(.4f,.58f,.75f), constructionCost = 600f,
                operatingCostPerSecond = .10f, staffSalaryPerSecond = .15f }, // 6 base + 9 per researcher / minute.
            new BuildingDefinition { kind = BuildingKind.MercenaryHQ, displayName = "Mercenary headquarters", width = 4, depth = 4,
                height = 8f, openColor = new Color(.54f,.58f,.4f), constructionCost = 450f,
                operatingCostPerSecond = .08f, staffSalaryPerSecond = .20f }, // 4.8 base + 12 per mercenary / minute.
            new BuildingDefinition { kind = BuildingKind.ZookeeperHut, displayName = "Zookeeper station", width = 4, depth = 2,
                height = 4f, openColor = new Color(.46f,.64f,.42f), constructionCost = 350f,
                operatingCostPerSecond = .06f, staffSalaryPerSecond = .10f } // 3.6 base + 6 per keeper / minute.
        };
    }

    public readonly struct BuildingFootprint
    {
        public BuildingDefinition Definition { get; }
        public Vector2Int Anchor { get; }
        public int Rotation { get; }
        public int Width => Rotation % 2 == 0 ? Definition.width : Definition.depth;
        public int Depth => Rotation % 2 == 0 ? Definition.depth : Definition.width;
        // One exterior path cell, immediately in front of the dedicated door.
        public Vector2Int AccessCell => Anchor + Rotate(new Vector2Int(Definition.DoorColumn, -1));
        public Vector2Int DoorCell => Anchor + Rotate(new Vector2Int(Definition.DoorColumn, 0));
        public FenceEdge EntranceEdge => DoorCell.x == AccessCell.x
            ? new FenceEdge(new Vector2Int(DoorCell.x, Math.Max(DoorCell.y, AccessCell.y)), true)
            : new FenceEdge(new Vector2Int(Math.Max(DoorCell.x, AccessCell.x), DoorCell.y), false);

        public BuildingFootprint(BuildingDefinition definition, Vector2Int anchor, int rotation)
        {
            if (definition == null || definition.width < 1 || definition.depth < 1)
                throw new ArgumentException("Building dimensions must be positive.");
            Definition = definition;
            Anchor = anchor;
            Rotation = ((rotation % 4) + 4) % 4;
        }

        private Vector2Int Rotate(Vector2Int cell)
        {
            switch (Rotation)
            {
                case 1: return new Vector2Int(Definition.depth - 1 - cell.y, cell.x);
                case 2: return new Vector2Int(Definition.width - 1 - cell.x, Definition.depth - 1 - cell.y);
                case 3: return new Vector2Int(cell.y, Definition.width - 1 - cell.x);
                default: return cell;
            }
        }

        public bool Contains(Vector2Int cell) => cell.x >= Anchor.x && cell.x < Anchor.x + Width
            && cell.y >= Anchor.y && cell.y < Anchor.y + Depth;

        public IEnumerable<Vector2Int> Cells()
        {
            for (int x = 0; x < Width; x++)
            for (int z = 0; z < Depth; z++) yield return Anchor + new Vector2Int(x, z);
        }

        public Vector3 Center(SquareBuildGrid grid)
        {
            var p=grid.Origin+new Vector3((Anchor.x+Width/2f)*grid.CellSize,0,(Anchor.y+Depth/2f)*grid.CellSize);
            p.y=grid.GroundHeight(p);return p;
        }
    }
}

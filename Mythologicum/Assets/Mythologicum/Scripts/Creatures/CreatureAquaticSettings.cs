using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Optional amphibious movement, habitat and water-hunting rules shared by species.</summary>
    [Serializable]
    public sealed class CreatureAquaticSettings
    {
        public bool canSwim;
        [Tooltip("Can walk along shallow pond edges before the water is deep enough to swim.")]
        public bool canWadeShallows = true;
        public bool preferWater = true;
        [Min(.05f)] public float minimumSwimDepth = .3f;
        [Min(.1f)] public float swimSpeed = 3f;
        [Min(.1f)] public float waterSearchRadius = 40f;
        [Min(0f), Tooltip("Nearby reachable water area in square metres needed for habitat satisfaction.")]
        public float preferredWaterArea = 48f;
        [Tooltip("Offset from the ordinary body-height origin while swimming, relative to the water surface.")]
        public float swimmingBodyOffset = -.5f;
        public float restingBodyOffset = -.65f;
        public bool dragPreyToWater;
        public CreatureTags preferredPreyTags = CreatureTags.Visitor | CreatureTags.Staff;
        [Min(.1f)] public float captureSeconds = .8f;
        [Min(.1f)] public float drowningSeconds = 3f;
        [Min(.05f)] public float dragSpeedMultiplier = .65f;

        public bool IsSwimmable(SquareBuildGrid grid, Vector2Int cell) => grid != null && grid.Contains(cell)
            && canSwim && grid.Habitat.IsWater(cell) && grid.Habitat.WaterDepth(cell) >= minimumSwimDepth;

        public bool CanOccupy(SquareBuildGrid grid, Vector2Int cell) => grid != null && grid.Contains(cell)
            && grid.ContentAt(cell) != BuildCellContent.Building
            && (grid.Habitat.IsWater(cell) ? IsSwimmable(grid, cell)
                || canSwim && canWadeShallows && (grid.Terrain?.Walkable(cell) ?? true) : grid.GroundWalkable(cell));

        public void Validate()
        {
            minimumSwimDepth = Mathf.Max(.05f, Amount(minimumSwimDepth));
            swimSpeed = Mathf.Max(.1f, Amount(swimSpeed)); waterSearchRadius = Mathf.Max(.1f, Amount(waterSearchRadius));
            preferredWaterArea = Amount(preferredWaterArea);
            swimmingBodyOffset = Signed(swimmingBodyOffset); restingBodyOffset = Signed(restingBodyOffset);
            captureSeconds = Mathf.Max(.1f, Amount(captureSeconds)); drowningSeconds = Mathf.Max(.1f, Amount(drowningSeconds));
            dragSpeedMultiplier = Mathf.Max(.05f, Amount(dragSpeedMultiplier));
        }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
        private static float Signed(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    }
}

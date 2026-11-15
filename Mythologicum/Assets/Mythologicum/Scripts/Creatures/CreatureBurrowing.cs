using UnityEngine;

namespace Mythologicum
{
    /// <summary>Ground restrictions shared by burrow route planning and actual traversal.</summary>
    public static class CreatureBurrowing
    {
        public static bool CanDigGround(SquareBuildGrid grid, Vector2Int cell) => grid != null
            && grid.Contains(cell) && grid.GroundWalkable(cell)
            && grid.ContentAt(cell) == BuildCellContent.Empty;
    }
}

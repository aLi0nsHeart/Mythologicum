using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Natural ground supplies grass. A grazed patch needs time to recover.</summary>
    public sealed class GrazingField
    {
        private readonly SquareBuildGrid grid;
        private readonly Dictionary<Vector2Int, float> recoversAt = new Dictionary<Vector2Int, float>();
        private readonly float regrowthSeconds;
        public GrazingField(SquareBuildGrid grid, float regrowthSeconds = 20f)
        { this.grid = grid; this.regrowthSeconds = Math.Max(0f, regrowthSeconds); }

        public bool HasFood(Vector2Int cell, float now) => grid.Contains(cell)
            && grid.ContentAt(cell) == BuildCellContent.Empty
            && (!recoversAt.TryGetValue(cell, out float ready) || now >= ready);

        public bool TryConsume(Vector2Int cell, float now)
        {
            if (!HasFood(cell, now)) return false;
            recoversAt[cell] = now + regrowthSeconds;
            return true;
        }
    }
}

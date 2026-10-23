using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Fills skipped mouse positions with cells connected along their edges.</summary>
    public static class GridStroke
    {
        public static IEnumerable<Vector2Int> Between(Vector2Int start, Vector2Int end)
        {
            int x = start.x, z = start.y;
            int dx = Math.Abs(end.x - x), dz = Math.Abs(end.y - z);
            int sx = Math.Sign(end.x - x), sz = Math.Sign(end.y - z);
            int ix = 0, iz = 0;
            yield return start;
            while (ix < dx || iz < dz)
            {
                if (ix < dx && (iz == dz || (1L + 2L * ix) * dz <= (1L + 2L * iz) * dx))
                { x += sx; ix++; }
                else
                { z += sz; iz++; }
                yield return new Vector2Int(x, z);
            }
        }
    }
}

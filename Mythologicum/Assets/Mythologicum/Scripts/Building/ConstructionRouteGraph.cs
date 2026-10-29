using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public static class ConstructionRouteGraph
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        public static IEnumerable<Vector2Int> PathNeighbors(Vector2Int cell, SquareBuildGrid grid, FenceLayout fences)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                var separatingEdge = direction.x != 0
                    ? new FenceEdge(new Vector2Int(Math.Max(cell.x, next.x), cell.y), false)
                    : new FenceEdge(new Vector2Int(cell.x, Math.Max(cell.y, next.y)), true);
                if (grid.Contains(next) && grid.CanTraverse(cell,next,true) && !fences.BlocksPath(separatingEdge)) yield return next;
            }
        }

        public static IEnumerable<FenceEdge> FenceNeighbors(FenceEdge edge, FenceLayout layout)
        {
            foreach (Vector2Int node in new[] { edge.Start, edge.End })
            foreach (Vector2Int direction in Directions)
            {
                FenceEdge next = FenceEdge.FromNodes(node, node + direction);
                if (!next.Equals(edge) && layout.Contains(next)) yield return next;
            }
        }

        public static int CellDistance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);

        public static int EdgeDistance(FenceEdge a, FenceEdge b)
        {
            Vector2Int centerA = a.Start * 2 + (a.AlongX ? Vector2Int.right : Vector2Int.up);
            Vector2Int centerB = b.Start * 2 + (b.AlongX ? Vector2Int.right : Vector2Int.up);
            return CellDistance(centerA, centerB) / 2;
        }
    }
}

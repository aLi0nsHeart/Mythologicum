using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>
    /// Shares geometry-only exhibit sight tests between guest decisions in one simulation tick.
    /// Reachability and each guest's travel score remain individual decisions.
    /// </summary>
    internal sealed class VisitorViewingCache
    {
        internal readonly struct Point
        {
            public readonly Vector2Int Cell;
            public readonly float Distance;
            public Point(Vector2Int cell, float distance) { Cell = cell; Distance = distance; }
        }

        private readonly SquareBuildGrid grid;
        private readonly Func<Vector2Int, Vector2Int, bool> visible;
        private readonly Dictionary<(Vector2Int cell, Vector3 position), List<Point>> entries
            = new Dictionary<(Vector2Int, Vector3), List<Point>>();
        private readonly Stack<List<Point>> pool = new Stack<List<Point>>();
        private float range;

        public VisitorViewingCache(SquareBuildGrid grid, Func<Vector2Int, Vector2Int, bool> visible)
        { this.grid = grid; this.visible = visible; }

        public void BeginTick(float viewRange)
        {
            foreach (var points in entries.Values) { points.Clear(); pool.Push(points); }
            entries.Clear(); range = viewRange;
        }

        public IReadOnlyList<Point> ForAnimal(Vector2Int cell, Vector3 position)
        {
            var key = (cell, position);
            if (entries.TryGetValue(key, out var points)) return points;
            points = pool.Count > 0 ? pool.Pop() : new List<Point>();
            entries.Add(key, points);
            if (range < 0) return points;
            int minX = 0, minZ = 0, maxX = grid.Width - 1, maxZ = grid.Depth - 1;
            if (!float.IsNaN(range) && !float.IsInfinity(range))
            {
                int radius = (int)Math.Min(Math.Max(grid.Width, grid.Depth), Math.Ceiling(range / grid.CellSize));
                var center = grid.WorldToCell(position);
                minX = Math.Max(0, center.x - radius); maxX = Math.Min(maxX, center.x + radius);
                minZ = Math.Max(0, center.y - radius); maxZ = Math.Min(maxZ, center.y + radius);
            }
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                var path = new Vector2Int(x, z);
                if (grid.ContentAt(path) != BuildCellContent.Path) continue;
                Vector3 point = grid.CellCenter(path);
                float distance = new Vector2(point.x - position.x, point.z - position.z).magnitude;
                if (distance > range || !visible(path, cell)) continue;
                points.Add(new Point(path, distance));
            }
            return points;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Ordinary guests use paths and staff gates; frightened guests can run across clear ground.</summary>
    public sealed class VisitorNavigation
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly Predicate<Vector2Int> clear;

        public VisitorNavigation(SquareBuildGrid grid, FenceLayout fences, Predicate<Vector2Int> clear = null)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
            this.clear = clear;
        }

        public bool IsWalkable(Vector2Int cell, bool allowGround = false) => grid.Contains(cell)
            && grid.GroundWalkable(cell,!allowGround)
            && grid.ContentAt(cell) != BuildCellContent.Building
            && (allowGround || grid.ContentAt(cell) == BuildCellContent.Path)
            && (clear == null || clear(cell));

        public bool CanCross(Vector2Int from, Vector2Int to, bool allowGround = false)
        {
            if (Distance(from, to) != 1 || !IsWalkable(from, allowGround) || !IsWalkable(to, allowGround)) return false;
            FenceEdge edge = from.x != to.x
                ? new FenceEdge(new Vector2Int(Math.Max(from.x, to.x), from.y), false)
                : new FenceEdge(new Vector2Int(from.x, Math.Max(from.y, to.y)), true);
            // Panic never opens a physical barrier. Normal path travel can use authorized gates.
            return allowGround ? !fences.HasFence(edge) : !fences.BlocksPath(edge);
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, bool allowGround = false) =>
            ConstructionRouteFinder.Find(start, goal, cell => Neighbors(cell, allowGround),
                cell => IsWalkable(cell, allowGround), Distance,grid.MovementCost);

        public List<Vector2Int> Reachable(Vector2Int start, bool allowGround = false)
        {
            var result = new List<Vector2Int>();
            if (!IsWalkable(start, allowGround)) return result;
            var visited = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue(); result.Add(cell);
                foreach (Vector2Int next in Neighbors(cell, allowGround))
                    if (visited.Add(next)) pending.Enqueue(next);
            }
            return result;
        }

        private IEnumerable<Vector2Int> Neighbors(Vector2Int cell, bool allowGround)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (CanCross(cell, next, allowGround)) yield return next;
            }
        }
        private static int Distance(Vector2Int from, Vector2Int to) => Math.Abs(from.x - to.x) + Math.Abs(from.y - to.y);
    }
}

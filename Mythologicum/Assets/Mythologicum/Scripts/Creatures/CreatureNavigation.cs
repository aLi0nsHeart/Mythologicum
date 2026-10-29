using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>
    /// Animals travel on any clear ground, independently of paths and enclosure identities.
    /// Fence edges, including closed staff gates, are physical barriers to ordinary movement.
    /// Climbing route planning can opt into wooden crossings; animation and actual crossing remain the agent's responsibility.
    /// </summary>
    public sealed class CreatureNavigation
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly Predicate<Vector2Int> physicalCellClear;
        private readonly bool canClimbWoodenFences;
        private readonly bool canPhaseFences, canAttackFences;

        public CreatureNavigation(SquareBuildGrid grid, FenceLayout fences,
            Predicate<Vector2Int> physicalCellClear = null, bool canClimbWoodenFences = false,
            bool canPhaseFences = false, bool canAttackFences = false)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
            this.physicalCellClear = physicalCellClear;
            this.canClimbWoodenFences = canClimbWoodenFences;
            this.canPhaseFences = canPhaseFences;
            this.canAttackFences = canAttackFences;
        }

        public bool IsWalkable(Vector2Int cell) => grid.Contains(cell)
            && grid.GroundWalkable(cell)
            && grid.ContentAt(cell) != BuildCellContent.Building
            && (physicalCellClear == null || physicalCellClear(cell));

        public bool CanCross(Vector2Int from, Vector2Int to)
        {
            if (Distance(from, to) != 1 || !IsWalkable(from) || !IsWalkable(to) || !grid.CanTraverse(from,to)) return false;
            var edge = from.x != to.x
                ? new FenceEdge(new Vector2Int(Math.Max(from.x, to.x), from.y), false)
                : new FenceEdge(new Vector2Int(from.x, Math.Max(from.y, to.y)), true);
            return !fences.HasFence(edge) || (canClimbWoodenFences && fences.CanClimb(edge))
                || (canPhaseFences && fences.CanPhase(edge))
                || (canAttackFences && fences.KindAt(edge) != FenceKind.Heavy);
        }

        public IEnumerable<Vector2Int> Neighbors(Vector2Int cell)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (CanCross(cell, next)) yield return next;
            }
        }

        /// <summary>Returns a cardinal route including both endpoints; empty means unreachable.</summary>
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal) =>
            ConstructionRouteFinder.Find(start, goal, Neighbors, IsWalkable, Distance,grid.MovementCost);

        /// <summary>Returns clear cells within the movement budget, including the starting cell.</summary>
        public List<Vector2Int> Reachable(Vector2Int start, int maxSteps)
        {
            if (maxSteps < 0) throw new ArgumentOutOfRangeException(nameof(maxSteps));
            var result = new List<Vector2Int>();
            if (!IsWalkable(start)) return result;

            var frontier = new Queue<Vector2Int>();
            var depth = new Dictionary<Vector2Int, int> { [start] = 0 };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                Vector2Int cell = frontier.Dequeue();
                result.Add(cell);
                if (depth[cell] >= maxSteps) continue;
                foreach (Vector2Int next in Neighbors(cell))
                {
                    if (depth.ContainsKey(next)) continue;
                    depth.Add(next, depth[cell] + 1);
                    frontier.Enqueue(next);
                }
            }
            return result;
        }

        /// <summary>
        /// Tests a centre-to-centre supercover ray. Buildings and physical obstacles obscure sight;
        /// open wooden fencing and closed gates allow sight even when movement is impossible.
        /// Cells touched at a corner are tested too, preventing sight through building corners.
        /// </summary>
        public bool HasLineOfSight(Vector2Int start, Vector2Int end)
        {
            if (!IsWalkable(start) || !IsWalkable(end) || !grid.GroundLineOfSight(start,end)) return false;
            int dx = end.x - start.x, dy = end.y - start.y;
            int nx = Math.Abs(dx), ny = Math.Abs(dy);
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            int ix = 0, iy = 0;
            Vector2Int cell = start;
            while (ix < nx || iy < ny)
            {
                // Integer comparison of the next vertical and horizontal boundary crossings.
                long decision = (1L + 2L * ix) * ny - (1L + 2L * iy) * nx;
                if (decision == 0)
                {
                    if (!IsWalkable(cell + new Vector2Int(sx, 0))
                        || !IsWalkable(cell + new Vector2Int(0, sy))) return false;
                    cell += new Vector2Int(sx, sy);
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    cell += new Vector2Int(sx, 0);
                    ix++;
                }
                else
                {
                    cell += new Vector2Int(0, sy);
                    iy++;
                }
                if (!IsWalkable(cell)) return false;
            }
            return true;
        }

        private static int Distance(Vector2Int a, Vector2Int b) =>
            Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>
    /// Prepares the entrance's public-path component over several updates. Results
    /// retain VisitorNavigation's BFS order, so exhibit selection ties stay unchanged.
    /// Returned lists are cache-owned and must be consumed before the next invalidation.
    /// </summary>
    internal sealed class VisitorArrivalPaths
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly VisitorNavigation navigation;
        private readonly List<Vector2Int> paths;
        private readonly Queue<Vector2Int> pending;
        private readonly HashSet<Vector2Int> visited;
        private Vector2Int entrance;
        private int gridRevision, fenceRevision;
        private bool hasEntrance, ready;

        public VisitorArrivalPaths(SquareBuildGrid grid, FenceLayout fences, VisitorNavigation navigation)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            // Reserve once while preparing the park, rather than growing collections
            // on the gameplay frame that discovers another path cell.
            int capacity = checked(grid.Width * grid.Depth);
            paths = new List<Vector2Int>(capacity);
            pending = new Queue<Vector2Int>(capacity);
            visited = new HashSet<Vector2Int>(capacity);
        }

        public bool Prepare(Vector2Int start, int maxCells)
        {
            if (!Matches(start)) Begin(start);
            int remaining = Math.Max(1, maxCells);
            while (!ready && remaining-- > 0)
            {
                Vector2Int cell = pending.Dequeue();
                paths.Add(cell);
                foreach (Vector2Int direction in Directions)
                {
                    Vector2Int next = cell + direction;
                    if (navigation.CanCross(cell, next) && visited.Add(next)) pending.Enqueue(next);
                }
                ready = pending.Count == 0;
            }
            return ready;
        }

        public bool TryGet(Vector2Int start, out List<Vector2Int> result)
        {
            result = ready && Matches(start) ? paths : null;
            return result != null;
        }

        public List<Vector2Int> GetOrBuild(Vector2Int start)
        {
            // Only one entrance is cached. Ordinary guests elsewhere keep their
            // own exact BFS rather than retaining one map-sized list per actor.
            if (hasEntrance && entrance != start) return navigation.Reachable(start);
            Prepare(start, int.MaxValue);
            return paths;
        }

        private bool Matches(Vector2Int start) => hasEntrance && entrance == start
            && gridRevision == grid.Revision && fenceRevision == fences.Revision;

        private void Begin(Vector2Int start)
        {
            hasEntrance = true; entrance = start;
            gridRevision = grid.Revision; fenceRevision = fences.Revision;
            paths.Clear(); pending.Clear(); visited.Clear();
            ready = !navigation.IsWalkable(start);
            if (ready) return;
            visited.Add(start); pending.Enqueue(start);
        }
    }
}

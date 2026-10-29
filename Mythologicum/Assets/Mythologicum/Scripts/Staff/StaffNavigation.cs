using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Employees use clear ground and authorized staff gates; patrols can be limited to paths.</summary>
    public sealed class StaffNavigation
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        private readonly SquareBuildGrid grid;
        private readonly FenceLayout fences;
        private readonly Predicate<Vector2Int> clear;
        private readonly CreatureNavigation sight;

        public StaffNavigation(SquareBuildGrid grid, FenceLayout fences, Predicate<Vector2Int> clear = null)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            this.fences = fences ?? throw new ArgumentNullException(nameof(fences));
            this.clear = clear;
            sight = new CreatureNavigation(grid, fences, clear);
        }

        public bool IsWalkable(Vector2Int cell, bool pathsOnly = false) => grid.Contains(cell)
            && grid.GroundWalkable(cell,pathsOnly)
            && grid.ContentAt(cell) != BuildCellContent.Building
            && (!pathsOnly || grid.ContentAt(cell) == BuildCellContent.Path)
            && (clear == null || clear(cell));

        public bool CanCross(Vector2Int from, Vector2Int to, bool pathsOnly = false)
        {
            if (Distance(from,to) != 1 || !IsWalkable(from,pathsOnly) || !IsWalkable(to,pathsOnly)) return false;
            FenceEdge edge = from.x != to.x
                ? new FenceEdge(new Vector2Int(Math.Max(from.x,to.x),from.y),false)
                : new FenceEdge(new Vector2Int(from.x,Math.Max(from.y,to.y)),true);
            return !fences.HasFence(edge) || fences.IsGate(edge);
        }

        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal, bool pathsOnly = false) =>
            ConstructionRouteFinder.Find(start,goal,cell => Neighbors(cell,pathsOnly),
                cell => IsWalkable(cell,pathsOnly),Distance,grid.MovementCost);

        public List<Vector2Int> Reachable(Vector2Int start, bool pathsOnly = false)
        {
            var result = new List<Vector2Int>();
            if (!IsWalkable(start,pathsOnly)) return result;
            var visited = new HashSet<Vector2Int> { start };
            var pending = new Queue<Vector2Int>(); pending.Enqueue(start);
            while (pending.Count > 0)
            {
                Vector2Int cell = pending.Dequeue(); result.Add(cell);
                foreach (Vector2Int next in Neighbors(cell,pathsOnly))
                    if (visited.Add(next)) pending.Enqueue(next);
            }
            return result;
        }

        public bool HasLineOfSight(Vector2Int start, Vector2Int end) => sight.HasLineOfSight(start,end);
        /// <summary>Unlike observing an exhibit, a shot must not cross a fence or closed gate.</summary>
        public bool HasClearShot(Vector2Int start, Vector2Int end)
        {
            if (!HasLineOfSight(start,end)) return false;
            int dx=end.x-start.x, dy=end.y-start.y;
            int nx=Math.Abs(dx), ny=Math.Abs(dy), sx=Math.Sign(dx), sy=Math.Sign(dy), ix=0, iy=0;
            Vector2Int cell=start;
            while (ix<nx || iy<ny)
            {
                long crossing=(1L+2L*ix)*ny-(1L+2L*iy)*nx;
                if (crossing==0)
                {
                    Vector2Int horizontal=cell+new Vector2Int(sx,0), vertical=cell+new Vector2Int(0,sy);
                    Vector2Int diagonal=cell+new Vector2Int(sx,sy);
                    if (!ShotEdgeOpen(cell,horizontal) || !ShotEdgeOpen(cell,vertical)
                        || !ShotEdgeOpen(horizontal,diagonal) || !ShotEdgeOpen(vertical,diagonal)) return false;
                    cell=diagonal; ix++; iy++;
                }
                else if (crossing<0)
                {
                    Vector2Int next=cell+new Vector2Int(sx,0);
                    if (!ShotEdgeOpen(cell,next)) return false;
                    cell=next; ix++;
                }
                else
                {
                    Vector2Int next=cell+new Vector2Int(0,sy);
                    if (!ShotEdgeOpen(cell,next)) return false;
                    cell=next; iy++;
                }
            }
            return true;
        }
        private bool ShotEdgeOpen(Vector2Int from,Vector2Int to)
        {
            var edge=from.x!=to.x ? new FenceEdge(new Vector2Int(Math.Max(from.x,to.x),from.y),false)
                : new FenceEdge(new Vector2Int(from.x,Math.Max(from.y,to.y)),true);
            return !fences.HasFence(edge);
        }
        private IEnumerable<Vector2Int> Neighbors(Vector2Int cell,bool pathsOnly)
        {
            foreach (Vector2Int direction in Directions)
            {
                Vector2Int next = cell + direction;
                if (CanCross(cell,next,pathsOnly)) yield return next;
            }
        }
        private static int Distance(Vector2Int a,Vector2Int b) => Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y);
    }
}

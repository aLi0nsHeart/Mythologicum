using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum FenceKind { Wooden, Spiked }

    /// <summary>A canonical grid edge, shared by the cells on either side.</summary>
    public readonly struct FenceEdge : IEquatable<FenceEdge>
    {
        public Vector2Int Start { get; }
        public bool AlongX { get; }
        public Vector2Int End => Start + (AlongX ? Vector2Int.right : Vector2Int.up);
        public Vector2Int CellA => Start;
        public Vector2Int CellB => Start + (AlongX ? Vector2Int.down : Vector2Int.left);

        public FenceEdge(Vector2Int start, bool alongX) { Start = start; AlongX = alongX; }

        public static FenceEdge FromNodes(Vector2Int a, Vector2Int b)
        {
            if (Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) != 1)
                throw new ArgumentException("Fence endpoints must be adjacent grid nodes.");
            return new FenceEdge(new Vector2Int(Math.Min(a.x, b.x), Math.Min(a.y, b.y)), a.y == b.y);
        }

        public bool Equals(FenceEdge other) => Start == other.Start && AlongX == other.AlongX;
        public override bool Equals(object obj) => obj is FenceEdge other && Equals(other);
        public override int GetHashCode() => unchecked(Start.GetHashCode() * 397 ^ AlongX.GetHashCode());

        public static IEnumerable<FenceEdge> Between(FenceEdge from, FenceEdge to)
        {
            yield return from;
            Vector2Int a = from.Start, b = to.Start;
            int distance = int.MaxValue;
            foreach (Vector2Int source in new[] { from.Start, from.End })
            foreach (Vector2Int target in new[] { to.Start, to.End })
            {
                int candidate = Math.Abs(source.x - target.x) + Math.Abs(source.y - target.y);
                if (candidate < distance) { a = source; b = target; distance = candidate; }
            }
            Vector2Int previous = a;
            foreach (Vector2Int node in GridStroke.Between(a, b))
            {
                if (node != previous) yield return FromNodes(previous, node);
                previous = node;
            }
            if (!from.Equals(to)) yield return to;
        }
    }

    public sealed class FenceLayout
    {
        public int Revision { get; private set; }
        private readonly SquareBuildGrid grid;
        private readonly HashSet<FenceEdge> edges = new HashSet<FenceEdge>();
        private readonly HashSet<FenceEdge> gates = new HashSet<FenceEdge>();
        private readonly Dictionary<FenceEdge, FenceKind> kinds = new Dictionary<FenceEdge, FenceKind>();
        public IEnumerable<FenceEdge> Edges => edges;
        public IEnumerable<FenceEdge> Gates => gates;
        public FenceLayout(SquareBuildGrid grid) => this.grid = grid;

        public bool Contains(FenceEdge edge) => edge.Start.x >= 0 && edge.Start.y >= 0
            && (edge.AlongX ? edge.Start.x < grid.Width && edge.Start.y <= grid.Depth
                : edge.Start.x <= grid.Width && edge.Start.y < grid.Depth);
        public bool HasFence(FenceEdge edge) => edges.Contains(edge);
        public bool IsGate(FenceEdge edge) => gates.Contains(edge);
        public FenceKind KindAt(FenceEdge edge) => kinds.TryGetValue(edge, out FenceKind kind) ? kind : FenceKind.Wooden;
        public bool CanClimb(FenceEdge edge) => HasFence(edge) && !IsGate(edge) && KindAt(edge) == FenceKind.Wooden;
        // Closed gates contain creatures, but provide an authorized staff/path connection.
        public bool BlocksPath(FenceEdge edge) => HasFence(edge) && !IsGate(edge);
        public bool TrySetGate(FenceEdge edge)
        {
            if (!HasFence(edge) || KindAt(edge) != FenceKind.Wooden || !gates.Add(edge)) return false;
            Revision++;
            return true;
        }
        public bool TryAdd(FenceEdge edge, FenceKind kind = FenceKind.Wooden)
        {
            if (!Contains(edge) || !edges.Add(edge)) return false;
            kinds.Add(edge, kind);
            Revision++;
            return true;
        }
        public bool TrySetKind(FenceEdge edge, FenceKind kind)
        {
            if (!HasFence(edge) || IsGate(edge) || KindAt(edge) == kind) return false;
            kinds[edge] = kind;
            Revision++;
            return true;
        }
        public bool TryRemove(FenceEdge edge)
        {
            if (!edges.Remove(edge)) return false;
            gates.Remove(edge);
            kinds.Remove(edge);
            Revision++;
            return true;
        }

        public FenceEdge NearestEdge(Vector3 position)
        {
            double x = (position.x - grid.Origin.x) / grid.CellSize;
            double z = (position.z - grid.Origin.z) / grid.CellSize;
            int nx = (int)Math.Round(x, MidpointRounding.AwayFromZero);
            int nz = (int)Math.Round(z, MidpointRounding.AwayFromZero);
            if (Math.Abs(z - nz) <= Math.Abs(x - nx))
                return new FenceEdge(new Vector2Int(Math.Max(0, Math.Min(grid.Width - 1, (int)Math.Floor(x))), nz), true);
            return new FenceEdge(new Vector2Int(nx, Math.Max(0, Math.Min(grid.Depth - 1, (int)Math.Floor(z)))), false);
        }

        public Vector3 Center(FenceEdge edge) => grid.Origin + new Vector3(
            (edge.Start.x + (edge.AlongX ? 0.5f : 0f)) * grid.CellSize, 0f,
            (edge.Start.y + (edge.AlongX ? 0f : 0.5f)) * grid.CellSize);

        public bool WouldSplitPath(FenceEdge edge) => grid.ContentAt(edge.CellA) == BuildCellContent.Path
            && grid.ContentAt(edge.CellB) == BuildCellContent.Path;

        public bool WouldConnectAcrossFence(Vector2Int cell)
        {
            foreach (FenceEdge edge in new[] { new FenceEdge(cell, true), new FenceEdge(cell, false),
                new FenceEdge(cell + Vector2Int.up, true), new FenceEdge(cell + Vector2Int.right, false) })
            {
                Vector2Int neighbor = edge.CellA == cell ? edge.CellB : edge.CellA;
                if (BlocksPath(edge) && grid.ContentAt(neighbor) == BuildCellContent.Path) return true;
            }
            return false;
        }
    }
}

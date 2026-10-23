using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum BuildCellContent { Empty, Building, Path }

    /// <summary>Cell coordinates and occupancy, independent of camera and presentation.</summary>
    public sealed class SquareBuildGrid
    {
        public float CellSize { get; }
        public Vector3 Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int Revision { get; private set; }
        private readonly Dictionary<Vector2Int, BuildCellContent> occupied = new Dictionary<Vector2Int, BuildCellContent>();

        public SquareBuildGrid(Vector3 origin, float cellSize, int width, int depth)
        {
            if (cellSize <= 0f || width < 1 || depth < 1)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Depth = depth;
        }

        public Vector2Int WorldToCell(Vector3 position) => new Vector2Int(
            (int)Math.Floor((position.x - Origin.x) / CellSize),
            (int)Math.Floor((position.z - Origin.z) / CellSize));

        public Vector3 CellCenter(Vector2Int cell) => Origin
            + new Vector3((cell.x + 0.5f) * CellSize, 0f, (cell.y + 0.5f) * CellSize);

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.x < Width
            && cell.y >= 0 && cell.y < Depth;

        public BuildCellContent ContentAt(Vector2Int cell) => occupied.TryGetValue(cell, out var content)
            ? content : BuildCellContent.Empty;

        public bool IsAvailable(Vector2Int cell) => Contains(cell) && !occupied.ContainsKey(cell);

        public bool TryOccupy(Vector2Int cell, BuildCellContent content = BuildCellContent.Building)
        {
            if (content == BuildCellContent.Empty || !IsAvailable(cell)) return false;
            occupied.Add(cell, content);
            Revision++;
            return true;
        }

        public bool TryRemove(Vector2Int cell, BuildCellContent expectedContent)
        {
            if (expectedContent == BuildCellContent.Empty || ContentAt(cell) != expectedContent) return false;
            if (!occupied.Remove(cell)) return false;
            Revision++;
            return true;
        }
    }
}

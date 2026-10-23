using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>One-cell path tiles share the construction grid with buildings.</summary>
    public sealed class PathConstruction
    {
        public const float Height = 0.08f;
        private readonly SquareBuildGrid grid;
        private readonly Transform parent;
        private readonly Material material;
        private readonly Dictionary<Vector2Int, GameObject> tiles = new Dictionary<Vector2Int, GameObject>();

        public PathConstruction(SquareBuildGrid grid, Transform parent, Material material)
        {
            this.grid = grid;
            this.parent = parent;
            this.material = material;
        }

        public bool TryPlace(Vector2Int cell)
        {
            if (!grid.TryOccupy(cell, BuildCellContent.Path)) return false;
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Trigger colliders allow demolition selection without blocking construction queries.
            tile.GetComponent<Collider>().isTrigger = true;
            tile.name = $"Path [{cell.x}, {cell.y}]";
            tile.transform.SetParent(parent);
            tile.transform.position = grid.CellCenter(cell) + Vector3.up * (Height / 2f);
            tile.transform.localScale = new Vector3(grid.CellSize, Height, grid.CellSize);
            tile.GetComponent<MeshRenderer>().sharedMaterial = material;
            tiles.Add(cell, tile);
            tile.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                grid.TryRemove(cell, BuildCellContent.Path);
                tiles.Remove(cell);
            });
            return true;
        }

        public bool TryRemove(Vector2Int cell)
        {
            return tiles.TryGetValue(cell, out GameObject tile) && tile.GetComponent<DemolitionTarget>().Demolish();
        }
    }
}

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
        private readonly GameObject tilePrefab;
        private readonly Dictionary<Vector2Int, GameObject> tiles = new Dictionary<Vector2Int, GameObject>();

        public PathConstruction(SquareBuildGrid grid, Transform parent, Material material, GameObject tilePrefab = null)
        {
            this.grid = grid;
            this.parent = parent;
            this.material = material;
            this.tilePrefab = tilePrefab;
        }

        public bool TryPlace(Vector2Int cell)
        {
            if(!grid.GroundWalkable(cell,true))return false;
            if (!grid.TryOccupy(cell, BuildCellContent.Path)) return false;
            var tile = new GameObject($"Path [{cell.x}, {cell.y}]");
            tile.transform.SetParent(parent, false);
            tile.transform.position = grid.CellCenter(cell) + Vector3.up * (Height / 2f);
            // Selection remains a simple cell-sized trigger; imported art never blocks navigation.
            var collider = tile.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(grid.CellSize, Height, grid.CellSize);
            CreateVisual(tile.transform);
            FitSelection(tile);
            tiles.Add(cell, tile);
            tile.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                grid.TryRemove(cell, BuildCellContent.Path);
                tiles.Remove(cell);
            });
            return true;
        }

        public void CreateVisual(Transform parent)
        {
            if(grid.Terrain!=null)
            {
                var visual=new GameObject("Conforming path");visual.transform.SetParent(parent,false);
                visual.AddComponent<GroundConformingVisual>().Initialize(grid,material);return;
            }
            if (tilePrefab != null)
            {
                var visual = Object.Instantiate(tilePrefab, parent, false);
                foreach (Collider importedCollider in visual.GetComponentsInChildren<Collider>(true))
                    importedCollider.enabled = false;
                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = RenderBounds(renderers);
                    visual.transform.localScale = Vector3.Scale(visual.transform.localScale,
                        new Vector3(grid.CellSize / Mathf.Max(0.001f, bounds.size.x),
                            Height / Mathf.Max(0.001f, bounds.size.y), grid.CellSize / Mathf.Max(0.001f, bounds.size.z)));
                    visual.transform.position += parent.position - RenderBounds(renderers).center;
                }
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(parent, false);
                visual.transform.localScale = new Vector3(grid.CellSize, Height, grid.CellSize);
                visual.GetComponent<Collider>().enabled = false;
                visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        private static Bounds RenderBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        private void FitSelection(GameObject tile)
        {
            if(grid.Terrain==null)return;
            var bounds=tile.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            var collider=tile.GetComponent<BoxCollider>();collider.center=bounds.center;
            collider.size=new Vector3(grid.CellSize,Mathf.Max(Height,bounds.size.y),grid.CellSize);
        }
        public void RefreshTerrain()
        {
            if(grid.Terrain==null)return;
            foreach(var pair in tiles)
            {
                pair.Value.transform.position=grid.CellCenter(pair.Key)+Vector3.up*(Height/2);
                pair.Value.GetComponentInChildren<GroundConformingVisual>().Refresh();FitSelection(pair.Value);
            }
        }

        public bool TryRemove(Vector2Int cell)
        {
            return tiles.TryGetValue(cell, out GameObject tile) && tile.GetComponent<DemolitionTarget>().Demolish();
        }
    }
}

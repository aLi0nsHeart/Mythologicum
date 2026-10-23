using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Walkable enrichment, tracked separately from path and building occupancy.</summary>
    public sealed class TreeConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly Transform parent;
        private readonly Material trunkMaterial, leafMaterial;
        private readonly Dictionary<Vector2Int, GameObject> trees = new Dictionary<Vector2Int, GameObject>();
        private bool disposed;

        public TreeConstruction(SquareBuildGrid grid, EnvironmentGrid environment, Transform parent, Material baseMaterial)
        {
            this.grid = grid;
            this.environment = environment;
            this.parent = parent;
            trunkMaterial = new Material(baseMaterial) { name = "Tree bark", color = new Color(0.36f, 0.22f, 0.12f) };
            leafMaterial = new Material(baseMaterial) { name = "Tree leaves", color = new Color(0.16f, 0.43f, 0.21f) };
        }

        public bool Occupies(Vector2Int cell) => trees.ContainsKey(cell);

        public bool CanPlace(Vector2Int cell)
        {
            if (disposed || !grid.IsAvailable(cell) || Occupies(cell) || environment.TreesAt(cell) > 0) return false;
            return !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * 1.5f,
                new Vector3(Mathf.Max(0.01f, grid.CellSize / 2f - 0.08f), 1.49f,
                    Mathf.Max(0.01f, grid.CellSize / 2f - 0.08f)),
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (!CanPlace(cell) || !environment.AddTree(cell)) return false;
            var root = new GameObject($"Tree [{cell.x}, {cell.y}]");
            root.transform.SetParent(parent, false);
            root.transform.position = grid.CellCenter(cell);
            Part(root.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1f, 0f),
                new Vector3(0.28f, 1f, 0.28f), trunkMaterial);
            Part(root.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 2.15f, 0f),
                new Vector3(1.6f, 1.8f, 1.6f), leafMaterial);
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(1.6f, 3.05f, 1.6f);
            collider.center = Vector3.up * 1.525f;
            trees.Add(cell, root);
            root.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                environment.RemoveTree(cell);
                trees.Remove(cell);
            });
            return true;
        }

        private static void Part(Transform parent, string name, PrimitiveType primitive, Vector3 position,
            Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var pair in trees)
            {
                environment.RemoveTree(pair.Key);
                if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value);
            }
            trees.Clear();
            UnityEngine.Object.Destroy(trunkMaterial);
            UnityEngine.Object.Destroy(leafMaterial);
        }
    }
}

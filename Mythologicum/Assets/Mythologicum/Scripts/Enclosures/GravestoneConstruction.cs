using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Walkable graveyard enrichment and Yurei sleep sites, separate from grid occupancy.</summary>
    public sealed class GravestoneConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly Transform parent;
        private readonly Material stoneMaterial;
        private readonly Dictionary<Vector2Int, GameObject> stones = new Dictionary<Vector2Int, GameObject>();
        private bool disposed;

        public GravestoneConstruction(SquareBuildGrid grid, EnvironmentGrid environment, Transform parent, Material material)
        {
            this.grid = grid; this.environment = environment; this.parent = parent;
            stoneMaterial = new Material(material) { name = "Gravestone stone", color = new Color(.46f, .48f, .49f) };
        }

        public bool Occupies(Vector2Int cell) => stones.ContainsKey(cell);
        public bool CanPlace(Vector2Int cell)
        {
            if (disposed || !grid.IsAvailable(cell) || Occupies(cell) || environment.GravestonesAt(cell) > 0
                || environment.TreesAt(cell) > 0) return false;
            float half = Mathf.Max(.01f, grid.CellSize / 2f - .08f);
            return !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * .65f,
                new Vector3(half, .64f, half), Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (!CanPlace(cell) || !environment.AddGravestone(cell)) return false;
            var root = new GameObject($"Gravestone [{cell.x}, {cell.y}]");
            root.transform.SetParent(parent, false); root.transform.position = grid.CellCenter(cell);
            CreateVisual(root.transform);
            var collider = root.AddComponent<BoxCollider>();
            collider.isTrigger = true; collider.center = Vector3.up * .6f;
            collider.size = new Vector3(grid.CellSize * .75f, 1.2f, grid.CellSize * .6f);
            stones.Add(cell, root);
            root.AddComponent<DemolitionTarget>().Initialize(() =>
            { environment.RemoveGravestone(cell); stones.Remove(cell); });
            return true;
        }

        public void CreateVisual(Transform parent)
        {
            float width = Mathf.Min(1f, grid.CellSize * .65f);
            Part(parent, "Stone base", PrimitiveType.Cube, new Vector3(0f,.12f,0f), new Vector3(width * 1.2f,.24f,.55f));
            Part(parent, "Headstone", PrimitiveType.Cube, new Vector3(0f,.62f,0f), new Vector3(width,.75f,.22f));
            Part(parent, "Rounded crown", PrimitiveType.Sphere, new Vector3(0f,.97f,0f), new Vector3(width,.4f,.22f));
            Part(parent, "Cross upright", PrimitiveType.Cube, new Vector3(0f,.68f,-.13f), new Vector3(.065f,.32f,.04f));
            Part(parent, "Cross arm", PrimitiveType.Cube, new Vector3(0f,.74f,-.13f), new Vector3(.23f,.065f,.04f));
        }

        private void Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(primitive); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var entry in stones)
            { environment.RemoveGravestone(entry.Key); if (entry.Value != null) UnityEngine.Object.Destroy(entry.Value); }
            stones.Clear(); UnityEngine.Object.Destroy(stoneMaterial);
        }
    }
}

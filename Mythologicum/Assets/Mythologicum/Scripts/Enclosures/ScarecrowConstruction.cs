using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Walkable, reusable fear-feeding enrichment with shared demolition and terrain support.</summary>
    public sealed class ScarecrowConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly Transform parent;
        private readonly ScarecrowDefinition settings;
        private readonly ParkBuildingNetwork buildings;
        private readonly Material wood, clothing, straw;
        private readonly Dictionary<Vector2Int, GameObject> models = new Dictionary<Vector2Int, GameObject>();
        private bool disposed;

        public ScarecrowConstruction(SquareBuildGrid grid, EnvironmentGrid environment, Transform parent, Material material,
            ScarecrowDefinition settings = null, ParkBuildingNetwork buildings = null)
        {
            this.grid = grid; this.environment = environment; this.parent = parent;
            this.buildings = buildings;
            this.settings = settings != null ? settings : ScarecrowDefinition.Current;
            wood = new Material(material) { name = "Scarecrow wood", color = this.settings.woodColor };
            clothing = new Material(material) { name = "Scarecrow clothing", color = this.settings.clothingColor };
            straw = new Material(material) { name = "Scarecrow straw", color = this.settings.strawColor };
        }

        public float Cost => Mathf.Max(0f, settings.constructionCost);
        public bool Occupies(Vector2Int cell) => models.ContainsKey(cell);
        internal bool TryRemove(Vector2Int cell) => models.TryGetValue(cell, out var model) && model.GetComponent<DemolitionTarget>().Demolish();
        public void RefreshTerrain() { foreach (var pair in models) pair.Value.transform.position = grid.CellCenter(pair.Key); }
        public bool CanPlace(Vector2Int cell)
        {
            if (disposed || !grid.IsAvailable(cell) || environment.ScarecrowsAt(cell) > 0
                || environment.TreesAt(cell) > 0 || environment.GravestonesAt(cell) > 0) return false;
            if (buildings != null)
                foreach (var building in buildings.Records) if (building.Footprint.AccessCell == cell) return false;
            float half = Mathf.Max(.01f, grid.CellSize * .5f - .08f);
            return !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * .65f,
                new Vector3(half, .64f, half), Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (!CanPlace(cell) || !environment.AddScarecrow(cell)) return false;
            var model = new GameObject($"Scarecrow [{cell.x}, {cell.y}]");
            model.transform.SetParent(parent, false); model.transform.position = grid.CellCenter(cell);
            CreateVisual(model.transform);
            var collider = model.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up * settings.height * .5f;
            collider.size = new Vector3(grid.CellSize * .7f, settings.height, grid.CellSize * .45f);
            models.Add(cell, model);
            model.AddComponent<DemolitionTarget>().Initialize(() =>
            { environment.RemoveScarecrow(cell); models.Remove(cell); });
            return true;
        }

        public void CreateVisual(Transform parent)
        {
            float h = settings.height, width = Mathf.Min(grid.CellSize * .75f, h * .8f);
            Part(parent, "Stake", PrimitiveType.Cylinder, new Vector3(0, h * .38f, 0), new Vector3(h * .065f, h * .38f, h * .065f), wood);
            Part(parent, "Crossbar", PrimitiveType.Cube, new Vector3(0, h * .64f, 0), new Vector3(width, h * .055f, h * .06f), wood);
            Part(parent, "Coat", PrimitiveType.Cube, new Vector3(0, h * .55f, 0), new Vector3(h * .28f, h * .39f, h * .14f), clothing);
            Part(parent, "Left sleeve", PrimitiveType.Cube, new Vector3(-width * .29f, h * .64f, 0), new Vector3(width * .38f, h * .13f, h * .14f), clothing);
            Part(parent, "Right sleeve", PrimitiveType.Cube, new Vector3(width * .29f, h * .64f, 0), new Vector3(width * .38f, h * .13f, h * .14f), clothing);
            Part(parent, "Straw head", PrimitiveType.Sphere, new Vector3(0, h * .83f, 0), new Vector3(h * .23f, h * .25f, h * .22f), straw);
            Part(parent, "Hat brim", PrimitiveType.Cylinder, new Vector3(0, h * .95f, 0), new Vector3(h * .34f, h * .018f, h * .34f), wood);
            Part(parent, "Hat crown", PrimitiveType.Cylinder, new Vector3(0, h * 1.01f, 0), new Vector3(h * .19f, h * .06f, h * .19f), wood);
        }

        private static void Part(Transform parent, string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Collider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var pair in models)
            { environment.RemoveScarecrow(pair.Key); if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value); }
            models.Clear();
            UnityEngine.Object.Destroy(wood); UnityEngine.Object.Destroy(clothing); UnityEngine.Object.Destroy(straw);
        }
    }
}

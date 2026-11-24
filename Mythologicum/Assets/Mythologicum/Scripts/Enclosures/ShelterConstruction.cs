using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Walkable cave mouths and persistent burrow mounds, separate from path/building occupancy.</summary>
    public sealed class ShelterConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly Transform parent;
        private readonly ShelterDefinition settings;
        private readonly Material rock, opening, dirt;
        private readonly Dictionary<Vector2Int, GameObject> views = new Dictionary<Vector2Int, GameObject>();
        private readonly Predicate<Vector2Int> foreignOccupancy;
        private bool disposed;
        public CreatureShelterRegistry Registry { get; }
        public float CaveCost => settings.caveConstructionCost;
        public IEnumerable<Vector2Int> Cells => Registry.Cells;
        public ShelterConstruction(SquareBuildGrid grid, EnvironmentGrid environment, Transform parent, Material material,
            ShelterDefinition settings = null, Predicate<Vector2Int> foreignOccupancy = null)
        {
            this.grid = grid; this.environment = environment; this.parent = parent;
            this.settings = settings != null ? settings : ShelterDefinition.Current; this.foreignOccupancy = foreignOccupancy;
            Registry = new CreatureShelterRegistry(grid, this.settings.caveCapacity, this.settings.burrowCapacity);
            rock = new Material(material) { name = "Shelter rock", color = this.settings.rockColor };
            opening = new Material(material) { name = "Shelter entrance", color = this.settings.entranceColor };
            dirt = new Material(material) { name = "Burrow soil", color = this.settings.dirtColor };
        }
        public bool Occupies(Vector2Int cell) => Registry.Occupies(cell);
        public bool CanPlaceCave(Vector2Int cell) => CanPlace(cell);
        private bool CanPlace(Vector2Int cell)
        {
            if (disposed || !grid.IsAvailable(cell) || !grid.GroundWalkable(cell) || Occupies(cell) || foreignOccupancy?.Invoke(cell) == true
                || environment.TreesAt(cell) > 0 || environment.GravestonesAt(cell) > 0 || environment.ScarecrowsAt(cell) > 0) return false;
            float half = Mathf.Max(.01f, grid.CellSize / 2 - .08f);
            return !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * .65f, new Vector3(half, .64f, half),
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        public bool TryPlaceCave(Vector2Int cell) => TryPlace(cell, ConstructedShelterKind.Cave);
        public bool TryEnsureBurrow(Vector2Int cell) => Registry.Contains(cell, ConstructedShelterKind.Burrow)
            || CreatureBurrowing.CanDigGround(grid, cell) && TryPlace(cell, ConstructedShelterKind.Burrow);
        private bool TryPlace(Vector2Int cell, ConstructedShelterKind kind)
        {
            Physics.SyncTransforms();
            if (!CanPlace(cell) || !Registry.TryAdd(cell, kind)) return false;
            var root = new GameObject(kind == ConstructedShelterKind.Cave ? "Cave shelter" : "Burrow mound");
            root.transform.SetParent(parent, false); root.transform.position = grid.CellCenter(cell);
            if (kind == ConstructedShelterKind.Cave) CreateCaveVisual(root.transform); else CreateBurrowVisual(root.transform);
            var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true;
            float height = kind == ConstructedShelterKind.Cave ? settings.caveHeight : settings.burrowHeight;
            collider.center = Vector3.up * height * .5f; collider.size = new Vector3(grid.CellSize * .88f, height, grid.CellSize * .88f);
            views.Add(cell, root);
            root.AddComponent<DemolitionTarget>().Initialize(() => { Registry.TryRemove(cell); views.Remove(cell); });
            return true;
        }
        public bool TryRemove(Vector2Int cell) => views.TryGetValue(cell, out var root) && root != null && root.GetComponent<DemolitionTarget>().Demolish();
        public List<SavedShelter> Capture() => Registry.Capture();
        public bool Restore(SavedShelter saved) => saved != null && Enum.IsDefined(typeof(ConstructedShelterKind), saved.kind) && TryPlace(saved.cell, saved.kind);
        public void RefreshTerrain() { foreach (var pair in views) if (pair.Value != null) pair.Value.transform.position = grid.CellCenter(pair.Key); }
        public void CreateCaveVisual(Transform owner)
        {
            float width = grid.CellSize * .96f, height = settings.caveHeight;
            Part(owner, "Dark cave mouth", PrimitiveType.Cube, new Vector3(0, height * .36f, -.15f), new Vector3(width * .64f, height * .72f, .2f), opening);
            for (int side = -1; side <= 1; side += 2)
                Part(owner, "Cave rock wall", PrimitiveType.Sphere, new Vector3(side * width * .43f, height * .45f, .12f),
                    new Vector3(width * .42f, height, grid.CellSize * .9f), rock);
            Part(owner, "Cave roof", PrimitiveType.Sphere, new Vector3(0, height * .82f, .2f), new Vector3(width * 1.2f, height * .45f, grid.CellSize), rock);
            Part(owner, "Cave entrance ground", PrimitiveType.Cube, new Vector3(0, .04f, -.05f), new Vector3(width * .82f, .08f, grid.CellSize * .84f), dirt);
        }
        public void CreateBurrowVisual(Transform owner)
        {
            Part(owner, "Persistent dirt mound", PrimitiveType.Sphere, new Vector3(0, settings.burrowHeight * .35f, 0),
                new Vector3(grid.CellSize * .65f, settings.burrowHeight * 1.5f, grid.CellSize * .57f), dirt);
            Part(owner, "Burrow entrance", PrimitiveType.Sphere, new Vector3(0, settings.burrowHeight * .28f, -grid.CellSize * .2f),
                new Vector3(grid.CellSize * .23f, settings.burrowHeight * .8f, .08f), opening);
        }
        private static void Part(Transform owner, string name, PrimitiveType kind, Vector3 point, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(kind); part.name = name; part.transform.SetParent(owner, false);
            part.transform.localPosition = point; part.transform.localScale = size; part.GetComponent<Collider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var pair in views) { Registry.TryRemove(pair.Key); if (pair.Value != null) UnityEngine.Object.Destroy(pair.Value); }
            views.Clear(); UnityEngine.Object.Destroy(rock); UnityEngine.Object.Destroy(opening); UnityEngine.Object.Destroy(dirt);
        }
    }
}

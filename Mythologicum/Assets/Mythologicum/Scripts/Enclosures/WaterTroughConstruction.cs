using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    [Serializable]
    public sealed class WaterTroughSave
    {
        public Vector2Int cell;
        public float stock, expenses;
    }

    public sealed class WaterTroughRecord
    {
        public Vector2Int Cell { get; }
        public WaterTroughDefinition Definition { get; }
        public float Stock { get; private set; }
        public float Capacity => Mathf.Max(.1f, Definition.capacity);
        public float Expenses { get; private set; }
        public WaterTroughRecord(Vector2Int cell, WaterTroughDefinition definition)
        { Cell = cell; Definition = definition; Stock = Capacity; }
        private static float Amount(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
        public float Consume(float units)
        {
            float consumed = Mathf.Min(Stock, Amount(units)); Stock -= consumed; return consumed;
        }
        public void Refill() => Stock = Capacity;
        public void RecordExpense(float amount) => Expenses = (float)Math.Min(float.MaxValue, Expenses + (double)Amount(amount));
        public void Restore(float stock, float expenses) { Stock = Mathf.Min(Capacity, Amount(stock)); Expenses = Amount(expenses); }
    }

    /// <summary>A solid, refillable enclosure feature. It does not require public path access.</summary>
    public sealed class WaterTroughConstruction : IDisposable
    {
        private sealed class View { public GameObject Root; public Transform Water; public float LastStock = -1; }
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly Transform parent;
        private readonly WaterTroughDefinition settings;
        private readonly ParkBuildingNetwork buildings;
        private readonly Predicate<Vector2Int> blockedCell;
        private readonly Material frameMaterial, waterMaterial;
        private readonly Dictionary<Vector2Int, WaterTroughRecord> records = new Dictionary<Vector2Int, WaterTroughRecord>();
        private readonly Dictionary<Vector2Int, View> views = new Dictionary<Vector2Int, View>();
        private bool disposed;
        public IEnumerable<WaterTroughRecord> Records => records.Values;
        public WaterTroughDefinition Definition => settings;
        public float Cost => Mathf.Max(0, settings.constructionCost);
        public bool Occupies(Vector2Int cell) => records.ContainsKey(cell);
        public WaterTroughRecord RecordAt(Vector2Int cell) => records.TryGetValue(cell, out var record) ? record : null;
        public bool Contains(WaterTroughRecord record) => record != null && ReferenceEquals(RecordAt(record.Cell), record);

        public WaterTroughConstruction(SquareBuildGrid grid, EnvironmentGrid environment, Transform parent, Material material,
            WaterTroughDefinition settings = null, ParkBuildingNetwork buildings = null, Predicate<Vector2Int> blockedCell = null)
        {
            this.grid = grid; this.environment = environment; this.parent = parent; this.buildings = buildings; this.blockedCell = blockedCell;
            this.settings = settings != null ? settings : WaterTroughDefinition.Current;
            frameMaterial = new Material(material) { name = "Water trough frame", color = this.settings.frameColor };
            waterMaterial = new Material(material) { name = "Water trough water", color = this.settings.waterColor };
        }
        public bool CanPlace(Vector2Int cell)
        {
            if (disposed || !grid.IsAvailable(cell) || !grid.GroundWalkable(cell) || Occupies(cell) || blockedCell?.Invoke(cell) == true
                || environment.TreesAt(cell) > 0 || environment.GravestonesAt(cell) > 0 || environment.ScarecrowsAt(cell) > 0) return false;
            if (buildings != null) foreach (var building in buildings.Records)
                if (building.Footprint.AccessCell == cell || building.Footprint.IsFenceMounted && building.Footprint.RearAccessCell == cell) return false;
            return !Physics.CheckBox(grid.CellCenter(cell) + Vector3.up * settings.height * .5f,
                new Vector3(grid.CellSize * .45f, settings.height * .49f, grid.CellSize * .4f), Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        public bool TryPlace(Vector2Int cell)
        {
            Physics.SyncTransforms();
            if (!CanPlace(cell)) return false;
            var record = new WaterTroughRecord(cell, settings);
            var root = new GameObject($"Water trough [{cell.x}, {cell.y}]"); root.transform.SetParent(parent, false); root.transform.position = grid.CellCenter(cell);
            Transform water = CreateVisual(root.transform);
            var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * settings.height * .5f;
            collider.size = new Vector3(grid.CellSize * .9f, settings.height, grid.CellSize * .8f);
            root.AddComponent<WaterTroughSelection>().Initialize(record);
            records.Add(cell, record); views.Add(cell, new View { Root = root, Water = water });
            root.AddComponent<DemolitionTarget>().Initialize(() => { records.Remove(cell); views.Remove(cell); grid.NotifyPhysicalChange(); });
            grid.NotifyPhysicalChange(); Refresh(); return true;
        }
        public bool TryRemove(Vector2Int cell) => views.TryGetValue(cell, out var view) && view.Root != null && view.Root.GetComponent<DemolitionTarget>().Demolish();
        public bool Restore(WaterTroughSave saved)
        {
            if (saved == null || !TryPlace(saved.cell)) return false;
            RecordAt(saved.cell).Restore(saved.stock, saved.expenses); Refresh(); return true;
        }
        public List<WaterTroughSave> Capture()
        {
            var result = new List<WaterTroughSave>();
            foreach (var record in records.Values) result.Add(new WaterTroughSave { cell = record.Cell, stock = record.Stock, expenses = record.Expenses });
            result.Sort((a, b) => a.cell.y != b.cell.y ? a.cell.y.CompareTo(b.cell.y) : a.cell.x.CompareTo(b.cell.x)); return result;
        }
        public Transform CreateVisual(Transform owner)
        {
            float w = grid.CellSize * .9f, d = grid.CellSize * .8f, h = settings.height, rim = Mathf.Min(.12f, h * .2f);
            Part(owner, "Trough bottom", new Vector3(0, rim * .5f, 0), new Vector3(w, rim, d), frameMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(owner, "Trough side", new Vector3(side * (w - rim) * .5f, h * .5f, 0), new Vector3(rim, h, d), frameMaterial);
                Part(owner, "Trough end", new Vector3(0, h * .5f, side * (d - rim) * .5f), new Vector3(w, h, rim), frameMaterial);
            }
            return Part(owner, "Water level", new Vector3(0, h * .8f, 0), new Vector3(w - rim * 2, .04f, d - rim * 2), waterMaterial);
        }
        private static Transform Part(Transform owner, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name; part.transform.SetParent(owner, false);
            part.transform.localPosition = position; part.transform.localScale = size; part.GetComponent<Collider>().enabled = false;
            part.GetComponent<MeshRenderer>().sharedMaterial = material; return part.transform;
        }
        public void Refresh()
        {
            foreach (var record in records.Values)
            {
                var view = views[record.Cell]; if (view.LastStock == record.Stock) continue;
                view.LastStock = record.Stock; view.Water.gameObject.SetActive(record.Stock > 0);
                view.Water.localPosition = Vector3.up * Mathf.Lerp(settings.height * .15f, settings.height * .8f, record.Stock / record.Capacity);
            }
        }
        public void RefreshTerrain() { foreach (var pair in views) pair.Value.Root.transform.position = grid.CellCenter(pair.Key); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var view in views.Values) if (view.Root != null) UnityEngine.Object.Destroy(view.Root);
            views.Clear(); records.Clear(); grid.NotifyPhysicalChange();
            UnityEngine.Object.Destroy(frameMaterial); UnityEngine.Object.Destroy(waterMaterial);
        }
    }
}

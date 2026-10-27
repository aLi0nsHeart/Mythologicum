using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class FenceConstruction
    {
        public const float Height = 1.6f;
        public const float SpikedHeight = 2.15f;
        public const float Thickness = 0.14f;
        public FenceLayout Layout { get; }
        private readonly SquareBuildGrid grid;
        private readonly Transform parent;
        private readonly Material material;
        private readonly Material gateMaterial;
        private readonly FenceVisualCatalog visuals;
        private readonly Dictionary<FenceEdge, GameObject> objects = new Dictionary<FenceEdge, GameObject>();
        private static Mesh spikeMesh;

        public FenceConstruction(SquareBuildGrid grid, Transform parent, Material material, Material gateMaterial = null, FenceVisualCatalog visuals = null)
        {
            this.grid = grid;
            this.parent = parent;
            this.material = material;
            this.visuals = visuals;
            this.gateMaterial = gateMaterial != null ? gateMaterial : material;
            Layout = new FenceLayout(grid);
        }

        public bool CanPlace(FenceEdge edge, FenceKind kind = FenceKind.Wooden)
        {
            if (!Layout.Contains(edge)) return false;
            if (Layout.HasFence(edge)) return !Layout.IsGate(edge);
            if (Layout.WouldSplitPath(edge)) return false;
            float height = kind == FenceKind.Spiked ? SpikedHeight : Height;
            Vector3 half = edge.AlongX ? new Vector3(grid.CellSize / 2f - 0.01f, height / 2f - 0.01f, Thickness / 2f)
                : new Vector3(Thickness / 2f, height / 2f - 0.01f, grid.CellSize / 2f - 0.01f);
            return !Physics.CheckBox(Layout.Center(edge) + Vector3.up * height / 2f, half,
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(FenceEdge edge, FenceKind kind = FenceKind.Wooden)
        {
            if (!CanPlace(edge, kind)) return false;
            if (Layout.HasFence(edge))
            {
                if (Layout.KindAt(edge) == kind) return true;
                if (!Layout.TrySetKind(edge, kind)) return false;
                RemoveObject(edge);
            }
            else if (!Layout.TryAdd(edge, kind)) return false;
            CreateObject(edge, false);
            return true;
        }

        public bool CanPlaceGate(FenceEdge edge) => Layout.HasFence(edge) && !Layout.IsGate(edge)
            && Layout.KindAt(edge) == FenceKind.Wooden;

        public bool TryPlaceGate(FenceEdge edge)
        {
            if (!CanPlaceGate(edge) || !Layout.TrySetGate(edge)) return false;
            RemoveObject(edge);
            CreateObject(edge, true);
            return true;
        }

        // Replacing the visual must not invoke demolition's layout-removal callback.
        private void RemoveObject(FenceEdge edge)
        {
            if (!objects.TryGetValue(edge, out GameObject old)) return;
            foreach (Collider collider in old.GetComponentsInChildren<Collider>()) collider.enabled = false;
            old.SetActive(false);
            Object.Destroy(old);
            objects.Remove(edge);
        }

        private void CreateObject(FenceEdge edge, bool gate)
        {
            bool spiked = !gate && Layout.KindAt(edge) == FenceKind.Spiked;
            float height = spiked ? SpikedHeight : Height;
            var root = new GameObject($"{(gate ? "Gate" : spiked ? "Spiked fence" : "Wooden fence")} [{edge.Start.x}, {edge.Start.y}] {(edge.AlongX ? "X" : "Z")}");
            root.transform.SetParent(parent);
            root.transform.position = Layout.Center(edge);
            root.transform.rotation = Quaternion.Euler(0f, edge.AlongX ? 0f : 90f, 0f);
            GameObject prefab = visuals == null ? null : gate ? visuals.gate : spiked ? visuals.spiked : visuals.normal;
            if (prefab != null)
            {
                float yaw = gate ? visuals.gateYaw : spiked ? visuals.spikedYaw : visuals.normalYaw;
                CreateModel(root.transform, prefab, yaw, height);
            }
            else
            {
                AddPart(root.transform, new Vector3(-grid.CellSize / 2f + Thickness / 2f, Height / 2f, 0f),
                    new Vector3(Thickness, Height, Thickness));
                AddPart(root.transform, new Vector3(grid.CellSize / 2f - Thickness / 2f, Height / 2f, 0f),
                    new Vector3(Thickness, Height, Thickness));
                AddPart(root.transform, new Vector3(0f, Height * 0.35f, 0f), new Vector3(grid.CellSize, 0.12f, 0.09f));
                AddPart(root.transform, new Vector3(0f, Height * 0.8f, 0f), new Vector3(grid.CellSize, 0.12f, 0.09f));
                if (spiked)
                {
                    int count = Mathf.Max(3, Mathf.CeilToInt(grid.CellSize / 0.3f));
                    for (int i = 0; i <= count; i++)
                    {
                        float x = Mathf.Lerp(-grid.CellSize / 2f + Thickness / 2f,
                            grid.CellSize / 2f - Thickness / 2f, (float)i / count);
                        AddPart(root.transform, new Vector3(x, Height / 2f, 0f), new Vector3(0.08f, Height, 0.08f));
                        AddSpike(root.transform, new Vector3(x, Height, 0f));
                    }
                }
                if (gate)
                {
                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaf.name = "Staff gate";
                    leaf.GetComponent<Collider>().enabled = false;
                    leaf.transform.SetParent(root.transform, false);
                    leaf.transform.localPosition = Vector3.up * Height / 2f;
                    leaf.transform.localScale = new Vector3(grid.CellSize - Thickness * 2f, Height * 0.65f, 0.1f);
                    leaf.GetComponent<MeshRenderer>().sharedMaterial = gateMaterial;
                }
            }
            var collider = root.AddComponent<BoxCollider>();
            collider.center = Vector3.up * height / 2f;
            collider.size = new Vector3(grid.CellSize, height, Thickness);
            collider.isTrigger = true;
            // Separate solid barrier: ignored by construction raycasts, available to future physical agents.
            var barrier = new GameObject("Closed barrier") { layer = 2 };
            barrier.transform.SetParent(root.transform, false);
            var solid = barrier.AddComponent<BoxCollider>();
            solid.center = collider.center;
            solid.size = collider.size;
            objects[edge] = root;
            root.AddComponent<FenceSegment>().Initialize(edge);
            root.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                Layout.TryRemove(edge);
                objects.Remove(edge);
            });
        }

        private void CreateModel(Transform parent, GameObject prefab, float yaw, float height)
        {
            var wrapper = new GameObject("Fence model");
            wrapper.transform.SetParent(parent, false);
            var model = Object.Instantiate(prefab, wrapper.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = LocalBounds(wrapper.transform, renderers);
            wrapper.transform.localScale = new Vector3(grid.CellSize / Mathf.Max(0.001f, bounds.size.x),
                height / Mathf.Max(0.001f, bounds.size.y), Thickness / Mathf.Max(0.001f, bounds.size.z));
            // Account for source pivots after fitting to the cell edge and gameplay barrier height.
            wrapper.transform.localPosition = -Vector3.Scale(bounds.center, wrapper.transform.localScale) + Vector3.up * height / 2f;
        }

        private static Bounds LocalBounds(Transform parent, Renderer[] renderers)
        {
            Bounds bounds = new Bounds();
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                Bounds local = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                            Vector3 point = parent.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                            if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                            else bounds.Encapsulate(point);
                        }
            }
            return bounds;
        }

        private void AddSpike(Transform parent, Vector3 position)
        {
            if (spikeMesh == null)
            {
                const int sides = 6;
                var vertices = new Vector3[sides * 3];
                var triangles = new int[sides * 3];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides, b = (i + 1) * Mathf.PI * 2f / sides;
                    vertices[i * 3] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    vertices[i * 3 + 1] = Vector3.up;
                    vertices[i * 3 + 2] = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
                    triangles[i * 3] = i * 3;
                    triangles[i * 3 + 1] = i * 3 + 1;
                    triangles[i * 3 + 2] = i * 3 + 2;
                }
                spikeMesh = new Mesh { name = "Fence spike", vertices = vertices, triangles = triangles };
                spikeMesh.RecalculateNormals();
                spikeMesh.RecalculateBounds();
            }
            var spike = new GameObject("Climb deterrent spike");
            spike.transform.SetParent(parent, false);
            spike.transform.localPosition = position;
            spike.transform.localScale = new Vector3(0.1f, SpikedHeight - Height, 0.1f);
            spike.AddComponent<MeshFilter>().sharedMesh = spikeMesh;
            spike.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void AddPart(Transform parent, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = "Fence Part";
            part.GetComponent<Collider>().enabled = false;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}

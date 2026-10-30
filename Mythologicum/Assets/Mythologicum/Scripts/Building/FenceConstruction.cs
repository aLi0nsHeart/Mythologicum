using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class FenceConstruction
    {
        public const float Height = 1.6f;
        public const float SpikedHeight = 2.15f;
        public const float HeavyHeight = 2.7f;
        public const float Thickness = 0.14f;
        public FenceLayout Layout { get; }
        private readonly SquareBuildGrid grid;
        private readonly Transform parent;
        private readonly Material material;
        private readonly Material gateMaterial;
        private readonly FenceVisualCatalog visuals;
        private readonly Dictionary<FenceEdge, GameObject> objects = new Dictionary<FenceEdge, GameObject>();
        private readonly Dictionary<FenceEdge, TextMesh> healthLabels = new Dictionary<FenceEdge, TextMesh>();
        private static Mesh spikeMesh;
        private readonly Dictionary<FenceKind,Material> terrainMaterials=new Dictionary<FenceKind,Material>();
        public static float HeightFor(FenceKind kind) => kind == FenceKind.Heavy ? HeavyHeight
            : kind == FenceKind.Spiked || kind == FenceKind.Sanctified ? SpikedHeight : Height;

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
            float height = HeightFor(kind);
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

        public bool CanPlaceGate(FenceEdge edge) => Layout.HasFence(edge) && !Layout.IsGate(edge);

        internal bool RestoreSegment(FenceEdge edge, FenceKind kind, bool gate, float health)
        {
            if (!Layout.TryAdd(edge, kind)) return false;
            if (gate && !Layout.TrySetGate(edge)) { Layout.TryRemove(edge); return false; }
            Layout.TryDamage(edge, Mathf.Max(0f, Layout.MaximumHealthAt(edge) - health));
            CreateObject(edge, gate); return true;
        }

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
            healthLabels.Remove(edge);
        }

        /// <summary>Synchronizes simulation damage with visuals; partial damage does not rebuild imported models.</summary>
        public void Refresh(Camera camera = null)
        {
            var removed = new List<FenceEdge>();
            foreach (var pair in objects)
            {
                FenceEdge edge = pair.Key;
                if (!Layout.HasFence(edge)) { removed.Add(edge); continue; }
                float current = Layout.HealthAt(edge), maximum = Layout.MaximumHealthAt(edge);
                if (current >= maximum) continue;
                if (!healthLabels.TryGetValue(edge, out TextMesh label))
                {
                    var go = new GameObject("Fence health", typeof(TextMesh));
                    go.transform.SetParent(pair.Value.transform, false);
                    label = go.GetComponent<TextMesh>();
                    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
                    label.fontSize = 48; label.characterSize = .08f;
                    label.anchor = TextAnchor.LowerCenter; label.alignment = TextAlignment.Center;
                    label.color = new Color(1f, .55f, .2f);
                    healthLabels.Add(edge, label);
                }
                label.text = $"Fence {current:0}/{maximum:0}";
                label.transform.position = Layout.Center(edge) + Vector3.up * (HeightFor(Layout.KindAt(edge)) + .25f);
                if (camera != null) label.transform.rotation = camera.transform.rotation;
            }
            foreach (FenceEdge edge in removed) RemoveObject(edge);
        }

        private void CreateObject(FenceEdge edge, bool gate)
        {
            FenceKind kind = Layout.KindAt(edge);
            float height = HeightFor(kind);
            var root = new GameObject($"{kind} {(gate ? "gate" : "fence")} [{edge.Start.x}, {edge.Start.y}] {(edge.AlongX ? "X" : "Z")}");
            root.transform.SetParent(parent);
            root.transform.position = Layout.Center(edge);
            root.transform.rotation = Quaternion.Euler(0f, edge.AlongX ? 0f : 90f, 0f);
            CreateVisual(root.transform, kind, gate);
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
            if(grid.Terrain!=null)
            {
                solid.enabled=false;barrier.AddComponent<MeshFilter>();barrier.AddComponent<MeshCollider>();barrier.AddComponent<RuntimeGroundMesh>();
                FitTerrain(edge,root);
            }
            objects[edge] = root;
            root.AddComponent<FenceSegment>().Initialize(edge);
            root.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                Layout.TryRemove(edge);
                objects.Remove(edge);
                healthLabels.Remove(edge);
            });
        }

        public void CreateVisual(Transform parent, FenceKind kind, bool gate = false)
        {
            if(grid.Terrain!=null)
            {
                if(!terrainMaterials.TryGetValue(kind,out var shared))
                {
                    shared=new Material(material){color=kind==FenceKind.Heavy?new Color(.28f,.32f,.36f):kind==FenceKind.Sanctified
                        ?new Color(.78f,.78f,.86f):new Color(.38f,.25f,.14f)};terrainMaterials[kind]=shared;
                }
                var visual=new GameObject(gate?"Conforming gate":"Conforming fence");visual.transform.SetParent(parent,false);
                visual.AddComponent<GroundConformingVisual>().Initialize(grid,shared,true,kind,gate);return;
            }
            bool spiked = !gate && kind == FenceKind.Spiked;
            float height = HeightFor(kind);
            // Appearance is a gate; protection and health remain those of its underlying fence kind.
            GameObject prefab = visuals == null ? null : gate ? visuals.gate : kind == FenceKind.Heavy ? visuals.heavy
                : kind == FenceKind.Sanctified ? visuals.sanctified
                : spiked ? visuals.spiked : visuals.normal;
            if (prefab != null)
            {
                float yaw = gate ? visuals.gateYaw : kind == FenceKind.Heavy ? visuals.heavyYaw : kind == FenceKind.Sanctified ? visuals.sanctifiedYaw
                    : spiked ? visuals.spikedYaw : visuals.normalYaw;
                CreateModel(parent, prefab, yaw, height);
            }
            else
            {
                AddPart(parent, new Vector3(-grid.CellSize / 2f + Thickness / 2f, height / 2f, 0f),
                    new Vector3(Thickness, height, Thickness));
                AddPart(parent, new Vector3(grid.CellSize / 2f - Thickness / 2f, height / 2f, 0f),
                    new Vector3(Thickness, height, Thickness));
                AddPart(parent, new Vector3(0f, height * 0.35f, 0f), new Vector3(grid.CellSize, 0.12f, 0.09f));
                AddPart(parent, new Vector3(0f, height * 0.8f, 0f), new Vector3(grid.CellSize, 0.12f, 0.09f));
                if (spiked)
                {
                    int count = Mathf.Max(3, Mathf.CeilToInt(grid.CellSize / 0.3f));
                    for (int i = 0; i <= count; i++)
                    {
                        float x = Mathf.Lerp(-grid.CellSize / 2f + Thickness / 2f,
                            grid.CellSize / 2f - Thickness / 2f, (float)i / count);
                        AddPart(parent, new Vector3(x, Height / 2f, 0f), new Vector3(0.08f, Height, 0.08f));
                        AddSpike(parent, new Vector3(x, Height, 0f));
                    }
                }
                if (gate)
                {
                    var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaf.name = "Staff gate";
                    leaf.GetComponent<Collider>().enabled = false;
                    leaf.transform.SetParent(parent, false);
                    leaf.transform.localPosition = Vector3.up * height / 2f;
                    leaf.transform.localScale = new Vector3(grid.CellSize - Thickness * 2f, height * 0.65f, 0.1f);
                    leaf.GetComponent<MeshRenderer>().sharedMaterial = gateMaterial;
                }
            }
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
        private void FitTerrain(FenceEdge edge,GameObject root)
        {
            root.transform.position=Layout.Center(edge);
            root.GetComponentInChildren<GroundConformingVisual>().Refresh();
            float h=HeightFor(Layout.KindAt(edge)),half=grid.CellSize/2;
            Vector3 Point(float x,float z,float height)
            {var p=root.transform.TransformPoint(new Vector3(x,0,z));p.y=grid.GroundHeight(p)+height;return root.transform.InverseTransformPoint(p);}
            var v=new List<Vector3>();var t=new List<int>();float d=Thickness/2;
            var a=Point(-half,-d,0);var b=Point(-half,d,0);var c=Point(half,d,0);var e=Point(half,-d,0);
            GroundMeshBuilder.AddQuad(v,t,e,c,b,a);GroundMeshBuilder.AddQuad(v,t,b+Vector3.up*h,c+Vector3.up*h,e+Vector3.up*h,a+Vector3.up*h);
            GroundMeshBuilder.AddQuad(v,t,a+Vector3.up*h,e+Vector3.up*h,e,a);GroundMeshBuilder.AddQuad(v,t,c,c+Vector3.up*h,b+Vector3.up*h,b);
            GroundMeshBuilder.AddQuad(v,t,b,b+Vector3.up*h,a+Vector3.up*h,a);GroundMeshBuilder.AddQuad(v,t,e+Vector3.up*h,c+Vector3.up*h,c,e);
            root.transform.Find("Closed barrier").GetComponent<RuntimeGroundMesh>().Set(GroundMeshBuilder.Finish("Sloping closed barrier",v,t));
            float low=Mathf.Min(a.y,b.y,c.y,e.y),high=Mathf.Max(a.y,b.y,c.y,e.y)+h;
            var collider=root.GetComponent<BoxCollider>();collider.center=Vector3.up*((low+high)/2);collider.size=new Vector3(grid.CellSize,high-low,Thickness);
        }
        public void RefreshTerrain()
        {if(grid.Terrain!=null)foreach(var pair in objects)if(Layout.HasFence(pair.Key))FitTerrain(pair.Key,pair.Value);}
        public void Dispose(){foreach(var m in terrainMaterials.Values)Object.Destroy(m);terrainMaterials.Clear();}

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

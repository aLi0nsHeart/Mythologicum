using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed class BuildingConstruction
    {
        private sealed class View
        {
            public MeshRenderer Body;
            public MeshRenderer Marker;
            public TextMesh Label;
            public bool? LastOpen;
        }
        public ParkBuildingNetwork Network { get; }
        private readonly SquareBuildGrid grid;
        private readonly Transform parent;
        private readonly Material material, entranceMaterial;
        private readonly Dictionary<ParkBuildingRecord, View> views = new Dictionary<ParkBuildingRecord, View>();

        public BuildingConstruction(SquareBuildGrid grid, FenceLayout fences, Transform parent,
            Material material, Material entranceMaterial)
        {
            this.grid = grid;
            this.parent = parent;
            this.material = material;
            this.entranceMaterial = entranceMaterial;
            Network = new ParkBuildingNetwork(grid, fences);
        }

        public bool CanPlace(BuildingFootprint footprint)
        {
            if (!Network.CanPlace(footprint)) return false;
            if(grid.Terrain!=null&&!Network.RestoringSave&&!grid.Terrain.TryPlanFoundation(footprint,grid,out _))return false;
            Vector3 half = new Vector3(footprint.Width * grid.CellSize / 2f - 0.05f,
                footprint.Definition.height / 2f - 0.01f, footprint.Depth * grid.CellSize / 2f - 0.05f);
            if (Physics.CheckBox(PreviewPosition(footprint) + Vector3.up * footprint.Definition.height / 2f,
                half, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            return !Physics.CheckBox(grid.CellCenter(footprint.AccessCell) + Vector3.up * 0.5f,
                new Vector3(grid.CellSize / 2f - 0.05f, 0.49f, grid.CellSize / 2f - 0.05f),
                Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool TryPlace(BuildingFootprint footprint)
        {
            Physics.SyncTransforms();
            if (!CanPlace(footprint)) return false;
            ParkBuildingRecord record = Network.TryPlace(footprint);
            if (record == null) return false;
            if(grid.Terrain!=null)
            {
                if(!Network.RestoringSave)
                {
                    if(!grid.Terrain.TryPlanFoundation(footprint,grid,out var plan)){Network.Remove(record);return false;}
                    grid.Terrain.Apply(plan);
                }
                if(!grid.Terrain.RegisterFoundation(record,footprint)){Network.Remove(record);return false;}
            }
            BuildingDefinition definition = footprint.Definition;
            var root = new GameObject(definition.displayName);
            root.AddComponent<BuildingSelection>().Initialize(record);
            root.transform.SetParent(parent);
            root.transform.position = footprint.Center(grid);
            root.transform.rotation = Quaternion.Euler(0f, -90f * footprint.Rotation, 0f);
            Vector3 size = new Vector3(definition.width * grid.CellSize - 0.16f,
                definition.height, definition.depth * grid.CellSize - 0.16f);
            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = Vector3.up * definition.height / 2f;
            GameObject body = CreateVisual(root.transform, definition);
            float doorX = (definition.DoorColumn + 0.5f - definition.width / 2f) * grid.CellSize;
            GameObject marker = Part(root.transform, definition.RequiresPathConnection ? "Connect a path here" : "Animal release point",
                new Vector3(doorX, 0.14f, -(definition.depth + 1) * grid.CellSize / 2f),
                new Vector3(grid.CellSize * 0.6f, 0.08f, grid.CellSize * 0.6f), entranceMaterial);
            var labelObject = new GameObject("Building status", typeof(TextMesh));
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = Vector3.up * (definition.height + 0.6f);
            TextMesh label = labelObject.GetComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.fontSize = 64;
            label.characterSize = 0.12f;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            views.Add(record, new View { Body = definition.visualPrefab == null ? body.GetComponent<MeshRenderer>() : null,
                Marker = marker.GetComponent<MeshRenderer>(), Label = label });
            root.AddComponent<DemolitionTarget>().Initialize(() =>
            {
                Network.Remove(record);
                grid.Terrain?.ReleaseFoundation(record);
                views.Remove(record);
            });
            return true;
        }
        public Vector3 PreviewPosition(BuildingFootprint footprint)
        {
            var position=footprint.Center(grid);
            if(grid.Terrain!=null&&!Network.RestoringSave&&grid.Terrain.TryPlanFoundation(footprint,grid,out var plan))position.y=grid.Origin.y+plan.Level;
            return position;
        }

        // Shared by placement and its ghost; creates no occupancy, labels or gameplay components.
        public GameObject CreateVisual(Transform parent, BuildingDefinition definition)
        {
            Vector3 size = new Vector3(definition.width * grid.CellSize - 0.16f,
                definition.height, definition.depth * grid.CellSize - 0.16f);
            bool resting = definition.kind == BuildingKind.RestArea;
            GameObject body;
            if (definition.visualPrefab != null)
                body = CreateModel(parent, definition, size);
            else if (resting)
            {
                float benchWidth = size.x * 0.8f;
                body = Part(parent, "Bench seat", new Vector3(0f, 0.65f, 0.2f),
                    new Vector3(benchWidth, 0.18f, 0.7f), material);
                Part(parent, "Bench back", new Vector3(0f, 1.03f, 0.51f),
                    new Vector3(benchWidth, 0.5f, 0.15f), material);
                for (int side = -1; side <= 1; side += 2)
                    Part(parent, "Bench support", new Vector3(side * benchWidth * 0.4f, 0.3f, 0.2f),
                        new Vector3(0.18f, 0.6f, 0.6f), material);
            }
            else body = Part(parent, "Building", Vector3.up * definition.height / 2f, size, material);
            float doorX = (definition.DoorColumn + 0.5f - definition.width / 2f) * grid.CellSize;
            if (!resting && definition.visualPrefab == null)
                Part(parent, "Dedicated entrance", new Vector3(doorX, 0.8f, -definition.depth * grid.CellSize / 2f),
                    new Vector3(grid.CellSize * 0.5f, 1.6f, 0.12f), entranceMaterial);
            return body;
        }

        private static GameObject CreateModel(Transform parent, BuildingDefinition definition, Vector3 size)
        {
            GameObject model = Object.Instantiate(definition.visualPrefab, parent, false);
            model.transform.localRotation = Quaternion.Euler(0f, definition.visualYaw, 0f);
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return model;
            Bounds bounds = ModelBounds(parent, renderers);
            float scale = Mathf.Min(size.x / Mathf.Max(0.001f, bounds.size.x),
                size.y / Mathf.Max(0.001f, bounds.size.y), size.z / Mathf.Max(0.001f, bounds.size.z));
            model.transform.localScale *= scale;
            bounds = ModelBounds(parent, renderers);
            model.transform.localPosition += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            return model;
        }

        private static Bounds ModelBounds(Transform parent, Renderer[] renderers)
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

        private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.GetComponent<Collider>().enabled = false;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part;
        }

        public void Refresh(Camera camera)
        {
            Network.Refresh();
            foreach (var pair in views)
            {
                ParkBuildingRecord record = pair.Key;
                View view = pair.Value;
                view.Label.transform.rotation = camera.transform.rotation;
                string state = record.IsOpen ? "Open" : "Closed: no path connection";
                if (record.Footprint.Definition.kind == BuildingKind.GoatDispenser)
                    state += record.FeedStock > 0 ? $" | Stock {record.FeedStock}/{record.Footprint.Definition.feederCapacity}"
                        : " | Empty: waiting for zookeeper";
                if (record.Footprint.Definition.IsEmployeeBuilding) state += $" | Personnel {record.HiredCount}/{ParkBuildingRecord.EmployeeCapacity}";
                view.Label.text = record.Footprint.Definition.displayName + "\n" + state;
                if (view.LastOpen == record.IsOpen) continue;
                view.LastOpen = record.IsOpen;
                Color color = record.IsOpen ? record.Footprint.Definition.openColor : new Color(0.85f, 0.12f, 0.1f);
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                if (view.Body != null) view.Body.SetPropertyBlock(properties);
                // Keep the model's authored palette; mark only entrances still needing a path.
                properties.SetColor("_BaseColor", record.IsOpen ? entranceMaterial.color : new Color(0.85f, 0.12f, 0.1f));
                properties.SetColor("_Color", record.IsOpen ? entranceMaterial.color : new Color(0.85f, 0.12f, 0.1f));
                view.Marker.SetPropertyBlock(properties);
                view.Marker.enabled = record.Footprint.Definition.RequiresPathConnection && !record.IsOpen;
                view.Label.color = record.IsOpen ? Color.white : new Color(1f, 0.45f, 0.35f);
            }
        }
    }
}

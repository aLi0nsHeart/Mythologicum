using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Mythologicum
{
    /// <summary>One shared mesh per completed cage/pool; no collider or per-cell scene objects.</summary>
    internal sealed class SpecialEnclosureVisuals : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly SpecialEnclosureRules rules;
        private readonly GameObject root;
        private readonly Material cageMaterial, glassMaterial;
        private readonly List<GameObject> generated = new List<GameObject>();
        private int revision = -1, style, habitatRevision = -1, waterSignature;

        public SpecialEnclosureVisuals(SquareBuildGrid grid, Transform parent, Material source, SpecialEnclosureRules rules)
        {
            this.grid = grid; this.rules = rules;
            root = new GameObject("Completed aviaries and aquariums");
            if (parent != null) root.transform.SetParent(parent, true);
            Material Make(string name, Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = source != null ? new Material(source) : new Material(shader);
                material.name = name; material.color = color;
                if (color.a < .999f)
                {
                    material.renderQueue = 3000; material.SetOverrideTag("RenderType", "Transparent");
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0);
                    if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHABLEND_ON");
                }
                return material;
            }
            cageMaterial = Make("Aviary cage", rules.cageColor); glassMaterial = Make("Aquarium retaining glass", rules.aquariumWallColor);
        }
        public void Refresh(SpecialEnclosureMap map)
        {
            int currentStyle = rules.cageBarWidth.GetHashCode() ^ rules.cageMeshSpacing.GetHashCode()
                ^ rules.cageColor.GetHashCode() ^ rules.aquariumWallColor.GetHashCode() ^ rules.aquariumWallFreeboard.GetHashCode();
            int signature = waterSignature;
            if (habitatRevision != grid.Habitat.Revision || revision != map.Revision)
            {
                signature = 17;
                unchecked
                {
                    foreach (var record in map.Records) if (record.Kind == FenceKind.Aquarium)
                        foreach (var cell in record.Cells) if (grid.Habitat.HasWaterSurface(cell))
                        { signature = signature * 31 + cell.GetHashCode(); signature = signature * 31 + grid.Habitat.WaterLevel(cell).GetHashCode(); }
                }
                habitatRevision = grid.Habitat.Revision;
            }
            if (revision == map.Revision && style == currentStyle && signature == waterSignature) return;
            waterSignature = signature;
            revision = map.Revision; style = currentStyle;
            cageMaterial.color = rules.cageColor; glassMaterial.color = rules.aquariumWallColor;
            foreach (var item in generated) { item.SetActive(false); Object.Destroy(item); } generated.Clear();
            foreach (var record in map.Records)
                if (record.Kind == FenceKind.Aviary) CreateCage(record); else CreatePoolWalls(record);
        }
        private void Mesh(string name, List<Vector3> vertices, List<int> triangles, Material material)
        {
            if (triangles.Count == 0) return;
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(RuntimeGroundMesh)) { layer = 2 };
            item.transform.SetParent(root.transform, false); generated.Add(item);
            var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            item.GetComponent<RuntimeGroundMesh>().Set(GroundMeshBuilder.Finish(name, vertices, triangles));
        }
        private void CreateCage(SpecialEnclosureRecord record)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var posts = new HashSet<Vector2Int>();
            float width = rules.cageBarWidth, ceiling = record.RoofHeight;
            foreach (var edge in record.Boundary)
            {
                var a = grid.NodePosition(edge.Start); var b = grid.NodePosition(edge.End);
                if (posts.Add(edge.Start)) GroundMeshBuilder.Beam(vertices, triangles, a, new Vector3(a.x, ceiling, a.z), width * 2, width * 2);
                if (posts.Add(edge.End)) GroundMeshBuilder.Beam(vertices, triangles, b, new Vector3(b.x, ceiling, b.z), width * 2, width * 2);
                int wires = Math.Max(1, (int)Math.Ceiling((ceiling - Math.Min(a.y, b.y)) / rules.cageMeshSpacing));
                for (int i = 1; i <= wires; i++)
                {
                    float fraction = (float)i / wires;
                    var first = a; var second = b;
                    first.y = Mathf.Lerp(a.y, ceiling, fraction); second.y = Mathf.Lerp(b.y, ceiling, fraction);
                    GroundMeshBuilder.Beam(vertices, triangles, first, second, width, width);
                }
            }
            int subdivisions = Math.Min(16, Math.Max(1, (int)Math.Ceiling(grid.CellSize / rules.cageMeshSpacing)));
            var rows = new Dictionary<int, List<int>>(); var columns = new Dictionary<int, List<int>>();
            void Add(Dictionary<int, List<int>> lines, int line, int cell)
            { if (!lines.TryGetValue(line, out var spans)) lines.Add(line, spans = new List<int>()); spans.Add(cell); }
            foreach (var cell in record.Cells) for (int i = 0; i <= subdivisions; i++)
            { Add(rows, cell.y * subdivisions + i, cell.x); Add(columns, cell.x * subdivisions + i, cell.y); }
            void Runs(Dictionary<int, List<int>> lines, bool alongX)
            {
                foreach (var line in lines)
                {
                    var spans = line.Value; spans.Sort(); int first = spans[0], end = first + 1;
                    void Emit()
                    {
                        float cross = (float)line.Key / subdivisions * grid.CellSize;
                        Vector3 a = grid.Origin + (alongX ? new Vector3(first * grid.CellSize, 0, cross) : new Vector3(cross, 0, first * grid.CellSize));
                        Vector3 b = grid.Origin + (alongX ? new Vector3(end * grid.CellSize, 0, cross) : new Vector3(cross, 0, end * grid.CellSize));
                        a.y = b.y = ceiling; GroundMeshBuilder.Beam(vertices, triangles, a, b, width, width);
                    }
                    for (int i = 1; i < spans.Count; i++)
                    {
                        if (spans[i] <= end) { end = Math.Max(end, spans[i] + 1); continue; }
                        Emit(); first = spans[i]; end = first + 1;
                    }
                    Emit();
                }
            }
            Runs(rows, true); Runs(columns, false);
            Mesh("Complete aviary roof " + record.Key, vertices, triangles, cageMaterial);
        }
        private void CreatePoolWalls(SpecialEnclosureRecord record)
        {
            float surface = float.NegativeInfinity;
            foreach (var cell in record.Cells)
                if (grid.Habitat.HasWaterSurface(cell)) surface = Math.Max(surface, grid.Habitat.WaterLevel(cell));
            if (float.IsNegativeInfinity(surface)) return;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var edge in record.Boundary)
            {
                var a = grid.NodePosition(edge.Start); var b = grid.NodePosition(edge.End);
                var c = b; var d = a;
                a.y = b.y = Math.Min(Math.Min(a.y, b.y), surface - rules.aquariumDepth);
                c.y = d.y = surface + rules.aquariumWallFreeboard;
                GroundMeshBuilder.AddQuad(vertices, triangles, a, b, c, d);
                GroundMeshBuilder.AddQuad(vertices, triangles, d, c, b, a);
            }
            Mesh("Complete aquarium walls " + record.Key, vertices, triangles, glassMaterial);
        }
        public void Dispose()
        { Object.Destroy(root); Object.Destroy(cageMaterial); Object.Destroy(glassMaterial); }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mythologicum
{
    /// <summary>Chunked, collider-free habitat overlays. Biome painting never rebuilds physical ground meshes.</summary>
    public sealed class HabitatTerrainSurface : IDisposable
    {
        private sealed class Chunk
        {
            public int X, Z, Width, Depth;
            public readonly RuntimeGroundMesh[] Biomes = new RuntimeGroundMesh[5];
            public RuntimeGroundMesh Water;
        }
        private struct WaterVertex
        {
            public Vector3 Position;
            public float Depth;
            public WaterVertex(Vector3 position, float depth) { Position = position; Depth = depth; }
        }
        private readonly SquareBuildGrid grid;
        private readonly GameObject root;
        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly Material[] biomeMaterials = new Material[5];
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly Material waterMaterial;
        private int habitatRevision = -1, terrainRevision = -1;
        public GameObject Root => root;

        public HabitatTerrainSurface(SquareBuildGrid grid, TerrainArtDefinition art, Transform parent = null)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            root = new GameObject("Habitat terrain surfaces");
            if (parent != null) root.transform.SetParent(parent, true);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material Fallback(Material source, Color tint, string name)
            {
                var result = source != null ? new Material(source) : new Material(shader);
                result.name = name; result.color = tint; ownedMaterials.Add(result); return result;
            }
            biomeMaterials[0] = art != null && art.grass != null ? art.grass : Fallback(null, new Color(.35f, .5f, .2f), "Habitat grass");
            biomeMaterials[1] = art != null && art.soil != null ? art.soil : Fallback(art != null ? art.cliff : null, art != null ? art.dirtTint : new Color(.45f, .29f, .16f), "Habitat dirt");
            biomeMaterials[2] = art != null && art.thicket != null ? art.thicket : Fallback(art != null ? art.grass : null, art != null ? art.thicketTint : new Color(.18f, .32f, .12f), "Habitat thicket");
            biomeMaterials[3] = art != null && art.stone != null ? art.stone : Fallback(art != null ? art.cliff : null, art != null ? art.stoneTint : new Color(.53f, .55f, .53f), "Habitat stone");
            biomeMaterials[4] = art != null && art.sand != null ? art.sand : Fallback(art != null ? art.cliff : null, art != null ? art.sandTint : new Color(.82f, .72f, .44f), "Habitat sand");
            if (art != null && art.water != null) waterMaterial = art.water;
            else
            {
                waterMaterial = Fallback(null, art != null ? art.waterTint : new Color(.13f, .47f, .61f, .7f), "Habitat pond water");
                waterMaterial.renderQueue = 3000; waterMaterial.SetOverrideTag("RenderType", "Transparent");
                waterMaterial.SetFloat("_Surface", 1); waterMaterial.SetFloat("_ZWrite", 0);
                waterMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); waterMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                waterMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); waterMaterial.EnableKeyword("_ALPHABLEND_ON");
                if (waterMaterial.HasProperty("_Smoothness")) waterMaterial.SetFloat("_Smoothness", .8f);
            }
            for (int z = 0; z < grid.Depth; z += 16) for (int x = 0; x < grid.Width; x += 16)
                chunks.Add(new Chunk { X = x, Z = z, Width = Math.Min(16, grid.Width - x), Depth = Math.Min(16, grid.Depth - z) });
            Refresh();
        }

        public void Refresh()
        {
            int currentTerrain = grid.Terrain?.Revision ?? 0;
            if (habitatRevision == grid.Habitat.Revision && terrainRevision == currentTerrain) return;
            bool all = terrainRevision != currentTerrain || habitatRevision < 0 || grid.Habitat.Revision - habitatRevision > 1;
            foreach (var chunk in chunks)
            {
                var min = grid.Habitat.ChangedMinimum; var max = grid.Habitat.ChangedMaximum;
                if (!all && (chunk.X > max.x || chunk.Z > max.y || chunk.X + chunk.Width <= min.x || chunk.Z + chunk.Depth <= min.y)) continue;
                Rebuild(chunk);
            }
            habitatRevision = grid.Habitat.Revision; terrainRevision = currentTerrain;
        }
        private RuntimeGroundMesh MakeMesh(string name, Material material)
        {
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(RuntimeGroundMesh)) { layer = 2 };
            child.transform.SetParent(root.transform, false);
            var renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return child.GetComponent<RuntimeGroundMesh>();
        }
        private void Rebuild(Chunk chunk)
        {
            var typed = new List<Vector2Int>[5];
            for (int index = 0; index < typed.Length; index++) typed[index] = new List<Vector2Int>();
            var waterVertices = new List<Vector3>(); var waterTriangles = new List<int>();
            for (int z = chunk.Z; z < chunk.Z + chunk.Depth; z++) for (int x = chunk.X; x < chunk.X + chunk.Width; x++)
            {
                var cell = new Vector2Int(x, z);
                if (grid.Habitat.HasBiomeOverride(cell)) typed[(int)grid.Habitat.BiomeAt(cell)].Add(cell);
                if (grid.Habitat.HasWaterSurface(cell)) AppendWaterCell(grid, cell, waterVertices, waterTriangles);
            }
            for (int index = 0; index < typed.Length; index++)
            {
                if (typed[index].Count == 0) { if (chunk.Biomes[index] != null) chunk.Biomes[index].gameObject.SetActive(false); continue; }
                if (chunk.Biomes[index] == null) chunk.Biomes[index] = MakeMesh("Painted " + (HabitatBiome)index + " [" + chunk.X + ", " + chunk.Z + "]", biomeMaterials[index]);
                chunk.Biomes[index].gameObject.SetActive(true);
                chunk.Biomes[index].Set(GroundMeshBuilder.Cells(grid, typed[index], .012f));
            }
            if (waterTriangles.Count == 0) { if (chunk.Water != null) chunk.Water.gameObject.SetActive(false); return; }
            if (chunk.Water == null) chunk.Water = MakeMesh("Pond water [" + chunk.X + ", " + chunk.Z + "]", waterMaterial);
            chunk.Water.gameObject.SetActive(true); chunk.Water.Set(GroundMeshBuilder.Finish("Clipped pond surface", waterVertices, waterTriangles));
        }
        internal static void AppendWaterCell(SquareBuildGrid grid, Vector2Int cell, List<Vector3> vertices, List<int> triangles)
        {
            int subdivisions = grid.Terrain != null ? GridHeightField.Subdivisions : 1;
            float level = grid.Habitat.WaterLevel(cell);
            WaterVertex Point(float x, float z)
            {
                var position = grid.Origin + new Vector3(x * grid.CellSize, 0, z * grid.CellSize);
                float depth = level - grid.GroundHeight(position); position.y = level + .018f;
                return new WaterVertex(position, depth);
            }
            for (int z = 0; z < subdivisions; z++) for (int x = 0; x < subdivisions; x++)
            {
                float a = cell.x + (float)x / subdivisions, b = cell.y + (float)z / subdivisions, step = 1f / subdivisions;
                var p0 = Point(a, b); var p1 = Point(a, b + step); var p2 = Point(a + step, b + step); var p3 = Point(a + step, b);
                ClipTriangle(p0, p1, p2, vertices, triangles); ClipTriangle(p0, p2, p3, vertices, triangles);
            }
        }
        private static void ClipTriangle(WaterVertex a, WaterVertex b, WaterVertex c, List<Vector3> vertices, List<int> triangles)
        {
            // A triangle clipped by one horizontal shoreline has at most four vertices.
            // Fixed locals keep brush rebuilds from allocating a temporary list per triangle.
            int start = vertices.Count;
            void Edge(WaterVertex previous, WaterVertex next)
            {
                bool previousWet = previous.Depth > 0f, nextWet = next.Depth > 0f;
                if (previousWet != nextWet)
                {
                    float fraction = previous.Depth / (previous.Depth - next.Depth);
                    vertices.Add(Vector3.Lerp(previous.Position, next.Position, fraction));
                }
                if (nextWet) vertices.Add(next.Position);
            }
            Edge(c, a); Edge(a, b); Edge(b, c);
            int count = vertices.Count - start;
            if (count < 3) { if (count > 0) vertices.RemoveRange(start, count); return; }
            var first = vertices[start]; var second = vertices[start + 1]; var third = vertices[start + 2];
            var fourth = count == 4 ? vertices[start + 3] : default;
            vertices.RemoveRange(start, count);
            void Triangle(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                // Exact shoreline corners can repeat the closing vertex. At distant
                // map origins float quantisation can also collapse a tiny sliver.
                // Emit only real triangles, including their vertices, so mesh normal
                // recalculation never sees an unused/zero-area shoreline vertex.
                if (!(Vector3.Cross(p1 - p0, p2 - p0).y > .000001f)) return;
                int index = vertices.Count; vertices.Add(p0); vertices.Add(p1); vertices.Add(p2);
                triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
            }
            Triangle(first, second, third);
            if (count == 4) Triangle(first, third, fourth);
        }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var material in ownedMaterials) if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}

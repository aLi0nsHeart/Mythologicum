using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mythologicum
{
    public static class BuildGridVisual
    {
        // Thin horizontal strips work with the same URP material as the ground.
        public static Mesh CreateMesh(SquareBuildGrid grid)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float width = grid.Width * grid.CellSize;
            float depth = grid.Depth * grid.CellSize;
            float thickness = Mathf.Min(0.035f, grid.CellSize * 0.02f);
            for (int x = 0; x <= grid.Width; x++)
                AddStrip(vertices, triangles, x * grid.CellSize - thickness / 2f, 0f, thickness, depth);
            for (int z = 0; z <= grid.Depth; z++)
                AddStrip(vertices, triangles, 0f, z * grid.CellSize - thickness / 2f, width, thickness);
            var mesh = new Mesh { name = "Build Grid", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddStrip(List<Vector3> vertices, List<int> triangles,
            float x, float z, float width, float depth)
        {
            int i = vertices.Count;
            vertices.Add(new Vector3(x, 0f, z));
            vertices.Add(new Vector3(x, 0f, z + depth));
            vertices.Add(new Vector3(x + width, 0f, z + depth));
            vertices.Add(new Vector3(x + width, 0f, z));
            triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
    }
}

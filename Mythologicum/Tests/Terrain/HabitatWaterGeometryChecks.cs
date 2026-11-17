using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class HabitatWaterGeometryChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { checks++; if (!value) throw new Exception(reason); }
    public static void Main()
    {
        foreach (float origin in new[] { 0f, 60000f, -60000f })
        {
            var field = new GridHeightField(24, 24, 2);
            var grid = new SquareBuildGrid(new Vector3(origin, 0, origin), 2, 24, 24, field);
            Check(grid.Habitat.AddWater(new Vector2(10.5f, 11.5f), 3, 0, 1.2f, cell => true, out _), "The actual water brush creates the geometry fixture");
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var cell in grid.Habitat.WaterSurfaceCells) HabitatTerrainSurface.AppendWaterCell(grid, cell, vertices, triangles);
            Check(vertices.Count > 0 && triangles.Count > 0, "The clipped pond contains visible geometry");
            var used = new HashSet<int>();
            for (int index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]]; var b = vertices[triangles[index + 1]]; var c = vertices[triangles[index + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Check(normal.y > .000001f && Math.Abs(normal.x) < .000001f && Math.Abs(normal.z) < .000001f,
                    "Every shoreline triangle has positive upward area, including exact zero-depth corners and distant map origins");
                used.Add(triangles[index]); used.Add(triangles[index + 1]); used.Add(triangles[index + 2]);
            }
            Check(used.Count == vertices.Count, "Clipping leaves no unused vertices with zero recalculated normals");
            Check(vertices.All(vertex => Math.Abs(vertex.y - .018f) < .002f), "Shoreline triangulation keeps the water flat");
        }
        Console.WriteLine("PASS: " + checks + " pond shoreline area, normals and large-origin geometry checks.");
    }
}

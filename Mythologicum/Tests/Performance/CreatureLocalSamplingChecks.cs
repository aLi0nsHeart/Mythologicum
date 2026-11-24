using System;
using Mythologicum;
using UnityEngine;

/// <summary>Proves bounded queries retain every sample from the former map-wide distance filter.</summary>
public static class CreatureLocalSamplingChecks
{
    private static int checks;
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); checks++; }
    private static float Distance(Vector3 a, Vector3 b)
        => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

    public static void Main()
    {
        var random = new System.Random(984201);
        foreach (float origin in new[] { 0f, 60000f, -60000f })
        {
            var grid = new SquareBuildGrid(new Vector3(origin, 0, origin), 2, 72, 61);
            for (int trial = 0; trial < 100; trial++)
            {
                var position = new Vector3(origin + (float)random.NextDouble() * 144, 0,
                    origin + (float)random.NextDouble() * 122);
                float radius = trial % 8 == 0 ? 0 : (float)random.NextDouble() * 35;
                CreatureWorld.TryLocalSampleBounds(grid, position, radius, out var minimum, out var maximum);
                int former = 0, local = 0;
                for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, z);
                    bool reachable = (x + 3 * z + trial) % 7 != 0;
                    if (!reachable || Distance(grid.CellCenter(cell), position) > radius) continue;
                    former++;
                    Check(x >= minimum.x && x <= maximum.x && z >= minimum.y && z <= maximum.y,
                        "A bounded query must include every reachable cell inside the exact distance filter");
                }
                for (int z = minimum.y; z <= maximum.y; z++) for (int x = minimum.x; x <= maximum.x; x++)
                    if ((x + 3 * z + trial) % 7 != 0 && Distance(grid.CellCenter(new Vector2Int(x, z)), position) <= radius) local++;
                Check(former == local, "Bounded and former map-wide query counts agree at sub-tile positions and distant origins");
            }
            foreach (float radius in new[] { 0f, 2f, 12f, 1000f, float.PositiveInfinity, float.NaN })
            {
                var position = grid.CellCenter(new Vector2Int(0, 0));
                CreatureWorld.TryLocalSampleBounds(grid, position, radius, out var minimum, out var maximum);
                for (int z = 0; z < grid.Depth; z++) for (int x = 0; x < grid.Width; x++)
                    if (!(Distance(grid.CellCenter(new Vector2Int(x, z)), position) > radius))
                        Check(x >= minimum.x && x <= maximum.x && z >= minimum.y && z <= maximum.y,
                            "Boundary, zero-radius and unusual authoring values retain the former meadow predicate");
            }
        }

        foreach (int size in new[] { 72, 720 })
        {
            var grid = new SquareBuildGrid(Vector3.zero, 2, size, size);
            CreatureWorld.TryLocalSampleBounds(grid, grid.CellCenter(new Vector2Int(35, 35)), 12,
                out var minimum, out var maximum);
            int visits = (maximum.x - minimum.x + 1) * (maximum.y - minimum.y + 1);
            Check(visits <= 225, "A twelve-metre habitat query stays local as the map grows");
        }
        var small = new SquareBuildGrid(Vector3.zero, 2, 3, 3);
        Check(!CreatureWorld.TryLocalSampleBounds(small, Vector3.zero, -1, out _, out var empty) && empty.x < 0,
            "A negative environment radius yields an empty bounded scan");
        Console.WriteLine("PASS: " + checks + " bounded creature habitat equivalence, distant-origin and fixed-work checks.");
    }
}

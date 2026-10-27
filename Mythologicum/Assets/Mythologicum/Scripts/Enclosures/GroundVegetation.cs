using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Decorative ground cover; never occupies cells or interferes with physics queries.</summary>
    public sealed class GroundVegetation : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly EnvironmentGrid environment;
        private readonly GameObject root;
        private readonly Dictionary<Vector2Int, GameObject> cells = new Dictionary<Vector2Int, GameObject>();
        private int gridRevision = -1, environmentRevision = -1;

        public GroundVegetation(SquareBuildGrid grid, EnvironmentGrid environment, VegetationDefinition definition)
        {
            this.grid = grid;
            this.environment = environment;
            root = new GameObject("Ground vegetation");
            if (definition == null) return;
            var random = new System.Random(definition.scatterSeed);
            for (int z = 0; z < grid.Depth; z++)
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new Vector2Int(x, z);
                    var patchRoot = new GameObject($"Ground cover [{x}, {z}]");
                    patchRoot.transform.SetParent(root.transform, false);
                    patchRoot.transform.position = grid.CellCenter(cell);
                    cells.Add(cell, patchRoot);
                    for (int i = 0; i < definition.patchesPerCell; i++)
                    {
                        double roll = random.NextDouble();
                        GameObject[] choices = roll < 0.6 ? definition.grasses : roll < 0.85 ? definition.herbs : definition.flowers;
                        if (choices == null || choices.Length == 0) continue;
                        GameObject prefab = choices[random.Next(choices.Length)];
                        if (prefab == null) continue;
                        float height = (float)(0.22 + random.NextDouble() * 0.26);
                        GameObject plant = VegetationVisuals.Create(prefab, patchRoot.transform, height,
                            (float)random.NextDouble() * 360f, false);
                        plant.transform.localPosition += new Vector3((float)(random.NextDouble() - 0.5) * grid.CellSize * 0.65f,
                            0.005f, (float)(random.NextDouble() - 0.5) * grid.CellSize * 0.65f);
                    }
                }
            Refresh();
        }

        public void Refresh()
        {
            if (gridRevision == grid.Revision && environmentRevision == environment.Revision) return;
            foreach (var pair in cells)
            {
                bool visible = grid.IsAvailable(pair.Key) && environment.TreesAt(pair.Key) == 0;
                if (pair.Value.activeSelf != visible) pair.Value.SetActive(visible);
            }
            gridRevision = grid.Revision;
            environmentRevision = environment.Revision;
        }

        public void Dispose() => UnityEngine.Object.Destroy(root);
    }
}
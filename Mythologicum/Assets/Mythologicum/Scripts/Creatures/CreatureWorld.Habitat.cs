using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class CreatureWorld
    {
        private readonly List<Vector2Int> meadowSample = new List<Vector2Int>();

        /// <summary>Conservative bounds only; callers retain their exact distance and reachability tests.</summary>
        internal static bool TryLocalSampleBounds(SquareBuildGrid grid, Vector3 position, float radius,
            out Vector2Int minimum, out Vector2Int maximum)
        {
            minimum = Vector2Int.zero; maximum = new Vector2Int(-1, -1);
            if (radius < 0f) return false;
            maximum = new Vector2Int(grid.Width - 1, grid.Depth - 1);
            // Keep existing comparison behavior for invalid authoring values;
            // normal finite radii use a small, local rectangle.
            if (float.IsNaN(radius) || float.IsInfinity(radius) || float.IsNaN(position.x) || float.IsInfinity(position.x)
                || float.IsNaN(position.z) || float.IsInfinity(position.z)) return true;
            int Bound(double value, int extent) => (int)Math.Max(0, Math.Min(extent - 1, value));
            minimum = new Vector2Int(Bound(Math.Floor(((double)position.x - grid.Origin.x - radius) / grid.CellSize) - 1, grid.Width),
                Bound(Math.Floor(((double)position.z - grid.Origin.z - radius) / grid.CellSize) - 1, grid.Depth));
            maximum = new Vector2Int(Bound(Math.Floor(((double)position.x - grid.Origin.x + radius) / grid.CellSize) + 1, grid.Width),
                Bound(Math.Floor(((double)position.z - grid.Origin.z + radius) / grid.CellSize) + 1, grid.Depth));
            return true;
        }

        /// <summary>Local reachable, unpaved ground contributes meadow in proportion to its nonforest cover.</summary>
        private float MeasureOpenGround(CreatureAgent agent, HashSet<Vector2Int> accessible)
        {
            meadowSample.Clear();
            int localTiles = 0;
            TryLocalSampleBounds(grid, agent.transform.position, agent.Definition.environmentRadius, out var minimum, out var maximum);
            for (int z = minimum.y; z <= maximum.y; z++)
            for (int x = minimum.x; x <= maximum.x; x++)
            {
                var cell = new Vector2Int(x, z);
                if (!accessible.Contains(cell)) continue;
                if (HorizontalDistance(grid.CellCenter(cell), agent.transform.position) > agent.Definition.environmentRadius) continue;
                localTiles++;
                if (grid.ContentAt(cell) == BuildCellContent.Empty && grid.Habitat.BiomeAt(cell) == HabitatBiome.Grass) meadowSample.Add(cell);
            }
            if (localTiles == 0) return 0f;
            var sample = environment.Sample(meadowSample);
            float localArea = localTiles * grid.CellSize * grid.CellSize;
            return Mathf.Clamp01((sample.Area - sample.ForestArea) / localArea);
        }

        /// <summary>Optional ground rest in reachable shade; lack of shade never prevents basic sleep.</summary>
        private bool TrySeekShadeSleep(CreatureAgent agent)
        {
            var definition = agent.Definition;
            if (!definition.prefersTreeShadeForSleep || definition.requiresTreeForSleep
                || definition.requiresGravestoneForSleep || definition.requiresBurrowForSleep) return false;
            Vector2Int? nearest = null;
            int best = int.MaxValue;
            foreach (var cell in ReachableRegion(agent.Cell))
            {
                if (environment.TreesAt(cell) == 0) continue;
                bool reserved = false;
                foreach (var other in agents)
                    if (other != agent && other.Alive && other.SleepTree == cell) { reserved = true; break; }
                if (reserved) continue;
                int distance = ConstructionRouteGraph.CellDistance(agent.Cell, cell);
                if (distance < best || distance == best && (!nearest.HasValue || cell.y < nearest.Value.y
                    || cell.y == nearest.Value.y && cell.x < nearest.Value.x))
                { best = distance; nearest = cell; }
            }
            if (!nearest.HasValue)
            {
                agent.SleepTree = null;
                return false;
            }
            agent.SleepTree = nearest;
            if (agent.Cell == nearest.Value && agent.Route.Count == 0)
            {
                agent.Activity = CreatureActivity.Sleeping;
                return true;
            }
            if (SetGoal(agent, nearest.Value, walkingOnly: true))
            {
                agent.Activity = CreatureActivity.SeekingSleep;
                return true;
            }
            agent.SleepTree = null;
            return false;
        }
    }
}

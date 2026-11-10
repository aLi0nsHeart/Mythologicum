using System;
using System.Collections.Generic;
namespace Mythologicum
{
    /// <summary>Remembers only edited terrain nodes; unrelated later changes remain intact.</summary>
    public sealed class TerrainUndoPatch
    {
        private readonly Dictionary<int, float> before = new Dictionary<int, float>();
        private readonly Dictionary<int, float> after = new Dictionary<int, float>();
        public IEnumerable<int> Nodes => before.Keys;
        public int Count => before.Count;
        public TerrainUndoPatch(float[] previous, float[] next)
        {
            if (previous == null || next == null || previous.Length != next.Length) return;
            for (int i = 0; i < previous.Length; i++)
                if (Math.Abs(previous[i] - next[i]) > .00001f) { before.Add(i, previous[i]); after.Add(i, next[i]); }
        }
        public bool TryRevert(float[] current, out float[] result)
        {
            result = null;
            if (current == null) return Count == 0;
            foreach (var pair in after)
                if (pair.Key >= current.Length || Math.Abs(current[pair.Key] - pair.Value) > .0001f) return false;
            result = (float[])current.Clone();
            foreach (var pair in before) result[pair.Key] = pair.Value;
            return true;
        }
    }
}

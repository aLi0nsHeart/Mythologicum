using System;
namespace Mythologicum
{
    /// <summary>New-park choices, independent of scenes and editor configuration.</summary>
    [Serializable]
    public sealed class MapGenerationOptions
    {
        public const float PreviewCellSize = 2f;
        public int width = 50;
        public int depth = 50;
        public int seed = 2471;
        public float relief = 7f;
        public float featureSize = 28f;
        public float forestDensity = .88f;

        public MapGenerationOptions Clone() => (MapGenerationOptions)MemberwiseClone();

        public bool Validate(out string error)
        {
            if (width != depth || !SupportedSize(width)) error = Loc.Text("park.map_generation_options.choose_small_standard_or_large_map_size");
            else if (!Finite(relief) || relief < 0f || relief > 20f) error = Loc.Text("park.map_generation_options.hill_height_must_be_between_0_and_20_metres");
            else if (!Finite(featureSize) || featureSize < 12f || featureSize > 64f) error = Loc.Text("park.map_generation_options.hill_width_must_be_between_12_and_64_metres");
            else if (!Finite(forestDensity) || forestDensity < 0f || forestDensity > 1f) error = Loc.Text("park.map_generation_options.forest_density_must_be_between_0_and_100");
            else { error = ""; return true; }
            return false;
        }

        public GridHeightField CreateHeightField(float cellSize = PreviewCellSize, float walkSlope = 45f,
            float pathSlope = 28f, float earthwork = 4f, float uphillCostMultiplier = .25f)
        {
            if (!Validate(out string error)) throw new ArgumentException(error);
            var field = new GridHeightField(width, depth, cellSize, walkSlope, pathSlope, earthwork, uphillCostMultiplier);
            // Relief zero remains seeded, so forest and natural decoration keep the chosen map identity.
            field.Generate(seed, relief, featureSize);
            return field;
        }

        public static bool SupportedSize(int size) => size == 32 || size == 50 || size == 72;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

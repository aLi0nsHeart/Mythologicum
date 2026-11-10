using System;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class TerrainSettings
    {
        public bool generateHills = true;
        public int seed = 2471;
        [Min(0f)] public float relief = 7f;
        [Min(4f)] public float featureSize = 28f;
        [Range(1f,80f)] public float maximumWalkSlope = 45f;
        [Range(1f,60f)] public float maximumPathSlope = 28f;
        [Min(.1f)] public float maximumFoundationEarthwork = 4f;
        [Tooltip("Extra pathfinding cost per uphill metre relative to tile size. Zero uses only the actual slope distance; larger values prefer flatter routes.")]
        [Min(0f)] public float uphillCostMultiplier = .25f;
        [Tooltip("Optional readable grayscale heightmap. Empty uses reproducible rolling hills.")]
        public Texture2D heightmap;

        public void Validate()
        {
            relief = Nonnegative(relief); featureSize = Math.Max(4f, Nonnegative(featureSize));
            maximumWalkSlope = Math.Max(1f, Math.Min(80f, Nonnegative(maximumWalkSlope)));
            maximumPathSlope = Math.Max(1f, Math.Min(60f, Nonnegative(maximumPathSlope)));
            maximumFoundationEarthwork = Math.Max(.1f, Nonnegative(maximumFoundationEarthwork));
            uphillCostMultiplier = Nonnegative(uphillCostMultiplier);
        }
        private static float Nonnegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
    }

    [Serializable]
    public sealed class TerrainBrushSettings
    {
        [Header("Radius in grid tiles")]
        [Min(1f)] public float initialRadius = 3f;
        [Min(1f)] public float minimumRadius = 1f;
        [Min(1f)] public float maximumRadius = 8f;
        [Min(.1f)] public float radiusStep = 1f;
        [Header("Height change in metres per simulation second")]
        [Min(.01f)] public float initialStrength = 1f;
        [Min(.01f)] public float minimumStrength = .25f;
        [Min(.01f)] public float maximumStrength = 4f;
        [Min(.01f)] public float strengthStep = .25f;

        public void Validate()
        {
            minimumRadius = Positive(minimumRadius, 1f); maximumRadius = Math.Max(minimumRadius, Positive(maximumRadius, 1f));
            initialRadius = Math.Max(minimumRadius, Math.Min(maximumRadius, Positive(initialRadius, 1f)));
            radiusStep = Positive(radiusStep, .1f);
            minimumStrength = Positive(minimumStrength, .01f); maximumStrength = Math.Max(minimumStrength, Positive(maximumStrength, .01f));
            initialStrength = Math.Max(minimumStrength, Math.Min(maximumStrength, Positive(initialStrength, .01f)));
            strengthStep = Positive(strengthStep, .01f);
        }
        private static float Positive(float value, float minimum) => float.IsNaN(value) || float.IsInfinity(value) ? minimum : Math.Max(minimum, value);
    }
    [CreateAssetMenu(menuName="Mythologicum/Terrain settings",fileName="DefaultTerrain")]
    public sealed class TerrainDefinition : ScriptableObject
    {
        public TerrainSettings settings = new TerrainSettings();
        public TerrainBrushSettings brush = new TerrainBrushSettings();
        private void OnValidate()
        {
            if (settings == null) settings = new TerrainSettings(); settings.Validate();
            if (brush == null) brush = new TerrainBrushSettings(); brush.Validate();
        }
    }
    [Serializable] public sealed class SavedTerrain
    {
        public int seed;
        public float maximumWalkSlope=45f,maximumPathSlope=28f,maximumFoundationEarthwork=4f;
        public float uphillCostMultiplier = .25f;
        public int uphillCostVersion;
        public float[] heights;
    }
}

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
        [Tooltip("Optional readable grayscale heightmap. Empty uses reproducible rolling hills.")]
        public Texture2D heightmap;
    }
    [CreateAssetMenu(menuName="Mythologicum/Terrain settings",fileName="DefaultTerrain")]
    public sealed class TerrainDefinition : ScriptableObject { public TerrainSettings settings = new TerrainSettings(); }
    [Serializable] public sealed class SavedTerrain
    {
        public int seed;
        public float maximumWalkSlope=45f,maximumPathSlope=28f,maximumFoundationEarthwork=4f;
        public float[] heights;
    }
}

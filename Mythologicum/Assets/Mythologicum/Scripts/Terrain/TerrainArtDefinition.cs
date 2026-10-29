using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    [Serializable] public sealed class ForestBorderSettings
    {
        public bool enabled = true;
        [Range(0,8)] public int widthInCells = 3;
        [Range(0f,1f)] public float density = .88f;
        [Min(.1f)] public float minimumHeight = 5f, maximumHeight = 7f;
        public int seed = 819;
    }
    [CreateAssetMenu(menuName="Mythologicum/Terrain art",fileName="TerrainArt")]
    public sealed class TerrainArtDefinition : ScriptableObject
    {
        public Material grass,cliff;
        public GameObject[] borderTrees = Array.Empty<GameObject>();
        public ForestBorderSettings forest = new ForestBorderSettings();
    }
    public static class ForestBorderLayout
    {
        public static List<Vector2Int> Cells(int width,int depth,int band,float density,int seed)
        {
            var result=new List<Vector2Int>();band=Math.Max(0,Math.Min(band,(Math.Min(width,depth)-1)/2));
            if(band==0||density<=0||float.IsNaN(density))return result;
            var random=new System.Random(seed);
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                int edge=Math.Min(Math.Min(x,width-1-x),Math.Min(z,depth-1-z));
                if(edge<band&&random.NextDouble()<density)result.Add(new Vector2Int(x,z));
            }
            return result;
        }
    }
    [Serializable] public sealed class SavedTreeVisual
    {
        public Vector2Int cell;
        public string prefab;
        public float height,yaw;
    }
}

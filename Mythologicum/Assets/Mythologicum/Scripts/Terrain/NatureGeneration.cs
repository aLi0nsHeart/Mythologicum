using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    public sealed class NatureLayout
    {
        public List<Vector2Int> Trees {get;} = new List<Vector2Int>();
        public List<Vector2Int> Rocks {get;} = new List<Vector2Int>();
    }
    /// <summary>Deterministic landscape decoration, shared by generation and the new-game preview.</summary>
    public static class NatureGeneration
    {
        public static NatureLayout Plan(int width,int depth,int band,float density,int seed)
        {
            var result=new NatureLayout();
            if(width<1||depth<1)return result;
            band=Math.Max(0,Math.Min(band,(Math.Min(width,depth)-1)/2));
            density=float.IsNaN(density)?0:Math.Max(0,Math.Min(1,density));
            var random=new System.Random(seed);
            for(int z=0;z<depth;z++)for(int x=0;x<width;x++)
            {
                int edge=Math.Min(Math.Min(x,width-1-x),Math.Min(z,depth-1-z));
                float localWidth=band*(.65f+.9f*Noise(x*.12f,z*.12f,seed+17));
                float clearing=Noise(x*.17f,z*.17f,seed+41);
                float treeChance=0;
                if(band>0&&edge<localWidth)
                {
                    float feather=Math.Min(1f,Math.Max(0,localWidth-edge));
                    treeChance=density*(.78f+.22f*Noise(x*.21f,z*.21f,seed+5))*feather;
                    if(edge==0)treeChance=Math.Min(1,density*1.09f);
                    else if(clearing>.72f)treeChance*=.12f;
                }
                else if(band>0)
                {
                    // Small woodland pockets leave most of the interior available for player construction.
                    float cluster=Math.Max(0,Noise(x*.105f,z*.105f,seed+67)-.64f);
                    treeChance=density*cluster*.9f;
                    if(clearing>.7f)treeChance*=.15f;
                }
                var cell=new Vector2Int(x,z);
                if(random.NextDouble()<treeChance){result.Trees.Add(cell);continue;}
                float rockPatch=Noise(x*.19f,z*.19f,seed+97);
                float rockChance=.004f+Math.Max(0,rockPatch-.62f)*.13f;
                if(edge<band)rockChance*=1.4f;
                if(random.NextDouble()<rockChance)result.Rocks.Add(cell);
            }
            return result;
        }
        public static int Seed(SquareBuildGrid grid,TreeConstruction trees,RockConstruction rocks,TerrainArtDefinition art,int terrainSeed,float forestDensity)
        {
            if(art==null)return 0;
            var settings=art.forest;
            int combined=terrainSeed^(settings?.seed??819);
            int band=settings!=null&&settings.enabled?settings.widthInCells:0;
            var plan=Plan(grid.Width,grid.Depth,band,forestDensity,combined);
            var random=new System.Random(combined^4139);int planted=0;
            if(art.borderTrees!=null&&art.borderTrees.Length>0&&settings!=null)
                foreach(var cell in plan.Trees)
                {
                    if(rocks!=null&&rocks.Occupies(cell))continue;
                    var prefab=art.borderTrees[random.Next(art.borderTrees.Length)];
                    float minimum=Math.Min(settings.minimumHeight,settings.maximumHeight),maximum=Math.Max(settings.minimumHeight,settings.maximumHeight);
                    float height=minimum+(maximum-minimum)*(float)random.NextDouble();
                    if(prefab!=null&&trees.TryPlace(cell,prefab,height,(float)random.NextDouble()*360f))planted++;
                }
            if(rocks!=null)foreach(var cell in plan.Rocks)if(!trees.Occupies(cell))rocks.TryPlace(cell);
            return planted;
        }
        public static bool GroundIsSoil(float cellX,float cellZ,int seed)
        {
            float broad=Noise(cellX*.115f,cellZ*.115f,seed+601);
            float detail=Noise(cellX*.37f,cellZ*.37f,seed+619);
            return broad*.82f+detail*.18f>.66f;
        }
        public static float Noise(float x,float z,int seed)
        {
            int a=(int)Math.Floor(x),b=(int)Math.Floor(z);x-=a;z-=b;x=x*x*(3-2*x);z=z*z*(3-2*z);
            return SquareTileHeightSampler.Bilinear(Hash(a,b,seed),Hash(a+1,b,seed),Hash(a,b+1,seed),Hash(a+1,b+1,seed),x,z);
        }
        internal static float Hash(int x,int z,int seed)
        {
            unchecked
            {
                uint value=(uint)(x*374761393+z*668265263+seed*1442695041);
                value=(value^(value>>13))*1274126177;
                return (value^(value>>16))/(float)uint.MaxValue;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Mythologicum
{
    public sealed class TerrainFoundationPlan
    {
        public float Level {get;internal set;}
        internal readonly Dictionary<int,float> Changes=new Dictionary<int,float>();
        internal readonly HashSet<int> Foundation=new HashSet<int>();
    }
    /// <summary>Shared corner heights, foundation reservations and slope queries; no scene or Physics dependency.</summary>
    public sealed class GridHeightField
    {
        public const int Subdivisions=2;
        public int Width {get;}
        public int Depth {get;}
        public float CellSize {get;}
        public int Seed {get;private set;}
        public float MaximumWalkSlope {get;private set;}
        public float MaximumPathSlope {get;private set;}
        public float MaximumEarthwork {get;private set;}
        public int Revision {get;private set;}
        public event Action Changed;
        private readonly float[] heights,slopes;
        private readonly Dictionary<object,HashSet<int>> foundations=new Dictionary<object,HashSet<int>>();
        private readonly Dictionary<int,int> locks=new Dictionary<int,int>();
        public GridHeightField(int width,int depth,float cellSize,float maximumWalkSlope=45f,float maximumPathSlope=28f,float maximumEarthwork=4f)
        {
            if(width<1||depth<1||cellSize<=0)throw new ArgumentOutOfRangeException(nameof(width));
            Width=width;Depth=depth;CellSize=cellSize;MaximumWalkSlope=maximumWalkSlope;MaximumPathSlope=maximumPathSlope;MaximumEarthwork=maximumEarthwork;
            heights=new float[(width+1)*(depth+1)];slopes=new float[width*depth];
        }
        private int Index(int x,int z)=>z*(Width+1)+x;
        public float Node(int x,int z)=>heights[Index(Math.Max(0,Math.Min(Width,x)),Math.Max(0,Math.Min(Depth,z)))];
        private float Corner(int x,int z,Dictionary<int,float> proposed)
        {int i=Index(x,z);return proposed!=null&&proposed.TryGetValue(i,out float value)?value:heights[i];}
        private float Bilinear(int x,int z,float u,float v,Dictionary<int,float> proposed=null)
            =>SquareTileHeightSampler.Bilinear(Corner(x,z,proposed),Corner(x+1,z,proposed),Corner(x,z+1,proposed),Corner(x+1,z+1,proposed),u,v);
        /// <summary>Matches the exact triangles rendered by the terrain, including between cell centres.</summary>
        public float Sample(float x,float z)
        {
            x=Math.Max(0,Math.Min(Width,x));z=Math.Max(0,Math.Min(Depth,z));
            int cx=Math.Min(Width-1,(int)x),cz=Math.Min(Depth-1,(int)z);
            float u=x-cx,v=z-cz;
            int sx=Math.Min(Subdivisions-1,(int)(u*Subdivisions)),sz=Math.Min(Subdivisions-1,(int)(v*Subdivisions));
            float a=(float)sx/Subdivisions,b=(float)sz/Subdivisions,step=1f/Subdivisions;
            float h00=Bilinear(cx,cz,a,b),h10=Bilinear(cx,cz,a+step,b),h01=Bilinear(cx,cz,a,b+step),h11=Bilinear(cx,cz,a+step,b+step);
            u=u*Subdivisions-sx;v=v*Subdivisions-sz;
            return v>=u?h00*(1-v)+h01*(v-u)+h11*u:h00*(1-u)+h11*v+h10*(u-v);
        }
        public float MaximumSlope(Vector2Int cell)=>Contains(cell)?slopes[cell.y*Width+cell.x]:90f;
        public bool Walkable(Vector2Int cell,bool path=false)=>Contains(cell)&&MaximumSlope(cell)<=(path?MaximumPathSlope:MaximumWalkSlope)+.001f;
        private bool Contains(Vector2Int cell)=>cell.x>=0&&cell.y>=0&&cell.x<Width&&cell.y<Depth;
        private float Slope(int x,int z,Dictionary<int,float> proposed=null)
        {
            float maximum=0,spacing=CellSize/Subdivisions;
            for(int i=0;i<Subdivisions;i++)for(int j=0;j<Subdivisions;j++)
            {
                float u=(float)i/Subdivisions,v=(float)j/Subdivisions,s=1f/Subdivisions;
                float a=Bilinear(x,z,u,v,proposed),b=Bilinear(x,z,u+s,v,proposed),c=Bilinear(x,z,u,v+s,proposed),d=Bilinear(x,z,u+s,v+s,proposed);
                maximum=Math.Max(maximum,TriangleSlope((b-a)/spacing,(d-b)/spacing));
                maximum=Math.Max(maximum,TriangleSlope((d-c)/spacing,(c-a)/spacing));
            }
            return maximum;
        }
        private static float TriangleSlope(float dx,float dz)=>(float)(Math.Atan(Math.Sqrt(dx*dx+dz*dz))*180/Math.PI);
        public void Generate(int seed,float relief,float featureSize)
        {
            Seed=seed;featureSize=Math.Max(4,featureSize);
            for(int z=0;z<=Depth;z++)for(int x=0;x<=Width;x++)
            {
                float nx=x*CellSize/featureSize,nz=z*CellSize/featureSize;
                heights[Index(x,z)]=relief*(.8f*Noise(nx,nz,seed)+.2f*Noise(nx*2,nz*2,seed+17));
            }
            Recalculate();
        }
        private static float Noise(float x,float z,int seed)
        {
            int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);x-=ix;z-=iz;x=x*x*(3-2*x);z=z*z*(3-2*z);
            float Hash(int a,int b){unchecked{uint v=(uint)(a*374761393+b*668265263+seed*1442695041);v=(v^(v>>13))*1274126177;return (v^(v>>16))/(float)uint.MaxValue;}}
            return SquareTileHeightSampler.Bilinear(Hash(ix,iz),Hash(ix+1,iz),Hash(ix,iz+1),Hash(ix+1,iz+1),x,z);
        }
        public void ImportHeights(float[] values)
        {
            if(values==null||values.Length!=heights.Length)throw new InvalidDataException("Terrain height count does not match the map.");
            foreach(float h in values)if(!Finite(h)||Math.Abs(h)>10000)throw new InvalidDataException("Invalid terrain elevation.");
            Array.Copy(values,heights,heights.Length);foundations.Clear();locks.Clear();Recalculate();
        }
        private void Recalculate()
        {for(int z=0;z<Depth;z++)for(int x=0;x<Width;x++)slopes[z*Width+x]=Slope(x,z);Revision++;Changed?.Invoke();}

        /// <summary>Tests positive brush influence against protected foundation corners for cursor feedback.</summary>
        public bool BrushTouchesFoundation(Vector2 center, float radius)
        {
            if (!Finite(center.x) || !Finite(center.y) || !Finite(radius) || radius <= 0f) return false;
            foreach (int index in locks.Keys)
            {
                float dx = index % (Width + 1) - center.x, dz = index / (Width + 1) - center.y;
                if (TerrainBrushRules.Falloff((float)Math.Sqrt(dx * dx + dz * dz), radius) > 0f) return true;
            }
            return false;
        }

        /// <summary>
        /// Applies one bounded, circular terrain edit. Coordinates and radius are in grid cells;
        /// amount and the flatten target are metres relative to the grid's origin.
        /// A rejected edit leaves all heights and foundation reservations untouched.
        /// </summary>
        public bool TryEdit(SquareBuildGrid grid, Vector2 center, float radius, float amount,
            TerrainBrushMode mode, float targetHeight, out string reason)
        {
            reason = "";
            if (grid == null || grid.Terrain != this)
            { reason = "The terrain editor is not attached to this map."; return false; }
            if (!Finite(center.x) || !Finite(center.y) || !Finite(radius) || radius <= 0f
                || radius > Math.Max(Width, Depth) * 2f || !Finite(amount) || amount < 0f
                || !Finite(targetHeight) || Math.Abs(targetHeight) > 10000f || !Enum.IsDefined(typeof(TerrainBrushMode), mode))
            { reason = "Choose a valid terrain tool, brush size and strength."; return false; }
            if (center.x < 0f || center.y < 0f || center.x > Width || center.y > Depth)
            { reason = "Choose a point inside the map."; return false; }
            if (amount == 0f) return false;

            var proposed = new Dictionary<int, float>();
            int firstX = Math.Max(0, (int)Math.Ceiling(center.x - radius));
            int lastX = Math.Min(Width, (int)Math.Floor(center.x + radius));
            int firstZ = Math.Max(0, (int)Math.Ceiling(center.y - radius));
            int lastZ = Math.Min(Depth, (int)Math.Floor(center.y + radius));
            for (int z = firstZ; z <= lastZ; z++)
            for (int x = firstX; x <= lastX; x++)
            {
                float dx = x - center.x, dz = z - center.y;
                float weight = TerrainBrushRules.Falloff((float)Math.Sqrt(dx * dx + dz * dz), radius);
                if (weight <= 0f) continue;
                int index = Index(x, z);
                if (locks.ContainsKey(index))
                { reason = "Buildings and their entrance foundations cannot be reshaped."; return false; }

                float current = heights[index], step = amount * weight, next;
                if (mode == TerrainBrushMode.Raise) next = current + step;
                else if (mode == TerrainBrushMode.Lower) next = current - step;
                else
                {
                    float target = targetHeight;
                    if (mode == TerrainBrushMode.Smooth)
                    {
                        // Read the original field for every node, so edit order cannot bias smoothing.
                        double total = 0d; int count = 0;
                        for (int nz = Math.Max(0, z - 1); nz <= Math.Min(Depth, z + 1); nz++)
                        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(Width, x + 1); nx++)
                        { total += heights[Index(nx, nz)]; count++; }
                        target = (float)(total / count);
                    }
                    next = TerrainBrushRules.MoveTowards(current, target, step);
                }
                if (!Finite(next) || Math.Abs(next) > 10000f)
                { reason = "The terrain would exceed the supported elevation range."; return false; }
                if (Math.Abs(next - current) > .00001f) proposed[index] = next;
            }
            if (proposed.Count == 0) return false;

            foreach (int index in AffectedCells(proposed.Keys))
            {
                var cell = new Vector2Int(index % Width, index / Width);
                if (grid.ContentAt(cell) == BuildCellContent.Building)
                { reason = "Terrain beneath an existing building cannot be reshaped."; return false; }
                float before = slopes[index], after = Slope(cell.x, cell.y, proposed);
                if (grid.ContentAt(cell) == BuildCellContent.Path && after > MaximumPathSlope + .001f
                    && (before <= MaximumPathSlope + .001f || after > before + .001f))
                { reason = "This edit would make an existing path too steep. Use a wider brush or a gentler change."; return false; }
                if (before <= MaximumWalkSlope + .001f && after > MaximumWalkSlope + .001f)
                { reason = "This edit would make walkable ground too steep. Use a wider brush or smooth the slope."; return false; }
            }

            foreach (var change in proposed) heights[change.Key] = change.Value;
            Recalculate();
            return true;
        }

        private HashSet<int> AffectedCells(IEnumerable<int> nodes)
        {
            var result = new HashSet<int>();
            foreach (int index in nodes)
            {
                int x = index % (Width + 1), z = index / (Width + 1);
                for (int dx = -1; dx <= 0; dx++)
                for (int dz = -1; dz <= 0; dz++)
                    if (x + dx >= 0 && z + dz >= 0 && x + dx < Width && z + dz < Depth)
                        result.Add((z + dz) * Width + x + dx);
            }
            return result;
        }
        private HashSet<int> FoundationNodes(BuildingFootprint footprint)
        {
            var result=new HashSet<int>();
            foreach(var cell in FoundationCells(footprint))
            {
                if(!Contains(cell))return null;
                result.Add(Index(cell.x,cell.y));result.Add(Index(cell.x+1,cell.y));result.Add(Index(cell.x,cell.y+1));result.Add(Index(cell.x+1,cell.y+1));
            }
            return result;
        }
        private static IEnumerable<Vector2Int> FoundationCells(BuildingFootprint footprint)
        {foreach(var c in footprint.Cells())yield return c;yield return footprint.AccessCell;}
        public bool TryPlanFoundation(BuildingFootprint footprint,SquareBuildGrid grid,out TerrainFoundationPlan plan)
        {
            plan=null;var nodes=FoundationNodes(footprint);if(nodes==null)return false;
            double sum=0;float? constrained=null;
            foreach(int i in nodes)
            {
                sum+=heights[i];if(!locks.ContainsKey(i))continue;
                if(constrained.HasValue&&Math.Abs(constrained.Value-heights[i])>.001f)return false;
                constrained=heights[i];
            }
            var p=new TerrainFoundationPlan{Level=constrained??(float)(sum/nodes.Count)};
            foreach(int i in nodes)
            {
                if(Math.Abs(heights[i]-p.Level)>MaximumEarthwork)return false;
                p.Foundation.Add(i);p.Changes[i]=p.Level;
            }
            // A one-cell shoulder blends the platform into its surroundings; existing foundations stay fixed.
            foreach(int i in nodes)
            {
                int nx=i%(Width+1),nz=i/(Width+1);
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                {
                    int x=nx+dx,z=nz+dz;if(x<0||z<0||x>Width||z>Depth)continue;
                    int neighbor=Index(x,z);if(nodes.Contains(neighbor)||locks.ContainsKey(neighbor))continue;
                    p.Changes[neighbor]=(heights[neighbor]+p.Level)*.5f;
                }
            }
            foreach(int i in AffectedCells(p.Changes.Keys))
            {
                var cell=new Vector2Int(i%Width,i/Width);float slope=Slope(cell.x,cell.y,p.Changes);
                if(Walkable(cell)&&slope>MaximumWalkSlope+.001f)return false;
                if(grid.ContentAt(cell)==BuildCellContent.Path&&slope>MaximumPathSlope+.001f)return false;
            }
            plan=p;return true;
        }
        public void Apply(TerrainFoundationPlan plan)
        {
            bool changed=false;foreach(var p in plan.Changes)if(Math.Abs(heights[p.Key]-p.Value)>.00001f){heights[p.Key]=p.Value;changed=true;}
            if(changed)Recalculate();
        }
        public bool RegisterFoundation(object owner,BuildingFootprint footprint)
        {
            var nodes=FoundationNodes(footprint);if(nodes==null||nodes.Count==0)return false;
            float? height=null;foreach(int i in nodes){if(height.HasValue&&Math.Abs(height.Value-heights[i])>.002f)return false;height=heights[i];}
            foundations[owner]=nodes;foreach(int i in nodes){locks.TryGetValue(i,out int count);locks[i]=count+1;}return true;
        }
        public void ReleaseFoundation(object owner)
        {if(!foundations.TryGetValue(owner,out var nodes))return;foundations.Remove(owner);foreach(int i in nodes){if(--locks[i]==0)locks.Remove(i);}}
        public SavedTerrain Capture()=>new SavedTerrain{seed=Seed,maximumWalkSlope=MaximumWalkSlope,maximumPathSlope=MaximumPathSlope,maximumFoundationEarthwork=MaximumEarthwork,heights=(float[])heights.Clone()};
        public void Restore(SavedTerrain saved)
        {
            if(saved==null||!Finite(saved.maximumWalkSlope)||saved.maximumWalkSlope<1||saved.maximumWalkSlope>80
                ||!Finite(saved.maximumPathSlope)||saved.maximumPathSlope<1||saved.maximumPathSlope>80
                ||!Finite(saved.maximumFoundationEarthwork)||saved.maximumFoundationEarthwork<=0||saved.maximumFoundationEarthwork>1000)throw new InvalidDataException("Invalid terrain settings.");
            MaximumWalkSlope=saved.maximumWalkSlope;MaximumPathSlope=saved.maximumPathSlope;MaximumEarthwork=saved.maximumFoundationEarthwork;Seed=saved.seed;ImportHeights(saved.heights);
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}

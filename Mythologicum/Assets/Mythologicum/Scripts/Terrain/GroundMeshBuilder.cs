using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Mythologicum
{
    public static class GroundMeshBuilder
    {
        public static Mesh Surface(SquareBuildGrid grid,int startX,int startZ,int width,int depth,Vector3 origin,float lift=0,bool skirts=false,bool separateSkirts=false)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int z=startZ;z<startZ+depth;z++)for(int x=startX;x<startX+width;x++)AddCell(grid,new Vector2Int(x,z),origin,lift,v,t);
            int topTriangles=t.Count,topVertices=v.Count;
            if(skirts)
            {
                void Skirt(Vector2Int first,Vector2Int second)
                {
                    var a=grid.NodePosition(first)-origin;var d=grid.NodePosition(second)-origin;var b=a;var c=d;
                    b.y=c.y=grid.Origin.y-origin.y-3f;AddQuad(v,t,a,b,c,d);
                }
                for(int z=startZ;z<startZ+depth;z++)
                {if(startX==0)Skirt(new Vector2Int(0,z),new Vector2Int(0,z+1));if(startX+width==grid.Width)Skirt(new Vector2Int(grid.Width,z+1),new Vector2Int(grid.Width,z));}
                for(int x=startX;x<startX+width;x++)
                {if(startZ==0)Skirt(new Vector2Int(x+1,0),new Vector2Int(x,0));if(startZ+depth==grid.Depth)Skirt(new Vector2Int(x,grid.Depth),new Vector2Int(x+1,grid.Depth));}
            }
            var mesh=Finish("Terrain surface",v,t);
            if(separateSkirts)
            {
                mesh.subMeshCount=2;mesh.SetTriangles(t.GetRange(0,topTriangles),0);mesh.SetTriangles(t.GetRange(topTriangles,t.Count-topTriangles),1);
                var uv=mesh.uv;
                for(int i=topVertices;i<uv.Length;i+=4){uv[i]=new Vector2(0,1);uv[i+1]=new Vector2(0,0);uv[i+2]=new Vector2(1,0);uv[i+3]=new Vector2(1,1);}
                mesh.uv=uv;
            }
            return mesh;
        }
        public static Mesh Cells(SquareBuildGrid grid,IEnumerable<Vector2Int> cells,float lift)
        {
            var v=new List<Vector3>();var t=new List<int>();foreach(var cell in cells)AddCell(grid,cell,Vector3.zero,lift,v,t);
            return Finish("Draped grid cells",v,t);
        }
        private static void AddCell(SquareBuildGrid grid,Vector2Int cell,Vector3 origin,float lift,List<Vector3> v,List<int> t)
        {
            int subdivisions=grid.Terrain!=null?GridHeightField.Subdivisions:1;
            for(int z=0;z<subdivisions;z++)for(int x=0;x<subdivisions;x++)
            {
                float u=cell.x+(float)x/subdivisions,w=cell.y+(float)z/subdivisions,s=1f/subdivisions;
                Vector3 Point(float a,float b){var p=grid.Origin+new Vector3(a*grid.CellSize,0,b*grid.CellSize);p.y=grid.GroundHeight(p)+lift;return p-origin;}
                AddQuad(v,t,Point(u,w),Point(u,w+s),Point(u+s,w+s),Point(u+s,w));
            }
        }
        public static void AddQuad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {int i=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
        public static Mesh Finish(string name,List<Vector3> v,List<int> t)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);
            var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x,p.z));mesh.SetUVs(0,uv);
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static void Beam(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,float width,float depth)
        {
            Vector3 direction=(b-a).normalized;
            Vector3 side=Vector3.Cross(direction,Vector3.up).normalized;if(side.sqrMagnitude<.001f)side=Vector3.right;
            Vector3 up=Vector3.Cross(side,direction).normalized;side*=width/2;up*=depth/2;
            var p=new[]{a-side-up,a-side+up,a+side+up,a+side-up,b-side-up,b-side+up,b+side+up,b+side-up};
            // This basis is right handed; faces must wind away from the beam's centre.
            AddQuad(v,t,p[3],p[2],p[1],p[0]);AddQuad(v,t,p[4],p[5],p[6],p[7]);
            AddQuad(v,t,p[1],p[5],p[4],p[0]);AddQuad(v,t,p[2],p[6],p[5],p[1]);
            AddQuad(v,t,p[3],p[7],p[6],p[2]);AddQuad(v,t,p[0],p[4],p[7],p[3]);
        }
    }
    /// <summary>Live, chunked ground colliders. Layer 2 excludes ground from obstacle queries, not from ground picking.</summary>
    public sealed class ParkTerrainSurface : System.IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly GameObject root;
        private readonly List<RuntimeGroundMesh> chunks=new List<RuntimeGroundMesh>();
        private readonly bool separateSkirts;
        public GameObject Root=>root;
        public ParkTerrainSurface(SquareBuildGrid grid,Material material,Material cliff=null)
        {
            this.grid=grid;root=new GameObject("Park terrain");
            separateSkirts=cliff!=null;
            for(int z=0;z<grid.Depth;z+=16)for(int x=0;x<grid.Width;x+=16)
            {
                var go=new GameObject($"Terrain [{x}, {z}]",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider),typeof(RuntimeGroundMesh)){layer=2};
                go.transform.SetParent(root.transform,false);go.GetComponent<MeshRenderer>().sharedMaterials=cliff!=null?new[]{material,cliff}:new[]{material};
                var chunk=go.GetComponent<RuntimeGroundMesh>();chunks.Add(chunk);
                chunk.Set(GroundMeshBuilder.Surface(grid,x,z,Mathf.Min(16,grid.Width-x),Mathf.Min(16,grid.Depth-z),Vector3.zero,skirts:true,separateSkirts:separateSkirts));
            }
            grid.Terrain.Changed+=Rebuild;
        }
        private void Rebuild()
        {
            int index=0;
            for(int z=0;z<grid.Depth;z+=16)for(int x=0;x<grid.Width;x+=16)
                chunks[index++].Set(GroundMeshBuilder.Surface(grid,x,z,Mathf.Min(16,grid.Width-x),Mathf.Min(16,grid.Depth-z),Vector3.zero,skirts:true,separateSkirts:separateSkirts));
        }
        public bool Raycast(Ray ray,out RaycastHit hit,float distance)
        {
            hit=default;bool found=false;
            foreach(var chunk in chunks)if(chunk.GetComponent<MeshCollider>().Raycast(ray,out var candidate,distance)&&(!found||candidate.distance<hit.distance))
            {hit=candidate;found=true;}
            return found;
        }
        public void Dispose(){grid.Terrain.Changed-=Rebuild;if(root!=null)Object.Destroy(root);}
    }
}

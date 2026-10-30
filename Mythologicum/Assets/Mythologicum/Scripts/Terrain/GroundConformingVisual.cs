using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    /// <summary>Generated construction meshes share the ground sampler used by navigation and creature feet.</summary>
    public sealed class GroundConformingVisual : MonoBehaviour
    {
        private SquareBuildGrid grid;
        private bool fence,gate;
        private FenceKind kind;
        private RuntimeGroundMesh owner;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private int lastRevision=-1;
        public void Initialize(SquareBuildGrid grid,Material material,bool fence=false,FenceKind kind=FenceKind.Wooden,bool gate=false)
        {
            this.grid=grid;this.fence=fence;this.kind=kind;this.gate=gate;
            gameObject.AddComponent<MeshFilter>();gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;owner=gameObject.AddComponent<RuntimeGroundMesh>();Refresh();
        }
        public void Refresh()
        {
            if(lastRevision==grid.Terrain.Revision&&lastPosition==transform.position&&lastRotation==transform.rotation)return;
            lastPosition=transform.position;lastRotation=transform.rotation;lastRevision=grid.Terrain.Revision;
            var v=new List<Vector3>();var t=new List<int>();
            Vector3 Ground(float x,float z,float lift=0)
            {var point=transform.TransformPoint(new Vector3(x,0,z));point.y=grid.GroundHeight(point)+lift;return transform.InverseTransformPoint(point);}
            if(!fence)
            {
                int n=GridHeightField.Subdivisions;float half=grid.CellSize/2,s=grid.CellSize/n;
                for(int z=0;z<n;z++)for(int x=0;x<n;x++)
                {
                    float a=-half+x*s,b=-half+z*s;
                    GroundMeshBuilder.AddQuad(v,t,Ground(a,b,PathConstruction.Height),Ground(a,b+s,PathConstruction.Height),Ground(a+s,b+s,PathConstruction.Height),Ground(a+s,b,PathConstruction.Height));
                }
            }
            else
            {
                float height=FenceConstruction.HeightFor(kind),half=grid.CellSize/2,post=kind==FenceKind.Heavy?.24f:.14f;
                foreach(float x in new[]{-half+post/2,half-post/2})
                    GroundMeshBuilder.Beam(v,t,Ground(x,0),Ground(x,0,height),post,post);
                if(kind==FenceKind.Heavy&&!gate)
                {
                    for(int i=0;i<4;i++)
                    {float a=-half+i*grid.CellSize/4,b=a+grid.CellSize/4;GroundMeshBuilder.Beam(v,t,Ground(a,0,height/2),Ground(b,0,height/2),.13f,height*.9f);}
                }
                else
                {
                    foreach(float rail in new[]{.35f,.8f})for(int i=0;i<4;i++)
                    {float a=-half+i*grid.CellSize/4,b=a+grid.CellSize/4;GroundMeshBuilder.Beam(v,t,Ground(a,0,height*rail),Ground(b,0,height*rail),.12f,.12f);}
                }
                if(kind==FenceKind.Spiked)
                    for(int i=0;i<=8;i++)
                    {
                        float x=-half+i*grid.CellSize/8;
                        GroundMeshBuilder.Beam(v,t,Ground(x,0),Ground(x,0,height-.25f),.07f,.07f);
                        Vector3 a=Ground(x,0,height-.25f),tip=Ground(x,0,height+.05f);int start=v.Count;
                        var p=new[]{a+new Vector3(-.09f,0,-.09f),a+new Vector3(-.09f,0,.09f),a+new Vector3(.09f,0,.09f),a+new Vector3(.09f,0,-.09f)};
                        for(int side=0;side<4;side++){v.Add(p[side]);v.Add(p[(side+1)%4]);v.Add(tip);t.Add(start+side*3);t.Add(start+side*3+1);t.Add(start+side*3+2);}
                    }
                if(kind==FenceKind.Sanctified)
                    foreach(float x in new[]{-half+post/2,half-post/2})
                    {GroundMeshBuilder.Beam(v,t,Ground(x,0,height-.25f),Ground(x,0,height+.35f),.1f,.1f);GroundMeshBuilder.Beam(v,t,Ground(x-.3f,0,height+.1f),Ground(x+.3f,0,height+.1f),.1f,.1f);}
                if(gate)
                {
                    GroundMeshBuilder.Beam(v,t,Ground(-half+.2f,0,.25f),Ground(half-.2f,0,height*.85f),.13f,.13f);
                    GroundMeshBuilder.Beam(v,t,Ground(-half+.2f,0,height*.85f),Ground(half-.2f,0,.25f),.13f,.13f);
                }
            }
            owner.Set(GroundMeshBuilder.Finish(fence?"Terrain-following fence":"Terrain-following path",v,t));
        }
    }
}

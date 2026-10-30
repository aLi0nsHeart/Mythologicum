using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    /// <summary>One-cell removable natural obstacles. Shape and tint depend on map seed and cell.</summary>
    public sealed class RockConstruction : IDisposable
    {
        private readonly SquareBuildGrid grid;
        private readonly Transform parent;
        private readonly Material material;
        private readonly int seed;
        private readonly Predicate<Vector2Int> occupied;
        private readonly Dictionary<Vector2Int,GameObject> rocks=new Dictionary<Vector2Int,GameObject>();
        private bool disposed;
        public event Action Changed;
        public RockConstruction(SquareBuildGrid grid,Transform parent,Material material,int seed=0,Predicate<Vector2Int> occupied=null)
        {this.grid=grid;this.parent=parent;this.material=material;this.seed=seed;this.occupied=occupied;}
        public bool Occupies(Vector2Int cell)=>rocks.ContainsKey(cell);
        public bool CanPlace(Vector2Int cell)
        {
            if(disposed||!grid.IsAvailable(cell)||Occupies(cell)||(occupied?.Invoke(cell)??false))return false;
            return !Physics.CheckBox(grid.CellCenter(cell)+Vector3.up*.65f,
                new Vector3(grid.CellSize*.38f,.64f,grid.CellSize*.38f),Quaternion.identity,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        public bool TryPlace(Vector2Int cell)
        {
            Physics.SyncTransforms();if(!CanPlace(cell))return false;
            var root=new GameObject($"Rock [{cell.x}, {cell.y}]",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider),typeof(RuntimeGroundMesh));
            root.transform.SetParent(parent,false);root.transform.position=grid.CellCenter(cell);
            root.GetComponent<MeshRenderer>().sharedMaterial=material;
            root.GetComponent<MeshCollider>().convex=true;
            var block=new MaterialPropertyBlock();
            float tint=NatureGeneration.Hash(cell.x,cell.y,seed+29);
            var color=Color.Lerp(new Color(.35f,.37f,.34f),new Color(.58f,.53f,.45f),tint);
            block.SetColor("_BaseColor",color);block.SetColor("_Color",color);root.GetComponent<MeshRenderer>().SetPropertyBlock(block);
            rocks.Add(cell,root);Rebuild(cell,root);
            root.AddComponent<DemolitionTarget>().Initialize(()=>{rocks.Remove(cell);Changed?.Invoke();});
            Changed?.Invoke();return true;
        }
        private void Rebuild(Vector2Int cell,GameObject root)
        {
            root.transform.position=grid.CellCenter(cell);
            float height=.65f+NatureGeneration.Hash(cell.x,cell.y,seed+137)*.85f;
            float radius=grid.CellSize*(.28f+.09f*NatureGeneration.Hash(cell.x,cell.y,seed+149));
            float yaw=NatureGeneration.Hash(cell.x,cell.y,seed+163)*(float)(Math.PI*2);
            var vertices=new List<Vector3>();var triangles=new List<int>();
            var bottom=new Vector3[6];var middle=new Vector3[6];
            for(int i=0;i<6;i++)
            {
                float angle=yaw+i*(float)(Math.PI/3);
                float r=radius*(.82f+.18f*NatureGeneration.Hash(cell.x+i,cell.y,seed+181));
                var p=new Vector3((float)Math.Cos(angle)*r,0,(float)Math.Sin(angle)*r);
                float floor=grid.GroundHeight(root.transform.position+p)-root.transform.position.y;
                bottom[i]=new Vector3(p.x*.8f,floor-.08f,p.z*.8f);
                middle[i]=new Vector3(p.x,floor+height*(.32f+.13f*NatureGeneration.Hash(cell.x+i,cell.y,seed+193)),p.z);
            }
            var top=new Vector3(radius*.12f,height,-radius*.13f);var basePoint=new Vector3(0,-.08f,0);
            var centre=new Vector3(0,height*.4f,0);
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-centre)<0){var temporary=b;b=c;c=temporary;}
                int index=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(index);triangles.Add(index+1);triangles.Add(index+2);
            }
            for(int i=0;i<6;i++)
            {
                int next=(i+1)%6;
                Triangle(basePoint,bottom[next],bottom[i]);
                Triangle(bottom[i],bottom[next],middle[next]);Triangle(bottom[i],middle[next],middle[i]);
                Triangle(middle[i],middle[next],top);
            }
            root.GetComponent<RuntimeGroundMesh>().Set(GroundMeshBuilder.Finish("Natural rock",vertices,triangles));
        }
        public void RefreshTerrain(){foreach(var pair in rocks)Rebuild(pair.Key,pair.Value);}
        public List<Vector2Int> Capture()=>new List<Vector2Int>(rocks.Keys);
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            foreach(var root in rocks.Values)
            {
                if(root==null)continue;foreach(var collider in root.GetComponentsInChildren<Collider>())collider.enabled=false;
                UnityEngine.Object.Destroy(root);
            }
            bool changed=rocks.Count>0;rocks.Clear();if(changed)Changed?.Invoke();
        }
    }
}

using UnityEngine;
namespace Mythologicum
{
    /// <summary>Owns generated mesh lifetime, including meshes attached to disabled placement ghosts.</summary>
    public sealed class RuntimeGroundMesh : MonoBehaviour
    {
        private Mesh mesh;
        public void Set(Mesh value)
        {
            if(mesh!=null)Destroy(mesh);mesh=value;GetComponent<MeshFilter>().sharedMesh=value;
            var collider=GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=value;}
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}

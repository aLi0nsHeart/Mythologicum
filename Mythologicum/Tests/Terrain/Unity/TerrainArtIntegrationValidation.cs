using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class TerrainArtIntegrationValidation
{
    const string Running="TerrainArtValidation.running";
    static bool pending;static int stage,checks;static ParkSaveData expected;static Vector2Int cleared;
    static string Result=>Path.Combine(Directory.GetCurrentDirectory(),"TerrainArtValidationResults.txt");
    static TerrainArtIntegrationValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Running,false)){SessionState.SetBool(Running,false);pending=true;}};
        EditorApplication.update+=()=>{
            if(!Application.isPlaying)return;
            try
            {
                var c=UnityEngine.Object.FindFirstObjectByType<BuildModeController>();if(c?.Progression==null)return;
                if(stage>0){Restored(c);return;}if(pending){pending=false;Run(c);}
            }
            catch(Exception error){File.WriteAllText(Result,"FAIL: "+error);Debug.LogException(error);EditorApplication.Exit(1);}
        };
    }
    public static void Start()
    {
        PlayerSettings.companyName="MythologicumValidation";PlayerSettings.productName="TerrainArtValidation";
        SessionState.SetBool(Running,true);EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);Debug.Log("TERRAIN ART PASS: "+text);}
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static bool Near(float a,float b)=>Math.Abs(a-b)<.01f;
    static GameObject Fence(FenceEdge edge)
    {foreach(var f in UnityEngine.Object.FindObjectsByType<FenceSegment>(FindObjectsSortMode.None))if(f.Edge.Equals(edge)&&f.gameObject.activeSelf)return f.gameObject;throw new Exception("Missing fence");}
    static void Run(BuildModeController c)
    {
        c.enabled=false;var art=Resources.Load<TerrainArtDefinition>("Terrain/TerrainArt");var vegetation=Resources.Load<VegetationDefinition>("Environment/Vegetation");
        Check(art!=null&&art.grass!=null&&art.cliff!=null&&art.borderTrees.Length>0,"Terrain art catalogue contains imported ground and trees");
        foreach(var material in new[]{art.grass,art.cliff,art.borderTrees[0].GetComponentInChildren<Renderer>().sharedMaterial})
            Check(material.shader.name=="Universal Render Pipeline/Lit"&&material.GetTexture("_BaseMap")!=null,"Retained materials use URP and their original textures");
        var treeMaterial=art.borderTrees[0].GetComponentInChildren<Renderer>().sharedMaterial;
        Check(treeMaterial.GetFloat("_AlphaClip")==1&&treeMaterial.GetFloat("_Cull")==0,"Pine foliage uses alpha clipping and is visible from both sides");
        Check(vegetation.trees.Contains(art.borderTrees[0]),"Imported pine is included in the normal tree placer catalogue");
        var surface=Field<ParkTerrainSurface>(c,"terrainSurface");bool textured=true,skirt=false;
        foreach(var renderer in surface.Root.GetComponentsInChildren<MeshRenderer>())
        {
            textured&=renderer.sharedMaterials.Length==2&&renderer.sharedMaterials[0]==art.grass&&renderer.sharedMaterials[1]==art.cliff;
            var terrainMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            if(terrainMesh.GetTriangles(1).Length>0)skirt=true;
        }
        Check(textured&&skirt,"Actual terrain uses grass on the surface and a separate cliff texture on its skirt");
        var data=c.CapturePark();var grid=Field<SquareBuildGrid>(c,"grid");
        var candidates=ForestBorderLayout.Cells(grid.Width,grid.Depth,art.forest.widthInCells,art.forest.density,art.forest.seed^c.Terrain.Seed);
        Check(data.trees.Count==candidates.Count&&data.trees.Count>400,"New park starts with a dense deterministic forest border");
        Check(data.trees.All(cell=>cell.x<3||cell.y<3||cell.x>=grid.Width-3||cell.y>=grid.Depth-3),"Border forest keeps the central construction area clear");
        Check(data.treeVisuals.Count==data.trees.Count&&data.treeVisuals.All(v=>v.prefab==art.borderTrees[0].name),"Every border pine has saved model and appearance data");
        Check(c.Environment.TreeCount(data.trees)==data.trees.Count,"Border trees contribute to creature habitat");
        var treeRoot=GameObject.Find("Enrichment trees");
        foreach(Transform tree in treeRoot.transform)
        {
            var view=tree.GetComponentInChildren<Renderer>();var cell=grid.WorldToCell(tree.position);
            Check(Near(view.bounds.min.y,grid.CellCenter(cell).y),"Pine mesh sits on the ground");
            if(checks>20)break;
        }
        var fences=Field<FenceConstruction>(c,"fences");var edge=new FenceEdge(new Vector2Int(20,20),false);
        Check(fences.TryPlace(edge,FenceKind.Spiked),"Spiked fence placed for protected gate test");
        int spikedVertices=Fence(edge).GetComponentInChildren<GroundConformingVisual>().GetComponent<MeshFilter>().sharedMesh.vertexCount;
        fences.Layout.TryDamage(edge,30);Check(fences.CanPlaceGate(edge)&&fences.TryPlaceGate(edge),"Gate tool accepts a damaged spiked fence");
        var root=Fence(edge);var meshGate=root.GetComponentInChildren<GroundConformingVisual>().GetComponent<MeshFilter>().sharedMesh;
        Check(fences.Layout.KindAt(edge)==FenceKind.Spiked&&fences.Layout.HealthAt(edge)==190&&!fences.Layout.CanClimb(edge),"Gate retains spike protection, current health and closed creature barrier");
        Check(meshGate.vertexCount>=spikedVertices,"Spiked gate keeps its visible spikes");
        Check(new StaffNavigation(grid,fences.Layout).CanCross(edge.CellA,edge.CellB),"Staff can enter through the spiked gate");
        var collider=root.transform.Find("Closed barrier").GetComponent<MeshCollider>();var mesh=collider.sharedMesh;var vertices=mesh.vertices;var indices=mesh.triangles;var centre=mesh.bounds.center;
        for(int i=0;i<indices.Length;i+=3)
        {
            var a=vertices[indices[i]];var b=vertices[indices[i+1]];var d=vertices[indices[i+2]];
            Check(Vector3.Dot(Vector3.Cross(b-a,d-a),(a+b+d)/3-centre)>0,"Closed fence collider triangle is outward-facing");
        }
        bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=false;
        try
        {
            Physics.SyncTransforms();var point=collider.bounds.center;
            Check(collider.Raycast(new Ray(point+root.transform.forward*3,-root.transform.forward),out _,8),"Fence barrier can be hit from its front without backface queries");
            Check(collider.Raycast(new Ray(point-root.transform.forward*3,root.transform.forward),out _,8),"Fence barrier can be hit from its back without backface queries");
        }
        finally{Physics.queriesHitBackfaces=backfaces;}
        var first=data.treeVisuals[0];cleared=first.cell;
        var target=treeRoot.GetComponentsInChildren<DemolitionTarget>().First(t=>grid.WorldToCell(t.transform.position)==cleared);
        Check(target.Demolish()&&c.Environment.TreesAt(cleared)==0,"Player can clear a border tree with the normal demolition tool");
        expected=c.CapturePark();c.ValidateSave(expected);
        Check(expected.trees.Count==data.trees.Count-1&&expected.treeVisuals.Count==expected.trees.Count,"Clearing removes the saved tree and its appearance");
        ParkSaveStore.Pending=expected;stage=1;SceneManager.LoadScene("GameScene");
    }
    static void Restored(BuildModeController c)
    {
        var current=c.CapturePark();
        Check(current.trees.Count==expected.trees.Count&&c.Environment.TreesAt(cleared)==0,"Loading does not regrow cleared forest or duplicate seeded trees");
        bool same=true;
        foreach(var item in expected.treeVisuals)
        {
            var after=current.treeVisuals.Find(v=>v.cell==item.cell);
            if(after==null||after.prefab!=item.prefab||!Near(after.height,item.height)||!Near(after.yaw,item.yaw))same=false;
        }
        Check(same,"Forest tree species, height and rotation survive save/load");
        var fences=Field<FenceConstruction>(c,"fences");var edge=new FenceEdge(new Vector2Int(20,20),false);
        Check(fences.Layout.IsGate(edge)&&fences.Layout.KindAt(edge)==FenceKind.Spiked&&fences.Layout.HealthAt(edge)==190,"Damaged spiked gate survives save/load");
        Check(!Directory.Exists("Assets/SquareTileTerrain"),"Original imported project folder has been removed");
        File.WriteAllText(Result,"PASS: "+checks+" terrain art, fence normals, spiked gates and forest Play mode checks.");stage=0;EditorApplication.Exit(0);
    }
}

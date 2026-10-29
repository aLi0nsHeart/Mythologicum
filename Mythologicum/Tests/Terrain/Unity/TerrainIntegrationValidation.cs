using System;
using System.IO;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class TerrainIntegrationValidation
{
    const string Running="TerrainValidation.running";
    static bool pending;
    static int checks,stage;
    static ParkSaveData expected;
    static string Result=>Path.Combine(Directory.GetCurrentDirectory(),"TerrainValidationResults.txt");
    static TerrainIntegrationValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Running,false)){SessionState.SetBool(Running,false);pending=true;}};
        EditorApplication.update+=()=>{
            if(!Application.isPlaying)return;
            try
            {
                var c=UnityEngine.Object.FindFirstObjectByType<BuildModeController>();if(c?.Terrain==null||c.Personnel==null)return;
                if(stage>0){Continue(c);return;}if(pending){pending=false;Run(c);}
            }
            catch(Exception error){File.WriteAllText(Result,"FAIL: "+error);Debug.LogException(error);EditorApplication.Exit(1);}
        };
    }
    public static void Start()
    {
        PlayerSettings.companyName="MythologicumValidation";PlayerSettings.productName="TerrainValidation";
        var path="Assets/Mythologicum/Resources/Terrain/DefaultTerrain.asset";
        if(AssetDatabase.LoadAssetAtPath<TerrainDefinition>(path)==null){AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TerrainDefinition>(),path);AssetDatabase.SaveAssets();}
        SessionState.SetBool(Running,true);EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string text){checks++;if(!value)throw new Exception(text);Debug.Log("TERRAIN PASS: "+text);}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.006f;
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static void Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).GetSetMethod(true).Invoke(o,new[]{value});
    static Vector2Int V(int x,int z)=>new Vector2Int(x,z);
    static void CheckPath(SquareBuildGrid grid,GameObject path)
    {
        var filter=path.GetComponentInChildren<MeshFilter>();
        foreach(var local in filter.sharedMesh.vertices)
        {var p=filter.transform.TransformPoint(local);if(!Near(p.y,grid.GroundHeight(p)+PathConstruction.Height))throw new Exception("Path vertex floats or sinks at "+p);}
        Check(true,"Every generated path vertex follows the actual ground sampler");
    }
    static void CheckFeet(SquareBuildGrid grid,CreatureAgent actor)
    {if(!Near(actor.transform.position.y,grid.ProjectToGround(actor.transform.position,actor.Definition.bodyScale.y).y))throw new Exception("Actor feet left the terrain: "+actor.name);}
    static void Run(BuildModeController c)
    {
        c.enabled=false;var grid=Field<SquareBuildGrid>(c,"grid");var surface=Field<ParkTerrainSurface>(c,"terrainSurface");
        var building=Field<BuildingConstruction>(c,"buildingConstruction");var paths=Field<PathConstruction>(c,"paths");var fences=Field<FenceConstruction>(c,"fences");
        var world=Field<CreatureWorld>(c,"creatureWorld");var trees=Field<TreeConstruction>(c,"trees");var graves=Field<GravestoneConstruction>(c,"graves");
        var heights=c.Terrain.Capture().heights;float low=float.MaxValue,high=float.MinValue;foreach(float h in heights){low=Math.Min(low,h);high=Math.Max(high,h);}
        Check(high-low>1,"New parks generate a non-flat landscape");
        Check(!Field<Collider>(c,"ground").enabled&&!Field<Collider>(c,"ground").GetComponent<Renderer>().enabled,"Original scene ground is hidden at runtime without overwriting the scene");
        for(int i=0;i<8;i++)
        {
            var p=grid.CellCenter(V(2+i*5,3+i*4));p.x+=.37f;p.z-=.23f;
            Check(surface.Raycast(new Ray(p+Vector3.up*30,Vector3.down),out var hit,100)&&Near(hit.point.y,grid.GroundHeight(p)),"Terrain collider and height sampler agree between grid points");
        }
        var entry=new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.ParkEntrance,displayName="Entrance",width=2,depth=2,height=2},V(3,6),0);
        var before=c.Terrain.Capture();var proposed=building.PreviewPosition(entry);
        Check(building.CanPlace(entry),"Building preview accepts a gentle slope");
        Check(Near(before.heights[3+6*(grid.Width+1)],c.Terrain.Node(3,6)),"Preview leaves terrain untouched");
        Check(building.TryPlace(entry),"Place entrance on generated hills");
        foreach(var cell in entry.Cells())Check(Near(grid.CellCenter(cell).y,proposed.y),"Entire multi-tile foundation is level at preview elevation");
        Check(Near(grid.CellCenter(entry.AccessCell).y,proposed.y),"Entrance connection tile is flattened too");
        foreach(var selected in UnityEngine.Object.FindObjectsByType<BuildingSelection>(FindObjectsSortMode.None))
            if(selected.Record==building.Network.Entrance)Check(Near(selected.transform.position.y,proposed.y),"Placed building model uses the level foundation elevation");
        Check(paths.TryPlace(V(11,14)),"Place slope-following path beside future foundation");
        var pathObject=GameObject.Find("Paths/Path [11, 14]");CheckPath(grid,pathObject);float oldPathY=pathObject.transform.position.y;
        var edge=new FenceEdge(V(11,15),true);Check(fences.TryPlace(edge),"Place fence beside future foundation");
        Check(trees.TryPlace(V(11,15))&&graves.TryPlace(V(16,15)),"Place tree and gravestone near planned terrain changes");
        var large=new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.Kiosk,displayName="Large hill building",width=4,depth=3,height=3},V(12,14),0);
        Check(building.TryPlace(large),"Place larger building and terraform its footprint");Invoke(c,"RefreshTerrainAttachments");
        CheckPath(grid,pathObject);Check(!Near(pathObject.transform.position.y,oldPathY),"Existing path follows neighboring foundation changes");
        Check(Near(GameObject.Find("Enrichment trees/Tree [11, 15]").transform.position.y,grid.CellCenter(V(11,15)).y),"Existing tree follows changed terrain");
        Check(Near(GameObject.Find("Graveyard gravestones/Gravestone [16, 15]").transform.position.y,grid.CellCenter(V(16,15)).y),"Existing gravestone follows changed terrain");
        foreach(FenceKind kind in Enum.GetValues(typeof(FenceKind)))
        {
            var e=new FenceEdge(V(28,10+((int)kind)*2),true);Check(fences.TryPlace(e,kind),"Build terrain-following "+kind+" fence");
            GameObject root=null;foreach(var segment in UnityEngine.Object.FindObjectsByType<FenceSegment>(FindObjectsSortMode.None))if(segment.Edge.Equals(e))root=segment.gameObject;
            Check(root!=null&&root.GetComponentInChildren<GroundConformingVisual>()!=null&&root.transform.Find("Closed barrier").GetComponent<MeshCollider>()!=null,"Visual and solid barrier conform for "+kind);
            if(kind==FenceKind.Heavy)Check(fences.TryPlaceGate(e)&&fences.Layout.IsGate(e)&&fences.Layout.KindAt(e)==kind,"Gate keeps its protection on hills");
        }
        var goat=Resources.Load<CreatureDefinition>("Creatures/Goat");Check(world.TrySpawn(goat,V(22,22)),"Creatures can spawn above terrain colliders");
        var animal=world.Agents[world.Agents.Count-1];var route=Field<System.Collections.Generic.List<Vector2Int>>(animal,"Route");route.Add(V(23,22));route.Add(V(24,22));Set(animal,"Activity",CreatureActivity.Wandering);
        for(int i=0;i<120;i++){Invoke(world,"Move",animal,.05f);CheckFeet(grid,animal);}
        Check(animal.Cell==V(24,22),"Creature movement follows the surface throughout a sloping route");
        for(int x=4;x<=10;x++)Check(paths.TryPlace(V(x,5)),"Build accessible visitor path on hills");
        var hut=new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.ZookeeperHut,displayName="Keepers",width=1,depth=1,height=2},V(9,6),0);
        Check(building.TryPlace(hut),"Staff building flattens ground while preserving the adjoining path");Invoke(c,"RefreshTerrainAttachments");building.Network.Refresh();
        var guest=c.Visitors.TrySpawn();Check(guest!=null,"Visitors spawn at elevated entrance");
        Field<System.Collections.Generic.List<Vector2Int>>(guest.Actor,"Route").Add(V(5,5));Field<System.Collections.Generic.List<Vector2Int>>(guest.Actor,"Route").Add(V(6,5));
        for(int i=0;i<140;i++){Invoke(c.Visitors,"Move",guest,.05f);CheckFeet(grid,guest.Actor);}
        Check(guest.Actor.Cell==V(6,5),"Visitor movement remains grounded on elevated paths");
        var home=building.Network.Records[building.Network.Records.Count-1];Check(c.Personnel.TryHire(home),"Hire staff through hill path connection");
        var worker=c.Personnel.Staff[0];Field<System.Collections.Generic.List<Vector2Int>>(worker.Actor,"Route").Clear();
        Set(worker.Actor,"Cell",V(9,5));worker.Actor.transform.position=grid.ActorPosition(V(9,5),worker.Actor.Definition.bodyScale.y);
        Field<System.Collections.Generic.List<Vector2Int>>(worker.Actor,"Route").Add(V(10,5));
        for(int i=0;i<100;i++){Invoke(c.Personnel,"Move",worker,.05f);CheckFeet(grid,worker.Actor);}
        Check(worker.Actor.Cell==V(10,5),"Employee movement follows the terrain and path surface");
        expected=c.CapturePark();c.ValidateSave(expected);
        Check(expected.version==3&&expected.terrain.heights.Length==(grid.Width+1)*(grid.Depth+1),"Save captures all original and flattened terrain corners");
        ParkSaveStore.Pending=expected;stage=1;SceneManager.LoadScene("GameScene");
    }
    static void Continue(BuildModeController c)
    {
        if(stage==1)
        {
            c.enabled=false;var actual=c.CapturePark();Check(GameSaveMenu.IsOpen,"Terrain save restores into paused park");
            Check(actual.terrain.heights.Length==expected.terrain.heights.Length,"Restored terrain retains dimensions");
            bool same=true;for(int i=0;i<actual.terrain.heights.Length;i++)if(!Near(actual.terrain.heights[i],expected.terrain.heights[i]))same=false;
            Check(same,"Loading preserves every terrain height without re-flattening shoulders");
            Check(actual.buildings.Count==expected.buildings.Count&&actual.paths.Count==expected.paths.Count&&actual.fences.Count==expected.fences.Count,"Sloping park construction survives scene reload");
            Check(actual.creatures.Count==expected.creatures.Count&&actual.staff.Count==1&&actual.visitors.Count==1,"Creature, visitor and staff populations survive terrain save");
            var world=Field<CreatureWorld>(c,"creatureWorld");var grid=Field<SquareBuildGrid>(c,"grid");foreach(var actor in world.Agents)CheckFeet(grid,actor);
            Check(true,"Restored actors stand on their saved terrain surfaces");
            var legacy=c.CapturePark();legacy.version=2;legacy.terrain=null;ParkSaveStore.Pending=legacy;stage=2;SceneManager.LoadScene("GameScene");
        }
        else
        {
            var actual=c.CapturePark();bool flat=true;foreach(float h in actual.terrain.heights)if(Math.Abs(h)>.0001f)flat=false;
            Check(flat,"Version-two parks keep their original flat landscape");
            Check(actual.buildings.Count==expected.buildings.Count&&actual.creatures.Count==expected.creatures.Count,"Legacy flat load preserves park contents");
            File.WriteAllText(Result,"PASS: "+checks+" terrain construction, colliders, movement and save/load Play mode checks.");stage=0;EditorApplication.Exit(0);
        }
    }
}

using System;
using System.IO;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[InitializeOnLoad]
public static class ProgressionIntegrationValidation
{
    const string Running="ProgressionValidation.running";
    static bool pending;
    static int checks,stage;
    static ParkSaveData expected;
    static string result=>Path.Combine(Directory.GetCurrentDirectory(),"ProgressionValidationResults.txt");
    static ProgressionIntegrationValidation()
    {
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Running,false)){SessionState.SetBool(Running,false);pending=true;}};
        EditorApplication.update+=()=>{
            if(!Application.isPlaying)return;
            try
            {
                if(stage>0){Continue();return;}
                var controller=UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
                if(pending&&controller?.Progression!=null){pending=false;Run(controller);}
            }
            catch(Exception error){File.WriteAllText(result,"FAIL: "+error);Debug.LogException(error);EditorApplication.Exit(1);}
        };
    }
    public static void Start()
    {
        PlayerSettings.companyName="MythologicumValidation";PlayerSettings.productName="ProgressionValidation";
        if(AssetDatabase.LoadAssetAtPath<ProgressionDefinition>("Assets/Mythologicum/Resources/Progression/DefaultProgression.asset")==null)
        {
            var settings=ScriptableObject.CreateInstance<ProgressionDefinition>();
            AssetDatabase.CreateAsset(settings,"Assets/Mythologicum/Resources/Progression/DefaultProgression.asset");AssetDatabase.SaveAssets();
        }
        SessionState.SetBool(Running,true);EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");EditorApplication.EnterPlaymode();
    }
    static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);Debug.Log("PROGRESSION PASS: "+why);}
    static T Field<T>(object target,string field)=>(T)target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    static void Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    static Button Button(string name)
    {foreach(var b in UnityEngine.Object.FindFirstObjectByType<Canvas>().GetComponentsInChildren<Button>(true))if(b.gameObject.name==name)return b;throw new Exception("Missing button: "+name);}
    static Vector2Int V(int x,int y)=>new Vector2Int(x,y);
    static ParkBuildingRecord Place(BuildModeController c,BuildingKind kind,int x)
    {
        var builder=Field<BuildingConstruction>(c,"buildingConstruction");
        if(!builder.TryPlace(new BuildingFootprint(new BuildingDefinition{kind=kind,displayName=kind.ToString(),width=1,depth=1,height=2},V(x,4),0)))throw new Exception("Could not place "+kind);
        return builder.Network.Records[builder.Network.Records.Count-1];
    }
    static bool Fits(RectTransform child,RectTransform parent)
    {
        var corners=new Vector3[4];child.GetWorldCorners(corners);var bounds=new Vector3[4];parent.GetWorldCorners(bounds);
        return corners[0].x>=bounds[0].x-.5f&&corners[0].y>=bounds[0].y-.5f&&corners[2].x<=bounds[2].x+.5f&&corners[2].y<=bounds[2].y+.5f;
    }
    static void Run(BuildModeController c)
    {
        foreach(var target in GameObject.Find("Enrichment trees").GetComponentsInChildren<DemolitionTarget>())target.Demolish();
        c.enabled=false;var p=c.Progression;var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
        Check(Resources.Load<ProgressionDefinition>("Progression/DefaultProgression")!=null,"Editable progression settings load from Resources");
        var chupa=Resources.Load<CreatureDefinition>("Creatures/Chupacabra");var oni=Resources.Load<CreatureDefinition>("Creatures/Oni");
        Check(chupa.rating==2&&oni.rating==5,"Species rating is independent of combat threat");
        Check(p.Stock(chupa.name)==0&&!Button("Chupacabra").interactable,"New park has zero creatures and disabled placement");
        Place(c,BuildingKind.ParkEntrance,2);var expedition=Place(c,BuildingKind.ExpeditionCentre,5);var lab=Place(c,BuildingKind.ResearchCentre,8);
        var paths=Field<PathConstruction>(c,"paths");for(int x=2;x<15;x++)paths.TryPlace(V(x,3));
        var network=Field<BuildingConstruction>(c,"buildingConstruction").Network;network.Refresh();
        var palette=Field<ConstructionPalette>(c,"palette");palette.Refresh(true);
        Check(!Button("Spiked fence").interactable&&!Button("Reinforced fence").interactable,"Advanced fence buttons start locked");
        bool kioskLocked=false;foreach(var b in canvas.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Kiosk\n"))kioskLocked=!b.interactable;
        Check(kioskLocked,"Kiosk research gate appears in actual construction UI");
        var management=Field<ProgressionPresentation>(c,"management");management.OpenExpeditions(expedition);Canvas.ForceUpdateCanvases();
        Check(c.ManagementOpen&&Fits((RectTransform)GameObject.Find("Expedition world map/Window").transform,(RectTransform)canvas.transform),"World map stays within the canvas");
        float balance=c.Economy.Balance;Button("Buy team").onClick.Invoke();Check(p.Teams==1&&c.Economy.Balance==balance-250,"Map team purchase charges displayed cost");
        Button("South America").onClick.Invoke();Button("Buy location").onClick.Invoke();Check(p.HasAccess("south-america"),"Clickable world region buys correct access");
        Button("Send expedition").onClick.Invoke();Check(p.Missions.Count==1&&p.AvailableTeams==0,"Map mission button reserves assigned teams");
        p.Missions[0].succeeds=true;p.Tick(p.Missions[0].remaining); // Force outcome; random probability is covered in pure logic checks.
        Check(p.Stock(chupa.name)==2&&p.AvailableTeams==1,"Successful mission adds two creatures to placement stock");
        palette.Refresh(true);Check(Button("Chupacabra").interactable,"Returning creatures enable placement choice");
        management.Close();var notices=Field<NotificationPresentation>(c,"notifications");notices.Refresh();
        Check(Button("Letters").transform.Find("Unread notifications").gameObject.activeSelf,"Letter button displays unread exclamation badge");
        notices.Toggle();Check(p.Inbox.UnreadCount==0&&!Button("Letters").transform.Find("Unread notifications").gameObject.activeSelf,"Opening letters clears existing unread badge");notices.Close();
        var creatures=Field<CreatureWorld>(c,"creatureWorld");Check(p.TryPlace(chupa.name,()=>creatures.TrySpawn(chupa,V(20,20)))&&p.Stock(chupa.name)==1,"Actual creature spawn consumes one acquired unit");
        Check(!p.TryPlace(chupa.name,()=>creatures.TrySpawn(chupa,V(20,20)))&&p.Stock(chupa.name)==1,"Blocked creature placement retains stock");
        var fence=Field<FenceConstruction>(c,"fences");var edge=new FenceEdge(V(25,25),true);
        Check(fence.TryPlace(edge),"Fence placed for event test");fence.Layout.TryDamage(edge,1000);
        Check(p.Inbox.Entries[p.Inbox.Entries.Count-1].kind==ParkNoticeKind.FenceOpened,"Creature damage opening a fence creates a letter");
        fence.TryPlace(edge);fence.Layout.TryDamage(edge,1000);Check(p.Inbox.Entries[p.Inbox.Entries.Count-1].count==2,"Nearby fence-opening events group together");
        c.Personnel.Research.Study(chupa,100);Check(p.Inbox.Entries[p.Inbox.Entries.Count-1].title.Contains("Chupacabra"),"Creature care completion creates separate notification");
        Check(c.Personnel.TryHire(lab),"Hire researcher at connected laboratory");c.Economy.UnlimitedFunds=true;
        var inspection=Field<BuildingInspection>(c,"buildingInspection");inspection.Select(lab);inspection.Refresh();Canvas.ForceUpdateCanvases();
        Check(Fits((RectTransform)GameObject.Find("Building information").transform,(RectTransform)canvas.transform),"Building info panel fits above bottom toolbar");
        Button("Technology research").onClick.Invoke();Check(c.ManagementOpen,"Research centre opens technology window");
        Check(c.Personnel.StartTechnology(lab,"food-service"),"Start technology using hired researcher");
        for(int i=0;i<1000&&!p.Completed("food-service");i++){c.Personnel.Tick(.1f);p.Tick(.1f);}
        Check(p.Completed("food-service"),"Physical researcher reaches laboratory and completes project");
        palette.Refresh(true);bool kioskOpen=false;foreach(var b in canvas.GetComponentsInChildren<Button>(true))if(b.name.StartsWith("Kiosk\n"))kioskOpen=b.interactable;
        Check(kioskOpen,"Finished research unlocks actual kiosk construction button");
        management.Close();inspection.Select(null);Place(c,BuildingKind.Kiosk,11);
        Check(c.Personnel.StartTechnology(lab,"spiked-fences"),"Start another project after completion");
        for(int i=0;i<50;i++){c.Personnel.Tick(.1f);p.Tick(.1f);}float work=p.ActiveProject(lab).Work;
        Field<SquareBuildGrid>(c,"grid").TryRemove(lab.Footprint.AccessCell,BuildCellContent.Path);network.Refresh();
        for(int i=0;i<50;i++)c.Personnel.Tick(.1f);
        Check(p.ActiveProject(lab).Work==work&&!lab.IsOpen,"Disconnected laboratory pauses active work");
        Check(p.Launch(expedition,"south-america","survey",1),"Launch mission to save while away");p.Tick(10);
        expected=c.CapturePark();c.ValidateSave(expected);
        Check(expected.version>=2&&expected.progression.projects.Count==1&&expected.progression.missions.Count==1,"Save contains in-flight mission and partial technology");
        Check(Application.companyName=="MythologicumValidation","Test save identity is isolated from player's park");
        ParkSaveStore.Write(expected);stage=1;SceneManager.LoadScene("MainMenuScene");
    }
    static void Continue()
    {
        if(stage==1&&SceneManager.GetActiveScene().name=="MainMenuScene")
        {
            if(UnityEngine.Object.FindFirstObjectByType<GameSaveMenu>()==null)return;
            stage=2;Button("Load game").onClick.Invoke();
        }
        else if(stage==2)
        {
            var c=UnityEngine.Object.FindFirstObjectByType<BuildModeController>();if(c?.Progression==null)return;
            var p=c.Progression;var current=c.CapturePark();
            Check(GameSaveMenu.IsOpen&&Time.timeScale==0,"Loading progression from main menu stays paused");
            Check(p.Teams==1&&p.AvailableTeams==0&&p.Missions.Count==1,"Reserved expedition team survives main-menu load");
            Check(p.Missions[0].remaining==expected.progression.missions[0].remaining&&p.Missions[0].succeeds==expected.progression.missions[0].succeeds,"No offline progress or rerolled expedition result");
            Check(p.Stock("Chupacabra")==1&&p.Completed("food-service")&&!p.Completed("spiked-fences"),"Acquired stock and permanent unlocks survive load");
            Check(current.progression.projects[0].work==expected.progression.projects[0].work&&!p.Projects[0].Home.IsOpen,"Partial project retains effort and disconnected workplace");
            int unread=0;foreach(var n in expected.progression.notices)if(!n.read)unread++;
            Check(p.Inbox.UnreadCount==unread&&p.Inbox.Entries.Count==expected.progression.notices.Count,"Inbox read state restored without false completion messages");
            var home=p.Projects[0].Home;Field<PathConstruction>(c,"paths").TryPlace(home.Footprint.AccessCell);c.Personnel.Buildings.Refresh();
            for(int i=0;i<1000&&!p.Completed("spiked-fences");i++){c.Personnel.Tick(.1f);p.Tick(.1f);}
            Check(p.Completed("spiked-fences"),"Restored researcher resumes project after reconnecting path");
            var legacy=c.CapturePark();legacy.version=1;legacy.progression=null;ParkSaveStore.Pending=legacy;stage=3;SceneManager.LoadScene("GameScene");
        }
        else if(stage==3)
        {
            var c=UnityEngine.Object.FindFirstObjectByType<BuildModeController>();if(c?.Progression==null)return;
            Check(c.Progression.BuildingUnlocked(BuildingKind.Kiosk),"Version-one park adopts technology for existing buildings");
            Check(c.Progression.Stock("Chupacabra")==0&&Field<CreatureWorld>(c,"creatureWorld").Population>=1,"Legacy migration preserves placed creatures without granting free stock");
            Check(c.Progression.Inbox.Entries.Count==0,"Legacy load does not generate duplicate research letters");
            File.WriteAllText(result,"PASS: "+checks+" progression UI, staff, events and save/load Play mode checks.");stage=0;EditorApplication.Exit(0);
        }
    }
}

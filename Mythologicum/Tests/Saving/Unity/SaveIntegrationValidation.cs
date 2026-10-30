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
public static class SaveIntegrationValidation
{
    const string Running = "SaveValidation.running";
    static int checks;
    static bool pending;
    static int stage;
    static ParkSaveData expected;
    static SaveIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running,false))
            { SessionState.SetBool(Running,false); pending = true; }
        };
        EditorApplication.update += () => {
            if(stage>0 && Application.isPlaying) { Continue(); return; }
            if (pending && Application.isPlaying && UnityEngine.Object.FindFirstObjectByType<BuildModeController>()?.Personnel != null) { pending=false; Run(); }
        };
    }
    public static void Start()
    {
        PlayerSettings.companyName="MythologicumValidation"; PlayerSettings.productName="SaveValidation";
        SessionState.SetBool(Running,true);
        EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/GameScene.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool value,string description) { checks++; if (!value) throw new Exception(description); Debug.Log("SAVE PASS: "+description); }
    static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    static void Invoke(object target,string name,params object[] args) => target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    static void Run()
    {
        string result = Path.Combine(Directory.GetCurrentDirectory(),"SaveValidationResults.txt");
        try
        {
            var controller = UnityEngine.Object.FindFirstObjectByType<BuildModeController>();
            controller.enabled=false;
            // This synthetic save starts empty, independent of new-game forest generation.
            foreach(var target in GameObject.Find("Enrichment trees").GetComponentsInChildren<DemolitionTarget>())target.Demolish();
            var data = controller.CapturePark(); data.buildings.Clear(); data.paths.Clear(); data.creatures.Clear(); data.staff.Clear(); data.visitors.Clear();
            data.version=2;data.terrain=null; // Legacy flat-park compatibility fixture.
            data.balance=1234; data.income=567; data.expenses=89; data.unlimited=false;
            data.buildings.Add(new SavedBuilding {kind=BuildingKind.ParkEntrance,cell=new Vector2Int(2,3),width=2,depth=2,height=3,door=0,price=23,revenue=115,expenses=7});
            data.paths.Add(new Vector2Int(2,2)); data.paths.Add(new Vector2Int(3,2)); data.paths.Add(new Vector2Int(4,2));
            data.buildings.Add(new SavedBuilding {kind=BuildingKind.ZookeeperHut,cell=new Vector2Int(6,6),width=4,depth=2,height=4,door=0,stock=0});
            data.staff.Add(new SavedActor {building=1,cell=new Vector2Int(6,5),health=70,energy=40,hunger=30,happiness=55});
            var species=Resources.LoadAll<CreatureDefinition>("Creatures")[0];
            data.creatures.Add(new SavedActor {species=species.name,cell=new Vector2Int(15,15),hunger=72,fear=18,energy=34,health=species.maximumHealth-5,happiness=45,agitation=.3f,sedation=50,explored=77});
            data.creatures.Add(new SavedActor {species=species.name,cell=new Vector2Int(15,15),hunger=15,energy=90,health=species.maximumHealth,happiness=70});
            data.visitors.Add(new SavedActor {cell=new Vector2Int(2,2),health=100,energy=55,hunger=35,fear=12,age=90,satisfaction=78,patience=.4f,leaving=true});
            data.fences.Add(new SavedFence {cell=new Vector2Int(3,2),alongX=false,kind=FenceKind.Sanctified,gate=true,health=153});
            data.trees.Add(new Vector2Int(24,24)); data.graves.Add(new Vector2Int(26,26));
            data.research.Clear(); data.research.Add(new SavedResearch {species=species.name,progress=63});
            controller.ValidateSave(data);
            Check(true,"Validate mixed park with disconnected staff home and overlapping moving actors");
            var withoutEntrance=JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(data));
            withoutEntrance.buildings.RemoveAt(0); withoutEntrance.staff[0].building=0;
            controller.ValidateSave(withoutEntrance);
            Check(true,"Validate existing park after player demolished its entrance");
            var invalid=JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(data)); invalid.width++;
            bool rejected=false; try {controller.ValidateSave(invalid);} catch(InvalidDataException) {rejected=true;}
            Check(rejected,"Reject incompatible map before loading");
            string path=Path.Combine(Directory.GetCurrentDirectory(),"Temp/test-save.json");
            var flags=BindingFlags.Static|BindingFlags.NonPublic;
            typeof(ParkSaveStore).GetMethod("WriteFile",flags).Invoke(null,new object[]{data,path});
            var loaded=(ParkSaveData)typeof(ParkSaveStore).GetMethod("ReadFile",flags).Invoke(null,new object[]{path});
            Check(loaded.creatures.Count==2 && loaded.buildings.Count==2,"JSON round trip includes population and buildings");
            typeof(ParkSaveStore).GetMethod("WriteFile",flags).Invoke(null,new object[]{data,path});
            Check(File.Exists(path+".bak"),"Atomic overwrite retains previous save");
            Invoke(controller,"RestorePark",loaded);
            var actual=controller.CapturePark();
            Check(actual.buildings.Count==2,"Recreate building models and footprints");
            Check(actual.buildings[0].width==2 && actual.buildings[0].price==23,"Restore original footprint and player price");
            Check(actual.buildings[0].revenue==115 && actual.buildings[0].expenses==7,"Restore individual profits without charging load");
            Check(actual.paths.Count==3 && actual.trees.Count==1 && actual.graves.Count==1,"Restore paths and enrichment");
            Check(actual.fences.Count==1 && actual.fences[0].gate && actual.fences[0].kind==FenceKind.Sanctified && actual.fences[0].health==153,"Restore gate protection and damaged fence health");
            Check(actual.creatures.Count==2,"Restore close creatures without placement collision checks");
            Check(actual.creatures[0].hunger==72 && actual.creatures[0].fear==18 && actual.creatures[0].energy==34,"Restore creature needs");
            Check(actual.creatures[0].sedation==50 && actual.creatures[0].explored==77,"Restore sedation and exploration experience");
            Check(actual.staff.Count==1 && controller.Personnel.Staff[0].Home.HiredCount==1,"Restore employee in disconnected home");
            Check(actual.visitors.Count==1 && actual.visitors[0].age==90 && actual.visitors[0].leaving,"Restore visitors and departure intent");
            Check(actual.visitors[0].satisfaction==78 && actual.visitors[0].patience==.4f,"Restore visitor satisfaction and patience");
            Check(actual.balance==1234 && actual.income==567 && actual.expenses==89,"Loading does not charge hiring or tickets");
            Check(controller.Personnel.Research.Progress(species)==63,"Restore species research");
            Check(GameSaveMenu.IsOpen && Time.timeScale==0,"Successful load remains paused for review");
            Invoke(UnityEngine.Object.FindFirstObjectByType<GameSaveMenu>(),"Toggle");
            Check(!GameSaveMenu.IsOpen && Time.timeScale==1,"Resume restores simulation time");
            foreach(var button in UnityEngine.Object.FindFirstObjectByType<Canvas>().GetComponentsInChildren<Button>(true))
                if(button.GetComponentInChildren<Text>()?.text=="Pause") {button.onClick.Invoke(); break;}
            Check(GameSaveMenu.IsOpen && Time.timeScale==0,"Authored top-right button now opens pause menu");
            Check(Application.companyName=="MythologicumValidation", "Primary-slot integration uses isolated test identity");
            ParkSaveStore.Write(loaded); expected=loaded; stage=1; SceneManager.LoadScene("MainMenuScene");
        }
        catch(Exception error) {File.WriteAllText(result,"FAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1);}
    }
    static void Continue()
    {
        try
        {
            if(stage==1 && SceneManager.GetActiveScene().name=="MainMenuScene")
            {
                var menu=UnityEngine.Object.FindFirstObjectByType<GameSaveMenu>(); if(menu==null) return;
                Button load=null;
                foreach(var button in UnityEngine.Object.FindFirstObjectByType<Canvas>().GetComponentsInChildren<Button>())
                    if(button.name=="Load game") load=button;
                Check(load!=null && load.interactable,"Main menu exposes enabled Load game for saved slot");
                stage=2; load.onClick.Invoke();
            }
            else if(stage==2)
            {
                var controller=UnityEngine.Object.FindFirstObjectByType<BuildModeController>(); if(controller?.Personnel==null) return;
                var actual=controller.CapturePark();
                Check(actual.creatures.Count==expected.creatures.Count && actual.staff.Count==1 && actual.visitors.Count==1,"Main-menu Load restores saved population through scene transition");
                Check(actual.balance==1234 && GameSaveMenu.IsOpen && Time.timeScale==0,"Main-menu Load restores finances and opens paused park");
                Check(ParkSaveStore.Pending==null,"Loaded request consumed exactly once");
                stage=3; SceneManager.LoadScene("MainMenuScene");
            }
            else if(stage==3 && SceneManager.GetActiveScene().name=="MainMenuScene")
            {
                var nav=UnityEngine.Object.FindFirstObjectByType<SceneNavigation>(); if(nav==null) return;
                stage=4; nav.StartGame();
            }
            else if(stage==4)
            {
                var controller=UnityEngine.Object.FindFirstObjectByType<BuildModeController>(); if(controller?.Personnel==null) return;
                Check(controller.CapturePark().buildings.Count==0,"New game starts empty after previously loading");
                Check(!GameSaveMenu.IsOpen && Time.timeScale==1,"Scene transitions reset pause state");
                stage=0;
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"SaveValidationResults.txt"),"PASS: "+checks+" save/load Play mode checks."); EditorApplication.Exit(0);
            }
        }
        catch(Exception error) {stage=0; File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"SaveValidationResults.txt"),"FAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1);}
    }
}

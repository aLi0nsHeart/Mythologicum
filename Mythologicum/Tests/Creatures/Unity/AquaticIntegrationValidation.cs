using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Aquatic habitats, natural bank drinking and capture continuity through production scene loading.</summary>
[InitializeOnLoad]
public static class AquaticIntegrationValidation
{
    private const string Running = "AquaticIntegrationValidation.running";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator steps;
    private static int checks;
    private static double started;
    private static string Result => Path.Combine(Directory.GetCurrentDirectory(),"AquaticIntegrationResults.txt");
    private static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,Hidden).GetValue(owner);
    private static void Set(object owner,string name,object value)
    {
        var field=owner.GetType().GetField(name,Hidden);
        if(field!=null)field.SetValue(owner,value);
        else owner.GetType().GetProperty(name,Hidden|BindingFlags.Public).SetValue(owner,value);
    }
    private static object Call(object owner,string name,params object[] args) => owner.GetType().GetMethod(name,Hidden).Invoke(owner,args);
    private static void Check(bool value,string why) { if (!value) throw new Exception(why); checks++; Debug.Log("AQUATIC PASS: "+why); }
    static AquaticIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Running,false)) return;
            SessionState.SetBool(Running,false); started=EditorApplication.timeSinceStartup; checks=0; steps=Run();
        };
        EditorApplication.update += Tick;
    }
    public static void Start()
    {
        PlayerSettings.companyName="MythologicumValidation"; PlayerSettings.productName="AquaticIntegrationValidation";
        if (File.Exists(Result)) File.Delete(Result);
        SessionState.SetBool(Running,true); EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Tick()
    {
        if (steps==null || !Application.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup-started>240 || SceneTransition.HasFailed) throw new Exception("Aquatic scene test timed out or failed preparation.");
            if (steps.MoveNext()) return;
            steps=null; File.WriteAllText(Result,$"PASS: {checks} aquatic navigation, drinking, shelters, capture and production save/load checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { steps=null; File.WriteAllText(Result,"FAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static IEnumerator Run()
    {
        while (Object.FindFirstObjectByType<MainMenuPresentation>()==null) yield return null;
        Object.FindFirstObjectByType<SceneNavigation>().StartConfiguredGame(new MapGenerationOptions
            {width=50,depth=50,seed=285117,relief=0,forestDensity=0,featureSize=28});
        while (SceneTransition.IsLoading || Object.FindFirstObjectByType<BuildModeController>()?.IsReady!=true) yield return null;
        var park=Object.FindFirstObjectByType<BuildModeController>(); park.enabled=false; Time.timeScale=0;
        park.Economy.UnlimitedFunds=true; park.Visitors.Definition.maximumVisitors=0;
        checks+=AquaticDataNativeChecks.RunChecks();
        checks+=AquaticHuntingNativeChecks.RunChecks();
        checks+=AquaticNavigationNativeChecks.RunChecks();
        foreach (var name in new[]{"Enrichment trees","Natural rocks"})
            foreach (var target in GameObject.Find(name).GetComponentsInChildren<DemolitionTarget>()) target.Demolish();
        GameObject.Find("Prototype Landmarks")?.SetActive(false); Physics.SyncTransforms();
        var world=Field<CreatureWorld>(park,"creatureWorld"); var grid=Field<SquareBuildGrid>(park,"grid");
        var buildings=Field<BuildingConstruction>(park,"buildingConstruction");
        var entrance=Field<BuildingDefinition[]>(park,"buildingCatalog").Single(item=>item.kind==BuildingKind.ParkEntrance).Copy();
        entrance.width=entrance.depth=1; entrance.entranceColumn=0;
        Check(buildings.TryPlace(new BuildingFootprint(entrance,new Vector2Int(2,2),0)),"The fixture has a normal park entrance.");
        Field<PathConstruction>(park,"paths").TryPlace(new Vector2Int(2,1)); buildings.Network.Refresh();
        Check(grid.Habitat.AddWater(new Vector2(20.5f,20.5f),4,grid.Origin.y,2,null,out _),"Terraforming creates a real pond.");
        Call(park,"RefreshTerrainAttachments");
        var kappa=Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Kappa")); kappa.name="Kappa";
        var nokk=Resources.Load<CreatureDefinition>("Creatures/Nokk");
        Check(kappa!=null && nokk!=null && kappa.origin=="creature.origin.yokai" && nokk.origin=="creature.origin.nordic",
            "The two new species retain their confirmed origins.");
        kappa.initialHunger=0; kappa.initialEnergy=0; kappa.energyDrainPerSecond=0; kappa.sleepRecoveryPerSecond=0;
        kappa.randomEscapeChance=kappa.hungryEscapeChance=kappa.agitatedEscapeChance=kappa.crowdedEscapeChance=0;
        var wet=grid.Habitat.WaterCells.First(cell=>kappa.Aquatic.CanOccupy(grid,cell));
        Check(world.CanSpawn(kappa,wet) && !world.CanSpawn(wet),"Only a suitable swimming profile can be placed in the pond.");
        Check(world.TrySpawn(kappa,wet),"The aquatic construction path spawns a swimmer at surface level.");
        var hunter=world.Agents.Last();
        for(int i=0;i<300 && hunter.Activity!=CreatureActivity.Sleeping;i++) world.Tick(.05f);
        Check(hunter.Activity==CreatureActivity.Sleeping && grid.Habitat.IsWater(hunter.Cell),"A tired aquatic creature sleeps in its water shelter.");
        Check(Mathf.Abs(hunter.transform.position.y-(grid.Habitat.WaterLevel(hunter.Cell)+kappa.bodyScale.y+kappa.Aquatic.restingBodyOffset))<.01f,
            "Its pose follows the surface rather than the pond bed.");
        var asleep=park.CapturePark(); park.ValidateSave(asleep);
        Check(asleep.creatures.Single().shelterSleeping,"Water shelter sleep is captured by the normal save system.");
        var goatProfile=Object.Instantiate(Resources.Load<CreatureDefinition>("Creatures/Goat")); goatProfile.name="Goat";
        goatProfile.initialHunger=0; goatProfile.hungerPerSecond=0; goatProfile.fearRange=.1f;
        goatProfile.Thirst.initialThirst=90; goatProfile.Thirst.thirstPerSecond=0;
        goatProfile.energyDrainPerSecond=0; goatProfile.initialEnergy=100;
        goatProfile.randomEscapeChance=goatProfile.hungryEscapeChance=goatProfile.agitatedEscapeChance=goatProfile.crowdedEscapeChance=0;
        var bank=new Vector2Int(14,20);
        Check(world.TrySpawn(goatProfile,bank),"A thirsty land animal starts outside the pond.");
        var goat=world.Agents.Last();
        for(int i=0;i<700 && goat.Activity!=CreatureActivity.Drinking;i++) world.Tick(.05f);
        Check(goat.Activity==CreatureActivity.Drinking && !grid.Habitat.IsWater(goat.Cell),"Thirst naturally brings prey to a reachable dry bank.");
        Set(goat,"DecisionTimer",10000f);
        Set(hunter,"Energy",new CreatureEnergy(100)); kappa.sleepRecoveryPerSecond=8;
        hunter.Vitals.Restore(100,0,0);
        Check(world.CanApproach(hunter,goat.Cell),"The carved pond has a usable shallow-water connection to the prey's bank.");
        for(int i=0;i<1000 && hunter.Activity!=CreatureActivity.DrowningPrey;i++) world.Tick(.05f);
        Check(hunter.Activity==CreatureActivity.DrowningPrey && goat.Alive,
            $"The predator captures live prey and brings it into water before the kill. Hunter={hunter.Activity} at {hunter.Cell}, H={hunter.Vitals.Hunger}, E={hunter.Energy.Energy}; prey={goat.Activity} at {goat.Cell}, alive={goat.Alive}");
        var saved=park.CapturePark(); park.ValidateSave(saved);
        var savedHunter=saved.creatures.Single(item=>item.species=="Kappa"); var savedPrey=saved.creatures.Single(item=>item.species=="Goat");
        Check(savedHunter.aquaticHunt!=null && savedPrey.capturedByAquatic && savedPrey.captureHunter==saved.creatures.IndexOf(savedHunter),
            "An underwater captive records its actual hunter relationship.");
        var invalid=JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        invalid.creatures.Single(item=>item.species=="Goat").captureHunter=999;
        bool rejected=false; try{park.ValidateSave(invalid);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Corrupt capture references are rejected before restoring actors.");
        // Unity JSON exercises the same optional fields used by disk saves; no user save is written.
        ParkSaveStore.Pending=JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        Check(SceneTransition.Load("GameScene"),"The mid-capture snapshot loads through production scene preparation.");
        while(SceneTransition.IsLoading)yield return null;
        park=Object.FindFirstObjectByType<BuildModeController>(); park.enabled=false;Time.timeScale=0;
        world=Field<CreatureWorld>(park,"creatureWorld");
        hunter=world.Agents.Single(item=>item.Definition.name=="Kappa");goat=world.Agents.Single(item=>item.Definition.name=="Goat");
        Check(hunter.Activity==CreatureActivity.DrowningPrey && goat.Alive,"Scene loading resumes the live water capture.");
        var again=park.CapturePark();park.ValidateSave(again);
        Check(again.creatures.Single(item=>item.species=="Goat").capturedByAquatic,"The restored captive remains attached in subsequent saves.");
        for(int i=0;i<700 && hunter.Vitals.Hunger>=90;i++)world.Tick(.05f);
        Check(!goat.Alive && hunter.Vitals.Hunger<90,"Only the completed water kill and feeding relieve hunger after load.");
        grid=Field<SquareBuildGrid>(park,"grid");
        var guest=(VisitorAgent)Call(park.Visitors,"RestoreGuestWithType",bank,VisitorType.Adventurers);
        Check(guest!=null,"A real visitor actor is registered through the visitor world.");
        hunter.Vitals.Restore(100,0,0);Set(hunter,"Energy",new CreatureEnergy(100));
        for(int i=0;i<1000 && hunter.Activity!=CreatureActivity.DrowningPrey;i++)world.Tick(.05f);
        Check(hunter.Activity==CreatureActivity.DrowningPrey && guest.Actor.Alive,"Aquatic hunting also captures a live human visitor.");
        saved=park.CapturePark();park.ValidateSave(saved);
        Check(saved.visitors.Single().capturedByAquatic && grid.Habitat.IsWater(saved.visitors.Single().cell),
            "A captive visitor in water has an explicit validated hunter link.");
        ParkSaveStore.Pending=JsonUtility.FromJson<ParkSaveData>(JsonUtility.ToJson(saved));
        Check(SceneTransition.Load("GameScene"),"A visitor capture survives a second production scene transition.");
        while(SceneTransition.IsLoading)yield return null;
        park=Object.FindFirstObjectByType<BuildModeController>();park.enabled=false;Time.timeScale=0;
        world=Field<CreatureWorld>(park,"creatureWorld");grid=Field<SquareBuildGrid>(park,"grid");
        guest=park.Visitors.Visitors.Single();hunter=world.Agents.Single(item=>item.Definition.name=="Kappa");
        Check(guest.Actor.Alive && hunter.Activity==CreatureActivity.DrowningPrey
            && Mathf.Abs(guest.Actor.transform.position.y-(grid.Habitat.WaterLevel(guest.Actor.Cell)+guest.Actor.Definition.bodyScale.y*.15f))<.01f,
            "The restored human captive immediately uses the water surface pose.");
        park.ValidateSave(park.CapturePark());
        Check(world.Sedate(hunter,10),"A sedative can interrupt the restored water capture.");
        Check(guest.Actor.Alive && grid.GroundWalkable(guest.Actor.Cell),"Interrupting a visitor capture rescues the living person onto its dry bank.");
        Object.Destroy(kappa);Object.Destroy(goatProfile);
    }
}

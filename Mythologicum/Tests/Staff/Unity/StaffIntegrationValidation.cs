using System;
using System.IO;
using Mythologicum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StaffIntegrationValidation
{
    private const string Running = "StaffIntegrationValidation.running";
    private static int checks;
    static StaffIntegrationValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running,false))
            { SessionState.SetBool(Running,false); Run(); }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/StaffValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static Vector2Int V(int x,int y) => new Vector2Int(x,y);
    private static void Check(bool condition,string message)
    { checks++; if (!condition) throw new Exception(message); Debug.Log("STAFF CHECK PASS: "+message); }

    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly ParkBuildingNetwork Buildings;
        public readonly CreatureWorld Creatures;
        public readonly StaffWorld Staff;
        public readonly ParkEconomy Economy = new ParkEconomy(5000);
        public readonly CreatureDefinition Animal, Other;
        public readonly StaffDefinition Tuning;
        public readonly ParkBuildingRecord Entrance;
        public VisitorWorld Visitors;
        public Fixture(bool importedPeople=false)
        {
            foreach (GameObject old in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(old);
            UnityEngine.Random.InitState(8143);
            Grid = new SquareBuildGrid(Vector3.zero,2,20,14);
            Fences = new FenceLayout(Grid); Buildings = new ParkBuildingNetwork(Grid,Fences);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(20,-.5f,14); ground.transform.localScale = new Vector3(40,1,28);
            Camera camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(20,25,-10); camera.transform.rotation = Quaternion.Euler(50,0,0);
            Animal = Species("Study animal"); Animal.tags = CreatureTags.Goat;
            Other = Species("Other species"); Other.tags = CreatureTags.Predator;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Creatures = new CreatureWorld(Grid,Fences,Buildings,camera,new Material(shader),new[]{Animal,Other},new EnvironmentGrid(Grid));
            Entrance = Buildings.TryPlace(new BuildingFootprint(new BuildingDefinition { kind=BuildingKind.ParkEntrance,width=1,depth=1 },V(1,2),0));
            for(int x=1;x<=18;x++) Grid.TryOccupy(V(x,1),BuildCellContent.Path);
            Buildings.Refresh();
            Tuning=ScriptableObject.CreateInstance<StaffDefinition>();
            Tuning.walkSpeed=Tuning.runSpeed=10; Tuning.shotInterval=.2f;
            Tuning.healPerSecond=Tuning.repairPerSecond=100; Tuning.restockSeconds=.3f;
            Tuning.researchPerSecond=10;
            Staff = new StaffWorld(Grid,Fences,Buildings,Creatures,Economy,
                importedPeople ? Resources.Load<PeopleDefinition>("People/People") : null,new[]{Animal,Other},Tuning);
        }
        public static CreatureDefinition Species(string name)
        {
            var species=ScriptableObject.CreateInstance<CreatureDefinition>();
            species.displayName=name; species.diet=CreatureDiet.None; species.maximumHealth=100;
            species.damage=0; species.threat=5; species.initialHunger=0;
            species.hungerPerSecond=species.energyDrainPerSecond=0;
            species.walkSpeed=species.runSpeed=0; species.minimumSpace=0;
            species.randomEscapeChance=species.hungryEscapeChance=species.agitatedEscapeChance=species.crowdedEscapeChance=0;
            return species;
        }
        public ParkBuildingRecord Home(BuildingKind kind,int x)
        {
            return Buildings.TryPlace(new BuildingFootprint(new BuildingDefinition {
                kind=kind,width=1,depth=1,staffSalaryPerSecond=2,operatingCostPerSecond=1,hireCost=50 },V(x,2),0));
        }
        public ParkBuildingRecord Feeder(int x,float interval=10000)
        {
            return Buildings.TryPlace(new BuildingFootprint(new BuildingDefinition {
                kind=BuildingKind.GoatDispenser,width=1,depth=1,feederCapacity=6,restockCost=12,
                dispenseInterval=interval,maximumDispensed=2,dispensedCreature=Animal },V(x,2),0));
        }
        public CreatureAgent Spawn(CreatureDefinition profile,Vector2Int cell)
        {
            if(!Creatures.TrySpawn(profile,cell)) throw new Exception("Fixture spawn failed at "+cell);
            return Creatures.Agents[Creatures.Agents.Count-1];
        }
        public void Tick(float seconds)
        {
            for(int i=0;i<Mathf.CeilToInt(seconds/.05f);i++)
            { Creatures.Tick(.05f); Staff.Tick(.05f); Visitors?.Tick(.05f); BuildingEconomy.Tick(Buildings,Economy,.05f); }
        }
        public bool Until(Func<bool> predicate,float seconds)
        {
            for(float elapsed=0;elapsed<seconds;elapsed+=.05f)
            { if(predicate()) return true; Tick(.05f); }
            return predicate();
        }
        public void Pen(int left,int bottom,int right,int top)
        {
            for(int x=left;x<right;x++) { Fences.TryAdd(new FenceEdge(V(x,bottom),true),FenceKind.Heavy); Fences.TryAdd(new FenceEdge(V(x,top),true),FenceKind.Heavy); }
            for(int y=bottom;y<top;y++) { Fences.TryAdd(new FenceEdge(V(left,y),false),FenceKind.Heavy); Fences.TryAdd(new FenceEdge(V(right,y),false),FenceKind.Heavy); }
        }
    }

    private static void HiringAndAccounts()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.ResearchCentre,4);
        Check(home!=null && home.IsOpen,"Employee building requires and receives a connected entrance path");
        for(int i=0;i<5;i++) Check(f.Staff.TryHire(home),"An employee can physically arrive without blocking the five-place hiring capacity");
        Check(home.HiredCount==5 && f.Staff.Staff.Count==5,"Each building owns five distinct employees");
        Check(!f.Staff.TryHire(home) && home.HiredCount==5,"Sixth employee is rejected without exceeding capacity");
        Check(Mathf.Abs(f.Economy.Balance-4750)<.01f && Mathf.Abs(home.Expenses-250)<.01f,"Hiring fees are charged once and attributed to the workplace");
        Check(Mathf.Abs(home.UpkeepPerSecond-11)<.01f,"Employee salaries increase the building upkeep rate");
        float before=home.Expenses; BuildingEconomy.Tick(f.Buildings,f.Economy,2);
        Check(Mathf.Abs(home.Expenses-before-22)<.01f,"Per-building operating accounts include salary expenses over elapsed time");
        Check(f.Staff.Fire(home) && home.HiredCount==4 && f.Staff.Staff.Count==4,"Firing removes one employee and salary obligation");
        Check(Mathf.Abs(home.UpkeepPerSecond-9)<.01f,"Upkeep falls immediately after firing");
        f.Buildings.Remove(home); f.Tick(.1f);
        Check(home.HiredCount==0 && f.Staff.Staff.Count==0,"Demolished workplace cannot leave orphan employees or salaries");
        var ordinary=f.Home(BuildingKind.Kiosk,6);
        Check(!f.Staff.TryHire(ordinary),"Ordinary visitor buildings cannot hire employee roles");
        var disconnected=f.Home(BuildingKind.ZookeeperHut,10); f.Grid.TryRemove(V(10,1),BuildCellContent.Path); f.Buildings.Refresh();
        Check(!disconnected.IsOpen && !f.Staff.TryHire(disconnected),"Disconnected employee building cannot hire active staff");
    }

    private static VisitorAgent Admit(Fixture f,float hunger=20,float energy=80)
    {
        var definition=ScriptableObject.CreateInstance<VisitorDefinition>();
        definition.initialHunger=hunger; definition.initialEnergy=energy;
        definition.hungerPerSecond=definition.energyDrainPerSecond=0;
        definition.minimumStaySeconds=1000; definition.maximumStaySeconds=10000;
        definition.maximumVisitors=1; definition.spawnInterval=10000; definition.walkSpeed=8;
        f.Visitors=new VisitorWorld(f.Grid,f.Fences,f.Buildings,f.Creatures,definition,f.Economy);
        return f.Visitors.TrySpawn();
    }

    private static void PricesAndProfit()
    {
        var f=new Fixture(); var kiosk=f.Home(BuildingKind.Kiosk,8); var other=f.Home(BuildingKind.Kiosk,10);
        kiosk.Footprint.Definition.serviceSeconds=1;
        Check(f.Entrance.SetPrice(22) && kiosk.SetPrice(12),"Entrance and kiosk prices can be set separately by the player");
        Check(other.Price==8,"Changing one kiosk's price does not alter another instance");
        var guest=Admit(f,90);
        Check(guest!=null && f.Entrance.Revenue==22 && f.Economy.Income==22,"Actual visitor arrival credits the selected entrance price");
        Check(f.Until(()=>guest.Activity==VisitorActivity.Eating,10),"Guest reaches the priced kiosk and begins a meal");
        Check(kiosk.Revenue==0,"Kiosk profit does not count meals before service completes");
        kiosk.SetPrice(30);
        Check(f.Until(()=>guest.Actor.Vitals.Hunger<40,3),"Completing a priced service still relieves hunger");
        Check(kiosk.Revenue==12 && f.Economy.Income==34,"A meal pays the quoted starting price exactly once even if the player changes it mid-service");
        Check(Mathf.Abs(kiosk.Profit-(kiosk.Revenue-kiosk.Expenses))<.001f,"Building profit is revenue minus its attributed operating expenses");

        f=new Fixture(); var rest=f.Home(BuildingKind.RestArea,8); rest.Footprint.Definition.restRecoveryPerSecond=50;
        Check(rest.SetPrice(5),"Rest areas support a player-set service price");
        guest=Admit(f,20,0);
        Check(f.Until(()=>guest.Activity==VisitorActivity.Resting,10),"Tired guest reaches a priced rest area");
        rest.SetPrice(9);
        Check(f.Until(()=>guest.Actor.Energy.Energy>=85,4),"Rest service restores guest energy");
        Check(rest.Revenue==5 && f.Economy.Income==20,"Rest completion pays its quoted price once and credits the correct building");
    }

    private static void Research()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.ResearchCentre,4);
        Check(f.Staff.SetResearchTarget(home,f.Animal) && f.Staff.ResearchTarget(home)==f.Animal,"Research centre can select a species independently");
        Check(f.Staff.TryHire(home),"A researcher is hired through its building");
        f.Tick(2);
        Check(f.Staff.Research.Progress(f.Animal)==0,"Researchers cannot invent knowledge when no living specimen is available");
        var animal=f.Spawn(f.Animal,V(5,4));
        Check(f.Until(()=>f.Staff.Research.Progress(f.Animal)>=25,20),"Researcher travels to observe a real creature and unlocks food information");
        string notes=f.Staff.Research.Description(f.Animal);
        Check(notes.Contains("Food:") && !notes.Contains("Space:"),"Knowledge reveals staged descriptions rather than all care rules at once");
        Check(f.Staff.Research.Progress(f.Other)==0,"Studying a species does not reveal unrelated species");
        f.Creatures.ApplyDamage(animal,1000);
        float progress=f.Staff.Research.Progress(f.Animal); f.Tick(3);
        Check(f.Staff.Research.Progress(f.Animal)==progress,"A dead specimen cannot continue generating observation research");
        Check(f.Staff.SetResearchTarget(home,f.Other),"Changing assignment is supported while preserving earned knowledge");
    }

    private static void Care()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.ZookeeperHut,10);
        var animal=f.Spawn(f.Animal,V(13,4)); animal.Health.Damage(50);
        Check(f.Staff.TryHire(home),"Zookeeper is hired for care jobs");
        f.Tick(.1f); Check(animal.Health.Current==50,"Distant animals are not healed remotely");
        Check(f.Until(()=>animal.Health.Current==animal.Health.Maximum,15),"Zookeeper physically reaches and heals an injured creature");

        f=new Fixture(); home=f.Home(BuildingKind.ZookeeperHut,10);
        var edge=new FenceEdge(V(13,4),false); f.Fences.TryAdd(edge); f.Fences.TryDamage(edge,60);
        f.Staff.TryHire(home); f.Tick(.1f);
        Check(f.Fences.HealthAt(edge)==60,"Fence repair waits for employee travel");
        Check(f.Until(()=>f.Fences.HealthAt(edge)==f.Fences.MaximumHealthAt(edge),15),"Zookeeper repairs an existing damaged fence");
        f.Fences.TryDamage(edge,1000); f.Tick(2);
        Check(!f.Fences.HasFence(edge) && f.Fences.TryRepair(edge,100)==0,"Completely broken fences are not recreated by a repair job");

        f=new Fixture(); home=f.Home(BuildingKind.ZookeeperHut,10); var feeder=f.Feeder(15,.2f);
        while(feeder.ConsumeFeed()) { }
        f.Tick(1);
        Check(feeder.FeedStock==0 && f.Creatures.Population==0,"Empty feeder cannot release goats");
        f.Staff.TryHire(home); f.Tick(.1f);
        Check(feeder.FeedStock==0,"Restocking cannot happen before the employee reaches the feeder");
        Check(f.Until(()=>feeder.FeedStock>0,15),"Zookeeper carries out a feeder restock");
        Check(feeder.Expenses>=12,"Restock supplies are charged to the feeder account");
        Check(f.Until(()=>feeder.FeedStock<feeder.Footprint.Definition.feederCapacity && f.Creatures.Population>f.Staff.Staff.Count,3),"Producing a goat consumes one restored feed supply");
    }

    private static void SedationAndMove()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.ZookeeperHut,10);
        f.Animal.threat=100;
        var animal=f.Spawn(f.Animal,V(13,5));
        Check(!f.Staff.RequestMove(animal,V(16,5)),"Awake animals cannot be scheduled for carrying");
        Check(f.Staff.RequestSedate(animal) && !animal.IsSedated,"Sedation request queues work instead of putting the animal to sleep instantly");
        f.Tick(1); Check(!animal.IsSedated,"A queued request waits while no zookeeper is hired");
        f.Staff.TryHire(home);
        Check(f.Until(()=>animal.IsSedated,15),"Zookeeper travels and shoots a non-lethal sedative");
        Check(animal.Health.Current==animal.Health.Maximum,"Sedating does not inflict weapon damage");
        Vector2Int source=animal.Cell;
        Check(f.Staff.RequestMove(animal,V(17,7)) && animal.Cell==source,"Move request preserves the pickup position until staff arrive");
        Check(f.Until(()=>animal.CarriedBy!=null,15),"Zookeeper physically picks up the sedated creature");
        float sleep=animal.SedationRemaining; Vector3 carriedPosition=animal.transform.position; f.Tick(.2f);
        Check(animal.CarriedBy!=null && Vector3.Distance(carriedPosition,animal.transform.position)>0,"Carried creature follows the moving employee");
        Check(animal.SedationRemaining>=sleep-.01f,"Sedation is preserved during transport");
        Check(f.Until(()=>animal.Cell==V(17,7) && animal.CarriedBy==null,15),"Zookeeper delivers and drops the creature at the selected location");
        Check(animal.IsSedated,"Delivery does not prematurely wake a sedated creature");

        f=new Fixture(); animal=f.Spawn(f.Animal,V(8,6)); f.Creatures.Sedate(animal,90);
        for(int y=0;y<f.Grid.Depth;y++) f.Fences.TryAdd(new FenceEdge(V(12,y),false),FenceKind.Heavy);
        Check(!f.Staff.RequestMove(animal,V(16,6)),"A sealed fence blocks physical relocation instead of allowing teleportation");
        f.Fences.TrySetGate(new FenceEdge(V(12,6),false));
        Check(f.Staff.RequestMove(animal,V(16,6)),"An authorized protected staff gate permits a carrying route");
    }

    private static void Security()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.MercenaryHQ,7);
        f.Animal.threat=100; f.Animal.maximumHealth=300;
        var animal=f.Spawn(f.Animal,V(13,5));
        Check(f.Staff.TryHire(home) && f.Staff.RequestRetire(animal),"Retire request is assigned to hired mercenaries");
        f.Tick(3);
        Check(animal.Health.Current==300 && animal.Alive,"One mercenary does not attack a threat requiring a stronger group");
        Check(f.Staff.TryHire(home) && f.Staff.TryHire(home),"Additional mercenaries can join the group");
        Check(f.Until(()=>!animal.Alive,20),"Regrouped mercenaries travel and shoot the requested creature");
        Check(animal.Health.Current==0,"Retirement uses the creature health and damage system");

        f=new Fixture(); home=f.Home(BuildingKind.MercenaryHQ,7); f.Pen(4,4,7,7);
        animal=f.Spawn(f.Animal,V(5,5)); f.Staff.TryHire(home); f.Tick(4);
        Check(animal.Alive && animal.Health.Current==100,"Mercenary patrol leaves animals in secured exhibits alone");
        Check(f.Staff.RequestRetire(animal),"Player can explicitly retire an animal that is still contained");
        f.Tick(4);
        Check(animal.Health.Current==100,"Retirement cannot shoot through a sealed enclosure wall");
        Check(f.Fences.TrySetGate(new FenceEdge(V(5,4),true)),"A staff gate provides retirement access");
        Check(f.Until(()=>!animal.Alive,20),"Mercenary enters through the gate and retires the contained creature");
        Check(f.Staff.Staff[0].Actor.Cell.y>=4,"The retiring mercenary physically reaches the enclosure interior");
    }

    private static void ImportedStaff()
    {
        var f=new Fixture(true);
        Check(Resources.Load<PeopleDefinition>("People/People")!=null,"Staff model catalogue loads through Resources");
        foreach(var item in new[]{new[]{(int)BuildingKind.ResearchCentre,4},new[]{(int)BuildingKind.MercenaryHQ,7},new[]{(int)BuildingKind.ZookeeperHut,10}})
        {
            var home=f.Home((BuildingKind)item[0],item[1]);
            Check(f.Staff.TryHire(home),"Imported staff role hires through its matching building");
            var worker=f.Staff.Staff[f.Staff.Staff.Count-1];
            Check(worker.Actor.GetComponentInChildren<SkinnedMeshRenderer>(true)!=null,"Employee uses the retained imported character art");
            var animator=worker.Actor.GetComponentInChildren<Animator>(true);
            Check(animator!=null && animator.runtimeAnimatorController!=null
                && animator.HasState(0,Animator.StringToHash("Base Layer.idle"))
                && animator.HasState(0,Animator.StringToHash("Base Layer.walk_forward")),
                "Employee uses a compatible idle and locomotion controller");
        }
    }

    private static void SedationCalmsVisitors()
    {
        var f=new Fixture(); f.Animal.threat=100;
        var animal=f.Spawn(f.Animal,V(3,1)); var guest=Admit(f);
        Check(f.Until(()=>guest.Actor.Vitals.Fear>0,1),"Visitor detects a visible threatening creature on open public ground");
        Check(f.Creatures.Sedate(animal,90),"Threatening specimen can be tranquilized without killing it");
        Check(f.Creatures.CanSee(guest.Actor,animal,10) && f.Creatures.FindThreat(guest.Actor,10)==null,
            "Sedated animal remains visible for observation but no longer counts as an active threat");
        f.Tick(1);
        Check(guest.Actor.Vitals.Fear==0,"Visitor fear recovers after sedation removes the active danger");
    }

    private static void SedationStopsAutomaticSecurity()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.MercenaryHQ,7);
        f.Animal.maximumHealth=1000; f.Animal.threat=20;
        var animal=f.Spawn(f.Animal,V(8,4)); f.Staff.TryHire(home);
        Check(f.Until(()=>animal.Health.Current<1000,6),"Mercenary automatically detects and shoots an exposed creature without a retirement order");
        f.Creatures.Sedate(animal,90); float health=animal.Health.Current; f.Tick(1);
        Check(animal.Alive && animal.Health.Current==health,"Automatic security stops firing when the creature is safely sedated");
        Check(f.Staff.RequestRetire(animal) && f.Until(()=>!animal.Alive,15),"An explicit retirement order can still retire a sedated creature");
    }

    private static void StaffDeathPreservesFeeding()
    {
        var f=new Fixture(); var home=f.Home(BuildingKind.ZookeeperHut,10);
        f.Tuning.runSpeed=.1f; f.Staff.TryHire(home); var employee=f.Staff.Staff[0].Actor;
        f.Other.diet=CreatureDiet.Carnivore; f.Other.preyTags=CreatureTags.Visitor;
        f.Other.threat=100; f.Other.damage=200; f.Other.initialHunger=100;
        f.Other.runSpeed=20; f.Other.attackSeconds=.2f; f.Other.feedingSeconds=1;
        var predator=f.Spawn(f.Other,V(11,1));
        Check(f.Until(()=>!employee.Alive && home.HiredCount==0,5),"Killed staff are removed from workplace employment without leaving a salary obligation");
        Check(f.Staff.Staff.Count==0 && home.UpkeepPerSecond==1,"Staff death reduces headcount and upkeep to the workplace base cost");
        bool corpse=false; foreach(var actor in f.Creatures.Agents) if(actor==employee) corpse=true;
        Check(corpse,"A killed employee remains in the shared corpse pipeline instead of being erased as a departure");
        Check(f.Until(()=>predator.Vitals.Hunger<50,3),"Removing an employee from payroll does not cancel predator feeding on the corpse");
    }

    private static void Run()
    {
        try
        {
            Check(Resources.Load<StaffDefinition>("Staff/DefaultStaff")!=null,"Authored staff tuning loads through Resources");
            PricesAndProfit(); HiringAndAccounts(); Research(); Care(); SedationAndMove(); Security(); ImportedStaff(); SedationCalmsVisitors(); SedationStopsAutomaticSecurity(); StaffDeathPreservesFeeding();
            string result="Staff integration validation: "+checks+" checks passed.";
            File.WriteAllText("StaffValidationResults.txt",result); Debug.Log(result); EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            File.WriteAllText("StaffValidationResults.txt","FAILED after "+checks+" checks: "+error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}

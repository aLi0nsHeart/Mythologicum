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
public static class CreatureStaffControlValidation
{
    private const string Running = "CreatureStaffControlValidation.running";
    private static int checks;
    static CreatureStaffControlValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Running, false))
            { SessionState.SetBool(Running, false); Run(); }
        };
    }
    public static void Start()
    {
        SessionState.SetBool(Running, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/CreatureStaffControlValidation.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; Debug.Log("CONTROL CHECK PASS: " + message); }
    private static Vector2Int V(int x, int y) => new Vector2Int(x,y);
    private sealed class Fixture
    {
        public readonly SquareBuildGrid Grid;
        public readonly FenceLayout Fences;
        public readonly CreatureWorld World;
        public readonly CreatureDefinition Animal, Keeper;
        public Fixture()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
            Grid = new SquareBuildGrid(Vector3.zero, 2, 10, 10); Fences = new FenceLayout(Grid);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(10,-.5f,10); ground.transform.localScale = new Vector3(20,1,20);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            Animal = Profile("Test creature", 5); Keeper = Profile("Test keeper", 10);
            Animal.initialEnergy = 10; Animal.hungerPerSecond = 1;
            World = new CreatureWorld(Grid,Fences,new ParkBuildingNetwork(Grid,Fences),camera,
                new Material(Shader.Find("Standard")),new[] { Animal },new EnvironmentGrid(Grid));
        }
        public static CreatureDefinition Profile(string name, float threat)
        {
            var definition = ScriptableObject.CreateInstance<CreatureDefinition>();
            definition.displayName = name; definition.threat = threat; definition.maximumHealth = 40;
            definition.damage = 0; definition.diet = CreatureDiet.None; definition.initialHunger = 20;
            definition.walkSpeed = definition.runSpeed = 0; definition.hungerPerSecond = 0;
            definition.energyDrainPerSecond = 0; definition.decisionInterval = .05f;
            definition.randomEscapeChance = definition.hungryEscapeChance = definition.agitatedEscapeChance = definition.crowdedEscapeChance = 0;
            return definition;
        }
        public CreatureAgent Spawn(CreatureDefinition definition, Vector2Int cell)
        { if (!World.TrySpawn(definition,cell)) throw new Exception("Fixture spawn failed"); return World.Agents[World.Agents.Count-1]; }
    }
    private static void SedationAndCarrying()
    {
        var f = new Fixture(); var animal = f.Spawn(f.Animal,V(2,2)); var keeper = f.World.SpawnExternal(f.Keeper,V(3,2));
        Check(!f.World.BeginCarry(animal,keeper), "Awake animals cannot be picked up");
        Check(!f.World.Sedate(animal,float.NaN) && !animal.IsSedated, "Invalid tranquilizer duration has no effect");
        Check(f.World.Sedate(animal,2), "Keeper sedation enters the controlled sleep state");
        float hunger = animal.Vitals.Hunger, energy = animal.Energy.Energy; Vector3 position = animal.transform.position;
        f.World.Tick(.25f);
        Check(animal.IsSedated && animal.Activity == CreatureActivity.Sedated && animal.transform.position == position,
            "Sedation prevents creature decisions and movement");
        Check(animal.Vitals.Hunger > hunger && animal.Energy.Energy > energy && animal.SedationRemaining < 2,
            "Sedation retains needs and counts down while restoring energy");
        f.World.PopulationLimit = 2;
        Check(!f.World.CanSpawn(V(5,5)) && f.World.CanRelocate(animal,V(5,5)),
            "Moving an existing creature is allowed at the population cap");
        f.Fences.TryAdd(new FenceEdge(V(3,2),false));
        Check(!f.World.BeginCarry(animal,keeper), "Keeper cannot pick an animal up through a closed fence");
        f.Fences.TryRemove(new FenceEdge(V(3,2),false));
        Check(f.World.BeginCarry(animal,keeper) && animal.CarriedBy == keeper, "Nearby sedated creature can be picked up on clear ground");
        float remaining = animal.SedationRemaining; f.World.Tick(1);
        Check(animal.Cell == keeper.Cell && animal.Activity == CreatureActivity.Carried
            && animal.transform.position.y > keeper.transform.position.y,
            "Carried creature follows the keeper rather than its own navigation");
        Check(Mathf.Abs(animal.SedationRemaining-remaining) < .001f, "Tranquilizer duration is paused during transport");
        Check(!f.World.CanSee(keeper,animal,10) && f.World.ApplyDamage(animal,10) == 0,
            "Carried creatures cannot be selected as ranged prey or damaged through their carrier");
        Check(!f.World.DropCarried(animal,V(7,2)), "Dropping cannot teleport an animal to a remote destination");
        Check(f.World.DropCarried(animal,keeper.Cell) && animal.CarriedBy == null && animal.IsSedated,
            "Keeper drops animal on the ground at its actual current destination");
        f.World.Tick(3);
        Check(!animal.IsSedated && animal.SedationRemaining == 0 && animal.Activity == CreatureActivity.Resting,
            "Animal wakes after the remaining sedation duration");
        Check(f.World.Sedate(animal,30) && f.World.BeginCarry(animal,keeper), "Creature can be sedated and carried again");
        f.World.ApplyDamage(keeper,1000); f.World.Tick(.1f);
        Check(animal.CarriedBy == null && animal.IsSedated && animal.Cell == keeper.Cell,
            "Losing a carrier safely leaves the living animal on the ground");
    }
    private static void DeathAndClaimCleanup()
    {
        var f = new Fixture(); var predator = Fixture.Profile("Predator",40);
        predator.diet = CreatureDiet.Carnivore; predator.preyTags = CreatureTags.Goat;
        predator.damage = 40; predator.initialHunger = 100; predator.interactionRange = 2.1f; predator.attackSeconds = 2;
        f.Animal.initialEnergy = 100; f.Animal.hungerPerSecond = 0; f.Animal.tags = CreatureTags.Goat;
        var hunter = f.Spawn(predator,V(1,1)); var prey = f.Spawn(f.Animal,V(2,1)); f.World.Tick(.05f);
        FieldInfo capture = typeof(CreatureAgent).GetField("CapturedBy",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(ReferenceEquals(capture.GetValue(prey), hunter) && hunter.Activity == CreatureActivity.Attacking,
            "External-damage scenario starts during an actual predator attack");
        Check(f.World.ApplyDamage(hunter,1000) == 40 && !hunter.Alive && hunter.Health.Current == 0,
            "External staff damage uses health and the existing death presentation");
        Check(capture.GetValue(prey) == null && prey.Alive, "Killing an attacker releases its living captured prey");
        Check(!f.World.Sedate(hunter) && !f.World.BeginCarry(hunter,prey), "Dead animals cannot be sedated or revived by transport");
    }
    private static void InspectionControls()
    {
        var f = new Fixture(); var animal = f.Spawn(f.Animal,V(2,2));
        var canvasObject = new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var inspection = new CreatureInspection(canvasObject.GetComponent<Canvas>(),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        int retired = 0, sedated = 0, moved = 0;
        inspection.ConfigureStaffActions(actor => { if (actor == animal) retired++; },
            actor => { if (actor == animal) sedated++; }, actor => { if (actor == animal) moved++; });
        inspection.ConfigureResearch(profile => "UNLOCKED CARE NOTE"); inspection.Select(animal);
        Button retire = null, sedate = null, move = null;
        foreach (Button button in canvasObject.GetComponentsInChildren<Button>(true))
        { if (button.name == "Retire") retire = button; if (button.name == "Sedate") sedate = button; if (button.name == "Move") move = button; }
        Check(retire != null && sedate != null && move != null && !move.gameObject.activeSelf,
            "Creature panel offers staff requests and hides Move until sedated");
        retire.onClick.Invoke(); sedate.onClick.Invoke();
        Check(retired == 1 && sedated == 1, "Staff request buttons dispatch the currently selected creature");
        f.World.Sedate(animal,30); inspection.Refresh(); move.onClick.Invoke();
        Check(move.gameObject.activeSelf && move.interactable && moved == 1,
            "Sedation reveals an enabled location-selection request");
        bool researchAndStats = false;
        foreach (Text label in canvasObject.GetComponentsInChildren<Text>(true))
            if (label.text.Contains("Health:") && label.text.Contains("UNLOCKED CARE NOTE")) researchAndStats = true;
        Check(researchAndStats && canvasObject.GetComponentInChildren<ScrollRect>(true) != null,
            "Research notes and all creature stats remain accessible in the scrolling panel");
        var panel=(RectTransform)retire.transform.parent;
        canvasObject.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        ((RectTransform)canvasObject.transform).sizeDelta=new Vector2(1280,720);
        inspection.Refresh(); Canvas.ForceUpdateCanvases();
        Check(((RectTransform)retire.transform).anchorMin.y==1f && ((RectTransform)move.transform).anchorMin.y==1f,
            "Creature actions stay at the top of the info panel instead of below the screen");
        Check(panel.sizeDelta.y<=480f,"Creature details fit a 720-high canvas with space for the build toolbar");
        inspection.Dispose();
    }
    private static void Run()
    {
        try
        {
            SedationAndCarrying(); DeathAndClaimCleanup(); InspectionControls();
            string result = "Creature staff controls: " + checks + " checks passed.";
            File.WriteAllText("CreatureStaffControlValidationResults.txt",result); Debug.Log(result); EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("CreatureStaffControlValidationResults.txt","FAILED after " + checks + " checks: " + exception);
            Debug.LogException(exception); EditorApplication.Exit(1);
        }
    }
}

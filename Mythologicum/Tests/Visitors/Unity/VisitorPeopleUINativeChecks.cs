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
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Age-specific visitor models and observable type information; disposable project only.</summary>
public static class VisitorPeopleUINativeChecks
{
    private static int checks;
    private static void Check(bool value, string reason)
    { if (!value) throw new Exception(reason); checks++; Debug.Log("VISITOR PEOPLE/UI PASS: " + reason); }
    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Set(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(owner, value);
        else owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }

    public static void Run()
    {
        try
        {
            PlayerSettings.companyName = "MythologicumValidation";
            PlayerSettings.productName = "VisitorPeopleUIValidation";
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int count = RunChecks();
            File.WriteAllText("VisitorPeopleUIResults.txt", "PASS: " + count + " visitor model pools, appearance, inspection and management checks.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorPeopleUIResults.txt", "FAIL: " + error); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }

    public static int RunChecks()
    {
        checks = 0;
        var catalog = Resources.Load<PeopleDefinition>("People/People");
        Check(catalog != null && catalog.civilians.Length == 7, "The existing general visitor pool remains intact");
        Check(catalog.VisitorModels(false).Length >= 5 && catalog.VisitorModels(true).Length >= 1,
            "Explicit adult and child visitor pools contain retained assets");
        Check(!catalog.VisitorModels(false).Intersect(catalog.VisitorModels(true)).Any(), "An age-specific model cannot appear in both pools");
        var reserved = new[] { catalog.police, catalog.doctor, catalog.worker };
        Check(!catalog.VisitorModels(false).Concat(catalog.VisitorModels(true)).Intersect(reserved).Any(),
            "Staff uniforms remain excluded from ordinary visitor arrivals");
        Check(catalog.VisitorModels(true).All(model => model != null && model.name == "little_boy_B"),
            "The child type uses the confirmed child model rather than a random adult");
        foreach (var model in catalog.VisitorModels(false).Concat(catalog.VisitorModels(true)))
            Check(model != null && AssetDatabase.GetAssetPath(model).StartsWith("Assets/Mythologicum/Prefabs/People/Visitors/", StringComparison.Ordinal),
                "Visitor appearance comes from the organized retained visitor models");

        var original = SceneManager.GetActiveScene(); bool dirty = original.isDirty;
        int actorsBefore = Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        var random = UnityEngine.Random.state;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var owned = new HashSet<Object>();
        VisitorWorld visitors = null;
        try
        {
            var grid = new SquareBuildGrid(new Vector3(10000, 0, 10000), 2, 30, 12);
            var fences = new FenceLayout(grid);
            var buildings = new ParkBuildingNetwork(grid, fences);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Hidden/InternalErrorShader")); owned.Add(material);
            var definition = Object.Instantiate(Resources.Load<VisitorDefinition>("Visitors/DefaultVisitors")); owned.Add(definition);
            Check(definition.people == catalog, "The actual visitor definition uses the organized people catalog");
            var creatures = new CreatureWorld(grid, fences, buildings, null, material, Array.Empty<CreatureDefinition>());
            var economy = new ParkEconomy(10000);
            visitors = new VisitorWorld(grid, fences, buildings, creatures, definition, economy);
            var restore = typeof(VisitorWorld).GetMethod("RestoreGuestWithType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(restore != null, "The actual visitor controller supports typed save restoration");
            var guests = new List<VisitorAgent>();
            foreach (var type in new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children, VisitorType.Legacy })
            {
                var guest = (VisitorAgent)restore.Invoke(visitors, new object[] { new Vector2Int(2 + guests.Count * 4, 2), type });
                Check(guest != null && guest.Type == type, "Typed restoration creates the intended visitor: " + type);
                guests.Add(guest);
                if (type == VisitorType.Legacy) continue;
                var body = Field<Transform>(guest.Actor, "Body");
                var pool = catalog.VisitorModels(type == VisitorType.Children);
                Check(pool.Any(model => body.name.StartsWith(model.name, StringComparison.Ordinal)), "The visitor controller chooses the correct age pool: " + type);
                Check(Field<bool>(guest.Actor, "HasVisualModel"), "The visitor displays an imported retained model: " + type);
            }

            var canvasObject = new GameObject("Visitor type UI fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            var presentation = new VisitorPresentation(canvas, UITheme.BodyFont, visitors, economy);
            foreach (var guest in guests)
            {
                presentation.Select(guest); UIThemeApplicator.ApplyAll(canvas); Canvas.ForceUpdateCanvases(); presentation.Refresh();
                string actual = Field<Text>(presentation, "details").text;
                Check(actual.Contains(guest.TypeSettings.DisplayName) && actual.Contains(guest.TypeSettings.Description),
                    "Inspection exposes the visitor's configured type and motivation: " + guest.Type);
            }
            var adventurer = guests.Single(guest => guest.Type == VisitorType.Adventurers);
            adventurer.ObservedSpecies.Add("Unicorn"); adventurer.ObservedOrigins.Add("creature.origin.central_europe");
            presentation.Select(adventurer);
            Check(Field<Text>(presentation, "details").text.Contains(Loc.Format("visitor.info.discoveries", 1, 1)),
                "Adventurer inspection shows the accumulated species and origin discoveries");
            Check(creatures.TrySpawn(Resources.Load<CreatureDefinition>("Creatures/Unicorn"), new Vector2Int(8, 8)),
                "A real creature can supply the selected visitor's exhibit information");
            var child = guests.Single(guest => guest.Type == VisitorType.Children);
            var exhibit = creatures.Agents.Last();
            Set(child, "Watching", exhibit); Set(child, "LastExhibitInterest", .75f); Set(child, "Activity", VisitorActivity.Watching);
            presentation.Select(child);
            Check(Field<Text>(presentation, "details").text.Contains(Loc.Format("visitor.info.exhibit_interest", exhibit.Definition.DisplayName, 75f)),
                "Inspection names the watched creature and the visitor's current interest in it");
            Check(child.Actor.GetComponent<CapsuleCollider>().height
                < guests.Single(guest => guest.Type == VisitorType.ThrillSeekers).Actor.GetComponent<CapsuleCollider>().height,
                "The child actor uses its smaller configured collider rather than adult dimensions");
            var manager = new ParkManagementPresentation(canvas, buildings, visitors, creatures, economy);
            manager.Open(); Canvas.ForceUpdateCanvases(); manager.Refresh();
            string statistics = Field<Text>(manager, "statistics").text;
            foreach (var type in new[] { VisitorType.ThrillSeekers, VisitorType.Wonderlust, VisitorType.Adventurers, VisitorType.Children })
                Check(statistics.Contains(definition.SettingsFor(type).DisplayName), "Park statistics names the current visitor type: " + type);
            Check(statistics.Contains(Loc.Format("park.visitor_types.general", 1)), "Existing general visitors appear as a separate count without save-system jargon");
        }
        finally
        {
            // Edit-mode fixture cleanup must use immediate destruction rather than runtime Dispose.
            if (visitors != null)
                foreach (var field in typeof(VisitorWorld).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                {
                    if (field.GetValue(visitors) is Object value && !AssetDatabase.Contains(value)) owned.Add(value);
                    if (field.GetValue(visitors) is IDictionary dictionary)
                        foreach (var item in dictionary.Values)
                            if (item is Object asset && !AssetDatabase.Contains(asset)) owned.Add(asset);
                }
            EditorSceneManager.CloseScene(scene, true);
            foreach (var asset in owned) if (asset != null) Object.DestroyImmediate(asset);
            SceneManager.SetActiveScene(original); UnityEngine.Random.state = random;
        }
        Check(original.isLoaded && original.isDirty == dirty
            && Object.FindObjectsByType<CreatureAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == actorsBefore,
            "The visitor UI fixture restores the original scene and actor population");
        return checks;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    public sealed class GameplayDataIssue
    {
        public MessageType severity;
        public string message;
        public UnityEngine.Object context;
        public GameplayDataIssue(MessageType severity, string message, UnityEngine.Object context)
        { this.severity = severity; this.message = message; this.context = context; }
    }

    /// <summary>Read-only cross-asset checks complement each asset's numeric Inspector constraints.</summary>
    public static class GameplayDataValidator
    {
        public static List<GameplayDataIssue> ValidateAll()
        {
            var results = new List<GameplayDataIssue>();
            results.AddRange(LocalizationValidation.Validate());
            string[] paths = { "Management/DefaultParkBalance", "Management/DefaultOperations", "Management/DefaultAlerts",
                "Saving/DefaultSaveSettings", "Interface/DefaultPlayControls", "Interface/DefaultLoadingScreen", "Buildings/DefaultBuildings", "Fences/DefaultFences", "Visitors/DefaultVisitors",
                "Staff/DefaultStaff", "Progression/DefaultProgression", "Terrain/DefaultTerrain", "Creatures/SimulationSettings", "Environment/Vegetation" };
            foreach (string path in paths)
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Mythologicum/Resources/" + path + ".asset") == null)
                    results.Add(new GameplayDataIssue(MessageType.Error, "Missing settings asset: " + path, null));
            var species = AssetDatabase.FindAssets("t:CreatureDefinition", new[] { "Assets/Mythologicum/Resources/Creatures" })
                .Select(id => AssetDatabase.LoadAssetAtPath<CreatureDefinition>(AssetDatabase.GUIDToAssetPath(id))).Where(item => item != null).ToArray();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var creature in species)
            {
                if (!names.Add(creature.name)) Error(results, creature, "Duplicate species identifier: " + creature.name);
                if (creature.requiresTreeForSleep && creature.requiresGravestoneForSleep)
                    Warn(results, creature, "Both sleeping-site abilities are enabled. Choose which shelter this species should use.");
                if (creature.maximumCompanions >= 0 && creature.maximumCompanions < creature.minimumCompanions)
                    Error(results, creature, "Maximum companions is lower than minimum companions.");
                if (creature.wakeThreshold <= creature.sleepThreshold) Error(results, creature, "Wake threshold must exceed sleep threshold.");
                if (creature.diet == CreatureDiet.Carnivore && creature.preyTags == CreatureTags.None)
                    Warn(results, creature, "Carnivore has no prey tags, so hunting cannot find a suitable meal.");
            }
            var buildings = Resources.Load<BuildingCatalogDefinition>("Buildings/DefaultBuildings");
            if (buildings != null)
            {
                var kinds = new HashSet<BuildingKind>();
                foreach (var building in buildings.buildings ?? Array.Empty<BuildingDefinition>())
                {
                    if (building == null) { Error(results, buildings, "Building catalogue contains an empty entry."); continue; }
                    if (!kinds.Add(building.kind)) Error(results, buildings, "Duplicate building kind: " + building.kind);
                    if (building.width < 1 || building.depth < 1 || building.height <= 0) Error(results, buildings, building.displayName + ": invalid footprint or height.");
                    if (building.entranceColumn >= building.width || building.entranceColumn < -1) Error(results, buildings, building.displayName + ": door lies outside its footprint.");
                }
                if (!kinds.Contains(BuildingKind.ParkEntrance)) Error(results, buildings, "The catalogue needs a park entrance to start a park.");
            }
            var progression = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
            if (progression != null) results.AddRange(ValidateProgression(progression, names));
            foreach (string id in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Mythologicum/Resources" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(id));
                if (asset == null) continue;
                using (var serialized = new SerializedObject(asset))
                {
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.Float
                            && (float.IsNaN(property.floatValue) || float.IsInfinity(property.floatValue)))
                            Error(results, asset, property.propertyPath + ": value must be finite.");
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                            && property.objectReferenceInstanceIDValue != 0)
                            Error(results, asset, property.propertyPath + ": missing asset reference.");
                    }
                }
            }
            if (!results.Any(item => item.severity == MessageType.Error))
                results.Insert(0, new GameplayDataIssue(MessageType.Info, "Gameplay data passed validation. Warnings describe content that may be intentionally unfinished.", null));
            return results;
        }

        public static List<GameplayDataIssue> ValidateProgression(ProgressionDefinition asset, ISet<string> species)
        {
            var result = new List<GameplayDataIssue>();
            var rules = asset.rules;
            if (rules == null) { Error(result, asset, "Progression rules are missing."); return result; }
            var regions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in rules.regions ?? Array.Empty<ExpeditionRegion>())
            {
                if (region == null || string.IsNullOrWhiteSpace(region.id) || !regions.Add(region.id))
                { Error(result, asset, "Regions need unique, non-empty IDs."); continue; }
                if (region.species == null || region.species.Length == 0) Warn(result, asset, region.name + ": no creatures assigned; the region is unavailable for purchase.");
                foreach (string name in region.species ?? Array.Empty<string>())
                    if (name == null || !species.Contains(name)) Error(result, asset, region.id + ": unknown creature ID '" + name + "'. Use the species asset filename.");
            }
            var difficulties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mission in rules.difficulties ?? Array.Empty<ExpeditionDifficulty>())
            {
                if (mission == null || string.IsNullOrWhiteSpace(mission.id) || !difficulties.Add(mission.id))
                { Error(result, asset, "Mission difficulties need unique, non-empty IDs."); continue; }
                if (mission.minimumRating > mission.maximumRating) Error(result, asset, mission.id + ": minimum rating exceeds maximum rating.");
                if (mission.seconds <= 0 || mission.costPerTeam < 0 || mission.creaturesFound < 1 || mission.successChance < 0 || mission.successChance > 1)
                    Error(result, asset, mission.id + ": invalid duration, cost, reward or probability.");
            }
            var technologies = new Dictionary<string, TechnologyDefinition>(StringComparer.Ordinal);
            foreach (var technology in rules.technologies ?? Array.Empty<TechnologyDefinition>())
            {
                if (technology == null || string.IsNullOrWhiteSpace(technology.id) || technologies.ContainsKey(technology.id))
                { Error(result, asset, "Technologies need unique, non-empty IDs."); continue; }
                technologies.Add(technology.id, technology);
            }
            var finished = new HashSet<string>(); var visiting = new HashSet<string>();
            void Visit(string id)
            {
                if (finished.Contains(id)) return;
                if (!visiting.Add(id)) { Error(result, asset, "Research prerequisite cycle includes '" + id + "'."); return; }
                foreach (string prerequisite in technologies[id].prerequisites ?? Array.Empty<string>())
                {
                    if (prerequisite == null || !technologies.ContainsKey(prerequisite)) Error(result, asset, id + ": unknown prerequisite '" + prerequisite + "'.");
                    else Visit(prerequisite);
                }
                visiting.Remove(id); finished.Add(id);
            }
            foreach (string id in technologies.Keys) Visit(id);
            foreach (string id in rules.startingTechnologies ?? Array.Empty<string>())
                if (id == null || !technologies.ContainsKey(id)) Error(result, asset, "Unknown starting technology: " + id);
            foreach (string id in rules.startingRegions ?? Array.Empty<string>())
                if (id == null || !regions.Contains(id)) Error(result, asset, "Unknown starting region: " + id);
            foreach (var stock in rules.startingCreatures ?? Array.Empty<CreatureStock>())
                if (stock == null || stock.species == null || !species.Contains(stock.species) || stock.count < 0)
                    Error(result, asset, "Starting creature stock has an unknown species or negative quantity.");
            return result;
        }
        private static void Error(List<GameplayDataIssue> list, UnityEngine.Object asset, string message) => list.Add(new GameplayDataIssue(MessageType.Error, message, asset));
        private static void Warn(List<GameplayDataIssue> list, UnityEngine.Object asset, string message) => list.Add(new GameplayDataIssue(MessageType.Warning, message, asset));
    }

    public sealed class GameplayValidationWindow : EditorWindow
    {
        private List<GameplayDataIssue> issues = new List<GameplayDataIssue>();
        private Vector2 scroll;
        [MenuItem("Mythologicum/Gameplay/Validate gameplay data")]
        public static void Open()
        { var window = GetWindow<GameplayValidationWindow>("Gameplay validation"); window.minSize = new Vector2(540, 320); window.Validate(); window.Show(); }
        private void Validate() { issues = GameplayDataValidator.ValidateAll(); Repaint(); }
        private void OnGUI()
        {
            if (GUILayout.Button("Validate gameplay data")) Validate();
            EditorGUILayout.LabelField(issues.Count(item => item.severity == MessageType.Error) + " errors, " + issues.Count(item => item.severity == MessageType.Warning) + " warnings");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.message, issue.severity);
                if (issue.context != null && GUILayout.Button("Select " + issue.context.name))
                { Selection.activeObject = issue.context; EditorGUIUtility.PingObject(issue.context); }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}

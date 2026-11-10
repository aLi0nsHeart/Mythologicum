using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mythologicum.Editor
{
    /// <summary>A single entry point to the real gameplay assets, without resetting or duplicating their values.</summary>
    public sealed class GameplayBalancingWindow : EditorWindow
    {
        private sealed class Entry
        {
            public string label, description;
            public ScriptableObject asset;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private int selected;
        private Vector2 listScroll, inspectorScroll;
        private UnityEditor.Editor assetEditor;

        [MenuItem("Mythologicum/Gameplay/Balancing Tools")]
        public static void Open()
        {
            var window = GetWindow<GameplayBalancingWindow>("Gameplay balancing");
            window.minSize = new Vector2(760, 460);
            window.Show();
        }

        private void OnEnable() => FindAssets();
        private void OnDisable() { if (assetEditor != null) DestroyImmediate(assetEditor); }

        private void FindAssets()
        {
            entries.Clear();
            Add("Park & economy", "Management/DefaultParkBalance",
                "Starting funds, reputation, exhibition appeal, death penalties, fair prices and visitor demand. Starting funds apply to new parks; saves retain their cash and reputation.");
            Add("Buildings", "Buildings/DefaultBuildings",
                "Building dimensions, purchase/upkeep costs, default prices, food/rest services, staff capacity and salaries, goat supply stock and dispensing. Buildings use a copy at park start; restart Play mode after editing. Existing saved footprints and player-set prices are preserved.");
            Add("Fences", "Fences/DefaultFences",
                "Health, climbing, phasing and resistance to creature attacks, separately for each fence type. New segments use the configured health; repairs and containment use the current rules.");
            Add("Visitors", "Visitors/DefaultVisitors",
                "Arrival rate, visitor capacity, hunger, rest, fear, patience, satisfaction rewards/penalties, viewing and departure behaviour. Initial values apply to newly arriving visitors.");
            Add("Staff & creature research", "Staff/DefaultStaff",
                "Travel, perception, shooting, healing, repairs, sedation, creature observation and discovery milestones. Hiring costs, capacity and salaries belong to each building.");
            Add("Expeditions & technology", "Progression/DefaultProgression",
                "Initial unlocks and specimen stock, teams, region permits, mission rewards/durations/chances, technology costs, prerequisites and upgrade effects. Keep existing IDs stable so saved parks can identify projects and species.");
            Add("Shared creature simulation", "Creatures/SimulationSettings",
                "Shared actor capacity, placement spacing, grazing regrowth, transport and corpse removal. Population/regrowth settings apply when a park starts; species-specific needs are in the individual creature assets below.");
            Add("Terrain & brush", "Terrain/DefaultTerrain",
                "Generation defaults, movement/path slopes, uphill travel cost, foundation earthwork and terrain brush ranges. Existing saves retain their terrain; test map generation with a new park.");
            Add("Vegetation & habitat", "Environment/Vegetation",
                "Tree contribution to forest coverage, available plants, scattering and tree height. Habitat coverage changes apply when a park starts; trees remain physical enrichment.");
            string[] creatures = AssetDatabase.FindAssets("t:CreatureDefinition", new[] { "Assets/Mythologicum/Resources/Creatures" });
            Array.Sort(creatures, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            foreach (string guid in creatures)
            {
                var asset = AssetDatabase.LoadAssetAtPath<CreatureDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                entries.Add(new Entry { label = "Creature: " + asset.displayName, asset = asset,
                    description = "Species rules: combat, diet, needs, sleep, social groups, escape, habitat and behaviour weights. Hunger/energy/health/initial happiness apply on spawn; most behaviour values are read during simulation. Rate fields use simulation seconds unless labelled otherwise." });
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, entries.Count - 1));
            Repaint();
        }

        private void Add(string label, string resourcePath, string description)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Mythologicum/Resources/" + resourcePath + ".asset");
            entries.Add(new Entry { label = label, asset = asset, description = description });
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Edit the assets here or in the Project window. Save asset changes to keep them. Stop and restart Play mode for a clean balancing comparison; loading a save keeps its existing state. No scene rebuild is needed.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(235));
                for (int i = 0; i < entries.Count; i++)
                {
                    if (GUILayout.Toggle(selected == i, entries[i].label, "Button", GUILayout.Height(30))) selected = i;
                }
                if (GUILayout.Button("Refresh asset list")) FindAssets();
                EditorGUILayout.EndScrollView();

                using (new EditorGUILayout.VerticalScope())
                {
                    if (entries.Count == 0) return;
                    var entry = entries[selected];
                    EditorGUILayout.LabelField(entry.label, EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(entry.description, MessageType.None);
                    if (entry.asset == null)
                    {
                        EditorGUILayout.HelpBox("The settings asset could not be found. Let Unity finish importing, then refresh this list.", MessageType.Warning);
                        return;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(entry.asset, typeof(ScriptableObject), false);
                        if (GUILayout.Button("Select in Project", GUILayout.Width(125)))
                        { Selection.activeObject = entry.asset; EditorGUIUtility.PingObject(entry.asset); }
                        if (GUILayout.Button("Save asset", GUILayout.Width(95))) AssetDatabase.SaveAssetIfDirty(entry.asset);
                    }
                    inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
                    UnityEditor.Editor.CreateCachedEditor(entry.asset, null, ref assetEditor);
                    assetEditor.OnInspectorGUI();
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }
}

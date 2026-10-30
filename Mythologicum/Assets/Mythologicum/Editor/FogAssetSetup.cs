using BKPureNature;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Mythologicum.Editor
{
    /// <summary>Creates the park preset and adds fog alongside existing renderer features.</summary>
    public static class FogAssetSetup
    {
        public const string PrefabPath = "Assets/Mythologicum/Resources/Rendering/ParkAtmosphere.prefab";

        [MenuItem("Mythologicum/Lighting/Set Up Volumetric Fog")]
        public static void Apply()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Mythologicum/Resources/Rendering"))
                AssetDatabase.CreateFolder("Assets/Mythologicum/Resources", "Rendering");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var instance = new GameObject("Park Atmosphere");
                try
                {
                    instance.AddComponent<PureVolumetricFog>().ApplyParkPreset();
                    instance.AddComponent<ParkAtmosphere>();
                    PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
                }
                finally { Object.DestroyImmediate(instance); }
            }
            Install("Assets/Settings/PC_Renderer.asset");
            Install("Assets/Settings/Mobile_Renderer.asset");
            AssetDatabase.SaveAssets();
        }

        private static void Install(string path)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null) throw new System.IO.FileNotFoundException("Renderer missing", path);
            bool installed = false;
            foreach (var existing in renderer.rendererFeatures)
                if (existing is PureVolumetricFogRendererFeature) installed = true;
            if (!installed)
            {
                var feature = ScriptableObject.CreateInstance<PureVolumetricFogRendererFeature>();
                feature.name = "Park Volumetric Fog";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            // URP uses the local-ID map to recover feature references after an assembly reload.
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
    }
}

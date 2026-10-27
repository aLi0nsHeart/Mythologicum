using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mythologicum.Editor
{
    public static class DaylightSceneSetup
    {
        private const string ScenePath = "Assets/Mythologicum/Scenes/GameScene.unity";
        private const string SkyPath = "Assets/Mythologicum/Materials/Lighting/DaylightSky.mat";
        private const string ProfilePath = "Assets/Mythologicum/Settings/DaylightVolume.asset";

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        [MenuItem("Mythologicum/Lighting/Apply Daylight to Game Scene")]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                throw new InvalidOperationException("Open GameScene before applying its daylight preset.");
            Folder("Assets/Mythologicum/Materials/Lighting");
            Folder("Assets/Mythologicum/Settings");
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "Daylight Sky" };
                AssetDatabase.CreateAsset(sky, SkyPath);
            }
            sky.SetFloat("_SunDisk", 2f);
            sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1f);
            sky.SetFloat("_Exposure", 1.1f);
            sky.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetColor("_GroundColor", new Color(0.55f, 0.67f, 0.74f));
            EditorUtility.SetDirty(sky);

            Light sun = null;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null) sun = new GameObject("Sun - Warm Daylight").AddComponent<Light>();
            sun.name = "Sun - Warm Daylight";
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.3f;
            sun.useColorTemperature = false;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.shadowBias = 0.12f;
            sun.shadowNormalBias = 0.5f;
            sun.shadowNearPlane = 0.2f;
            sun.GetUniversalAdditionalLightData().usePipelineSettings = false;
            RenderSettings.sun = sun;
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.52f, 0.57f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.32f, 0.23f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.64f, 0.75f, 0.82f);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 180f;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "Park Daylight";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var tone = Component<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(4f);
            color.saturation.Override(2f);
            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1.4f);
            bloom.intensity.Override(0.06f);
            bloom.scatter.Override(0.6f);
            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.06f);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);
            EditorUtility.SetDirty(profile);
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            var volumeObject = GameObject.Find("Park Daylight");
            if (volumeObject == null) volumeObject = new GameObject("Park Daylight");
            var volume = volumeObject.GetComponent<Volume>() ?? volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;

            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("GameScene requires its main camera.");
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.renderShadows = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.volumeLayerMask = 1;
            cameraData.volumeTrigger = camera.transform;

            // Keep shadows visible when the park camera is zoomed out.
            foreach (string path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;
                pipeline.shadowDistance = 110f;
                if (path.Contains("PC_RPAsset")) pipeline.mainLightShadowmapResolution = 4096;
                EditorUtility.SetDirty(pipeline);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            DynamicGI.UpdateEnvironment();
            Debug.Log("Applied park daylight: visible sky, realtime sun shadows, gradient ambient, distance haze and subtle grading.");
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
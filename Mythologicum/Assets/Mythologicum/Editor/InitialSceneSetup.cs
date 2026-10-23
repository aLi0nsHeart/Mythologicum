using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mythologicum.Editor
{
    public static class InitialSceneSetup
    {
        private const string SceneFolder = "Assets/Mythologicum/Scenes";

        // Explicit command only: never modifies scenes automatically on editor startup.
        [MenuItem("Mythologicum/Create Initial Scenes")]
        public static void CreateScenes()
        {
            string menuPath = SceneFolder + "/MainMenuScene.unity";
            string gamePath = SceneFolder + "/GameScene.unity";
            if (File.Exists(menuPath) || File.Exists(gamePath))
                throw new InvalidOperationException("Initial scenes already exist. Nothing was overwritten.");

            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();
            // Batch mode starts with an untitled scene; Unity cannot add scenes to it.
            if (Application.isBatchMode)
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    throw new InvalidOperationException("Please save or close untitled scenes before creating the initial scenes.");
            Scene previous = SceneManager.GetActiveScene();
            try
            {
                CreateMenu(menuPath);
                CreateGame(gamePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(gamePath, true)
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
                if (existing.path != menuPath && existing.path != gamePath)
                    scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Mythologicum: MainMenuScene and GameScene created successfully.");
        }

        private static void CreateMenu(string path)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Camera camera = NewCamera();
                camera.backgroundColor = new Color(0.045f, 0.065f, 0.08f);
                var navigation = new GameObject("Scene Navigation").AddComponent<SceneNavigation>();
                RectTransform canvas = CreateCanvas();
                Label(canvas, "MYTHOLOGICUM", new Vector2(0f, 135f), 48);
                Label(canvas, "The Grand Exhibition", new Vector2(0f, 80f), 25);
                Button(canvas, "Spiel starten", new Vector2(0f, -15f), navigation.StartGame);
                Button(canvas, "Beenden", new Vector2(0f, -85f), navigation.QuitGame);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void CreateGame(string path)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Camera camera = NewCamera();
                camera.orthographic = false;
                camera.orthographicSize = 18f;
                camera.farClipPlane = 300f;
                camera.gameObject.AddComponent<IsometricCameraController>();
                var sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.5f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.6f);
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Prototype Ground - 100 x 100 metres";
                ground.transform.localScale = new Vector3(10f, 1f, 10f);
                var landmarks = new GameObject("Prototype Landmarks");
                for (int i = 0; i < 3; i++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Landmark " + (i + 1);
                    cube.transform.SetParent(landmarks.transform);
                    cube.transform.localScale = new Vector3(3f, 2f + i * 2f, 3f);
                    cube.transform.position = new Vector3((i - 1) * 10f, 1f + i, i * 7f);
                }
                var navigation = new GameObject("Scene Navigation").AddComponent<SceneNavigation>();
                RectTransform canvas = CreateCanvas();
                Text help = Label(canvas, "WASD / Pfeile: Bewegen    Q / E: Drehen    Mausrad: Zoom", Vector2.zero, 20);
                help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                help.rectTransform.anchoredPosition = new Vector2(0f, 35f);
                UnityEngine.UI.Button back = Button(canvas, "Hauptmenü", Vector2.zero, navigation.MainMenu);
                var rect = (RectTransform)back.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-165f, -45f);
                EditorSceneManager.SaveScene(scene, path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static Camera NewCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.13f, 0.17f);
            return camera;
        }

        private static RectTransform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return (RectTransform)go.transform;
        }

        private static Text Label(Transform parent, string content, Vector2 position, int size)
        {
            var go = new GameObject(content, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.94f, 0.86f, 0.66f);
            text.raycastTarget = false;
            text.rectTransform.sizeDelta = new Vector2(1000f, 65f);
            text.rectTransform.anchoredPosition = position;
            return text;
        }

        private static UnityEngine.UI.Button Button(Transform parent, string title, Vector2 position, UnityAction action)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(280f, 52f);
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(0.16f, 0.23f, 0.27f);
            var button = go.GetComponent<UnityEngine.UI.Button>();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            Text label = Label(go.transform, title, Vector2.zero, 24);
            label.rectTransform.sizeDelta = rect.sizeDelta;
            return button;
        }
    }
}

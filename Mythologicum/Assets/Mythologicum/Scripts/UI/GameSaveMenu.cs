using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace Mythologicum
{
    /// <summary>Runtime menu wiring leaves authored scenes intact.</summary>
    public sealed class GameSaveMenu : MonoBehaviour
    {
        private static GameSaveMenu instance;
        public static bool IsOpen => instance != null && instance.overlay != null && instance.overlay.activeSelf;
        private BuildModeController park;
        private GameObject overlay;
        private Text message;
        private Font font;
        private float previousScale = 1f;
        public static void Attach(BuildModeController controller)
        {
            if (instance != null) return;
            instance = controller.gameObject.AddComponent<GameSaveMenu>(); instance.park = controller; instance.CreatePause();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            instance = null; ParkSaveStore.Pending = null; ParkSaveStore.LoadError = null; Time.timeScale = 1f;
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenuScene") return;
            Time.timeScale = 1f;
            var canvas = FindFirstObjectByType<Canvas>(); if (canvas == null) return;
            var component = canvas.gameObject.AddComponent<GameSaveMenu>(); instance = component;
            component.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Insert beside the existing start button, preserving authored menu art and events.
            Button start = null;
            foreach (var button in canvas.GetComponentsInChildren<Button>())
                if (button.onClick.GetPersistentEventCount() > 0 && button.onClick.GetPersistentMethodName(0) == "StartGame") { start = button; break; }
            var load = component.Button(canvas.transform, "Load game", new Vector2(0f, -30f), component.Load);
            if (start != null)
            {
                var source = (RectTransform)start.transform; var rect = (RectTransform)load.transform;
                rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax; rect.pivot = source.pivot;
                rect.sizeDelta = source.sizeDelta;
                rect.anchoredPosition = source.anchoredPosition + new Vector2(source.rect.width + 20f, 0f);
                // Keep the pair centered instead of placing load outside small screens.
                source.anchoredPosition -= new Vector2((source.rect.width + 20f) / 2f, 0f);
                rect.anchoredPosition -= new Vector2((source.rect.width + 20f) / 2f, 0f);
            }
            load.interactable = ParkSaveStore.Exists;
            component.message = component.Label(canvas.transform, "", new Vector2(0,-180), new Vector2(650,100));
            component.message.text = ParkSaveStore.LoadError ?? ""; ParkSaveStore.LoadError = null;
        }
        private void CreatePause()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = FindFirstObjectByType<Canvas>();
            // Replace the existing top-right MainMenu action with the pause menu.
            bool wired = false;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.onClick.GetPersistentMethodName(i) == "MainMenu")
                    {
                        button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(Toggle);
                        var text = button.GetComponentInChildren<Text>(); if (text != null) text.text = "Pause";
                        wired = true; break;
                    }
            if (!wired)
            {
                var pause = Button(canvas.transform, "Pause", Vector2.zero, Toggle);
                var r = (RectTransform)pause.transform; r.anchorMin = r.anchorMax = r.pivot = Vector2.one;
                r.anchoredPosition = new Vector2(-15,-15); r.sizeDelta = new Vector2(130,44);
            }
            overlay = new GameObject("Pause menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)overlay.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(.025f,.04f,.06f,.97f);
            Label(overlay.transform, "Paused", new Vector2(0,190), new Vector2(500,50));
            Button(overlay.transform, "Resume", new Vector2(0,130), Toggle);
            Button(overlay.transform, "Save game", new Vector2(0,70), Save);
            Button(overlay.transform, "Load game", new Vector2(0,10), Load);
            Button(overlay.transform, "Quit to main menu", new Vector2(0,-50), () => Confirm("Return to the main menu? Unsaved changes will be lost.", () => Navigate("MainMenuScene")));
            Button(overlay.transform, "Quit game", new Vector2(0,-110), () => Confirm("Quit the game? Unsaved changes will be lost.", () => { Time.timeScale = 1f; FindFirstObjectByType<SceneNavigation>().QuitGame(); }));
            message = Label(overlay.transform, "", new Vector2(0,-210), new Vector2(650,100));
            overlay.SetActive(false);
        }
        private void Update()
        {
            if (park != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!IsOpen && park.CloseManagementWindow()) return;
                Toggle();
            }
        }
        private void Toggle()
        {
            if (overlay == null) return;
            if (IsOpen) { overlay.SetActive(false); Time.timeScale = previousScale; }
            else { previousScale = Time.timeScale; Time.timeScale = 0f; overlay.SetActive(true); overlay.transform.SetAsLastSibling(); message.text = ""; }
        }
        private void Save()
        {
            if (ParkSaveStore.Exists) Confirm("Overwrite the current save?", Write);
            else Write();
        }
        private void Write()
        {
            try { ParkSaveStore.Write(park.CapturePark()); message.text = "Game saved."; }
            catch (Exception error) { message.text = "Save failed: " + error.Message; Debug.LogException(error); }
        }
        private void Load()
        {
            if (!ParkSaveStore.Exists) { message.text = "No saved game yet."; return; }
            if (park != null) Confirm("Load the saved game? Unsaved changes will be lost.", Read);
            else Read();
        }
        private void Read()
        {
            try
            {
                var data = ParkSaveStore.Read(); if (park != null) park.ValidateSave(data);
                ParkSaveStore.Pending = data; Navigate("GameScene");
            }
            catch (Exception error) { message.text = "Load failed: " + error.Message; Debug.LogException(error); }
        }
        private void Navigate(string scene) { Time.timeScale = 1f; SceneManager.LoadScene(scene); }
        private void Confirm(string question, UnityEngine.Events.UnityAction action)
        {
            var host = overlay != null ? overlay.transform : transform;
            var dialog = new GameObject("Confirm action", typeof(RectTransform), typeof(Image)); dialog.transform.SetParent(host,false);
            var r = (RectTransform)dialog.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            dialog.GetComponent<Image>().color = new Color(.03f,.05f,.07f,.99f);
            Label(dialog.transform, question, new Vector2(0,70),new Vector2(600,100));
            Button(dialog.transform,"Confirm",new Vector2(0,-10),() => { Destroy(dialog); action(); });
            Button(dialog.transform,"Cancel",new Vector2(0,-70),() => Destroy(dialog));
        }
        public static void ShowMessage(string value)
        {
            if (instance == null) return;
            if (!IsOpen && instance.park != null) instance.Toggle();
            if (instance.message != null) instance.message.text = value;
        }
        private Button Button(Transform parent, string title, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(title,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(280,48); rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = new Color(.19f,.28f,.31f);
            var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var label = Label(go.transform,title,Vector2.zero,new Vector2(270,46)); label.fontSize = 22;
            return button;
        }
        private Text Label(Transform parent, string title, Vector2 position, Vector2 size)
        {
            var go = new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); go.transform.SetParent(parent,false);
            var label = go.GetComponent<Text>(); label.font = font; label.text = title; label.fontSize = 24; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = size; rect.anchoredPosition = position; return label;
        }
        private void OnDestroy() { if (instance == this) { instance = null; Time.timeScale = 1f; } }
    }
}

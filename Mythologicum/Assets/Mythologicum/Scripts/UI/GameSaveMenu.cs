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
        public static void TogglePause() { if(!SceneTransition.IsLoading)instance?.Toggle(); }
        private BuildModeController park;
        private GameObject overlay;
        private GameObject confirmation;
        private Text message;
        private Font font;
        private float previousScale = 1f;
        public static void Attach(BuildModeController controller)
        {
            if (instance != null) return;
            instance = controller.gameObject.AddComponent<GameSaveMenu>(); instance.park = controller; instance.CreatePause();
            if (ParkSaveStore.Pending == null) ParkSaveStore.BeginNewPark();
            ParkAutosave.Attach(controller);
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
            if(!SceneTransition.IsLoading)Time.timeScale = 1f;
            var canvas = SceneTransition.GameplayCanvas; if (canvas == null) return;
            var component = canvas.gameObject.AddComponent<GameSaveMenu>(); instance = component;
            component.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var navigation = FindFirstObjectByType<SceneNavigation>();
            if(navigation==null)navigation=canvas.gameObject.AddComponent<SceneNavigation>();
            var menu=MainMenuPresentation.Create(canvas,navigation,component.Load);
            component.message = menu.Message;
            component.message.text = ParkSaveStore.LoadError ?? ""; ParkSaveStore.LoadError = null;
        }
        private void CreatePause()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = SceneTransition.GameplayCanvas;
            // The bottom toolbar now owns Pause; hide the older authored navigation button.
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.onClick.GetPersistentMethodName(i) == "MainMenu")
                    {
                        button.gameObject.SetActive(false);
                        break;
                    }
            overlay = new GameObject("Pause menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)overlay.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().color = new Color(.025f,.04f,.06f,.97f);
            var window=ManagementWidgets.Rect(overlay.transform,"Pause window",Vector2.zero,Vector2.one);
            window.anchorMin=window.anchorMax=window.pivot=new Vector2(.5f,.5f);
            var body=UIFramedPanel.Attach(window.gameObject,true,500).Content;
            float top=body.rect.height*.5f;
            Label(body, "ui.pause.paused", new Vector2(0,top-20), new Vector2(340,38));
            Button(body, "ui.pause.resume", new Vector2(0,top-75), Toggle);
            Button(body, "ui.pause.save_game", new Vector2(0,top-135), Save);
            Button(body, "ui.pause.load_game", new Vector2(0,top-195), Load);
            Button(body, "ui.pause.settings", new Vector2(0,top-255), () => SettingsPresentation.Open(canvas));
            Button(body, "ui.pause.quit_to_menu", new Vector2(0,top-315), () => Confirm("ui.pause.return_confirmation", () => Navigate("MainMenuScene")));
            Button(body, "ui.pause.quit_game", new Vector2(0,top-375), () => Confirm("ui.pause.quit_confirmation", () => { Time.timeScale = 1f; FindFirstObjectByType<SceneNavigation>().QuitGame(); }));
            message = Label(body, "", new Vector2(0,-top+35), new Vector2(330,60));
            overlay.SetActive(false);
            UIThemeApplicator.ApplyAll(canvas);
        }
        private void Update()
        {
            if (SceneTransition.IsLoading || GameInput.BlocksGameplayInput) return;
            if (Keyboard.current != null && (Keyboard.current.escapeKey.wasPressedThisFrame
                || (!SaveBrowserPresentation.EditingName && GameInput.Pressed(GameplayAction.Pause))))
            {
                if(SettingsPresentation.IsOpen){SettingsPresentation.CloseActive();return;}
                if (confirmation != null) { CloseConfirmation(); return; }
                if (SaveBrowserPresentation.IsOpen) { SaveBrowserPresentation.CloseActive(); return; }
                if(park==null)return;
                if (!IsOpen && park.CloseManagementWindow()) return;
                Toggle();
            }
        }
        private void Toggle()
        {
            if(SettingsPresentation.IsOpen){SettingsPresentation.CloseActive();return;}
            if (SaveBrowserPresentation.IsOpen) { SaveBrowserPresentation.CloseActive(); return; }
            if (overlay == null) return;
            if (IsOpen) { overlay.SetActive(false); Time.timeScale = previousScale; }
            else { previousScale = SceneTransition.IsLoading&&Time.timeScale<=0?SimulationSpeed.SelectedSpeed:Time.timeScale; Time.timeScale = 0f; overlay.SetActive(true); overlay.transform.SetAsLastSibling(); message.text = ""; }
        }
        private void Save()
        {
            if(SceneTransition.IsLoading)return;
            OpenBrowser(true);
        }
        private void Write()
        {
            try { ParkSaveStore.Write(park.CapturePark()); Loc.Set(message, "ui.save.saved"); }
            catch (Exception error) { Loc.Set(message, "ui.save.save_failed", error.Message); Debug.LogException(error); }
        }
        private void Load()
        {
            if(SettingsPresentation.IsOpen||SceneTransition.IsLoading)return;
            if (!ParkSaveStore.Exists) { Loc.Set(message, "ui.save.no_saved_game"); return; }
            OpenBrowser(false);
        }
        private void OpenBrowser(bool saving)
        {
            var canvas = SceneTransition.GameplayCanvas;
            SaveBrowserPresentation.Open(canvas, saving, LoadSlot, SaveSlot);
        }
        private void SaveSlot(string id, string name)
        {
            if (park == null || SceneTransition.IsLoading || !park.IsReady) return;
            Action write = () =>
            {
                try
                {
                    ParkSaveData data = park.CapturePark(); park.ValidateSave(data);
                    ParkSaveStore.WriteSlot(data, id, name);
                    park.GetComponent<ParkAutosave>()?.ResetSchedule();
                    SaveBrowserPresentation.ShowFeedback("ui.save.saved_named", ParkSaveStore.CleanName(name));
                    Loc.Set(message, "ui.save.saved");
                }
                catch (Exception error)
                {
                    SaveBrowserPresentation.ShowFeedback("ui.save.failed_preserved");
                    Debug.LogException(error);
                }
            };
            SaveSlotInfo existing = null;
            try { foreach (SaveSlotInfo slot in ParkSaveStore.ListSlots()) if (slot.Id == id) { existing = slot; break; } }
            catch (Exception error)
            {
                SaveBrowserPresentation.ShowFeedback("ui.save.folder_unavailable");
                Debug.LogWarning("Cannot inspect save slots: " + error.Message);
                return;
            }
            if (existing != null) Confirm("ui.save.overwrite_confirmation", () => write(), existing.Name);
            else write();
        }
        private void LoadSlot(SaveSlotInfo slot, bool backup)
        {
            if(SceneTransition.IsLoading)return;
            Action read = () =>
            {
                if(SceneTransition.IsLoading)return;
                try
                {
                    ParkSaveData data = ParkSaveStore.ReadSlot(slot.Id, backup);
                    if (park != null) park.ValidateSave(data);
                    ParkSaveStore.AdoptParkName(data);
                    ParkSaveStore.Pending = data;
                    SaveBrowserPresentation.CloseActive(); Navigate("GameScene");
                }
                catch (Exception error)
                {
                    SaveBrowserPresentation.ShowFeedback("ui.save.load_failed_recovery");
                    Debug.LogWarning("Load failed: " + error.Message);
                }
            };
            if (park != null) Confirm("ui.save.load_confirmation", () => read());
            else read();
        }
        private void Read()
        {
            if(SceneTransition.IsLoading)return;
            try
            {
                var data = ParkSaveStore.Read(); if (park != null) park.ValidateSave(data);
                ParkSaveStore.AdoptParkName(data);
                ParkSaveStore.Pending = data; Navigate("GameScene");
            }
            catch (Exception error) { Loc.Set(message, "ui.save.load_failed", error.Message); Debug.LogException(error); }
        }
        private void Navigate(string scene) { SceneTransition.Load(scene); }
        private void Confirm(string question, UnityEngine.Events.UnityAction action, params object[] arguments)
        {
            CloseConfirmation();
            var host = SaveBrowserPresentation.ActiveRoot ?? (overlay != null ? overlay.transform : transform);
            var dialog = new GameObject("Confirm action", typeof(RectTransform), typeof(Image)); dialog.transform.SetParent(host,false);
            confirmation = dialog;
            var r = (RectTransform)dialog.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            dialog.GetComponent<Image>().color = new Color(.03f,.05f,.07f,.99f);
            var window=ManagementWidgets.Rect(dialog.transform,"Confirmation window",Vector2.zero,Vector2.one);
            window.anchorMin=window.anchorMax=window.pivot=new Vector2(.5f,.5f);
            var body=UIFramedPanel.Attach(window.gameObject,false,900).Content;
            Label(body, question, new Vector2(0,70),new Vector2(600,100), arguments);
            Button(body,"ui.pause.confirm",new Vector2(0,-10),() => { CloseConfirmation(); action(); });
            Button(body,"ui.pause.cancel",new Vector2(0,-70),CloseConfirmation);
            UIThemeApplicator.ApplyAll(dialog.GetComponentInParent<Canvas>());
        }
        private void CloseConfirmation()
        {
            if (confirmation == null) return;
            confirmation.SetActive(false); Destroy(confirmation); confirmation = null;
        }
        public static void ShowMessage(string value)
        {
            if (instance == null) return;
            if (!IsOpen && instance.park != null) instance.Toggle();
            if (instance.message != null) instance.message.text = Loc.Resolve(value);
        }
        private Button Button(Transform parent, string title, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(Loc.InternalName(title),typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = new Vector2(280,48); rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = new Color(.19f,.28f,.31f);
            var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            var label = Label(go.transform,title,Vector2.zero,new Vector2(270,46)); label.fontSize = 22;
            return button;
        }
        private Text Label(Transform parent, string title, Vector2 position, Vector2 size, params object[] arguments)
        {
            var go = new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); go.transform.SetParent(parent,false);
            var label = go.GetComponent<Text>(); label.font = font; if (title.StartsWith("ui.", StringComparison.Ordinal)) Loc.Set(label, title, arguments); else label.text = title; label.fontSize = 24; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.supportRichText = false;
            var rect = label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.sizeDelta = size; rect.anchoredPosition = position; return label;
        }
        private void OnDestroy() { if (instance == this) { instance = null; if(!SceneTransition.IsLoading)Time.timeScale = 1f; } }
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    /// <summary>Owns the loading cover until scene activation and park preparation have both finished.</summary>
    [DefaultExecutionOrder(31000)]
    public sealed class SceneTransition : MonoBehaviour
    {
        private static SceneTransition instance;
        private bool busy, failed, preparing, prepared, fadeIn;
        private float progress, beganAt;
        private string target;
        private LoadingScreenPresentation overlay;
        private GameObject fallbackEventSystem;
        private CanvasGroup shieldedCanvas;
        private bool wasInteractable;
        public static bool IsLoading => instance != null && instance.busy;
        public static bool HasFailed => instance != null && instance.failed;
        public static float CurrentProgress => instance != null ? instance.progress : 0;
        public static string TargetScene => instance != null ? instance.target : null;
        public static LoadingScreenPresentation Overlay => instance != null ? instance.overlay : null;

        // The persistent cover must never become the parent of the incoming scene's interface.
        public static Canvas GameplayCanvas
        {
            get
            {
                var scene = SceneManager.GetActiveScene();
                foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                    if (canvas.enabled && canvas.gameObject.activeInHierarchy && canvas.isRootCanvas
                        && canvas.gameObject.scene == scene && canvas.GetComponentsInParent<Canvas>(true).Length == 1) return canvas;
                return null;
            }
        }
        public static bool Load(string scene)
        {
            if (IsLoading || string.IsNullOrEmpty(scene)) return false;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            { Debug.LogWarning("Scene transition rejected: scene is not available in the build: " + scene); return false; }
            var host = Create();
            host.StartFlow(scene, true);
            return true;
        }
        public static void BeginPreparation()
        {
            if (IsLoading)
            {
                instance.preparing = true;
                instance.SetProgress(.45f, "ui.loading.terrain");
                return;
            }
            var host = Create();
            host.StartFlow(SceneManager.GetActiveScene().name, false);
            host.preparing = true;
        }
        public static void ReportPreparation(float amount, string stageId)
        {
            if (!IsLoading || instance.failed) return;
            instance.preparing = true;
            instance.SetProgress(.45f + Mathf.Clamp01(amount) * .5f, stageId);
        }
        public static void CompletePreparation()
        {
            if (!IsLoading || instance.failed) return;
            instance.prepared = true;
            instance.SetProgress(.95f, "ui.loading.ready");
        }
        public static void FailPreparation(Exception error)
        {
            if (!IsLoading) BeginPreparation();
            instance.Fail(error);
        }
        private static SceneTransition Create()
        {
            var root = new GameObject("Scene transition");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SceneTransition>();
            instance.overlay = LoadingScreenPresentation.Create(root.transform);
            return instance;
        }
        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (busy) BlockSceneUI(); }
        private void BlockSceneUI()
        {
            var sceneCanvas = GameplayCanvas;
            if (sceneCanvas == null || (shieldedCanvas != null && shieldedCanvas.gameObject == sceneCanvas.gameObject)) return;
            RestoreSceneUI();
            shieldedCanvas = sceneCanvas.GetComponent<CanvasGroup>();
            if (shieldedCanvas == null) shieldedCanvas = sceneCanvas.gameObject.AddComponent<CanvasGroup>();
            wasInteractable = shieldedCanvas.interactable;
            shieldedCanvas.interactable = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }
        private void RestoreSceneUI()
        { if (shieldedCanvas != null) shieldedCanvas.interactable = wasInteractable; shieldedCanvas = null; }
        private void StartFlow(string scene, bool loadScene)
        {
            fadeIn = loadScene && !busy;
            target = scene; busy = true; failed = preparing = prepared = false;
            beganAt = Time.realtimeSinceStartup; progress = 0;
            GameInput.CancelCapture();
            BlockSceneUI();
            Time.timeScale = 0;
            overlay.Show(scene == "MainMenuScene" ? "ui.loading.returning_menu" : "ui.loading.opening_park", "ui.loading.scene");
            overlay.Alpha = fadeIn && LoadingScreenDefinition.Current.fadeInSeconds > 0 ? 0 : 1;
            StartCoroutine(Run(loadScene));
        }
        private IEnumerator Run(bool loadScene)
        {
            if (fadeIn)
            {
                float duration = LoadingScreenDefinition.Current.fadeInSeconds, fadeElapsed = 0;
                while (fadeElapsed < duration)
                { fadeElapsed += Time.unscaledDeltaTime; overlay.Alpha = Mathf.Clamp01(fadeElapsed / duration); yield return null; }
                overlay.Alpha = 1;
            }
            // Give the loading canvas two render opportunities before expensive activation or generation.
            yield return null;
            yield return null;
            AsyncOperation operation = null;
            if (loadScene)
            {
                Exception failure = null;
                try { operation = SceneManager.LoadSceneAsync(target, LoadSceneMode.Single); }
                catch (Exception error) { failure = error; }
                if (failure != null || operation == null) { Fail(failure); yield break; }
                while (!operation.isDone && !failed)
                {
                    if (!preparing) SetProgress(Mathf.Clamp01(operation.progress / .9f) * .45f, "ui.loading.scene");
                    yield return null;
                }
            }
            yield return null; // The menu's sceneLoaded callbacks and the park's Start coroutine may still be running.
            while (!failed && !DestinationReady())
            {
                if (Time.realtimeSinceStartup - beganAt > LoadingScreenDefinition.Current.preparationTimeoutSeconds)
                { Fail(new TimeoutException("The destination scene did not finish preparation.")); yield break; }
                yield return null;
            }
            if (failed) yield break;
            SetProgress(1, "ui.loading.ready");
            while (!failed && Time.realtimeSinceStartup - beganAt < LoadingScreenDefinition.Current.minimumVisibleSeconds) yield return null;
            if (failed) yield break;
            float fade = LoadingScreenDefinition.Current.fadeOutSeconds;
            float elapsed = 0;
            while (elapsed < fade && !failed)
            { elapsed += Time.unscaledDeltaTime; overlay.Alpha = 1 - Mathf.Clamp01(elapsed / fade); yield return null; }
            if (failed) yield break;
            Finish();
        }
        private bool DestinationReady()
        {
            if (SceneManager.GetActiveScene().name != target) return false;
            if (target == "GameScene") return prepared;
            return GameplayCanvas != null && UnityEngine.Object.FindFirstObjectByType<MainMenuPresentation>() != null;
        }
        private void SetProgress(float amount, string stageId)
        { progress = Mathf.Max(progress, Mathf.Clamp01(amount)); overlay.SetProgress(progress, stageId); }
        private void LateUpdate()
        {
            if (!busy) return;
            BlockSceneUI();
            Time.timeScale = 0;
            if (fallbackEventSystem != null && EventSystem.current != null && EventSystem.current.gameObject != fallbackEventSystem)
            { Destroy(fallbackEventSystem); fallbackEventSystem = null; }
        }
        private void Fail(Exception error)
        {
            if (failed) return;
            failed = true; Time.timeScale = 0;
            if (error != null) Debug.LogException(error);
            if (EventSystem.current == null)
            {
                fallbackEventSystem = new GameObject("Loading recovery input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                fallbackEventSystem.transform.SetParent(transform, false);
            }
            overlay.Alpha = 1;
            overlay.SetError("ui.loading.failed", ReturnToMenu);
        }
        private void ReturnToMenu()
        {
            StopAllCoroutines();
            ParkSaveStore.Pending = null; NewGameSetup.Pending = null;
            failed = preparing = prepared = false;
            StartFlow("MainMenuScene", true);
        }
        private void Finish()
        {
            float destinationSpeed = target == "GameScene" && GameSaveMenu.IsOpen ? 0
                : target == "GameScene" ? SimulationSpeed.SelectedSpeed : 1;
            overlay.gameObject.SetActive(false);
            RestoreSceneUI();
            busy = false;
            if (instance == this) instance = null;
            Time.timeScale = destinationSpeed;
            Destroy(gameObject);
        }
        private void OnDestroy() { RestoreSceneUI(); if (instance == this) instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => instance = null;
    }
}

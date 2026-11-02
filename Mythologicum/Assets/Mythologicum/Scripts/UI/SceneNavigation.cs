using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    public sealed class SceneNavigation : MonoBehaviour
    {
        public void StartGame()
        {
            if(SettingsPresentation.IsOpen)return;
            var canvas=FindFirstObjectByType<UnityEngine.Canvas>();
            if(canvas==null){Debug.LogError("New-game setup requires the main menu canvas.",this);return;}
            NewGameSetup.Open(canvas,StartConfiguredGame);
        }
        public void StartConfiguredGame(MapGenerationOptions options)
        {
            if(options==null||!options.Validate(out _))throw new System.ArgumentException("Invalid new-game map settings.",nameof(options));
            SettingsPresentation.CloseActive();GameSettings.Save();
            NewGameSetup.Pending=options.Clone();
            ParkSaveStore.Pending = null; Time.timeScale = 1f;
            SceneManager.LoadScene("GameScene");
        }
        public void MainMenu()
        {
            SettingsPresentation.CloseActive();GameSettings.Save();
            NewGameSetup.Pending=null;
            Time.timeScale = 1f; SceneManager.LoadScene("MainMenuScene");
        }

        public void OpenSettings()
        {
            var canvas=FindFirstObjectByType<UnityEngine.Canvas>();
            if(canvas==null){Debug.LogError("Settings require a UI canvas.",this);return;}
            SettingsPresentation.Open(canvas);
        }

        public void QuitGame()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

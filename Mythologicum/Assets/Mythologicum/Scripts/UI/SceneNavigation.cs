using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    public sealed class SceneNavigation : MonoBehaviour
    {
        public void StartGame()
        {
            var canvas=FindFirstObjectByType<UnityEngine.Canvas>();
            if(canvas==null){Debug.LogError("New-game setup requires the main menu canvas.",this);return;}
            NewGameSetup.Open(canvas,StartConfiguredGame);
        }
        public void StartConfiguredGame(MapGenerationOptions options)
        {
            if(options==null||!options.Validate(out _))throw new System.ArgumentException("Invalid new-game map settings.",nameof(options));
            NewGameSetup.Pending=options.Clone();
            ParkSaveStore.Pending = null; Time.timeScale = 1f;
            SceneManager.LoadScene("GameScene");
        }
        public void MainMenu()
        {
            NewGameSetup.Pending=null;
            Time.timeScale = 1f; SceneManager.LoadScene("MainMenuScene");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

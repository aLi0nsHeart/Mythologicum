using UnityEngine;

namespace Mythologicum
{
    public sealed class SceneNavigation : MonoBehaviour
    {
        public void StartGame()
        {
            if(SettingsPresentation.IsOpen||SceneTransition.IsLoading)return;
            var canvas=SceneTransition.GameplayCanvas;
            if(canvas==null){Debug.LogError("New-game setup requires the main menu canvas.",this);return;}
            NewGameSetup.Open(canvas,StartConfiguredGame);
        }
        public void StartConfiguredGame(MapGenerationOptions options)
        {
            if(SceneTransition.IsLoading)return;
            if(options==null||!options.Validate(out _))throw new System.ArgumentException("Invalid new-game map settings.",nameof(options));
            SettingsPresentation.CloseActive();GameSettings.Save();
            NewGameSetup.Pending=options.Clone();
            ParkSaveStore.Pending = null;
            SceneTransition.Load("GameScene");
        }
        public void MainMenu()
        {
            if(SceneTransition.IsLoading)return;
            SettingsPresentation.CloseActive();GameSettings.Save();
            NewGameSetup.Pending=null;
            SceneTransition.Load("MainMenuScene");
        }

        public void OpenSettings()
        {
            if(SceneTransition.IsLoading)return;
            var canvas=SceneTransition.GameplayCanvas;
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

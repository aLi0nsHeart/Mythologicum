using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    public sealed class SceneNavigation : MonoBehaviour
    {
        public void StartGame()
        {
            ParkSaveStore.Pending = null; Time.timeScale = 1f;
            SceneManager.LoadScene("GameScene");
        }
        public void MainMenu()
        {
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

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mythologicum
{
    public sealed class SceneNavigation : MonoBehaviour
    {
        public void StartGame() => SceneManager.LoadScene("GameScene");
        public void MainMenu() => SceneManager.LoadScene("MainMenuScene");

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

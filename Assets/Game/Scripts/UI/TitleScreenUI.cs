using Core.Timing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay.UI
{
    /// <summary>
    /// Title screen buttons. Wire Start to <see cref="StartGame"/> and Quit to <see cref="QuitGame"/>.
    /// The game scene must be in File &gt; Build Profiles (Scene List).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreenUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Name of the gameplay scene to load.")]
        private string gameSceneName = "Main";

        private void Start()
        {
            // Coming back from a paused game: make sure time runs on the title screen.
            GameTime.SetPaused(false);
        }

        /// <summary>Loads the gameplay scene. A new run starts automatically there.</summary>
        public void StartGame()
        {
            GameTime.SetPaused(false);
            SceneManager.LoadScene(gameSceneName);
        }

        /// <summary>Quits the built game, or stops Play mode in the editor.</summary>
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

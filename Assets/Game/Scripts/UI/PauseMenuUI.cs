using Core.Timing;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Gameplay.UI
{
    /// <summary>
    /// Pause menu toggled with Escape (or Start on a gamepad). Buttons: Resume (<see cref="Resume"/>),
    /// Restart (<see cref="RestartRun"/>, raises <see cref="onRestart"/> — wire to <c>StageManager.RestartRun</c>)
    /// and Title (<see cref="QuitToTitle"/>).
    /// </summary>
    /// <remarks>
    /// Won't open over the Stage Clear or Game Over panels: it only opens when gameplay isn't already paused.
    /// </remarks>
    public sealed class PauseMenuUI : ModalPanel
    {
        [SerializeField]
        private string titleSceneName = "Title";

        [SerializeField]
        [Tooltip("Raised by the Restart button after unpausing. Wire to StageManager.RestartRun.")]
        private UnityEvent onRestart = new UnityEvent();

        private bool isOpen;

        /// <summary>True while the menu is open.</summary>
        public bool IsOpen => isOpen;

        private void LateUpdate()
        {
            bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
            // Esc closes the controls panel first; it shouldn't also resume the game on the same press.
            if (!pressed || ControlsPanelUI.AnyOpen || ControlsPanelUI.ClosedThisFrame)
            {
                return;
            }

            if (isOpen)
            {
                Resume();
            }
            else if (!GameTime.IsPaused)
            {
                Open();
            }
        }

        private void OnDestroy()
        {
            if (isOpen)
            {
                GameTime.SetPaused(false);
            }
        }

        /// <summary>Opens the menu and pauses the game.</summary>
        public void Open()
        {
            isOpen = true;
            GameTime.SetPaused(true);
            Show();
        }

        /// <summary>Closes the menu and resumes.</summary>
        public void Resume()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            HideImmediate();
            GameTime.SetPaused(false);
        }

        /// <summary>Closes the menu and restarts the run from stage 1.</summary>
        public void RestartRun()
        {
            Resume();
            onRestart.Invoke();
        }

        /// <summary>Unpauses and loads the title scene.</summary>
        public void QuitToTitle()
        {
            isOpen = false;
            GameTime.SetPaused(false);
            SceneManager.LoadScene(titleSceneName);
        }
    }
}

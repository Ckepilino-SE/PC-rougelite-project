using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.UI
{
    /// <summary>
    /// "How to Play" panel listing the controls. Opened from the title screen and the pause menu with
    /// <see cref="Open"/>; closed with its Back button, Esc, or the gamepad's B/East button.
    /// </summary>
    public sealed class ControlsPanelUI : ModalPanel
    {
        private static int lastClosedFrame = -1;

        /// <summary>True while any controls panel is open. The pause menu ignores Esc while this is true.</summary>
        public static bool AnyOpen { get; private set; }

        /// <summary>True on the frame a panel was closed with Esc, so the same key press doesn't also unpause.</summary>
        public static bool ClosedThisFrame => lastClosedFrame == Time.frameCount;

        private bool isOpen;

        /// <summary>Shows the panel.</summary>
        public void Open()
        {
            isOpen = true;
            AnyOpen = true;
            Show();
        }

        /// <summary>Hides the panel.</summary>
        public void Close()
        {
            if (!isOpen)
            {
                return;
            }

            isOpen = false;
            AnyOpen = false;
            lastClosedFrame = Time.frameCount;
            HideImmediate();
        }

        private void OnDisable()
        {
            if (isOpen)
            {
                isOpen = false;
                AnyOpen = false;
            }
        }

        private void LateUpdate()
        {
            if (!isOpen)
            {
                return;
            }

            bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (back)
            {
                Close();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyOpen = false;
            lastClosedFrame = -1;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>
    /// Base for full-screen modal panels: hidden via its <see cref="CanvasGroup"/> (never deactivated, so
    /// derived classes keep receiving events), shown with a fade-in that runs on unscaled time and therefore
    /// animates while gameplay is paused with <c>Time.timeScale = 0</c>.
    /// </summary>
    /// <remarks>
    /// The panel only becomes clickable once the fade completes, so a click held over from gameplay can't
    /// trigger a button the instant it appears.
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class ModalPanel : MonoBehaviour
    {
        [Header("Modal")]
        [SerializeField, Min(0f)]
        [Tooltip("Fade-in duration in real-time seconds.")]
        private float fadeDuration = 0.35f;

        [SerializeField]
        [Tooltip("Optional. Selected when the fade finishes, for keyboard/gamepad navigation.")]
        private Selectable firstSelected;

        private CanvasGroup canvasGroup;
        private bool isFadingIn;

        /// <summary>True while the panel is visible or fading in.</summary>
        public bool IsShown { get; private set; }

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            HideImmediate();
        }

        /// <summary>Starts the fade-in from transparent.</summary>
        protected void Show()
        {
            IsShown = true;
            isFadingIn = true;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = true;
        }

        /// <summary>Hides the panel instantly and makes it ignore input.</summary>
        protected void HideImmediate()
        {
            IsShown = false;
            isFadingIn = false;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            if (!isFadingIn)
            {
                return;
            }

            float step = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, 1f, step);

            if (canvasGroup.alpha >= 1f)
            {
                isFadingIn = false;
                canvasGroup.interactable = true;
                if (firstSelected != null)
                {
                    firstSelected.Select();
                }
            }
        }
    }
}

using Gameplay.Stages;
using UnityEngine;

namespace Gameplay.UI
{
    /// <summary>
    /// A small controls reminder along the bottom of the screen, shown at the start of every run (first stage
    /// only) and faded out after a few seconds. Purely visual; runs on unscaled time.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class ControlsHintUI : MonoBehaviour
    {
        [SerializeField]
        private StageEventChannelSO stageStartedChannel;

        [SerializeField]
        [Tooltip("The hint shows when this catalog's first stage starts.")]
        private StageCatalog catalog;

        [SerializeField, Min(0f)]
        private float holdSeconds = 9f;

        [SerializeField, Min(0.01f)]
        private float fadeSeconds = 1f;

        private CanvasGroup group;
        private float shownAt = -1000f;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void OnEnable()
        {
            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }
        }

        private void OnDisable()
        {
            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised -= HandleStageStarted;
            }
        }

        private void HandleStageStarted(StageData stage)
        {
            if (catalog == null || (catalog.Count > 0 && catalog.GetStage(0) == stage))
            {
                shownAt = Time.unscaledTime;
            }
        }

        private void Update()
        {
            float t = Time.unscaledTime - shownAt;
            group.alpha = t < holdSeconds ? 1f : Mathf.Clamp01(1f - (t - holdSeconds) / fadeSeconds);
        }
    }
}

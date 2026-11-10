using Gameplay.Stages;
using TMPro;
using UnityEngine;

namespace Gameplay.UI
{
    /// <summary>
    /// Big text that fades in and out when a stage starts: "Act 1 - Stage 3", "Act 1 - BOSS", "FINAL BOSS",
    /// with the stage title underneath. Purely visual; runs on unscaled time.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class StageBannerUI : MonoBehaviour
    {
        [SerializeField]
        private StageEventChannelSO stageStartedChannel;

        [SerializeField]
        private TMP_Text labelText;

        [SerializeField]
        [Tooltip("Optional. Shows the stage title.")]
        private TMP_Text titleText;

        [SerializeField]
        private Color normalColor = Color.white;

        [SerializeField]
        private Color bossColor = new Color(1f, 0.3f, 0.25f);

        [SerializeField]
        private Color finalBossColor = new Color(0.8f, 0.35f, 1f);

        [SerializeField, Min(0f)]
        private float fadeIn = 0.25f;

        [SerializeField, Min(0f)]
        private float hold = 1.4f;

        [SerializeField, Min(0f)]
        private float fadeOut = 0.5f;

        private CanvasGroup group;
        private float shownAt = -100f;

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
            if (stage == null)
            {
                return;
            }

            Color color = stage.Kind == StageKind.FinalBoss ? finalBossColor
                : stage.Kind == StageKind.Boss ? bossColor
                : normalColor;

            if (labelText != null)
            {
                labelText.text = stage.DisplayLabel;
                labelText.color = color;
            }

            if (titleText != null)
            {
                titleText.text = stage.StageTitle;
            }

            shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float alpha;
            if (t < fadeIn)
            {
                alpha = fadeIn > 0f ? t / fadeIn : 1f;
            }
            else if (t < fadeIn + hold)
            {
                alpha = 1f;
            }
            else
            {
                alpha = fadeOut > 0f ? 1f - (t - fadeIn - hold) / fadeOut : 0f;
            }

            group.alpha = Mathf.Clamp01(alpha);
        }
    }
}

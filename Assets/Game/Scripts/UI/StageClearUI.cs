using Core.Events;
using Gameplay.Stages;
using TMPro;
using UnityEngine;

namespace Gameplay.UI
{
    /// <summary>
    /// "Stage Cleared!" modal. Fades in when the stage-completed channel fires and hides when the next stage
    /// starts or the run ends.
    /// </summary>
    /// <remarks>
    /// The Continue button is wired in the Inspector to <c>StageManager.LoadNextStage</c>; this component only
    /// handles presentation and never references the manager.
    /// </remarks>
    public sealed class StageClearUI : ModalPanel
    {
        [Header("Channels")]
        [SerializeField]
        [Tooltip("Shows the panel.")]
        private StageEventChannelSO stageCompletedChannel;

        [SerializeField]
        [Tooltip("Hides the panel when the next stage begins.")]
        private StageEventChannelSO stageStartedChannel;

        [SerializeField]
        [Tooltip("Hides the panel when the run ends (Continue on the final stage).")]
        private GameOverEventChannelSO gameOverChannel;

        [Header("Text")]
        [SerializeField]
        private TMP_Text headerText;

        [SerializeField]
        private TMP_Text stageText;

        [SerializeField]
        private string headerMessage = "Stage Cleared!";

        [SerializeField]
        private string bossHeaderMessage = "Boss Defeated!";

        private void OnEnable()
        {
            if (stageCompletedChannel != null)
            {
                stageCompletedChannel.OnEventRaised += HandleStageCompleted;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }

            if (gameOverChannel != null)
            {
                gameOverChannel.OnGameOver += HandleGameOver;
            }
        }

        private void OnDisable()
        {
            if (stageCompletedChannel != null)
            {
                stageCompletedChannel.OnEventRaised -= HandleStageCompleted;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised -= HandleStageStarted;
            }

            if (gameOverChannel != null)
            {
                gameOverChannel.OnGameOver -= HandleGameOver;
            }
        }

        private void HandleStageCompleted(StageData stage)
        {
            if (headerText != null)
            {
                headerText.text = stage != null && stage.IsBossStage ? bossHeaderMessage : headerMessage;
            }

            if (stageText != null)
            {
                stageText.text = stage != null
                    ? $"{stage.DisplayLabel}: {stage.StageTitle}"
                    : string.Empty;
            }

            Show();
        }

        private void HandleStageStarted(StageData stage)
        {
            HideImmediate();
        }

        private void HandleGameOver(GameOverReason reason)
        {
            HideImmediate();
        }
    }
}

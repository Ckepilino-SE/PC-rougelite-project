using Core.Events;
using Gameplay.Stages;
using TMPro;
using UnityEngine;

namespace Gameplay.UI
{
    /// <summary>
    /// End-of-run modal for both outcomes: "Victory" after the last stage, "Game Over" on player death.
    /// Fades in on the game-over channel and hides when a new run's first stage starts.
    /// </summary>
    /// <remarks>
    /// The Restart button is wired in the Inspector to <c>StageManager.RestartRun</c>.
    /// </remarks>
    public sealed class GameOverUI : ModalPanel
    {
        [Header("Channels")]
        [SerializeField]
        [Tooltip("Shows the panel.")]
        private GameOverEventChannelSO gameOverChannel;

        [SerializeField]
        [Tooltip("Hides the panel when a stage starts (after Restart).")]
        private StageEventChannelSO stageStartedChannel;

        [Header("Text")]
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text detailText;

        [SerializeField]
        private string victoryTitle = "Victory!";

        [SerializeField]
        private string victoryDetail = "You defeated the Final Boss. The run is complete!";

        [SerializeField]
        private string defeatTitle = "Game Over";

        [SerializeField]
        private string defeatDetail = "You were overwhelmed.";

        private void OnEnable()
        {
            if (gameOverChannel != null)
            {
                gameOverChannel.OnGameOver += HandleGameOver;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }
        }

        private void OnDisable()
        {
            if (gameOverChannel != null)
            {
                gameOverChannel.OnGameOver -= HandleGameOver;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised -= HandleStageStarted;
            }
        }

        private void HandleGameOver(GameOverReason reason)
        {
            bool won = reason == GameOverReason.AllStagesCleared;

            if (titleText != null)
            {
                titleText.text = won ? victoryTitle : defeatTitle;
            }

            if (detailText != null)
            {
                detailText.text = won ? victoryDetail : defeatDetail;
            }

            Show();
        }

        private void HandleStageStarted(StageData stage)
        {
            HideImmediate();
        }
    }
}

using Core.Events;
using Core.References;
using Gameplay.Stages;
using TMPro;
using UnityEngine;

namespace Gameplay.Tempo
{
    /// <summary>
    /// Run score: every kill's points times the player's tempo multiplier. Resets when the first stage of a run
    /// starts. Shows the score on a HUD text and the final score on the Game Over / Victory panel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreKeeper : MonoBehaviour
    {
        [SerializeField]
        private EnemyKilledEventChannelSO enemyKilledChannel;

        [SerializeField]
        private StageEventChannelSO stageStartedChannel;

        [SerializeField]
        private GameOverEventChannelSO gameOverChannel;

        [SerializeField]
        [Tooltip("Used to detect the start of a new run (its first stage).")]
        private StageCatalog catalog;

        [SerializeField]
        [Tooltip("The player's anchor; its TempoMeter multiplies points.")]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        private TMP_Text scoreText;

        [SerializeField]
        [Tooltip("Optional. Text on the Game Over / Victory panel.")]
        private TMP_Text finalScoreText;

        private Transform cachedPlayer;
        private TempoMeter tempo;
        private int score;

        /// <summary>Current score.</summary>
        public int Score => score;

        private void OnEnable()
        {
            if (enemyKilledChannel != null)
            {
                enemyKilledChannel.OnEnemyKilled += HandleKilled;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }

            if (gameOverChannel != null)
            {
                gameOverChannel.OnGameOver += HandleGameOver;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (enemyKilledChannel != null)
            {
                enemyKilledChannel.OnEnemyKilled -= HandleKilled;
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

        private void HandleKilled(int points)
        {
            score += Mathf.RoundToInt(points * GetMultiplier());
            Refresh();
        }

        private void HandleStageStarted(StageData stage)
        {
            if (catalog != null && catalog.Count > 0 && catalog.GetStage(0) == stage)
            {
                score = 0;
                if (TryGetTempo(out TempoMeter meter))
                {
                    meter.ResetTempo();
                }

                Refresh();
            }
        }

        private void HandleGameOver(GameOverReason reason)
        {
            if (finalScoreText != null)
            {
                finalScoreText.SetText("Score: {0}", score);
            }
        }

        private void Refresh()
        {
            if (scoreText != null)
            {
                scoreText.SetText("{0}", score);
            }
        }

        private float GetMultiplier()
        {
            return TryGetTempo(out TempoMeter meter) ? meter.Multiplier : 1f;
        }

        private bool TryGetTempo(out TempoMeter meter)
        {
            if (playerAnchor != null && playerAnchor.IsSet && playerAnchor.Value != cachedPlayer)
            {
                cachedPlayer = playerAnchor.Value;
                tempo = cachedPlayer.GetComponentInChildren<TempoMeter>();
            }

            meter = tempo;
            return meter != null;
        }
    }
}

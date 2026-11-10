using System;
using Core.Events;
using Core.Pooling;
using Core.References;
using Core.Timing;
using Gameplay.Combat;
using Gameplay.Upgrades;
using Gameplay.Waves;
using UnityEngine;

namespace Gameplay.Stages
{
    /// <summary>
    /// Top-level run flow: walks through a <see cref="StageCatalog"/>, pauses between stages, and ends the run
    /// on victory or player death.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Flow</b>: <see cref="StartRun"/> → stage plays (spawning delegated to <see cref="WaveDirector"/>) →
    /// stage cleared: pause and raise <see cref="OnStageCompleted"/> → UI calls <see cref="LoadNextStage"/> →
    /// next stage, or <see cref="OnGameWon"/> when none remain. Player death at any point pauses and raises
    /// <see cref="OnGameLost"/>; <see cref="RestartRun"/> starts over from the first stage.
    /// </para>
    /// <para>
    /// <b>Pausing</b> goes through <see cref="GameTime.SetPaused"/> (time scale 0): physics, enemy AI, wave timers
    /// and projectiles all freeze.
    /// UI that animates while paused must use unscaled time.
    /// </para>
    /// <para>
    /// <b>Decoupling</b>: the player is found through a <see cref="TransformAnchorSO"/>, player death is heard on
    /// the <see cref="PlayerDamagedEventChannelSO"/>, and every event is mirrored on a ScriptableObject channel
    /// so UI and audio never reference this component.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class StageManager : MonoBehaviour
    {
        /// <summary>Run lifecycle.</summary>
        public enum RunState
        {
            /// <summary><see cref="StartRun"/> has not been called.</summary>
            NotStarted,

            /// <summary>A stage is in progress.</summary>
            Playing,

            /// <summary>Paused on the stage-clear screen, waiting for <see cref="LoadNextStage"/>.</summary>
            StageCleared,

            /// <summary>Paused after the player won or died, waiting for <see cref="RestartRun"/>.</summary>
            GameOver,
        }

        [Header("Stages")]
        [SerializeField]
        private StageCatalog catalog;

        [SerializeField]
        private WaveDirector waveDirector;

        [SerializeField]
        [Tooltip("Begin the run automatically when the scene starts.")]
        private bool startOnStart = true;

        [Header("Player")]
        [SerializeField]
        [Tooltip("Anchor the player registers itself in.")]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        [Tooltip("Where the player is moved at the start of each stage. Leave empty to keep the player where they are.")]
        private Transform playerSpawnPoint;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction of max health restored when moving to the next stage.")]
        private float healthRestoreFraction = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction of max health restored after beating a boss (replaces the normal amount).")]
        private float bossHealthRestoreFraction = 1f;

        [SerializeField]
        [Tooltip("Beating the Final Boss (or the last stage) wins the run straight away, without the Stage Clear panel.")]
        private bool skipClearScreenOnFinalStage = true;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds of invulnerability granted to the player when a stage starts.")]
        private float stageStartInvulnerability = 1.5f;

        [Header("Listened Channels")]
        [SerializeField]
        [Tooltip("Health reaching 0 on this channel ends the run.")]
        private PlayerDamagedEventChannelSO playerDamagedChannel;

        [Header("Broadcast Channels")]
        [SerializeField]
        private StageEventChannelSO stageStartedChannel;

        [SerializeField]
        private StageEventChannelSO stageCompletedChannel;

        [SerializeField]
        [Tooltip("Raised with AllStagesCleared or PlayerDied.")]
        private GameOverEventChannelSO gameOverChannel;

        private int stageIndex;

        /// <summary>Raised when a stage begins playing.</summary>
        public event Action<StageData> OnStageStarted;

        /// <summary>Raised when every wave and enemy in a stage is cleared. Gameplay is paused at this point.</summary>
        public event Action<StageData> OnStageCompleted;

        /// <summary>Raised by <see cref="LoadNextStage"/> when no stages remain.</summary>
        public event Action OnGameWon;

        /// <summary>Raised when the player's health reaches zero.</summary>
        public event Action OnGameLost;

        /// <summary>Current run state.</summary>
        public RunState State { get; private set; }

        /// <summary>Zero-based index of the current stage in the catalog.</summary>
        public int CurrentStageIndex => stageIndex;

        /// <summary>The current stage, or null before the run starts.</summary>
        public StageData CurrentStage { get; private set; }

        /// <summary>True if the current stage is the last one in the catalog.</summary>
        public bool IsFinalStage => catalog != null && stageIndex >= catalog.Count - 1;

        /// <summary>True while gameplay is paused by this manager.</summary>
        public bool IsPaused => State == RunState.StageCleared || State == RunState.GameOver;

        private void OnEnable()
        {
            if (waveDirector != null)
            {
                waveDirector.StageCleared += HandleStageCleared;
            }

            if (playerDamagedChannel != null)
            {
                playerDamagedChannel.OnPlayerDamaged += HandlePlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (waveDirector != null)
            {
                waveDirector.StageCleared -= HandleStageCleared;
            }

            if (playerDamagedChannel != null)
            {
                playerDamagedChannel.OnPlayerDamaged -= HandlePlayerDamaged;
            }
        }

        private void Start()
        {
            if (startOnStart)
            {
                StartRun();
            }
        }

        private void OnDestroy()
        {
            // Never leave the game frozen if this object goes away while paused (e.g. scene change).
            GameTime.SetPaused(false);
        }

        /// <summary>Starts the run from the first stage with the player at full health.</summary>
        public void StartRun()
        {
            if (catalog == null || catalog.Count == 0 || waveDirector == null)
            {
                Debug.LogError($"{nameof(StageManager)} on '{name}' needs a Stage Catalog with at least one stage and a Wave Director.", this);
                return;
            }

            ClearField();
            stageIndex = 0;
            PreparePlayer(fullHeal: true, 1f);
            BeginStage();
        }

        /// <summary>
        /// Advances from the stage-clear screen: clears the field, moves and partly heals the player, unpauses,
        /// and starts the next stage. If no stages remain, raises <see cref="OnGameWon"/> instead.
        /// Wire the Stage Clear panel's Continue button to this.
        /// </summary>
        public void LoadNextStage()
        {
            if (State != RunState.StageCleared)
            {
                return;
            }

            ClearField();

            if (stageIndex + 1 >= catalog.Count)
            {
                WinRun();
                return;
            }

            bool beatBoss = CurrentStage != null && CurrentStage.IsBossStage;
            stageIndex++;
            PreparePlayer(fullHeal: false, beatBoss ? bossHealthRestoreFraction : healthRestoreFraction);
            BeginStage();
        }

        /// <summary>Restarts from the first stage. Wire the Game Over panel's Restart button to this.</summary>
        public void RestartRun()
        {
            StartRun();
        }

        private void WinRun()
        {
            waveDirector.Stop();
            State = RunState.GameOver;
            GameTime.SetPaused(true);
            Debug.Log("[StageManager] Final stage cleared. Run won.", this);

            OnGameWon?.Invoke();
            if (gameOverChannel != null)
            {
                gameOverChannel.Raise(GameOverReason.AllStagesCleared);
            }
        }

        private void BeginStage()
        {
            CurrentStage = catalog.GetStage(stageIndex);
            State = RunState.Playing;
            GameTime.SetPaused(false);

            waveDirector.BeginStage(CurrentStage);

            string title = CurrentStage != null ? CurrentStage.StageTitle : "(missing)";
            Debug.Log($"[StageManager] Stage {stageIndex + 1}/{catalog.Count} started: {title}.", this);

            OnStageStarted?.Invoke(CurrentStage);
            if (stageStartedChannel != null)
            {
                stageStartedChannel.Raise(CurrentStage);
            }
        }

        private void HandleStageCleared()
        {
            if (State != RunState.Playing)
            {
                return;
            }

            if (skipClearScreenOnFinalStage && (IsFinalStage || (CurrentStage != null && CurrentStage.Kind == StageKind.FinalBoss)))
            {
                WinRun();
                return;
            }

            State = RunState.StageCleared;
            GameTime.SetPaused(true);

            OnStageCompleted?.Invoke(CurrentStage);
            if (stageCompletedChannel != null)
            {
                stageCompletedChannel.Raise(CurrentStage);
            }
        }

        private void HandlePlayerDamaged(int current, int max)
        {
            if (current > 0 || State != RunState.Playing)
            {
                return;
            }

            State = RunState.GameOver;
            waveDirector.Stop();
            GameTime.SetPaused(true);
            Debug.Log("[StageManager] Player died. Game over.", this);

            OnGameLost?.Invoke();
            if (gameOverChannel != null)
            {
                gameOverChannel.Raise(GameOverReason.PlayerDied);
            }
        }

        /// <summary>Stops spawning and returns every pooled object in the game (enemies, projectiles, effects).</summary>
        private void ClearField()
        {
            waveDirector.Stop();
            PoolRegistry.ReturnAllActive();
        }

        private void PreparePlayer(bool fullHeal, float restoreFraction)
        {
            if (playerAnchor == null || !playerAnchor.IsSet)
            {
                return;
            }

            Transform player = playerAnchor.Value;

            if (playerSpawnPoint != null)
            {
                Vector2 spawn = playerSpawnPoint.position;
                if (player.TryGetComponent(out Rigidbody2D body))
                {
                    body.position = spawn;
                    body.linearVelocity = Vector2.zero;
                }

                player.position = new Vector3(spawn.x, spawn.y, player.position.z);
            }

            // A new run starts with no upgrades.
            if (fullHeal && player.TryGetComponent(out PlayerUpgrades upgrades))
            {
                upgrades.ClearAll();
            }

            if (player.TryGetComponent(out Health health))
            {
                if (fullHeal)
                {
                    health.ResetHealth();
                }
                else
                {
                    health.HealFraction(restoreFraction);
                }

                health.GrantInvulnerability(stageStartInvulnerability);
            }
        }
    }
}

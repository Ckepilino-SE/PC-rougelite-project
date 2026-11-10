using System;
using UnityEngine;

namespace Gameplay.Enemies.States
{
    /// <summary>Inspector-tunable values for <see cref="AttackState"/>.</summary>
    [Serializable]
    public sealed class AttackSettings
    {
        [SerializeField, Min(0f)]
        [Tooltip("Seconds the enemy stands still, winding up, before lunging. The player's window to dodge.")]
        private float telegraphDuration = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("Lunge speed in units per second.")]
        private float lungeSpeed = 12f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the lunge lasts.")]
        private float lungeDuration = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds after the attack ends before the enemy may attack again.")]
        private float cooldown = 1.25f;

        /// <summary>Telegraph (wind-up) duration in seconds.</summary>
        public float TelegraphDuration => telegraphDuration;

        /// <summary>Lunge speed in units per second.</summary>
        public float LungeSpeed => lungeSpeed;

        /// <summary>Lunge duration in seconds.</summary>
        public float LungeDuration => lungeDuration;

        /// <summary>Post-attack cooldown in seconds.</summary>
        public float Cooldown => cooldown;
    }

    /// <summary>Sub-phase of <see cref="AttackState"/>.</summary>
    public enum AttackPhase
    {
        /// <summary>Not attacking.</summary>
        None,

        /// <summary>Standing still, winding up.</summary>
        Telegraph,

        /// <summary>Dashing along the locked direction. Contact damage is live.</summary>
        Lunge,
    }

    /// <summary>
    /// Telegraph → lunge → back to <see cref="ChaseState"/> with a cooldown.
    /// </summary>
    /// <remarks>
    /// The lunge direction is locked when the telegraph starts, so a player who moves during the wind-up
    /// dodges it. The cooldown is applied in <see cref="Exit"/>, so it also applies when the attack is
    /// interrupted (e.g. by the enemy being returned to the pool).
    /// </remarks>
    public sealed class AttackState : IEnemyState
    {
        private readonly EnemyController enemy;
        private readonly AttackSettings settings;

        private Vector2 lungeDirection;
        private float phaseEndTime;

        /// <summary>Creates the state. Called once by <see cref="EnemyController"/> in <c>Awake</c>.</summary>
        public AttackState(EnemyController enemy, AttackSettings settings)
        {
            this.enemy = enemy;
            this.settings = settings;
        }

        /// <summary>Current sub-phase. Visual components can poll this to show the telegraph.</summary>
        public AttackPhase Phase { get; private set; }

        /// <summary>True while the lunge is in progress.</summary>
        public bool IsLunging => Phase == AttackPhase.Lunge;

        /// <inheritdoc />
        public void Enter()
        {
            Rigidbody2D body = enemy.Body;

            lungeDirection = enemy.TryGetTargetPosition(out Vector2 target)
                ? (target - body.position).normalized
                : Vector2.zero;

            Phase = AttackPhase.Telegraph;
            phaseEndTime = Time.time + settings.TelegraphDuration;
            body.linearVelocity = Vector2.zero;
        }

        /// <inheritdoc />
        public void Update()
        {
            Rigidbody2D body = enemy.Body;

            switch (Phase)
            {
                case AttackPhase.Telegraph:
                    body.linearVelocity = Vector2.zero;
                    if (Time.time >= phaseEndTime)
                    {
                        Phase = AttackPhase.Lunge;
                        phaseEndTime = Time.time + settings.LungeDuration;
                    }

                    break;

                case AttackPhase.Lunge:
                    // Re-applied every tick so collisions and damping can't stall the lunge.
                    body.linearVelocity = lungeDirection * (settings.LungeSpeed * enemy.SpeedMultiplier);
                    if (Time.time >= phaseEndTime)
                    {
                        enemy.StateMachine.ChangeState(enemy.ChaseState);
                    }

                    break;
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
            Phase = AttackPhase.None;
            enemy.NextAttackTime = Time.time + settings.Cooldown;
        }
    }
}

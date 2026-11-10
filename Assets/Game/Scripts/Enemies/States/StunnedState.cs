using UnityEngine;

namespace Gameplay.Enemies.States
{
    /// <summary>
    /// Knocked back and helpless for a moment (e.g. after the player parries a lunge), then back to
    /// <see cref="ChaseState"/>. The enemy cannot attack until the stun ends.
    /// </summary>
    /// <remarks>
    /// Duration and knockback come from <see cref="Begin"/> rather than Inspector settings, so whatever causes
    /// the stun decides how strong it is.
    /// </remarks>
    public sealed class StunnedState : IEnemyState
    {
        private const float KnockbackDamping = 4f;

        private readonly EnemyController enemy;

        private float endTime;
        private Vector2 knockbackVelocity;

        /// <summary>Creates the state. Called once by <see cref="EnemyController"/> in <c>Awake</c>.</summary>
        public StunnedState(EnemyController enemy)
        {
            this.enemy = enemy;
        }

        /// <summary>Sets up the next stun. Call before switching to this state.</summary>
        public void Begin(float duration, Vector2 knockback)
        {
            endTime = Time.time + Mathf.Max(0f, duration);
            knockbackVelocity = knockback;
        }

        /// <inheritdoc />
        public void Enter()
        {
            enemy.Body.linearVelocity = knockbackVelocity;
        }

        /// <inheritdoc />
        public void Update()
        {
            // Knockback slides to a stop.
            Rigidbody2D body = enemy.Body;
            body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero, KnockbackDamping * knockbackVelocity.magnitude * Time.deltaTime);

            if (Time.time >= endTime)
            {
                enemy.NotifyStunRecovered();
                enemy.StateMachine.ChangeState(enemy.ChaseState);
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
        }
    }
}

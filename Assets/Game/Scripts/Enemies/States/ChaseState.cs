using System;
using UnityEngine;

namespace Gameplay.Enemies.States
{
    /// <summary>Inspector-tunable values for <see cref="ChaseState"/>.</summary>
    [Serializable]
    public sealed class ChaseSettings
    {
        [SerializeField, Min(0f)]
        [Tooltip("Top chase speed in units per second.")]
        private float maxSpeed = 3f;

        [SerializeField, Min(0f)]
        [Tooltip("How quickly velocity turns toward the desired direction, in units per second squared.")]
        private float acceleration = 20f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance to the target at which the enemy switches to its attack.")]
        private float attackRange = 1.75f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance this enemy tries to hold from the target. 0 = always close in (melee). " +
                 "For ranged enemies, set a little below Attack Range so they stop and shoot instead of crowding.")]
        private float preferredDistance;

        [SerializeField, Min(0f)]
        [Tooltip("Width of the band around Preferred Distance where the enemy holds position instead of adjusting.")]
        private float distanceTolerance = 0.75f;

        /// <summary>Top chase speed in units per second.</summary>
        public float MaxSpeed => maxSpeed;

        /// <summary>Steering acceleration in units per second squared.</summary>
        public float Acceleration => acceleration;

        /// <summary>Distance at which the attack begins.</summary>
        public float AttackRange => attackRange;

        /// <summary>Distance held from the target; 0 means close in.</summary>
        public float PreferredDistance => preferredDistance;

        /// <summary>Dead band around <see cref="PreferredDistance"/>.</summary>
        public float DistanceTolerance => distanceTolerance;
    }

    /// <summary>
    /// Steers toward the target with simple seek steering, and hands off to the enemy's
    /// <see cref="EnemyController.ActiveAttackState"/> once in range and off cooldown. With a
    /// <see cref="ChaseSettings.PreferredDistance"/> set, it backs away when too close (kiting).
    /// With no target, brakes to a stop.
    /// </summary>
    public sealed class ChaseState : IEnemyState
    {
        private const float ArrivalEpsilonSqr = 0.0001f;

        private readonly EnemyController enemy;
        private readonly ChaseSettings settings;

        /// <summary>Creates the state. Called once by <see cref="EnemyController"/> in <c>Awake</c>.</summary>
        public ChaseState(EnemyController enemy, ChaseSettings settings)
        {
            this.enemy = enemy;
            this.settings = settings;
        }

        /// <inheritdoc />
        public void Enter()
        {
        }

        /// <inheritdoc />
        public void Update()
        {
            Rigidbody2D body = enemy.Body;

            if (!enemy.TryGetTargetPosition(out Vector2 target))
            {
                Steer(body, Vector2.zero);
                return;
            }

            Vector2 toTarget = target - body.position;
            float distanceSqr = toTarget.sqrMagnitude;
            float range = settings.AttackRange;

            // Never attack from off-screen: the player must be able to see every attack coming.
            bool onScreen = enemy.IsOnScreen;
            if (onScreen && distanceSqr <= range * range && Time.time >= enemy.NextAttackTime)
            {
                enemy.StateMachine.ChangeState(enemy.ActiveAttackState);
                return;
            }

            Vector2 desiredVelocity = Vector2.zero;
            if (distanceSqr > ArrivalEpsilonSqr)
            {
                float distance = Mathf.Sqrt(distanceSqr);
                float speed = settings.MaxSpeed * enemy.SpeedMultiplier;
                float preferred = settings.PreferredDistance;

                // +1 = approach, -1 = retreat, 0 = hold. Melee enemies (preferred 0) always approach.
                float approach = 1f;
                // Off-screen enemies always close in, so kiting shooters can't hover out of view.
                if (preferred > 0f && onScreen)
                {
                    float halfBand = settings.DistanceTolerance * 0.5f;
                    approach = distance > preferred + halfBand ? 1f
                        : distance < preferred - halfBand ? -1f
                        : 0f;
                }

                desiredVelocity = toTarget / distance * (speed * approach);
            }

            Steer(body, desiredVelocity);
        }

        /// <inheritdoc />
        public void Exit()
        {
        }

        /// <summary>Seek steering: nudge velocity toward <paramref name="desiredVelocity"/>, capped by acceleration.</summary>
        private void Steer(Rigidbody2D body, Vector2 desiredVelocity)
        {
            Vector2 steering = Vector2.ClampMagnitude(desiredVelocity - body.linearVelocity, settings.Acceleration * enemy.SpeedMultiplier * Time.deltaTime);
            body.linearVelocity += steering;
        }
    }
}

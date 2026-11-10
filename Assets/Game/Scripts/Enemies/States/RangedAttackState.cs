using System;
using Core.Pooling;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Enemies.States
{
    /// <summary>Inspector-tunable values for <see cref="RangedAttackState"/>.</summary>
    [Serializable]
    public sealed class RangedSettings
    {
        [SerializeField]
        [Tooltip("Enemy projectile prefab. Its Hit Mask should be the Player layer.")]
        private Projectile projectilePrefab;

        [SerializeField, Min(1)]
        [Tooltip("Max enemy shots of this prefab alive at once, shared by every enemy that fires it.")]
        private int sharedPoolCapacity = 64;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the enemy stands still, aiming, before firing. The player's window to move.")]
        private float telegraphDuration = 0.5f;

        [SerializeField, Min(1)]
        [Tooltip("Projectiles fired per volley.")]
        private int projectilesPerVolley = 1;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("Total fan angle in degrees when firing more than one projectile.")]
        private float spreadAngle = 20f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance from the enemy's centre at which shots appear, so they don't start inside it.")]
        private float muzzleOffset = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds after firing before the enemy may fire again.")]
        private float cooldown = 2f;

        /// <summary>Projectile prefab fired by this enemy.</summary>
        public Projectile ProjectilePrefab => projectilePrefab;

        /// <summary>Capacity of the shared pool for <see cref="ProjectilePrefab"/>.</summary>
        public int SharedPoolCapacity => sharedPoolCapacity;

        /// <summary>Aiming time in seconds.</summary>
        public float TelegraphDuration => telegraphDuration;

        /// <summary>Projectiles per volley.</summary>
        public int ProjectilesPerVolley => projectilesPerVolley;

        /// <summary>Fan angle in degrees.</summary>
        public float SpreadAngle => spreadAngle;

        /// <summary>Spawn distance from the enemy's centre.</summary>
        public float MuzzleOffset => muzzleOffset;

        /// <summary>Post-volley cooldown in seconds.</summary>
        public float Cooldown => cooldown;
    }

    /// <summary>
    /// Stand still and aim → fire a volley at the target → back to <see cref="ChaseState"/> with a cooldown.
    /// </summary>
    /// <remarks>
    /// Aim is locked when the telegraph starts, matching <see cref="AttackState"/>: a player who moves during
    /// the wind-up dodges the shot. Projectiles come from <see cref="SharedPools{T}"/>, so they survive
    /// the shooter dying. Allocation-free per volley.
    /// </remarks>
    public sealed class RangedAttackState : IEnemyState
    {
        private readonly EnemyController enemy;
        private readonly RangedSettings settings;

        private Vector2 aimDirection;
        private float fireTime;

        /// <summary>Creates the state. Called once by <see cref="EnemyController"/> in <c>Awake</c>.</summary>
        public RangedAttackState(EnemyController enemy, RangedSettings settings)
        {
            this.enemy = enemy;
            this.settings = settings;
        }

        /// <summary>True while winding up. Visual components can poll this to show the telegraph.</summary>
        public bool IsAiming { get; private set; }

        /// <inheritdoc />
        public void Enter()
        {
            Rigidbody2D body = enemy.Body;

            aimDirection = enemy.TryGetTargetPosition(out Vector2 target)
                ? (target - body.position).normalized
                : Vector2.up;

            if (aimDirection == Vector2.zero)
            {
                aimDirection = Vector2.up;
            }

            IsAiming = true;
            fireTime = Time.time + settings.TelegraphDuration;
            body.linearVelocity = Vector2.zero;
        }

        /// <inheritdoc />
        public void Update()
        {
            enemy.Body.linearVelocity = Vector2.zero;

            if (Time.time < fireTime)
            {
                return;
            }

            FireVolley();
            enemy.StateMachine.ChangeState(enemy.ChaseState);
        }

        /// <inheritdoc />
        public void Exit()
        {
            IsAiming = false;
            enemy.NextAttackTime = Time.time + settings.Cooldown;
        }

        private void FireVolley()
        {
            ObjectPool<Projectile> pool = SharedPools<Projectile>.Get(settings.ProjectilePrefab, settings.SharedPoolCapacity);
            if (pool == null)
            {
                return;
            }

            int count = settings.ProjectilesPerVolley;
            float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            float step = count > 1 ? settings.SpreadAngle / (count - 1) : 0f;
            float startAngle = baseAngle - (count > 1 ? settings.SpreadAngle * 0.5f : 0f);
            Vector2 origin = enemy.Body.position;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector3 position = origin + direction * settings.MuzzleOffset;

                // Projectiles fly along transform.up, so rotate +Y onto the shot direction.
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle - 90f);

                if (!pool.TryGet(position, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, enemy.DamageMultiplier, 0, enemy);
            }
        }
    }
}

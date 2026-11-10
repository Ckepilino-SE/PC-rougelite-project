using Core.Pooling;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Enemies.Boss
{
    /// <summary>
    /// The boss's attack: telegraph → run one <see cref="BossPattern"/> (volleys, charges or a summon) →
    /// short recovery → back to <see cref="States.ChaseState"/> with the phase's cooldown.
    /// </summary>
    /// <remarks>
    /// Allocation-free per tick. Projectiles and minions come from <see cref="SharedPools{T}"/>, so they
    /// outlive the boss; the wave director clears them when the boss dies.
    /// </remarks>
    public sealed class BossAttackState : IEnemyState, IEnemyAttackStatus
    {
        private const float RecoverDuration = 0.35f;
        private const float MuzzleOffset = 1f;

        private enum Step
        {
            Telegraph,
            Execute,
            Recover,
        }

        private readonly EnemyController enemy;
        private readonly BossController boss;

        private BossPattern pattern;
        private Step step;
        private float stepEndTime;
        private float nextActionTime;
        private int repetitionsDone;
        private Vector2 aimDirection;
        private bool isCharging;

        /// <summary>Created once by <see cref="BossController"/>.</summary>
        public BossAttackState(EnemyController enemy, BossController boss)
        {
            this.enemy = enemy;
            this.boss = boss;
        }

        /// <inheritdoc />
        public bool IsTelegraphing { get; private set; }

        /// <inheritdoc />
        public bool IsMeleeTelegraph => pattern != null && pattern.Type == BossPatternType.Charge;

        /// <inheritdoc />
        public bool IsContactDamageActive => isCharging;

        /// <inheritdoc />
        public void Enter()
        {
            pattern = boss.PickPattern();
            if (pattern == null)
            {
                BeginRecover();
                return;
            }

            repetitionsDone = 0;
            isCharging = false;
            Aim();
            BeginTelegraph(pattern.TelegraphDuration);
        }

        /// <inheritdoc />
        public void Update()
        {
            Rigidbody2D body = enemy.Body;

            switch (step)
            {
                case Step.Telegraph:
                    body.linearVelocity = Vector2.zero;
                    if (pattern.Type == BossPatternType.Charge || (pattern.Type == BossPatternType.AimedSpread && pattern.TrackTarget))
                    {
                        Aim();
                    }

                    if (Time.time >= stepEndTime)
                    {
                        IsTelegraphing = false;
                        step = Step.Execute;
                        nextActionTime = Time.time;
                    }

                    break;

                case Step.Execute:
                    TickExecute(body);
                    break;

                case Step.Recover:
                    body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, Vector2.zero, 40f * Time.deltaTime);
                    if (Time.time >= stepEndTime)
                    {
                        enemy.StateMachine.ChangeState(enemy.ChaseState);
                    }

                    break;
            }
        }

        /// <inheritdoc />
        public void Exit()
        {
            IsTelegraphing = false;
            isCharging = false;
            BossPhase phase = boss.CurrentPhase;
            enemy.NextAttackTime = Time.time + (phase != null ? phase.PatternCooldown : 1f);
        }

        private void TickExecute(Rigidbody2D body)
        {
            switch (pattern.Type)
            {
                case BossPatternType.AimedSpread:
                case BossPatternType.Radial:
                case BossPatternType.Wall:
                    body.linearVelocity = Vector2.zero;
                    if (Time.time < nextActionTime)
                    {
                        return;
                    }

                    if (pattern.Type != BossPatternType.Radial && pattern.TrackTarget)
                    {
                        Aim();
                    }

                    if (pattern.Type == BossPatternType.Wall)
                    {
                        FireWall();
                    }
                    else
                    {
                        FireVolley();
                    }

                    repetitionsDone++;
                    nextActionTime = Time.time + pattern.Interval;
                    if (repetitionsDone >= pattern.Repetitions)
                    {
                        BeginRecover();
                    }

                    return;

                case BossPatternType.Charge:
                    if (isCharging)
                    {
                        body.linearVelocity = aimDirection * (pattern.ChargeSpeed * enemy.SpeedMultiplier);
                        if (Time.time >= stepEndTime)
                        {
                            isCharging = false;
                            repetitionsDone++;
                            if (repetitionsDone >= pattern.Repetitions)
                            {
                                BeginRecover();
                            }
                            else
                            {
                                // Short re-aim flash between charges.
                                Aim();
                                BeginTelegraph(pattern.Interval);
                            }
                        }

                        return;
                    }

                    isCharging = true;
                    stepEndTime = Time.time + pattern.ChargeDuration;
                    body.linearVelocity = aimDirection * (pattern.ChargeSpeed * enemy.SpeedMultiplier);
                    return;

                case BossPatternType.Summon:
                    body.linearVelocity = Vector2.zero;
                    Summon();
                    BeginRecover();
                    return;
            }
        }

        private void BeginTelegraph(float baseDuration)
        {
            BossPhase phase = boss.CurrentPhase;
            float multiplier = phase != null ? phase.TelegraphMultiplier : 1f;
            step = Step.Telegraph;
            IsTelegraphing = true;
            stepEndTime = Time.time + baseDuration * multiplier;
            enemy.Body.linearVelocity = Vector2.zero;
        }

        private void BeginRecover()
        {
            step = Step.Recover;
            IsTelegraphing = false;
            isCharging = false;
            stepEndTime = Time.time + RecoverDuration;
        }

        private void Aim()
        {
            if (enemy.TryGetTargetPosition(out Vector2 target))
            {
                Vector2 to = target - enemy.Body.position;
                if (to.sqrMagnitude > 0.0001f)
                {
                    aimDirection = to.normalized;
                    return;
                }
            }

            if (aimDirection == Vector2.zero)
            {
                aimDirection = Vector2.down;
            }
        }

        private void FireVolley()
        {
            Projectile prefab = pattern.ProjectilePrefab != null ? pattern.ProjectilePrefab : boss.DefaultProjectile;
            ObjectPool<Projectile> pool = SharedPools<Projectile>.Get(prefab, boss.ProjectilePoolCapacity);
            if (pool == null)
            {
                return;
            }

            int count = pattern.ProjectileCount;
            float startAngle;
            float step;

            float gapCentre = 0f;
            float halfGap = 0f;
            if (pattern.Type == BossPatternType.Radial)
            {
                step = 360f / count;
                startAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg + pattern.RotationStep * repetitionsDone;
                halfGap = pattern.GapAngle * 0.5f;
                gapCentre = Random.value * 360f;
            }
            else
            {
                float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                step = count > 1 ? pattern.SpreadAngle / (count - 1) : 0f;
                startAngle = baseAngle - (count > 1 ? pattern.SpreadAngle * 0.5f : 0f);
            }

            Vector2 origin = enemy.Body.position;
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                if (halfGap > 0f && Mathf.Abs(Mathf.DeltaAngle(angle, gapCentre)) < halfGap)
                {
                    continue;
                }

                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                Vector3 position = origin + direction * MuzzleOffset;
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle - 90f);

                if (!pool.TryGet(position, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, enemy.DamageMultiplier, 0, enemy);
            }
        }

        /// <summary>A line of parallel shots across the aim direction, with a random gap.</summary>
        private void FireWall()
        {
            Projectile prefab = pattern.ProjectilePrefab != null ? pattern.ProjectilePrefab : boss.DefaultProjectile;
            ObjectPool<Projectile> pool = SharedPools<Projectile>.Get(prefab, boss.ProjectilePoolCapacity);
            if (pool == null)
            {
                return;
            }

            int count = pattern.ProjectileCount;
            int gap = Mathf.Min(pattern.GapSize, count - 1);
            int gapStart = gap > 0 ? Random.Range(1, Mathf.Max(2, count - gap)) : -1;
            Vector2 side = new Vector2(-aimDirection.y, aimDirection.x);
            Vector2 centre = enemy.Body.position + aimDirection * MuzzleOffset;
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            for (int i = 0; i < count; i++)
            {
                if (i >= gapStart && i < gapStart + gap)
                {
                    continue;
                }

                float offset = (i - (count - 1) * 0.5f) * pattern.WallSpacing;
                if (!pool.TryGet(centre + side * offset, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, enemy.DamageMultiplier, 0, enemy);
            }
        }

        private void Summon()
        {
            if (pattern.SummonPrefab == null)
            {
                return;
            }

            ObjectPool<EnemyController> pool = SharedPools<EnemyController>.Get(pattern.SummonPrefab, boss.SummonPoolCapacity);
            if (pool == null)
            {
                return;
            }

            Vector2 origin = enemy.Body.position;
            int count = pattern.SummonCount;
            float offset = Random.value * 360f;
            for (int i = 0; i < count; i++)
            {
                float radians = (offset + 360f * i / count) * Mathf.Deg2Rad;
                Vector2 position = origin + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * pattern.SummonRadius;
                if (!pool.TryGet(position, Quaternion.identity, out EnemyController minion))
                {
                    return;
                }

                minion.Initialize(pool);
            }
        }
    }
}

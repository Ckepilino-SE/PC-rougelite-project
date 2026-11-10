using System;
using Core.Combat;
using Core.Pooling;
using Core.Timing;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Enemies.Boss
{
    /// <summary>
    /// Turns an enemy into a boss: replaces its attack with pattern attacks (<see cref="BossAttackState"/>) and
    /// moves through <see cref="BossPhase"/>s as its health drops. Add it next to <see cref="EnemyController"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Movement, contact damage, parry stuns, pooling and death all stay in <see cref="EnemyController"/>;
    /// this component only decides which attack to use and when.
    /// </para>
    /// <para>
    /// <b>Armour and posture</b>: the boss takes reduced damage until it is staggered. Parrying its charges
    /// and hitting it with reflected shots fills a posture meter; when full, the boss is stunned for a few
    /// seconds and takes bonus damage. This is what makes parrying the key to boss fights.
    /// </para>
    /// <para>
    /// The live boss is published through <see cref="Active"/> / <see cref="ActiveChanged"/> so a health bar
    /// can show it without a scene reference.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(EnemyController))]
    [DisallowMultipleComponent]
    public sealed class BossController : MonoBehaviour, IEnemyAttackProvider, IPostureReceiver
    {
        [SerializeField]
        private string bossName = "Boss";

        [SerializeField]
        [Tooltip("Ordered from first (threshold 1) to last (lowest threshold).")]
        private BossPhase[] phases = new BossPhase[0];

        [Header("Shared Resources")]
        [SerializeField]
        [Tooltip("Projectile used by patterns that don't set their own. Use the enemy projectile (parryable).")]
        private Projectile defaultProjectile;

        [SerializeField, Min(1)]
        [Tooltip("Max boss shots of each projectile prefab alive at once.")]
        private int projectilePoolCapacity = 160;

        [SerializeField, Min(1)]
        [Tooltip("Max summoned minions of each prefab alive at once.")]
        private int summonPoolCapacity = 10;

        [Header("Armour & Stagger")]
        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Damage multiplier while NOT staggered. 0.5 = boss shrugs off half of all damage.")]
        private float armoredDamageMultiplier = 0.5f;

        [SerializeField, Min(1f)]
        [Tooltip("Damage multiplier while staggered.")]
        private float staggeredDamageMultiplier = 2.5f;

        [SerializeField, Min(1f)]
        [Tooltip("Posture needed to stagger.")]
        private float maxPosture = 100f;

        [SerializeField, Min(0f)]
        [Tooltip("Posture added when the boss is parried (doubled on a perfect parry).")]
        private float posturePerParry = 25f;

        [SerializeField, Min(0f)]
        [Tooltip("Posture lost per second once the boss hasn't been pressured for a while.")]
        private float postureDecayPerSecond = 6f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds without posture gain before it starts to decay.")]
        private float postureDecayDelay = 2.5f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the boss is helpless when staggered.")]
        private float staggerDuration = 3f;

        [Header("Phase Change")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds the boss can't be hurt when it enters a new phase.")]
        private float phaseInvulnerability = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Real-time hit-stop when a phase changes, for impact.")]
        private float phaseHitStop = 0.15f;

        private EnemyController enemy;
        private Health health;
        private int phaseIndex;
        private int lastPatternIndex = -1;
        private float posture;
        private float lastPostureGainTime;
        private float staggerEndTime = -1f;

        /// <summary>The boss currently alive in the scene, or null.</summary>
        public static BossController Active { get; private set; }

        /// <summary>Raised when <see cref="Active"/> changes (null when the boss dies or despawns).</summary>
        public static event Action<BossController> ActiveChanged;

        /// <summary>Raised when the active boss enters a new phase. Arguments: boss, new phase index.</summary>
        public static event Action<BossController, int> PhaseChanged;

        /// <summary>Raised when the active boss is staggered (posture broken). Argument: boss.</summary>
        public static event Action<BossController> Staggered;

        /// <summary>Posture as 0..1 (1 = about to stagger).</summary>
        public float PostureFraction => maxPosture > 0f ? Mathf.Clamp01(posture / maxPosture) : 0f;

        /// <summary>True while staggered.</summary>
        public bool IsStaggered => Time.time < staggerEndTime;

        /// <summary>0..1 of the stagger remaining, for a UI meter.</summary>
        public float StaggerRemainingFraction => IsStaggered ? Mathf.Clamp01((staggerEndTime - Time.time) / staggerDuration) : 0f;

        /// <summary>Display name for the health bar and banners.</summary>
        public string BossName => bossName;

        /// <summary>The enemy this boss drives.</summary>
        public EnemyController Enemy => enemy;

        /// <summary>The boss's hit points.</summary>
        public Health Health => health;

        /// <summary>Index of the current phase.</summary>
        public int PhaseIndex => phaseIndex;

        /// <summary>Number of phases.</summary>
        public int PhaseCount => phases != null ? phases.Length : 0;

        /// <summary>Current phase, or null if none are configured.</summary>
        public BossPhase CurrentPhase => PhaseCount > 0 ? phases[Mathf.Clamp(phaseIndex, 0, PhaseCount - 1)] : null;

        /// <summary>Projectile used when a pattern sets none.</summary>
        public Projectile DefaultProjectile => defaultProjectile;

        /// <summary>Shared projectile pool size.</summary>
        public int ProjectilePoolCapacity => projectilePoolCapacity;

        /// <summary>Shared summon pool size.</summary>
        public int SummonPoolCapacity => summonPoolCapacity;

        /// <inheritdoc />
        public IEnemyState CreateAttackState(EnemyController owner)
        {
            CacheComponents();
            return new BossAttackState(owner, this);
        }

        /// <summary>
        /// Creates the shared pools this boss's patterns need, so nothing is instantiated mid-fight.
        /// Safe to call on the prefab asset; the wave director does so when the boss stage begins.
        /// </summary>
        public void WarmUpPools()
        {
            for (int p = 0; p < PhaseCount; p++)
            {
                BossPhase phase = phases[p];
                for (int i = 0; i < phase.PatternCount; i++)
                {
                    BossPattern pattern = phase.GetPattern(i);
                    if (pattern == null)
                    {
                        continue;
                    }

                    if (pattern.Type == BossPatternType.Summon)
                    {
                        if (pattern.SummonPrefab != null)
                        {
                            SharedPools<EnemyController>.Get(pattern.SummonPrefab, summonPoolCapacity);
                        }
                    }
                    else if (pattern.Type != BossPatternType.Charge)
                    {
                        Projectile prefab = pattern.ProjectilePrefab != null ? pattern.ProjectilePrefab : defaultProjectile;
                        if (prefab != null)
                        {
                            SharedPools<Projectile>.Get(prefab, projectilePoolCapacity);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public void AddPosture(float amount)
        {
            if (enemy == null || !enemy.IsAlive || IsStaggered || amount <= 0f)
            {
                return;
            }

            posture += amount;
            lastPostureGainTime = Time.time;
            if (posture >= maxPosture)
            {
                BeginStagger();
            }
        }

        private void BeginStagger()
        {
            posture = 0f;
            staggerEndTime = Time.time + staggerDuration;
            enemy.Stun(staggerDuration, Vector2.zero);
            health.DamageTakenMultiplier = staggeredDamageMultiplier;
            GameTime.HitStop(0.12f);
            Staggered?.Invoke(this);
        }

        private void HandleParried(ParryResult result)
        {
            if (IsStaggered)
            {
                // A parry's short stun must not cut a stagger short.
                enemy.Stun(staggerEndTime - Time.time, Vector2.zero);
                return;
            }

            AddPosture(posturePerParry * (result.IsPerfect ? 2f : 1f));
        }

        /// <summary>Picks a random pattern from the current phase, avoiding an immediate repeat.</summary>
        internal BossPattern PickPattern()
        {
            BossPhase phase = CurrentPhase;
            if (phase == null || phase.PatternCount == 0)
            {
                return null;
            }

            int count = phase.PatternCount;
            int index = UnityEngine.Random.Range(0, count);
            if (count > 1 && index == lastPatternIndex)
            {
                index = (index + 1 + UnityEngine.Random.Range(0, count - 1)) % count;
            }

            lastPatternIndex = index;
            return phase.GetPattern(index);
        }

        private void Awake()
        {
            CacheComponents();
        }

        private void CacheComponents()
        {
            if (enemy == null)
            {
                enemy = GetComponent<EnemyController>();
                health = GetComponent<Health>();
            }
        }

        private void OnEnable()
        {
            CacheComponents();
            phaseIndex = 0;
            lastPatternIndex = -1;
            posture = 0f;
            staggerEndTime = -1f;
            enemy.Parried += HandleParried;
            Active = this;
            ActiveChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            if (enemy != null)
            {
                enemy.Parried -= HandleParried;
            }

            if (Active == this)
            {
                Active = null;
                ActiveChanged?.Invoke(null);
            }
        }

        private void Update()
        {
            if (enemy == null || !enemy.IsAlive || PhaseCount == 0)
            {
                return;
            }

            // Armour: Health resets its multiplier on spawn, so it is re-applied every frame.
            bool staggered = IsStaggered;
            health.DamageTakenMultiplier = staggered ? staggeredDamageMultiplier : armoredDamageMultiplier;
            if (!staggered && posture > 0f && Time.time - lastPostureGainTime > postureDecayDelay)
            {
                posture = Mathf.Max(0f, posture - postureDecayPerSecond * Time.deltaTime);
            }

            // Pooled bosses are reset after OnEnable, so recompute every frame rather than caching on spawn.
            float fraction = health.MaxHealth > 0 ? (float)health.CurrentHealth / health.MaxHealth : 0f;
            int target = phaseIndex;
            while (target + 1 < PhaseCount && fraction <= phases[target + 1].HealthThreshold)
            {
                target++;
            }

            if (target != phaseIndex)
            {
                phaseIndex = target;
                lastPatternIndex = -1;
                health.GrantInvulnerability(phaseInvulnerability);
                enemy.NextAttackTime = Time.time + 0.4f;
                if (phaseHitStop > 0f)
                {
                    GameTime.HitStop(phaseHitStop);
                }

                PhaseChanged?.Invoke(this, phaseIndex);
            }

            enemy.PhaseSpeedMultiplier = CurrentPhase.SpeedMultiplier;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active = null;
            ActiveChanged = null;
            PhaseChanged = null;
            Staggered = null;
        }
    }
}

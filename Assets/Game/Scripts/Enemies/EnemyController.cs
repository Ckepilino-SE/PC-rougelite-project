using System;
using Core.Cameras;
using Core.Combat;
using Core.Events;
using Core.Pooling;
using Core.References;
using Core.StateMachines;
using Gameplay.Combat;
using Gameplay.Enemies.States;
using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>Which attack <see cref="ChaseState"/> hands off to when in range.</summary>
    public enum EnemyAttackStyle
    {
        /// <summary>Telegraph, then lunge into the target (<see cref="AttackState"/>).</summary>
        Melee,

        /// <summary>Telegraph, then fire projectiles (<see cref="RangedAttackState"/>).</summary>
        Ranged,
    }

    /// <summary>
    /// Base enemy: owns the finite state machine, pool lifecycle, lunge contact damage, and death handling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Behaviour</b> lives entirely in <see cref="IEnemyState"/> objects (<see cref="ChaseState"/>,
    /// <see cref="AttackState"/>) created once in <c>Awake</c> and ticked from <c>FixedUpdate</c>.
    /// </para>
    /// <para>
    /// <b>Damage intake</b> is delegated to the required <see cref="Health"/> component; this class implements
    /// <see cref="IDamageable"/> only as a pass-through, so hitting either component has the same effect.
    /// </para>
    /// <para>
    /// <b>Targeting</b> reads the player from a <see cref="TransformAnchorSO"/>, so enemies hold no scene
    /// reference to the player.
    /// </para>
    /// <para>
    /// <b>Death</b>: raises <see cref="EnemyKilledEventChannelSO"/> with <see cref="scoreValue"/>, then returns
    /// to the pool set via <see cref="Initialize"/>. Enemies placed in a scene by hand (no pool) are disabled instead.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class EnemyController : MonoBehaviour, IPoolable, IDamageable, IParryable, IPowerSource
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Anchor the player registers itself in.")]
        private TransformAnchorSO targetAnchor;

        [SerializeField]
        [Tooltip("Channel raised with Score Value when this enemy dies.")]
        private EnemyKilledEventChannelSO enemyKilledChannel;

        [Header("Scoring")]
        [SerializeField, Min(0)]
        private int scoreValue = 10;

        [Header("Lunge Damage")]
        [SerializeField, Min(0)]
        [Tooltip("Damage dealt to an IDamageable touched during the lunge.")]
        private int lungeDamage = 1;

        [SerializeField]
        [Tooltip("Layers the lunge can damage. Set to the Player layer.")]
        private LayerMask damageMask;

        [Header("Power Theft")]
        [SerializeField]
        [Tooltip("Power the player steals by perfect-parrying this enemy (or reflecting its shot). A StolenPowerDefinition asset.")]
        private ScriptableObject stealablePower;

        [Header("When Parried")]
        [SerializeField, Min(0f)]
        [Tooltip("Seconds this enemy is stunned after the player parries its lunge.")]
        private float parriedStunDuration = 1.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Speed at which a parried enemy is knocked away from the player.")]
        private float parriedKnockback = 8f;

        [SerializeField, Min(0)]
        [Tooltip("Damage this enemy takes when parried. Scaled up by perfect parries and upgrades.")]
        private int parriedDamage = 1;

        [Header("Screen")]
        [SerializeField, Min(0f)]
        [Tooltip("How far inside the screen edge this enemy must be before it may attack. Stops off-screen sniping.")]
        private float onScreenAttackInset = 0.5f;

        [Header("States")]
        [SerializeField]
        [Tooltip("Melee uses the Attack settings; Ranged uses the Ranged settings.")]
        private EnemyAttackStyle attackStyle = EnemyAttackStyle.Melee;

        [SerializeField]
        private ChaseSettings chase = new ChaseSettings();

        [SerializeField]
        [Tooltip("Melee lunge. Used when Attack Style is Melee.")]
        private AttackSettings attack = new AttackSettings();

        [SerializeField]
        [Tooltip("Projectile volley. Used when Attack Style is Ranged.")]
        private RangedSettings ranged = new RangedSettings();

        private Rigidbody2D body;
        private Health health;
        private ObjectPool<EnemyController> pool;
        private bool isAlive;

        /// <summary>The enemy's Rigidbody2D. States move the enemy through this.</summary>
        public Rigidbody2D Body => body;

        /// <summary>The enemy's state machine. States call <c>ChangeState</c> on it to transition.</summary>
        public StateMachine<IEnemyState> StateMachine { get; private set; }

        /// <summary>Shared chase state instance.</summary>
        public ChaseState ChaseState { get; private set; }

        /// <summary>Shared melee attack state instance.</summary>
        public AttackState AttackState { get; private set; }

        /// <summary>Shared ranged attack state instance.</summary>
        public RangedAttackState RangedAttackState { get; private set; }

        /// <summary>
        /// Raised by any enemy when it recovers from a stun (not when it dies stunned). Argument: the enemy.
        /// Static so run-wide systems such as upgrades can react without a reference to every pooled enemy.
        /// </summary>
        public static event Action<EnemyController> AnyStunRecovered;

        /// <summary>Shared stunned state instance.</summary>
        public StunnedState StunnedState { get; private set; }

        /// <summary>True while stunned (e.g. after being parried).</summary>
        public bool IsStunned => StateMachine != null && StateMachine.CurrentState == StunnedState;

        /// <summary>The attack state <see cref="ChaseState"/> switches to, chosen by the Attack Style field.</summary>
        /// <remarks>An <see cref="IEnemyAttackProvider"/> on the same object (e.g. a boss) replaces both.</remarks>
        public IEnemyState ActiveAttackState =>
            customAttackState ?? (attackStyle == EnemyAttackStyle.Ranged ? (IEnemyState)RangedAttackState : AttackState);

        /// <summary>The custom attack state's status, or null when the enemy uses the built-in attacks.</summary>
        public IEnemyAttackStatus CustomAttackStatus => customAttackStatus;

        /// <summary>The enemy's hit points.</summary>
        public Health Health => health;

        /// <inheritdoc />
        public ScriptableObject StealablePower => stealablePower;

        /// <summary>Raised after this enemy is parried and stunned. Argument: the parry result.</summary>
        public event Action<ParryResult> Parried;

        /// <summary>
        /// Raised by any enemy when it dies (not when it is despawned). Argument: the enemy, still at its death
        /// position. Static so upgrades can react to kills without a reference to every pooled enemy.
        /// </summary>
        public static event Action<EnemyController> AnyDied;

        /// <summary>Raised by any enemy when it takes damage. Arguments: enemy, damage. For hit sparks and sounds.</summary>
        public static event Action<EnemyController, int> AnyDamaged;

        /// <summary>Melee or ranged.</summary>
        public EnemyAttackStyle AttackStyle => attackStyle;

        /// <summary>Earliest <c>Time.time</c> at which a new attack may start. Set by <see cref="AttackState"/>.</summary>
        public float NextAttackTime { get; internal set; }

        /// <summary>Points awarded when this enemy dies.</summary>
        public int ScoreValue => scoreValue;

        /// <summary>True between spawn and death.</summary>
        public bool IsAlive => isAlive;

        /// <summary>Multiplier on movement and lunge speed for this life, including any phase bonus. Read by the states.</summary>
        public float SpeedMultiplier => stageSpeedMultiplier * PhaseSpeedMultiplier;

        /// <summary>Extra speed multiplier set by a boss phase. Reset to 1 on spawn.</summary>
        public float PhaseSpeedMultiplier { get; set; } = 1f;

        private float stageSpeedMultiplier = 1f;
        private IEnemyState customAttackState;
        private IEnemyAttackStatus customAttackStatus;

        /// <summary>Multiplier on lunge and projectile damage for this life.</summary>
        public float DamageMultiplier => damageMultiplier;

        private float damageMultiplier = 1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();

            StateMachine = new StateMachine<IEnemyState>();
            ChaseState = new ChaseState(this, chase);
            AttackState = new AttackState(this, attack);
            RangedAttackState = new RangedAttackState(this, ranged);
            StunnedState = new StunnedState(this);

            if (TryGetComponent(out IEnemyAttackProvider provider))
            {
                customAttackState = provider.CreateAttackState(this);
                customAttackStatus = customAttackState as IEnemyAttackStatus;
            }
        }

        private void OnEnable()
        {
            health.Died += HandleDied;
            health.DamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            health.Died -= HandleDied;
            health.DamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(int amount)
        {
            AnyDamaged?.Invoke(this, amount);
        }

        private void Start()
        {
            // Pooled enemies are started by OnSpawnFromPool (which runs before Start) and have a pool by now.
            // Hand-placed enemies never pass through the pool, so start them here.
            if (pool == null && !isAlive)
            {
                BeginLife();
            }
        }

        /// <summary>
        /// Binds this enemy to the pool it must return to on death. Call immediately after taking it from the pool.
        /// </summary>
        /// <param name="owner">The pool that handed out this instance.</param>
        public void Initialize(ObjectPool<EnemyController> owner)
        {
            pool = owner;
        }

        /// <summary>
        /// Binds this enemy to its pool and applies stage difficulty. Call immediately after taking it from the pool.
        /// </summary>
        /// <param name="owner">The pool that handed out this instance.</param>
        /// <param name="modifiers">Stat multipliers for this life. Reset to defaults on the next spawn.</param>
        public void Initialize(ObjectPool<EnemyController> owner, in EnemyModifiers modifiers)
        {
            pool = owner;
            stageSpeedMultiplier = modifiers.SpeedMultiplier;
            damageMultiplier = modifiers.DamageMultiplier;
            health.ApplyMaxHealthMultiplier(modifiers.HealthMultiplier);
        }

        /// <summary>
        /// Creates the shared projectile pool a ranged enemy fires from, so the first shot doesn't instantiate.
        /// Safe to call on the prefab asset; the wave director does so when a stage begins.
        /// </summary>
        public void WarmUpSharedPools()
        {
            if (attackStyle == EnemyAttackStyle.Ranged && ranged != null && ranged.ProjectilePrefab != null)
            {
                SharedPools<Projectile>.Get(ranged.ProjectilePrefab, ranged.SharedPoolCapacity);
            }
        }

        /// <summary>True if the enemy is far enough inside the camera view to be allowed to attack.</summary>
        public bool IsOnScreen => CameraBounds.Contains(body.position, onScreenAttackInset);

        /// <summary>World position of the target, if one is registered.</summary>
        /// <returns><c>false</c> if no target is registered (e.g. the player is dead or not spawned).</returns>
        public bool TryGetTargetPosition(out Vector2 position)
        {
            if (targetAnchor != null && targetAnchor.IsSet)
            {
                position = targetAnchor.Value.position;
                return true;
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Interrupts whatever the enemy is doing and stuns it. Restarts the stun if already stunned.
        /// </summary>
        /// <param name="duration">Seconds before the enemy resumes chasing.</param>
        /// <param name="knockback">Initial velocity applied; slides to a stop.</param>
        public void Stun(float duration, Vector2 knockback)
        {
            if (!isAlive)
            {
                return;
            }

            StunnedState.Begin(duration, knockback);
            if (IsStunned)
            {
                StunnedState.Enter();
            }
            else
            {
                StateMachine.ChangeState(StunnedState);
            }
        }

        /// <inheritdoc />
        void IParryable.OnParried(Vector2 parrierPosition, in ParryResult result)
        {
            if (!isAlive)
            {
                return;
            }

            Vector2 away = body.position - parrierPosition;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -body.linearVelocity.normalized;
            Stun(parriedStunDuration * result.StunMultiplier, away * parriedKnockback);

            if (parriedDamage > 0)
            {
                health.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(parriedDamage * result.CounterDamageMultiplier)));
            }

            Parried?.Invoke(result);
        }

        /// <summary>Called by <see cref="StunnedState"/> when the stun runs out.</summary>
        internal void NotifyStunRecovered()
        {
            AnyStunRecovered?.Invoke(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyStunRecovered = null;
            AnyDied = null;
            AnyDamaged = null;
        }

        /// <inheritdoc />
        public bool TakeDamage(int amount)
        {
            return health.TakeDamage(amount);
        }

        void IPoolable.OnSpawnFromPool()
        {
            BeginLife();
        }

        void IPoolable.OnReturnToPool()
        {
            EndLife();
        }

        private void FixedUpdate()
        {
            if (isAlive)
            {
                StateMachine.Tick();
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryDealLungeDamage(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            // Covers a lunge that starts while already touching the player. The player's i-frames stop repeat hits.
            TryDealLungeDamage(collision.collider);
        }

        private void TryDealLungeDamage(Collider2D other)
        {
            bool contactActive = AttackState.IsLunging || (customAttackStatus != null && customAttackStatus.IsContactDamageActive);
            if (!isAlive || !contactActive || (damageMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (ParryLookup.TryParry(other, body.position, this, out ParryResult result))
            {
                // Parried: no damage to the player; this enemy is knocked away, stunned and hurt instead.
                ((IParryable)this).OnParried(other.transform.position, result);
                return;
            }

            if (lungeDamage > 0 && DamageableLookup.TryGet(other, out IDamageable target))
            {
                target.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(lungeDamage * damageMultiplier)));
            }
        }

        private void BeginLife()
        {
            isAlive = true;
            NextAttackTime = 0f;
            stageSpeedMultiplier = 1f;
            PhaseSpeedMultiplier = 1f;
            damageMultiplier = 1f;
            body.linearVelocity = Vector2.zero;
            StateMachine.ChangeState(ChaseState);
        }

        private void EndLife()
        {
            isAlive = false;
            StateMachine.Stop();
            body.linearVelocity = Vector2.zero;
        }

        private void HandleDied()
        {
            if (!isAlive)
            {
                return;
            }

            if (enemyKilledChannel != null)
            {
                enemyKilledChannel.Raise(scoreValue);
            }

            AnyDied?.Invoke(this);

            if (pool != null && pool.ReturnToPool(this))
            {
                return;
            }

            // Not pooled (hand-placed) or the pool was disposed.
            EndLife();
            gameObject.SetActive(false);
        }
    }
}

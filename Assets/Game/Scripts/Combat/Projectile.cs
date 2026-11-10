using Core.Combat;
using Core.Pooling;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Pooled projectile that flies along its local up vector, damages the first <see cref="IDamageable"/>
    /// it touches on <see cref="hitMask"/>, and returns itself to its pool on hit or after <see cref="lifetime"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Forward" is <c>transform.up</c>, matching the project convention that 2D art faces +Y. Spawn it with
    /// the muzzle's rotation and it flies where the muzzle points.
    /// </para>
    /// <para>
    /// Requires a Kinematic <see cref="Rigidbody2D"/> and a trigger <see cref="Collider2D"/>. Kinematic bodies
    /// receive trigger callbacks from Dynamic bodies (enemies) by default. To also stop on Static colliders
    /// such as walls, enable <b>Use Full Kinematic Contacts</b> on the Rigidbody2D and add the wall layer to
    /// <see cref="hitMask"/>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(0f)]
        [Tooltip("Travel speed in units per second.")]
        private float speed = 18f;

        [SerializeField, Min(1)]
        [Tooltip("Damage applied to the IDamageable that is hit.")]
        private int damage = 1;

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds before the projectile returns to the pool if it hits nothing.")]
        private float lifetime = 2f;

        [SerializeField]
        [Tooltip("Layers that stop this projectile. Set to the Enemy layer (plus walls, if wanted).")]
        private LayerMask hitMask;

        [Header("Parry")]
        [SerializeField]
        [Tooltip("If ticked, a parrying target reflects this projectile instead of taking damage. For enemy shots.")]
        private bool canBeParried;

        [SerializeField]
        [Tooltip("Layer the projectile switches to once reflected, e.g. PlayerProjectile.")]
        private string reflectedLayerName = "PlayerProjectile";

        [SerializeField]
        [Tooltip("What a reflected projectile can hit, e.g. Enemy.")]
        private LayerMask reflectedHitMask;

        [SerializeField, Min(0f)]
        [Tooltip("Damage multiplier for a reflected projectile.")]
        private float reflectedDamageMultiplier = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("Speed multiplier for a reflected projectile.")]
        private float reflectedSpeedMultiplier = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Posture (stagger meter) a reflected shot adds to a boss it hits.")]
        private float reflectedPostureDamage = 8f;

        private Rigidbody2D body;
        private ObjectPool<Projectile> owner;
        private int originalLayer;
        private LayerMask originalHitMask;
        private int reflectedLayer = -1;
        private bool isReflected;
        private float expireTime;
        private float damageMultiplier = 1f;
        private int pierceRemaining;
        private IPowerSource source;
        private bool isLive;

        /// <summary>Travel speed in units per second.</summary>
        public float Speed => speed;

        /// <summary>Damage applied on hit.</summary>
        public int Damage => damage;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            originalLayer = gameObject.layer;
            originalHitMask = hitMask;

            if (canBeParried)
            {
                reflectedLayer = LayerMask.NameToLayer(reflectedLayerName);
                if (reflectedLayer < 0)
                {
                    Debug.LogWarning($"[Projectile] '{name}': layer '{reflectedLayerName}' does not exist; reflected shots keep their layer.", this);
                }
            }
        }

        private void Reset()
        {
            // Editor convenience: sensible defaults when the component is first added.
            var rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GetComponent<Collider2D>().isTrigger = true;
        }

        /// <summary>
        /// Binds this projectile to the pool it must return to. Call immediately after taking it from the pool.
        /// </summary>
        /// <param name="pool">The pool that handed out this instance.</param>
        /// <param name="damageMultiplier">Scales <see cref="Damage"/> for this flight (e.g. stage difficulty).</param>
        /// <param name="pierce">Extra enemies this shot passes through before it stops.</param>
        /// <param name="shooter">Who fired it, so a perfect-parry reflect can steal their power. Enemies pass themselves.</param>
        public void Launch(ObjectPool<Projectile> pool, float damageMultiplier = 1f, int pierce = 0, IPowerSource shooter = null)
        {
            source = shooter;
            owner = pool;
            this.damageMultiplier = damageMultiplier;
            pierceRemaining = Mathf.Max(0, pierce);
        }

        void IPoolable.OnSpawnFromPool()
        {
            isLive = true;
            expireTime = Time.time + lifetime;
            body.linearVelocity = (Vector2)transform.up * speed;
        }

        void IPoolable.OnReturnToPool()
        {
            isLive = false;
            damageMultiplier = 1f;
            pierceRemaining = 0;
            source = null;
            body.linearVelocity = Vector2.zero;

            if (isReflected)
            {
                isReflected = false;
                gameObject.layer = originalLayer;
                hitMask = originalHitMask;
            }
        }

        private void Update()
        {
            if (isLive && Time.time >= expireTime)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Several overlaps can be reported in the same physics step; only the first one counts.
            if (!isLive || (hitMask.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            if (canBeParried && !isReflected && ParryLookup.TryParry(other, body.position, source, out ParryResult parry))
            {
                Reflect(parry.Redirect, parry.ReflectDamageMultiplier);
                return;
            }

            bool hitDamageable = DamageableLookup.TryGet(other, out IDamageable target);
            if (hitDamageable)
            {
                target.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(damage * damageMultiplier)));
            }

            if (isReflected && reflectedPostureDamage > 0f && ParryLookup.TryGet(other, out IPostureReceiver posture))
            {
                posture.AddPosture(reflectedPostureDamage * damageMultiplier);
            }

            if (hitDamageable && pierceRemaining > 0)
            {
                pierceRemaining--;
                return;
            }

            Despawn();
        }

        /// <summary>Turns the projectile around: new direction, new layer and targets, more damage and speed.</summary>
        private void Reflect(Vector2 direction, float extraDamageMultiplier)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = -body.linearVelocity.normalized;
            }

            isReflected = true;
            if (reflectedLayer >= 0)
            {
                gameObject.layer = reflectedLayer;
            }

            hitMask = reflectedHitMask;
            damageMultiplier *= reflectedDamageMultiplier * extraDamageMultiplier;
            expireTime = Time.time + lifetime;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            body.rotation = angle;
            body.linearVelocity = direction.normalized * (speed * reflectedSpeedMultiplier);
        }

        private void Despawn()
        {
            isLive = false;

            if (owner != null && owner.ReturnToPool(this))
            {
                return;
            }

            // Not pooled (e.g. placed in a scene by hand) or the pool was disposed.
            gameObject.SetActive(false);
        }
    }
}

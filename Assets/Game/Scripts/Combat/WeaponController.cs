using Core.Pooling;
using Core.Timing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Combat
{
    /// <summary>
    /// How a <see cref="WeaponController"/> decides when to pull the trigger.
    /// </summary>
    public enum FireMode
    {
        /// <summary>Fires continuously at the fire rate with no input.</summary>
        Automatic,

        /// <summary>Fires at the fire rate while the Attack button is held.</summary>
        HoldToFire,

        /// <summary>Fires once per Attack press, still capped by the fire rate. A press during cooldown is buffered.</summary>
        ClickToFire,
    }

    /// <summary>
    /// Owns firing cadence and spawns <see cref="Projectile"/>s from an <see cref="ObjectPool{T}"/> at the muzzle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Aim comes from the <see cref="muzzle"/>'s rotation (its local up), so place the muzzle under the
    /// player's AimPivot and this component needs no reference to movement or input-aim code.
    /// </para>
    /// <para>
    /// The pool is created and pre-warmed in <c>Awake</c>. After that, <c>Update</c> and <see cref="Fire"/>
    /// allocate nothing: no Instantiate, no LINQ, no closures, no boxing. If the pool reaches its capacity
    /// the shot is skipped rather than allocating a new projectile mid-combat.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WeaponController : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField]
        [Tooltip("Projectile prefab to pool and fire.")]
        private Projectile projectilePrefab;

        [SerializeField]
        [Tooltip("Spawn point. Projectiles inherit its position and rotation and fly along its up vector.")]
        private Transform muzzle;

        [Header("Cadence")]
        [SerializeField]
        private FireMode fireMode = FireMode.HoldToFire;

        [SerializeField, Min(0.01f)]
        [Tooltip("Shots per second.")]
        private float fireRate = 8f;

        [Header("Multishot")]
        [SerializeField, Range(0f, 45f)]
        [Tooltip("Degrees between shots when upgrades add extra projectiles.")]
        private float multishotSpacing = 10f;

        [Header("Pool")]
        [SerializeField, Min(1)]
        [Tooltip("Hard cap on projectiles alive at once. Roughly fireRate x projectile lifetime, plus headroom.")]
        private int poolCapacity = 32;

        [SerializeField, Min(0)]
        [Tooltip("Projectiles instantiated up front during Awake, so firing never instantiates.")]
        private int prewarmCount = 32;

        private ObjectPool<Projectile> pool;
        private float nextFireTime;
        private bool isTriggerHeld;
        private bool isShotBuffered;
        private int fireBlockers;

        /// <summary>Raised after each normal shot (not bursts or rings). For sound and muzzle flash.</summary>
        public event System.Action Fired;

        /// <summary>The muzzle transform shots come from.</summary>
        public Transform Muzzle => muzzle;

        /// <summary>Current fire mode.</summary>
        public FireMode Mode => fireMode;

        /// <summary>Shots per second, including upgrades.</summary>
        public float FireRate => fireRate * FireRateMultiplier;

        /// <summary>Multiplier on shots per second. Upgrades multiply into this and divide back out.</summary>
        public float FireRateMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on every projectile's damage, including bursts and rings.</summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>Extra projectiles per normal shot, fanned out by the multishot spacing.</summary>
        public int ExtraProjectiles { get; set; }

        /// <summary>Extra enemies each projectile passes through.</summary>
        public int Pierce { get; set; }

        /// <summary>True while an upgrade has disabled normal firing. <see cref="FireBurst"/> still works.</summary>
        public bool IsTriggerBlocked => fireBlockers > 0;

        /// <summary>Disables normal firing (e.g. a "can't shoot" upgrade). Pair with <see cref="RemoveFireBlock"/>.</summary>
        public void AddFireBlock()
        {
            fireBlockers++;
        }

        /// <summary>Undoes one <see cref="AddFireBlock"/>.</summary>
        public void RemoveFireBlock()
        {
            fireBlockers = Mathf.Max(0, fireBlockers - 1);
        }

        /// <summary>
        /// Fires <paramref name="count"/> projectiles at once in a fan around the muzzle direction, ignoring the
        /// fire rate and any fire block. Used by upgrades (e.g. a shotgun burst on parry). Allocation-free.
        /// </summary>
        /// <param name="count">Projectiles to fire.</param>
        /// <param name="spreadDegrees">Total fan angle.</param>
        /// <param name="damageMultiplier">Damage multiplier for these projectiles.</param>
        public void FireBurst(int count, float spreadDegrees, float damageMultiplier = 1f)
        {
            if (pool == null || count <= 0)
            {
                return;
            }

            float step = count > 1 ? spreadDegrees / (count - 1) : 0f;
            float start = count > 1 ? -spreadDegrees * 0.5f : 0f;
            Vector3 position = muzzle.position;
            Quaternion baseRotation = muzzle.rotation;

            for (int i = 0; i < count; i++)
            {
                Quaternion rotation = baseRotation * Quaternion.Euler(0f, 0f, start + step * i);
                if (!pool.TryGet(position, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, damageMultiplier * DamageMultiplier, Pierce);
            }
        }

        /// <summary>
        /// Fires <paramref name="count"/> projectiles evenly in every direction from <paramref name="origin"/>,
        /// ignoring fire rate and blocks (e.g. a nova on dash, or a burst from a dying enemy). Allocation-free.
        /// </summary>
        public void FireRing(Vector2 origin, int count, float damageMultiplier = 1f)
        {
            if (pool == null || count <= 0)
            {
                return;
            }

            float offset = Random.value * 360f;
            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, 0f, offset + step * i);
                Vector3 position = (Vector3)origin + rotation * (Vector3.up * 0.5f);
                if (!pool.TryGet(position, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, damageMultiplier * DamageMultiplier, Pierce);
            }
        }

        private void Awake()
        {
            if (muzzle == null)
            {
                muzzle = transform;
            }

            if (projectilePrefab == null)
            {
                Debug.LogError($"{nameof(WeaponController)} on '{name}' has no projectile prefab assigned.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<Projectile>(projectilePrefab, poolCapacity, Mathf.Min(prewarmCount, poolCapacity));
        }

        private void OnDisable()
        {
            isTriggerHeld = false;
            isShotBuffered = false;
        }

        private void OnDestroy()
        {
            pool?.Dispose();
        }

        /// <summary>
        /// Input callback for the <c>Attack</c> action (Button). Wire through PlayerInput's Unity Events.
        /// </summary>
        public void OnAttack(InputAction.CallbackContext context)
        {
            isTriggerHeld = context.ReadValueAsButton();

            if (context.performed)
            {
                isShotBuffered = true;
            }
        }

        private void Update()
        {
            // Paused (stage clear, game over): drop clicks aimed at UI buttons so they don't fire on resume.
            if (GameTime.IsPaused)
            {
                isShotBuffered = false;
                return;
            }

            if (fireBlockers > 0 || !WantsToFire() || Time.time < nextFireTime)
            {
                return;
            }

            Fire();
            Fired?.Invoke();
            isShotBuffered = false;

            // Keep an exact cadence while firing continuously, but don't let idle time bank extra shots.
            float interval = 1f / Mathf.Max(0.01f, FireRate);
            nextFireTime = Time.time - nextFireTime > interval
                ? Time.time + interval
                : nextFireTime + interval;
        }

        private bool WantsToFire()
        {
            switch (fireMode)
            {
                case FireMode.Automatic:
                    return true;
                case FireMode.HoldToFire:
                    return isTriggerHeld;
                case FireMode.ClickToFire:
                    return isShotBuffered;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Spawns one projectile at the muzzle. Allocation-free; skips the shot if the pool is exhausted.
        /// </summary>
        private void Fire()
        {
            int count = 1 + Mathf.Max(0, ExtraProjectiles);
            float start = -multishotSpacing * (count - 1) * 0.5f;
            Vector3 position = muzzle.position;
            Quaternion baseRotation = muzzle.rotation;

            for (int i = 0; i < count; i++)
            {
                Quaternion rotation = count > 1 ? baseRotation * Quaternion.Euler(0f, 0f, start + multishotSpacing * i) : baseRotation;
                if (!pool.TryGet(position, rotation, out Projectile projectile))
                {
                    return;
                }

                projectile.Launch(pool, DamageMultiplier, Pierce);
            }
        }
    }
}

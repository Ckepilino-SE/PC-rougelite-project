using Core.Combat;
using Core.Pooling;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// A pooled circular area that damages everything on <see cref="damageMask"/> inside it at a fixed tick
    /// rate, then fades out and returns to its pool.
    /// </summary>
    /// <remarks>
    /// Uses a physics overlap query each tick, so it needs no collider or Rigidbody of its own. The query
    /// writes into a preallocated buffer and does not allocate.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HazardZone : MonoBehaviour, IPoolable
    {
        private const int MaxHitsPerTick = 32;

        [SerializeField, Min(0.05f)]
        [Tooltip("Damage radius in world units. Scale the sprite to match (a 1-unit circle sprite needs scale = 2 × radius).")]
        private float radius = 1.25f;

        [SerializeField, Min(1)]
        private int damagePerTick = 1;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between damage ticks.")]
        private float tickInterval = 0.4f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the zone lasts.")]
        private float lifetime = 2.5f;

        [SerializeField]
        [Tooltip("Layers that take damage. Set to Enemy.")]
        private LayerMask damageMask;

        [SerializeField]
        [Tooltip("Optional. Faded out over the last third of the lifetime.")]
        private SpriteRenderer spriteRenderer;

        private readonly Collider2D[] hits = new Collider2D[MaxHitsPerTick];
        private ContactFilter2D filter;
        private ObjectPool<HazardZone> owner;
        private float endTime;
        private float nextTickTime;
        private Color baseColor;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
            }

            filter = new ContactFilter2D();
            filter.SetLayerMask(damageMask);
            filter.useTriggers = false;
        }

        /// <summary>Binds the zone to its pool. Call right after taking it from the pool.</summary>
        public void Activate(ObjectPool<HazardZone> pool)
        {
            owner = pool;
        }

        void IPoolable.OnSpawnFromPool()
        {
            endTime = Time.time + lifetime;
            nextTickTime = Time.time + tickInterval * 0.5f;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
        }

        void IPoolable.OnReturnToPool()
        {
        }

        private void Update()
        {
            if (Time.time >= endTime)
            {
                if (owner == null || !owner.ReturnToPool(this))
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            if (spriteRenderer != null)
            {
                float remaining = (endTime - Time.time) / lifetime;
                Color c = baseColor;
                c.a *= Mathf.Clamp01(remaining * 3f);
                spriteRenderer.color = c;
            }

            if (Time.time >= nextTickTime)
            {
                nextTickTime += tickInterval;
                DamageOverlaps();
            }
        }

        private void DamageOverlaps()
        {
            int count = Physics2D.OverlapCircle(transform.position, radius, filter, hits);
            for (int i = 0; i < count; i++)
            {
                if (DamageableLookup.TryGet(hits[i], out IDamageable target))
                {
                    target.TakeDamage(damagePerTick);
                }

                hits[i] = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

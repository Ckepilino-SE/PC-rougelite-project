using Core.Pooling;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// A pooled one-shot visual effect (hit spark, parry flash, death burst). Works two ways, and both can be
    /// combined:
    /// <list type="bullet">
    /// <item><b>Sprite pop</b>: a SpriteRenderer (e.g. a Kenney particle PNG) that grows, spins and fades out.</item>
    /// <item><b>Particle systems</b> on this object or its children are restarted on every spawn.</item>
    /// </list>
    /// Returns itself to its pool after <see cref="lifetime"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledEffect : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(0.02f)]
        [Tooltip("Seconds before the effect returns to its pool.")]
        private float lifetime = 0.35f;

        [SerializeField]
        [Tooltip("Optional. Sprite that pops out and fades.")]
        private SpriteRenderer sprite;

        [SerializeField, Min(0f)]
        private float startScale = 0.4f;

        [SerializeField, Min(0f)]
        private float endScale = 1.2f;

        [SerializeField]
        [Tooltip("Random rotation on spawn, so repeated hits don't look identical.")]
        private bool randomRotation = true;

        [SerializeField]
        [Tooltip("Runs on real time, so it still plays during hit-stop.")]
        private bool unscaledTime = true;

        private ObjectPool<PooledEffect> owner;
        private ParticleSystem[] particles;
        private Color baseColor = Color.white;
        private float age;
        private float sizeMultiplier = 1f;

        private void Awake()
        {
            if (sprite == null)
            {
                sprite = GetComponent<SpriteRenderer>();
            }

            if (sprite != null)
            {
                baseColor = sprite.color;
            }

            particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        /// <summary>Binds the effect to its pool, and optionally tints and resizes it. Call right after taking it.</summary>
        public void Play(ObjectPool<PooledEffect> pool, Color tint, float size = 1f)
        {
            owner = pool;
            sizeMultiplier = size;
            if (sprite != null)
            {
                sprite.color = baseColor * tint;
            }

            Apply(0f);
        }

        void IPoolable.OnSpawnFromPool()
        {
            age = 0f;
            sizeMultiplier = 1f;
            if (randomRotation)
            {
                transform.rotation = Quaternion.Euler(0f, 0f, Random.value * 360f);
            }

            if (sprite != null)
            {
                sprite.color = baseColor;
            }

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }

            Apply(0f);
        }

        void IPoolable.OnReturnToPool()
        {
        }

        private void Update()
        {
            age += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            if (age >= lifetime)
            {
                if (owner == null || !owner.ReturnToPool(this))
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            Apply(age / lifetime);
        }

        private void Apply(float t)
        {
            if (sprite == null)
            {
                return;
            }

            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * (Mathf.Lerp(startScale, endScale, eased) * sizeMultiplier);
            Color c = sprite.color;
            c.a = (1f - t) * baseColor.a;
            sprite.color = c;
        }
    }
}

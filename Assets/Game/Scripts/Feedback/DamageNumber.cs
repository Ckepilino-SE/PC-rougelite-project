using Core.Pooling;
using TMPro;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// A pooled, world-space floating number. Pops up, drifts upward and fades, then returns to its pool.
    /// </summary>
    /// <remarks>
    /// Use a world-space <b>TextMeshPro</b> (3D Object → Text - TextMeshPro), not the UI version, so it needs no
    /// Canvas. Setting the number uses <c>TMP_Text.SetText</c> with a format, which does not allocate.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DamageNumber : MonoBehaviour, IPoolable
    {
        [SerializeField]
        [Tooltip("Defaults to the TMP text on this object.")]
        private TMP_Text text;

        [SerializeField, Min(0.05f)]
        private float lifetime = 0.6f;

        [SerializeField]
        [Tooltip("Units per second the number drifts upward.")]
        private float riseSpeed = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Random sideways drift so stacked numbers don't overlap.")]
        private float horizontalJitter = 0.6f;

        [SerializeField, Min(1f)]
        [Tooltip("Scale at spawn; settles to 1 quickly for a 'pop'.")]
        private float popScale = 1.6f;

        [SerializeField]
        private Color normalColor = Color.white;

        [SerializeField]
        private Color bigHitColor = new Color(1f, 0.85f, 0.2f, 1f);

        [SerializeField, Min(1)]
        [Tooltip("Hits of at least this much damage use the big-hit colour and size.")]
        private int bigHitThreshold = 3;

        private ObjectPool<DamageNumber> owner;
        private Vector3 velocity;
        private Vector3 baseScale;
        private float age;
        private float sizeMultiplier = 1f;
        private Color color;

        private void Awake()
        {
            if (text == null)
            {
                text = GetComponentInChildren<TMP_Text>();
            }

            baseScale = transform.localScale;
        }

        /// <summary>Shows <paramref name="amount"/>. Call right after taking this from its pool.</summary>
        public void Show(ObjectPool<DamageNumber> pool, int amount)
        {
            owner = pool;
            bool big = amount >= bigHitThreshold;
            color = big ? bigHitColor : normalColor;
            sizeMultiplier = big ? 1.35f : 1f;
            velocity = new Vector3(Random.Range(-horizontalJitter, horizontalJitter), riseSpeed, 0f);

            if (text != null)
            {
                text.SetText("{0}", amount);
                text.color = color;
            }
        }

        void IPoolable.OnSpawnFromPool()
        {
            age = 0f;
        }

        void IPoolable.OnReturnToPool()
        {
            transform.localScale = baseScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / lifetime;
            if (t >= 1f)
            {
                if (owner == null || !owner.ReturnToPool(this))
                {
                    gameObject.SetActive(false);
                }

                return;
            }

            transform.position += velocity * Time.deltaTime;

            float pop = Mathf.Lerp(popScale, 1f, Mathf.Clamp01(t * 6f));
            transform.localScale = baseScale * (pop * sizeMultiplier);

            if (text != null)
            {
                Color c = color;
                c.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
                text.color = c;
            }
        }
    }
}

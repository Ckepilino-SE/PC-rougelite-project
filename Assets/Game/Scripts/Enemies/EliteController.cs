using Core.Combat;
using Core.Timing;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>
    /// Makes an enemy an elite: armoured, with its own posture meter shown as a small bar above it. Parrying the
    /// elite and hitting it with reflected shots fill the meter; when full it is staggered (stunned, takes bonus
    /// damage). Add it next to <see cref="EnemyController"/>.
    /// </summary>
    /// <remarks>
    /// The bar is two child SpriteRenderers (background and fill) assigned in the Inspector; the fill is scaled
    /// along X from its left edge, so give it a sprite whose pivot is centre (the default) and leave it unscaled.
    /// </remarks>
    [RequireComponent(typeof(EnemyController))]
    [DisallowMultipleComponent]
    public sealed class EliteController : MonoBehaviour, IPostureReceiver
    {
        [Header("Armour & Stagger")]
        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Damage multiplier while NOT staggered.")]
        private float armoredDamageMultiplier = 0.4f;

        [SerializeField, Min(1f)]
        private float staggeredDamageMultiplier = 3f;

        [SerializeField, Min(1f)]
        private float maxPosture = 40f;

        [SerializeField, Min(0f)]
        [Tooltip("Posture added when parried (doubled on a perfect parry).")]
        private float posturePerParry = 20f;

        [SerializeField, Min(0f)]
        private float postureDecayPerSecond = 5f;

        [SerializeField, Min(0f)]
        private float postureDecayDelay = 2f;

        [SerializeField, Min(0.1f)]
        private float staggerDuration = 2.5f;

        [Header("Bar")]
        [SerializeField]
        private SpriteRenderer barBackground;

        [SerializeField]
        private SpriteRenderer barFill;

        [SerializeField]
        private Color postureColor = new Color(1f, 0.85f, 0.2f);

        [SerializeField]
        private Color staggeredColor = Color.white;

        private EnemyController enemy;
        private Health health;
        private float posture;
        private float lastGainTime;
        private float staggerEndTime = -1f;
        private Vector3 fillBaseScale;
        private Vector3 fillOffset;
        private Vector3 backgroundOffset;
        private float fillWorldWidth;

        /// <summary>Raised when any elite is staggered. Argument: the elite.</summary>
        public static event System.Action<EliteController> AnyStaggered;

        /// <summary>Posture 0..1.</summary>
        public float PostureFraction => Mathf.Clamp01(posture / maxPosture);

        /// <summary>True while staggered.</summary>
        public bool IsStaggered => Time.time < staggerEndTime;

        private void Awake()
        {
            enemy = GetComponent<EnemyController>();
            health = GetComponent<Health>();
            // Offsets are captured in world space (the prefab is unrotated here) so the bar can stay level
            // above the elite even when the elite turns to face the player.
            if (barFill != null)
            {
                fillBaseScale = barFill.transform.localScale;
                fillOffset = barFill.transform.position - transform.position;
                float spriteWidth = barFill.sprite != null ? barFill.sprite.bounds.size.x : 1f;
                fillWorldWidth = spriteWidth * barFill.transform.lossyScale.x;
            }

            if (barBackground != null)
            {
                backgroundOffset = barBackground.transform.position - transform.position;
            }
        }

        private void OnEnable()
        {
            posture = 0f;
            staggerEndTime = -1f;
            enemy.Parried += HandleParried;
        }

        private void OnDisable()
        {
            enemy.Parried -= HandleParried;
        }

        /// <inheritdoc />
        public void AddPosture(float amount)
        {
            if (!enemy.IsAlive || IsStaggered || amount <= 0f)
            {
                return;
            }

            posture += amount;
            lastGainTime = Time.time;
            if (posture >= maxPosture)
            {
                posture = 0f;
                staggerEndTime = Time.time + staggerDuration;
                enemy.Stun(staggerDuration, Vector2.zero);
                GameTime.HitStop(0.08f);
                AnyStaggered?.Invoke(this);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyStaggered = null;
        }

        private void HandleParried(ParryResult result)
        {
            if (IsStaggered)
            {
                enemy.Stun(staggerEndTime - Time.time, Vector2.zero);
                return;
            }

            AddPosture(posturePerParry * (result.IsPerfect ? 2f : 1f));
        }

        private void Update()
        {
            bool staggered = IsStaggered;

            // Health resets its multiplier on spawn, so the armour is re-applied every frame.
            health.DamageTakenMultiplier = staggered ? staggeredDamageMultiplier : armoredDamageMultiplier;

            if (!staggered && posture > 0f && Time.time - lastGainTime > postureDecayDelay)
            {
                posture = Mathf.Max(0f, posture - postureDecayPerSecond * Time.deltaTime);
            }

            if (barFill == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            if (barBackground != null)
            {
                barBackground.transform.SetPositionAndRotation(origin + backgroundOffset, Quaternion.identity);
            }

            float fraction = staggered ? Mathf.Clamp01((staggerEndTime - Time.time) / staggerDuration) : PostureFraction;
            Transform fill = barFill.transform;
            fill.localScale = new Vector3(fillBaseScale.x * fraction, fillBaseScale.y, fillBaseScale.z);
            fill.SetPositionAndRotation(origin + fillOffset + Vector3.left * (fillWorldWidth * (1f - fraction) * 0.5f), Quaternion.identity);
            barFill.color = staggered ? staggeredColor : postureColor;
        }
    }
}

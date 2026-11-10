using Gameplay.Combat;
using Gameplay.Enemies.States;
using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>
    /// Pulses the enemy's sprite while it winds up an attack (melee or ranged), tints it while stunned, and
    /// flashes it when hit, so the player can read what's happening. Purely visual.
    /// </summary>
    /// <remarks>
    /// Runs on scaled time, so the pulse freezes along with everything else while the game is paused.
    /// The sprite's original colour is captured once and restored whenever no attack is being telegraphed.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemyTelegraphVisual : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Enemy to watch. Defaults to the one on this object or a parent.")]
        private EnemyController enemy;

        [SerializeField]
        [Tooltip("Sprite to tint. Defaults to the first SpriteRenderer on this object or its children.")]
        private SpriteRenderer spriteRenderer;

        [Header("Hit Flash")]
        [SerializeField]
        [Tooltip("Colour shown briefly when the enemy takes damage. White works with plain tinted sprites; " +
                 "for detailed art a flash shader looks better.")]
        private Color hitFlashColor = Color.white;

        [SerializeField, Min(0f)]
        [Tooltip("Real seconds the hit flash lasts (real time, so it shows through hit-stop).")]
        private float hitFlashDuration = 0.08f;

        [Header("Melee Wind-up")]
        [SerializeField]
        private Color meleeFlashColor = new Color(1f, 0.15f, 0.15f, 1f);

        [Header("Ranged Wind-up")]
        [SerializeField]
        private Color rangedFlashColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("Stunned (after being parried)")]
        [SerializeField]
        private Color stunnedColor = new Color(0.45f, 0.65f, 1f, 1f);

        [Header("Pulse")]
        [SerializeField, Min(0f)]
        [Tooltip("Flashes per second during a wind-up.")]
        private float pulseFrequency = 8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How far toward the flash colour the sprite goes at the dimmest point of each pulse.")]
        private float minimumBlend = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Uniform scale added at the peak of each pulse. 0 to disable. Only applies when the sprite is on a child object.")]
        private float pulseScale = 0.12f;

        private Color baseColor;
        private Vector3 baseScale;
        private bool isTinted;
        private bool canScale;
        private Health health;
        private float hitFlashEndTime = -1f;

        private void Awake()
        {
            if (enemy == null)
            {
                enemy = GetComponentInParent<EnemyController>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (enemy != null)
            {
                enemy.TryGetComponent(out health);
            }

            if (spriteRenderer != null)
            {
                baseColor = spriteRenderer.color;
                baseScale = spriteRenderer.transform.localScale;

                // Only swell a child sprite. Scaling the enemy's root would also resize its collider.
                canScale = enemy == null || spriteRenderer.transform != enemy.transform;
            }
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.DamageTaken += HandleDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamageTaken -= HandleDamageTaken;
            }

            hitFlashEndTime = -1f;
            Restore();
        }

        private void HandleDamageTaken(int amount)
        {
            hitFlashEndTime = Time.unscaledTime + hitFlashDuration;
        }

        private void LateUpdate()
        {
            if (enemy == null || spriteRenderer == null || !enemy.IsAlive)
            {
                Restore();
                return;
            }

            if (Time.unscaledTime < hitFlashEndTime)
            {
                Apply(hitFlashColor, 1f, 0f);
                return;
            }

            if (enemy.IsStunned)
            {
                Apply(stunnedColor, 1f, 0f);
                return;
            }

            Color flash;
            IEnemyAttackStatus custom = enemy.CustomAttackStatus;
            if (custom != null && custom.IsTelegraphing)
            {
                flash = custom.IsMeleeTelegraph ? meleeFlashColor : rangedFlashColor;
            }
            else if (enemy.AttackState.Phase == AttackPhase.Telegraph)
            {
                flash = meleeFlashColor;
            }
            else if (enemy.RangedAttackState.IsAiming)
            {
                flash = rangedFlashColor;
            }
            else
            {
                Restore();
                return;
            }

            // 0..1 triangle-ish wave via a cosine; starts at full flash on the first frame of the wind-up.
            float wave = 0.5f + 0.5f * Mathf.Cos(Time.time * pulseFrequency * 2f * Mathf.PI);
            Apply(flash, Mathf.Lerp(minimumBlend, 1f, wave), pulseScale * wave);
        }

        private void Apply(Color target, float blend, float extraScale)
        {
            spriteRenderer.color = Color.Lerp(baseColor, target, blend);
            if (canScale)
            {
                spriteRenderer.transform.localScale = baseScale * (1f + extraScale);
            }

            isTinted = true;
        }

        private void Restore()
        {
            if (!isTinted || spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = baseColor;
            if (canScale)
            {
                spriteRenderer.transform.localScale = baseScale;
            }

            isTinted = false;
        }
    }
}

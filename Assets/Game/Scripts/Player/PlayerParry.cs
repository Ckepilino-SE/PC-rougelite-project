using System;
using Core.Combat;
using Core.Timing;
using Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Player
{
    /// <summary>Data sent with <see cref="PlayerParry.Parried"/>.</summary>
    public readonly struct ParryEvent
    {
        /// <summary>Creates the event.</summary>
        public ParryEvent(Vector2 attackerPosition, bool isPerfect, bool fromDash, IPowerSource source = null)
        {
            AttackerPosition = attackerPosition;
            IsPerfect = isPerfect;
            FromDash = fromDash;
            Source = source;
        }

        /// <summary>Who made the parried attack (the enemy, or the enemy that fired the shot). May be null.</summary>
        public IPowerSource Source { get; }

        /// <summary>Where the parried attacker or projectile was.</summary>
        public Vector2 AttackerPosition { get; }

        /// <summary>True if the parry landed in the perfect window.</summary>
        public bool IsPerfect { get; }

        /// <summary>True if the parry came from dashing into an enemy (upgrade), not the Parry button.</summary>
        public bool FromDash { get; }
    }

    /// <summary>
    /// Timed parry. Press Parry to open a short window; any enemy lunge or parryable projectile that touches
    /// the player during it is cancelled. Lunging enemies are stunned and knocked back; projectiles are
    /// reflected toward the cursor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Perfect parry</b>: an attack that lands within <see cref="perfectWindow"/> of the press is "perfect":
    /// stronger reflect and counter-hit, a longer stun, and the dash cooldown is reset. Effects such as
    /// slow motion and sound live in <c>ParryFeedback</c>, which listens to <see cref="Parried"/>.
    /// </para>
    /// <para>
    /// A successful parry resets the parry cooldown and grants brief invulnerability, so parrying well is
    /// rewarded with chaining. A miss leaves you open for the full cooldown.
    /// </para>
    /// <para>
    /// Upgrades adjust behaviour through <see cref="ReflectDamageMultiplier"/>, <see cref="AddParryWhileDashing"/>
    /// and <see cref="ParryByDash"/>.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerParry : MonoBehaviour, IParryHandler
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds the parry is active after pressing the button.")]
        private float parryWindow = 0.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds from pressing Parry until it can be pressed again (unless the parry succeeds).")]
        private float cooldown = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Invulnerability granted on a successful parry.")]
        private float successInvulnerability = 0.4f;

        [SerializeField]
        [Tooltip("Reset the cooldown when a parry lands.")]
        private bool refundCooldownOnSuccess = true;

        [Header("Perfect Parry")]
        [SerializeField, Min(0f)]
        [Tooltip("An attack landing within this many seconds of the press is a perfect parry.")]
        private float perfectWindow = 0.06f;

        [SerializeField, Min(1f)]
        [Tooltip("Extra reflected-projectile damage on a perfect parry.")]
        private float perfectReflectMultiplier = 2f;

        [SerializeField, Min(1f)]
        [Tooltip("Extra counter-hit damage to a parried melee enemy on a perfect parry.")]
        private float perfectCounterMultiplier = 2f;

        [SerializeField, Min(1f)]
        [Tooltip("Stun duration multiplier on a perfect parry.")]
        private float perfectStunMultiplier = 1.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Invulnerability granted on a perfect parry (replaces the normal amount).")]
        private float perfectInvulnerability = 0.7f;

        [SerializeField]
        [Tooltip("A perfect parry makes the dash available immediately.")]
        private bool perfectResetsDash = true;

        [Header("Optional Feedback")]
        [SerializeField]
        [Tooltip("Sprite tinted while the parry window is open and flashed on success.")]
        private SpriteRenderer feedbackRenderer;

        [SerializeField]
        private Color activeColor = new Color(0.55f, 0.85f, 1f, 1f);

        [SerializeField]
        private Color successColor = Color.white;

        [SerializeField]
        private Color perfectColor = new Color(1f, 0.9f, 0.3f, 1f);

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the success colour is held (real time, so it shows through hit-stop).")]
        private float successFlashDuration = 0.12f;

        private PlayerMovement movement;
        private PlayerDash dash;
        private Health health;

        private float windowStartTime = -1f;
        private float windowEndTime = -1f;
        private float nextParryTime;
        private float successFlashEndTime = -1f;
        private bool lastWasPerfect;
        private int parryWhileDashingSources;
        private Color baseColor;
        private bool isTinted;

        /// <summary>Raised on every successful parry, after its consequences are applied.</summary>
        public event Action<ParryEvent> Parried;

        /// <summary>True while the parry window is open (or while dashing, with the matching upgrade).</summary>
        public bool IsParrying =>
            Time.time < windowEndTime || (parryWhileDashingSources > 0 && dash != null && dash.IsDashing);

        /// <summary>0 when parry is available, rising to 1 right after use. For a HUD cooldown meter.</summary>
        public float CooldownFraction =>
            cooldown > 0f ? Mathf.Clamp01((nextParryTime - Time.time) / (cooldown * CooldownMultiplier)) : 0f;

        /// <summary>Multiplier on reflected-projectile damage. Upgrades multiply into this and divide back out.</summary>
        public float ReflectDamageMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on the parry and perfect-parry windows.</summary>
        public float WindowMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on the parry cooldown.</summary>
        public float CooldownMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on damage dealt to parried enemies.</summary>
        public float CounterDamageMultiplier { get; set; } = 1f;

        /// <summary>Multiplier on how long parried enemies stay stunned.</summary>
        public float StunMultiplier { get; set; } = 1f;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            TryGetComponent(out dash);
            TryGetComponent(out health);

            if (feedbackRenderer != null)
            {
                baseColor = feedbackRenderer.color;
            }
        }

        private void OnDisable()
        {
            windowEndTime = -1f;
            successFlashEndTime = -1f;
            RestoreColor();
        }

        /// <summary>Input callback for the <c>Parry</c> action (Button).</summary>
        public void OnParry(InputAction.CallbackContext context)
        {
            if (!context.performed || GameTime.IsPaused || Time.time < nextParryTime)
            {
                return;
            }

            windowStartTime = Time.time;
            windowEndTime = Time.time + parryWindow * WindowMultiplier;
            nextParryTime = Time.time + cooldown * CooldownMultiplier;
        }

        /// <summary>Lets the dash count as a parry window. Call <see cref="RemoveParryWhileDashing"/> to undo.</summary>
        public void AddParryWhileDashing()
        {
            parryWhileDashingSources++;
        }

        /// <summary>Undoes one <see cref="AddParryWhileDashing"/>.</summary>
        public void RemoveParryWhileDashing()
        {
            parryWhileDashingSources = Mathf.Max(0, parryWhileDashingSources - 1);
        }

        /// <inheritdoc />
        public bool TryParry(Vector2 attackerPosition, IPowerSource source, out ParryResult result)
        {
            if (!IsParrying)
            {
                result = default;
                return false;
            }

            bool fromButton = Time.time < windowEndTime;
            bool perfect = fromButton && Time.time - windowStartTime <= perfectWindow * WindowMultiplier;
            result = BuildResult(perfect);
            OnSuccess(attackerPosition, perfect, !fromButton, source);
            return true;
        }

        /// <summary>
        /// Parries <paramref name="target"/> directly, without it attacking first (e.g. dashing into an enemy).
        /// Never perfect. Fires <see cref="Parried"/> like a normal parry.
        /// </summary>
        public void ParryByDash(IParryable target, Vector2 targetPosition)
        {
            if (target == null)
            {
                return;
            }

            ParryResult result = BuildResult(perfect: false);
            target.OnParried(transform.position, result);
            OnSuccess(targetPosition, false, true, target as IPowerSource);
        }

        private ParryResult BuildResult(bool perfect)
        {
            return new ParryResult(
                movement.AimDirection,
                perfect,
                ReflectDamageMultiplier * (perfect ? perfectReflectMultiplier : 1f),
                CounterDamageMultiplier * (perfect ? perfectCounterMultiplier : 1f),
                StunMultiplier * (perfect ? perfectStunMultiplier : 1f));
        }

        private void OnSuccess(Vector2 attackerPosition, bool perfect, bool fromDash, IPowerSource source)
        {
            if (health != null)
            {
                health.GrantInvulnerability(perfect ? perfectInvulnerability : successInvulnerability);
            }

            if (refundCooldownOnSuccess)
            {
                nextParryTime = Time.time;
            }

            if (perfect && perfectResetsDash && dash != null)
            {
                dash.ResetCooldown();
            }

            lastWasPerfect = perfect;
            successFlashEndTime = Time.unscaledTime + successFlashDuration;
            Parried?.Invoke(new ParryEvent(attackerPosition, perfect, fromDash, source));
        }

        private void LateUpdate()
        {
            if (feedbackRenderer == null)
            {
                return;
            }

            if (Time.unscaledTime < successFlashEndTime)
            {
                feedbackRenderer.color = lastWasPerfect ? perfectColor : successColor;
                isTinted = true;
            }
            else if (Time.time < windowEndTime)
            {
                feedbackRenderer.color = activeColor;
                isTinted = true;
            }
            else
            {
                RestoreColor();
            }
        }

        private void RestoreColor()
        {
            if (isTinted && feedbackRenderer != null)
            {
                feedbackRenderer.color = baseColor;
                isTinted = false;
            }
        }
    }
}

using System;
using Core.Timing;
using Gameplay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Player
{
    /// <summary>
    /// Short, fast dash with invulnerability frames. Dashes in the movement direction, or toward the cursor
    /// when standing still.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wire <c>Player/Dash</c> to <see cref="OnDash"/> on the <see cref="PlayerInput"/> component.
    /// </para>
    /// <para>
    /// While dashing, <see cref="PlayerMovement"/> stops writing velocity and this component drives the
    /// Rigidbody2D instead. A press during the cooldown is buffered briefly so a slightly early press still dashes.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerMovement))]
    [DisallowMultipleComponent]
    public sealed class PlayerDash : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Dash speed in units per second.")]
        private float dashSpeed = 20f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Seconds the dash lasts. Distance = speed × duration.")]
        private float dashDuration = 0.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds from the start of one dash until the next is allowed.")]
        private float cooldown = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Extra invulnerability after the dash ends, so you don't get hit the instant you stop.")]
        private float invulnerabilityPadding = 0.05f;

        [SerializeField, Min(0f)]
        [Tooltip("How early, in seconds, a press during the cooldown is remembered.")]
        private float inputBuffer = 0.12f;

        [Header("Optional Feedback")]
        [SerializeField]
        [Tooltip("Trail that only emits while dashing.")]
        private TrailRenderer trail;

        private Rigidbody2D body;
        private PlayerMovement movement;
        private Health health;

        private Vector2 dashDirection;
        private float dashEndTime;
        private float nextDashTime;
        private float bufferedUntil = -1f;

        /// <summary>Raised when a dash starts. Argument: dash direction.</summary>
        public event Action<Vector2> Dashed;

        /// <summary>Raised for each collider the player bumps into while dashing. Used by upgrades.</summary>
        public event Action<Collider2D> DashCollided;

        /// <summary>Multiplier on the dash cooldown. Upgrades multiply into this and divide back out.</summary>
        public float CooldownMultiplier { get; set; } = 1f;

        /// <summary>True while the dash is moving the player.</summary>
        public bool IsDashing { get; private set; }

        /// <summary>0 when a dash is available, rising to 1 right after dashing. For a HUD cooldown meter.</summary>
        public float CooldownFraction =>
            cooldown > 0f ? Mathf.Clamp01((nextDashTime - Time.time) / (cooldown * CooldownMultiplier)) : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            movement = GetComponent<PlayerMovement>();
            TryGetComponent(out health);

            if (trail != null)
            {
                trail.emitting = false;
            }
        }

        private void OnDisable()
        {
            EndDash();
            bufferedUntil = -1f;
        }

        /// <summary>Input callback for the <c>Dash</c> action (Button).</summary>
        public void OnDash(InputAction.CallbackContext context)
        {
            if (context.performed && !GameTime.IsPaused)
            {
                bufferedUntil = Time.time + inputBuffer;
            }
        }

        private void FixedUpdate()
        {
            if (IsDashing)
            {
                body.linearVelocity = dashDirection * dashSpeed;
                if (Time.time >= dashEndTime)
                {
                    EndDash();
                }

                return;
            }

            if (Time.time <= bufferedUntil && Time.time >= nextDashTime)
            {
                bufferedUntil = -1f;
                StartDash();
            }
        }

        /// <summary>Shortens the remaining cooldown by <paramref name="seconds"/>.</summary>
        public void ReduceCooldown(float seconds)
        {
            nextDashTime = Mathf.Max(Time.time, nextDashTime - seconds);
        }

        /// <summary>Makes the dash available immediately (e.g. a perfect-parry reward).</summary>
        public void ResetCooldown()
        {
            nextDashTime = Time.time;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDashing)
            {
                DashCollided?.Invoke(collision.collider);
            }
        }

        private void StartDash()
        {
            Vector2 direction = movement.MoveInput;
            dashDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : movement.AimDirection;

            IsDashing = true;
            dashEndTime = Time.time + dashDuration;
            nextDashTime = Time.time + cooldown * CooldownMultiplier;
            body.linearVelocity = dashDirection * dashSpeed;

            if (health != null)
            {
                health.GrantInvulnerability(dashDuration + invulnerabilityPadding);
            }

            if (trail != null)
            {
                trail.emitting = true;
            }

            Dashed?.Invoke(dashDirection);
        }

        private void EndDash()
        {
            if (!IsDashing)
            {
                return;
            }

            IsDashing = false;
            if (trail != null)
            {
                trail.emitting = false;
            }
        }
    }
}

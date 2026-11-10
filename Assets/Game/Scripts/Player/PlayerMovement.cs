using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Player
{
    /// <summary>
    /// Top-down 2D movement and mouse-cursor aiming. Reads input and drives physics velocity only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Input arrives through a <see cref="PlayerInput"/> component set to <b>Invoke Unity Events</b>:
    /// wire <c>Player/Move</c> to <see cref="OnMove"/> and <c>Player/Aim</c> (pointer position) to
    /// <see cref="OnAim"/>.
    /// </para>
    /// <para>
    /// The Rigidbody2D's rotation should be frozen so collisions cannot spin the player. Aiming therefore
    /// rotates <see cref="aimPivot"/>, a child transform holding the sprite and weapon muzzle, rather
    /// than the body itself.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Top speed in units per second.")]
        private float moveSpeed = 6f;

        [SerializeField]
        [Tooltip("Child transform rotated toward the cursor (sprite, weapon muzzle).")]
        private Transform aimPivot;

        [SerializeField]
        [Tooltip("Degrees added to the aim angle. -90 for art facing up (+Y), 0 for art facing right (+X).")]
        private float rotationOffset = -90f;

        [SerializeField]
        [Tooltip("Camera used to convert the cursor to world space. Defaults to Camera.main.")]
        private Camera aimCamera;

        private Rigidbody2D body;
        private PlayerDash dash;
        private Vector2 moveInput;
        private Vector2 aimScreenPosition;
        private bool hasAimInput;

        /// <summary>Configured top speed in units per second.</summary>
        public float MoveSpeed => moveSpeed * SpeedMultiplier;

        /// <summary>Multiplier on move speed. Upgrades multiply into this and divide back out.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Current movement input, clamped to unit length.</summary>
        public Vector2 MoveInput => moveInput;

        /// <summary>Normalized world-space direction from the player to the cursor.</summary>
        public Vector2 AimDirection { get; private set; } = Vector2.up;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            TryGetComponent(out dash);

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }

            if (aimPivot == null)
            {
                aimPivot = transform;
            }
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// Input callback for the <c>Move</c> action (Vector2). Diagonals are clamped to unit length.
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);
        }

        /// <summary>
        /// Input callback for the <c>Aim</c> action (Vector2, bound to <c>&lt;Pointer&gt;/position</c>).
        /// </summary>
        public void OnAim(InputAction.CallbackContext context)
        {
            aimScreenPosition = context.ReadValue<Vector2>();
            hasAimInput = true;
        }

        private void Update()
        {
            // Aim is visual only, so it runs at frame rate. It is recomputed every frame even when the
            // cursor is still, because the player moving changes the direction to the cursor.
            if (!hasAimInput || aimCamera == null)
            {
                return;
            }

            Vector3 pivotPosition = aimPivot.position;
            float depth = Mathf.Abs(aimCamera.transform.position.z - pivotPosition.z);
            Vector3 cursorWorld = aimCamera.ScreenToWorldPoint(new Vector3(aimScreenPosition.x, aimScreenPosition.y, depth));

            Vector2 toCursor = (Vector2)(cursorWorld - pivotPosition);
            if (toCursor.sqrMagnitude < 0.0001f)
            {
                return;
            }

            AimDirection = toCursor.normalized;
            float angle = Mathf.Atan2(toCursor.y, toCursor.x) * Mathf.Rad2Deg + rotationOffset;
            aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FixedUpdate()
        {
            // The dash owns velocity while it runs.
            if (dash != null && dash.IsDashing)
            {
                return;
            }

            body.linearVelocity = moveInput * (moveSpeed * SpeedMultiplier);
        }
    }
}

using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>
    /// Turns the enemy to face the player, for top-down art (Kenney characters face right). Turns smoothly, and
    /// holds still while stunned or winding up so the telegraph direction stays readable.
    /// </summary>
    /// <remarks>
    /// Rotates the enemy's Rigidbody2D, so its collider turns with it. For side-view art, tick
    /// <see cref="flipInsteadOfRotate"/> to mirror the sprite left/right instead.
    /// </remarks>
    [RequireComponent(typeof(EnemyController))]
    [DisallowMultipleComponent]
    public sealed class FaceTarget : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Degrees added so the art's front points at the player. Art facing right = 0, facing up = -90.")]
        private float rotationOffset;

        [SerializeField, Min(0f)]
        [Tooltip("Max turn speed in degrees per second. 0 = snap instantly.")]
        private float turnSpeed = 540f;

        [SerializeField]
        [Tooltip("Mirror the sprite left/right instead of rotating (side-view art).")]
        private bool flipInsteadOfRotate;

        [SerializeField]
        [Tooltip("Sprite to flip. Defaults to the first SpriteRenderer on this object or its children.")]
        private SpriteRenderer spriteRenderer;

        private EnemyController enemy;
        private Rigidbody2D body;

        private void Awake()
        {
            enemy = GetComponent<EnemyController>();
            body = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void FixedUpdate()
        {
            if (!enemy.IsAlive || enemy.IsStunned || !enemy.TryGetTargetPosition(out Vector2 target))
            {
                return;
            }

            Vector2 to = target - body.position;
            if (to.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (flipInsteadOfRotate)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = to.x < 0f;
                }

                return;
            }

            // Lock facing during a wind-up so the player can read where the attack will go.
            IEnemyAttackStatus custom = enemy.CustomAttackStatus;
            bool windingUp = enemy.AttackState.Phase == States.AttackPhase.Telegraph
                || enemy.AttackState.IsLunging
                || (custom != null && (custom.IsContactDamageActive || (custom.IsTelegraphing && custom.IsMeleeTelegraph)));
            if (windingUp)
            {
                return;
            }

            float desired = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg + rotationOffset;
            float angle = turnSpeed > 0f
                ? Mathf.MoveTowardsAngle(body.rotation, desired, turnSpeed * Time.fixedDeltaTime)
                : desired;
            // Set directly so it also works when the Rigidbody2D has Freeze Rotation ticked.
            body.rotation = angle;
        }
    }
}

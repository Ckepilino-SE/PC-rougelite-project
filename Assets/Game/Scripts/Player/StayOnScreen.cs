using Core.Cameras;
using UnityEngine;

namespace Gameplay.Player
{
    /// <summary>
    /// Keeps the player inside the camera's view: walking or dashing into an edge stops at the edge.
    /// Put it on the player (the object with the Rigidbody2D).
    /// </summary>
    /// <remarks>
    /// Runs after movement and dash each physics step, clamps the position, and removes any velocity pushing
    /// outward, so the player slides along the edge instead of jittering against it.
    /// </remarks>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public sealed class StayOnScreen : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Distance kept from the screen edge, in world units. About the player's radius.")]
        private float edgePadding = 0.5f;

        private Rigidbody2D body;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        private void FixedUpdate()
        {
            if (!CameraBounds.TryGetView(edgePadding, out Vector2 min, out Vector2 max))
            {
                return;
            }

            Vector2 position = body.position;
            Vector2 clamped = new Vector2(Mathf.Clamp(position.x, min.x, max.x), Mathf.Clamp(position.y, min.y, max.y));
            if (clamped == position)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            if ((clamped.x > position.x && velocity.x < 0f) || (clamped.x < position.x && velocity.x > 0f))
            {
                velocity.x = 0f;
            }

            if ((clamped.y > position.y && velocity.y < 0f) || (clamped.y < position.y && velocity.y > 0f))
            {
                velocity.y = 0f;
            }

            body.position = clamped;
            body.linearVelocity = velocity;
        }
    }
}

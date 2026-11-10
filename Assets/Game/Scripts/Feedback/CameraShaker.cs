using Core.Events;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// Trauma-based screen shake. Put it on the camera. Anything can call <see cref="Shake"/>; strengths add up
    /// and decay smoothly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs on real (unscaled) time so the shake keeps moving through hit-stop and slow motion, which is what
    /// sells an impact.
    /// </para>
    /// <para>
    /// The offset is removed and re-applied each frame, after every other script (late execution order), so it
    /// never drifts the camera and stays compatible with a follow script that moves the camera itself.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class CameraShaker : MonoBehaviour
    {
        private static CameraShaker instance;

        [SerializeField, Min(0f)]
        [Tooltip("Largest positional offset in world units, at full trauma.")]
        private float maxOffset = 0.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Largest roll in degrees, at full trauma.")]
        private float maxRoll = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Trauma lost per second. Higher = shorter shakes.")]
        private float decayPerSecond = 2.5f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Shake speed (noise frequency).")]
        private float frequency = 25f;

        [Header("Optional Auto-Shake")]
        [SerializeField]
        [Tooltip("Shake when the player is damaged.")]
        private PlayerDamagedEventChannelSO playerDamagedChannel;

        [SerializeField, Range(0f, 1f)]
        private float playerDamagedTrauma = 0.6f;

        private float trauma;
        private Vector3 appliedOffset;
        private float appliedRoll;
        private float seed;

        /// <summary>
        /// Adds screen shake. <paramref name="amount"/> is 0..1 trauma; shake intensity is trauma squared, so
        /// small hits stay subtle and big ones punch. Safe to call with no shaker in the scene.
        /// </summary>
        public static void Shake(float amount)
        {
            if (instance != null)
            {
                instance.trauma = Mathf.Clamp01(instance.trauma + amount);
            }
        }

        private void Awake()
        {
            seed = Random.value * 100f;
        }

        private void OnEnable()
        {
            instance = this;
            if (playerDamagedChannel != null)
            {
                playerDamagedChannel.OnPlayerDamaged += HandlePlayerDamaged;
            }
        }

        private void OnDisable()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (playerDamagedChannel != null)
            {
                playerDamagedChannel.OnPlayerDamaged -= HandlePlayerDamaged;
            }

            RemoveOffset();
            trauma = 0f;
        }

        private void HandlePlayerDamaged(int current, int max)
        {
            Shake(playerDamagedTrauma);
        }

        private void LateUpdate()
        {
            RemoveOffset();

            if (trauma <= 0f)
            {
                return;
            }

            float intensity = trauma * trauma;
            float t = Time.unscaledTime * frequency;
            float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
            float y = Mathf.PerlinNoise(seed + 10f, t) * 2f - 1f;
            float r = Mathf.PerlinNoise(seed + 20f, t) * 2f - 1f;

            appliedOffset = new Vector3(x, y, 0f) * (maxOffset * intensity);
            appliedRoll = r * maxRoll * intensity;
            transform.localPosition += appliedOffset;
            transform.localRotation *= Quaternion.Euler(0f, 0f, appliedRoll);

            trauma = Mathf.Max(0f, trauma - decayPerSecond * Time.unscaledDeltaTime);
        }

        private void RemoveOffset()
        {
            if (appliedOffset != Vector3.zero || appliedRoll != 0f)
            {
                transform.localPosition -= appliedOffset;
                transform.localRotation *= Quaternion.Euler(0f, 0f, -appliedRoll);
                appliedOffset = Vector3.zero;
                appliedRoll = 0f;
            }
        }
    }
}

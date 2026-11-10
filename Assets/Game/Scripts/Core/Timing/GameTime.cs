using UnityEngine;

namespace Core.Timing
{
    /// <summary>
    /// The single owner of <see cref="Time.timeScale"/>. Combines menu pausing, hit-stop and slow motion so
    /// those systems never overwrite each other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Priority</b>: paused (0) &gt; hit-stop (0) &gt; slow motion (its scale) &gt; normal (1).
    /// Hit-stop and slow motion count down in real (unscaled) time, so they end on schedule even though they
    /// freeze or slow the game clock.
    /// </para>
    /// <para>
    /// Gameplay input should check <see cref="IsPaused"/>, not <c>Time.timeScale == 0</c>: during a hit-stop the
    /// time scale is also 0, but the player is still playing and their inputs must count.
    /// </para>
    /// <para>
    /// <see cref="Time.fixedDeltaTime"/> is scaled with slow motion so physics stays smooth instead of stepping
    /// at a fraction of the normal rate.
    /// </para>
    /// <para>
    /// A hidden, persistent runner object is created automatically on first use; nothing to place in the scene.
    /// </para>
    /// </remarks>
    public static class GameTime
    {
        private static bool isPaused;
        private static float hitStopEndsAt;
        private static float slowMotionEndsAt;
        private static float slowMotionScale = 1f;
        private static float baseFixedDeltaTime = 0.02f;
        private static Runner runner;

        /// <summary>True while gameplay is paused by a menu (stage clear, game over).</summary>
        public static bool IsPaused => isPaused;

        /// <summary>True during a hit-stop freeze.</summary>
        public static bool IsHitStopped => Time.unscaledTime < hitStopEndsAt;

        /// <summary>Pauses or resumes gameplay. Cancels any running hit-stop or slow motion.</summary>
        public static void SetPaused(bool paused)
        {
            isPaused = paused;
            hitStopEndsAt = 0f;
            slowMotionEndsAt = 0f;
            Apply();
        }

        /// <summary>Freezes the game for <paramref name="realSeconds"/>. Overlapping calls extend, never shorten.</summary>
        public static void HitStop(float realSeconds)
        {
            if (isPaused || realSeconds <= 0f)
            {
                return;
            }

            EnsureRunner();
            hitStopEndsAt = Mathf.Max(hitStopEndsAt, Time.unscaledTime + realSeconds);
            Apply();
        }

        /// <summary>
        /// Slows the game to <paramref name="scale"/> for <paramref name="realSeconds"/>. Starts after any active
        /// hit-stop. A stronger (lower) scale replaces a weaker one.
        /// </summary>
        public static void SlowMotion(float scale, float realSeconds)
        {
            if (isPaused || realSeconds <= 0f)
            {
                return;
            }

            EnsureRunner();
            float start = Mathf.Max(Time.unscaledTime, hitStopEndsAt);
            bool active = Time.unscaledTime < slowMotionEndsAt;
            slowMotionScale = active ? Mathf.Min(slowMotionScale, scale) : Mathf.Clamp(scale, 0.01f, 1f);
            slowMotionEndsAt = Mathf.Max(slowMotionEndsAt, start + realSeconds);
            Apply();
        }

        private static void Apply()
        {
            float now = Time.unscaledTime;
            float scale;

            if (isPaused || now < hitStopEndsAt)
            {
                scale = 0f;
            }
            else if (now < slowMotionEndsAt)
            {
                scale = slowMotionScale;
            }
            else
            {
                scale = 1f;
            }

            Time.timeScale = scale;
            Time.fixedDeltaTime = baseFixedDeltaTime * (scale > 0f ? scale : 1f);
        }

        private static void EnsureRunner()
        {
            if (runner != null)
            {
                return;
            }

            var go = new GameObject("[GameTime]");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<Runner>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            isPaused = false;
            hitStopEndsAt = 0f;
            slowMotionEndsAt = 0f;
            slowMotionScale = 1f;
            runner = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureFixedStep()
        {
            baseFixedDeltaTime = Time.fixedDeltaTime;
            Time.timeScale = 1f;
        }

        /// <summary>Re-evaluates the time scale every frame so timed effects end on schedule.</summary>
        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                Apply();
            }

            private void OnDestroy()
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDeltaTime;
            }
        }
    }
}

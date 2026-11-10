using UnityEngine;

namespace Core.Cameras
{
    /// <summary>
    /// The visible area of the main camera on the gameplay plane (z = 0). Allocation-free; works with
    /// orthographic and perspective cameras.
    /// </summary>
    public static class CameraBounds
    {
        /// <summary>Gets the visible rectangle, shrunk by <paramref name="inset"/> on every side.</summary>
        /// <returns><c>false</c> if there is no main camera.</returns>
        public static bool TryGetView(float inset, out Vector2 min, out Vector2 max)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                min = max = default;
                return false;
            }

            float depth = Mathf.Abs(cam.transform.position.z);
            min = (Vector2)cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth)) + new Vector2(inset, inset);
            max = (Vector2)cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth)) - new Vector2(inset, inset);
            return true;
        }

        /// <summary>
        /// True if <paramref name="point"/> is on screen, at least <paramref name="inset"/> inside the edges.
        /// Also true when there is no camera, so gameplay never stalls.
        /// </summary>
        public static bool Contains(Vector2 point, float inset = 0f)
        {
            if (!TryGetView(inset, out Vector2 min, out Vector2 max))
            {
                return true;
            }

            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
        }
    }
}

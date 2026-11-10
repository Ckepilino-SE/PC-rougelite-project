using UnityEngine;

namespace Core.References
{
    /// <summary>
    /// Registers this GameObject's transform in a <see cref="TransformAnchorSO"/> while enabled.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransformAnchorRegistrar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Anchor asset to register this transform in.")]
        private TransformAnchorSO anchor;

        private void OnEnable()
        {
            if (anchor != null)
            {
                anchor.Set(transform);
            }
        }

        private void OnDisable()
        {
            if (anchor != null)
            {
                anchor.Clear(transform);
            }
        }
    }
}

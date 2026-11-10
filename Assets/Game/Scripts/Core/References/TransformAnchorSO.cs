using System;
using UnityEngine;

namespace Core.References
{
    /// <summary>
    /// ScriptableObject slot that holds a runtime <see cref="Transform"/> (e.g. the player) so other systems
    /// can find it without a direct scene reference or a <c>FindObjectOfType</c> call.
    /// </summary>
    /// <remarks>
    /// The owner registers itself with a <see cref="TransformAnchorRegistrar"/>. Readers check
    /// <see cref="IsSet"/> (or null-check <see cref="Value"/>) every time they use it, since the owner
    /// may be disabled or destroyed at any point.
    /// </remarks>
    [CreateAssetMenu(menuName = "Core/References/Transform Anchor", fileName = "TransformAnchor")]
    public sealed class TransformAnchorSO : ScriptableObject
    {
        [NonSerialized]
        private Transform value;

        /// <summary>The registered transform, or null if nothing is registered.</summary>
        public Transform Value => value;

        /// <summary>True while a live transform is registered.</summary>
        public bool IsSet => value != null;

        /// <summary>Registers <paramref name="target"/>, replacing any previous value.</summary>
        public void Set(Transform target)
        {
            value = target;
        }

        /// <summary>Clears the slot, but only if <paramref name="target"/> is the one registered.</summary>
        public void Clear(Transform target)
        {
            if (value == target)
            {
                value = null;
            }
        }
    }
}

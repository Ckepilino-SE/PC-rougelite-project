using UnityEngine;

namespace Core.Combat
{
    /// <summary>
    /// Allocation-free helpers for finding an <see cref="IDamageable"/> from a physics contact.
    /// </summary>
    public static class DamageableLookup
    {
        /// <summary>
        /// Finds an <see cref="IDamageable"/> on <paramref name="collider"/>, or on its Rigidbody2D's GameObject
        /// when the collider lives on a child. <c>TryGetComponent</c> does not allocate, even when nothing is found.
        /// </summary>
        public static bool TryGet(Collider2D collider, out IDamageable damageable)
        {
            if (collider.TryGetComponent(out damageable))
            {
                return true;
            }

            Rigidbody2D attached = collider.attachedRigidbody;
            return attached != null && attached.TryGetComponent(out damageable);
        }
    }
}

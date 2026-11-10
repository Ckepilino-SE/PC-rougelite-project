using UnityEngine;

namespace Core.Combat
{
    /// <summary>Outcome of a successful parry, handed to whatever was parried.</summary>
    public readonly struct ParryResult
    {
        /// <summary>Creates a result.</summary>
        public ParryResult(Vector2 redirect, bool isPerfect, float reflectDamageMultiplier, float counterDamageMultiplier, float stunMultiplier)
        {
            Redirect = redirect;
            IsPerfect = isPerfect;
            ReflectDamageMultiplier = reflectDamageMultiplier;
            CounterDamageMultiplier = counterDamageMultiplier;
            StunMultiplier = stunMultiplier;
        }

        /// <summary>Direction a reflected projectile should fly (unit length).</summary>
        public Vector2 Redirect { get; }

        /// <summary>True if the parry landed inside the tight "perfect" window.</summary>
        public bool IsPerfect { get; }

        /// <summary>Extra damage multiplier for a reflected projectile.</summary>
        public float ReflectDamageMultiplier { get; }

        /// <summary>Extra damage multiplier for the counter-hit on a parried melee attacker.</summary>
        public float CounterDamageMultiplier { get; }

        /// <summary>Multiplier on how long a parried attacker is stunned.</summary>
        public float StunMultiplier { get; }
    }

    /// <summary>
    /// Something whose ability the player can steal with a perfect parry (an enemy). Projectiles carry their
    /// shooter as their source, so reflecting a shot can steal from whoever fired it.
    /// </summary>
    public interface IPowerSource
    {
        /// <summary>The stealable power asset, or null if this source has none.</summary>
        ScriptableObject StealablePower { get; }
    }

    /// <summary>
    /// Something with a posture (stagger) meter, such as a boss. Reflected shots and parries fill it; when full,
    /// the target is staggered and takes extra damage.
    /// </summary>
    public interface IPostureReceiver
    {
        /// <summary>Adds <paramref name="amount"/> to the posture meter.</summary>
        void AddPosture(float amount);
    }

    /// <summary>
    /// Something that can parry incoming attacks (the player). Attackers ask it before dealing damage; if it
    /// returns <c>true</c>, the attack is cancelled and the attacker applies its own consequence (a lunging
    /// enemy is stunned, a projectile is reflected).
    /// </summary>
    public interface IParryHandler
    {
        /// <summary>Attempts to parry an attack coming from <paramref name="attackerPosition"/>.</summary>
        /// <returns><c>true</c> if the attack was parried and must not deal damage.</returns>
        /// <param name="source">Who made the attack (for power stealing). May be null.</param>
        bool TryParry(Vector2 attackerPosition, IPowerSource source, out ParryResult result);
    }

    /// <summary>Something that reacts to being parried (e.g. an enemy that gets stunned).</summary>
    public interface IParryable
    {
        /// <summary>Called when this attacker is parried by something at <paramref name="parrierPosition"/>.</summary>
        void OnParried(Vector2 parrierPosition, in ParryResult result);
    }

    /// <summary>Allocation-free component lookup from a physics contact.</summary>
    public static class ParryLookup
    {
        /// <summary>
        /// Finds a <typeparamref name="T"/> on <paramref name="collider"/> or on its Rigidbody2D's GameObject.
        /// </summary>
        public static bool TryGet<T>(Collider2D collider, out T component) where T : class
        {
            if (collider.TryGetComponent(out component))
            {
                return true;
            }

            Rigidbody2D attached = collider.attachedRigidbody;
            if (attached != null && attached.TryGetComponent(out component))
            {
                return true;
            }

            component = null;
            return false;
        }

        /// <summary>
        /// True if <paramref name="collider"/> belongs to something that parries this attack right now.
        /// </summary>
        public static bool TryParry(Collider2D collider, Vector2 attackerPosition, IPowerSource source, out ParryResult result)
        {
            if (TryGet(collider, out IParryHandler handler))
            {
                return handler.TryParry(attackerPosition, source, out result);
            }

            result = default;
            return false;
        }
    }
}

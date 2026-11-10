namespace Core.Combat
{
    /// <summary>
    /// Anything that can receive damage: the player, enemies, destructible props.
    /// </summary>
    /// <remarks>
    /// Damage dealers (projectiles, hazards, melee hitboxes) should depend only on this interface,
    /// typically via <c>collider.TryGetComponent(out IDamageable target)</c>, never on concrete types.
    /// </remarks>
    public interface IDamageable
    {
        /// <summary>
        /// Applies <paramref name="amount"/> damage.
        /// </summary>
        /// <param name="amount">Damage to apply. Non-positive values are ignored.</param>
        /// <returns>
        /// <c>true</c> if the damage was applied; <c>false</c> if it was rejected
        /// (e.g. invulnerable, already dead, or a non-positive amount).
        /// </returns>
        bool TakeDamage(int amount);
    }
}

using System;
using Core.Combat;
using Core.Events;
using Core.Pooling;
using UnityEngine;

namespace Gameplay.Combat
{
    /// <summary>
    /// Owns hit points, damage intake, invulnerability frames, and death notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usable on the player and on enemies. Local C# events (<see cref="Damaged"/>, <see cref="Died"/>)
    /// serve components on the same object (flash, knockback, AI). The optional
    /// <see cref="PlayerDamagedEventChannelSO"/> broadcasts to decoupled systems such as the HUD;
    /// assign it on the player only and leave it empty on enemies.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable, IPoolable
    {
        [SerializeField, Min(1)]
        [Tooltip("Base hit points on spawn, before any multiplier.")]
        private int maxHealth = 5;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds of invulnerability after taking a hit. 0 disables i-frames.")]
        private float invulnerabilityDuration = 0.75f;

        [SerializeField]
        [Tooltip("Optional. Assign on the player to broadcast OnPlayerDamaged(current, max).")]
        private PlayerDamagedEventChannelSO playerDamagedChannel;

        private int currentHealth;
        private int currentMaxHealth;
        private int bonusMaxHealth;
        private int deathWards;
        private float damageRemainder;
        private float invulnerableUntil;

        /// <summary>Raised after damage is applied. Arguments: current health, max health.</summary>
        public event Action<int, int> Damaged;

        /// <summary>Raised after damage is applied. Argument: hit points actually removed. For hit flashes and damage numbers.</summary>
        public event Action<int> DamageTaken;

        /// <summary>Raised after healing is applied. Arguments: current health, max health.</summary>
        public event Action<int, int> Healed;

        /// <summary>Raised when a death ward saves this object from a lethal hit (health is left at 1).</summary>
        public event Action DeathWardUsed;

        /// <summary>Raised once when health reaches zero.</summary>
        public event Action Died;

        /// <summary>Current hit points.</summary>
        public int CurrentHealth => currentHealth;

        /// <summary>Maximum hit points, including any multiplier applied this life.</summary>
        public int MaxHealth => currentMaxHealth;

        /// <summary>Base maximum hit points as configured in the Inspector.</summary>
        public int BaseMaxHealth => maxHealth;

        /// <summary>Length of the post-hit invulnerability window, in seconds.</summary>
        public float InvulnerabilityDuration => invulnerabilityDuration;

        /// <summary>
        /// Multiplier on all incoming damage (e.g. 0.5 for an armoured boss, 2 while staggered). Fractions carry
        /// over between hits, so 1-damage shots against 0.5 armour deal 1 damage every second hit.
        /// </summary>
        public float DamageTakenMultiplier { get; set; } = 1f;

        /// <summary>Lethal hits this object will survive at 1 health. Granted by upgrades.</summary>
        public int DeathWards => deathWards;

        /// <summary>Flat max-health bonus from upgrades. Survives stage heals; removed when the upgrade detaches.</summary>
        public int BonusMaxHealth => bonusMaxHealth;

        /// <summary>True once health has reached zero.</summary>
        public bool IsDead => currentHealth <= 0;

        /// <summary>True while inside an invulnerability window.</summary>
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        private void Awake()
        {
            ResetHealth();
        }

        /// <inheritdoc />
        public bool TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvulnerable)
            {
                return false;
            }

            if (!Mathf.Approximately(DamageTakenMultiplier, 1f))
            {
                float scaled = amount * Mathf.Max(0f, DamageTakenMultiplier) + damageRemainder;
                amount = Mathf.FloorToInt(scaled);
                damageRemainder = scaled - amount;
                if (amount <= 0)
                {
                    return false;
                }
            }

            int previous = currentHealth;
            currentHealth = Mathf.Max(0, currentHealth - amount);
            invulnerableUntil = Time.time + invulnerabilityDuration;

            bool warded = false;
            if (currentHealth == 0 && deathWards > 0)
            {
                deathWards--;
                currentHealth = 1;
                invulnerableUntil = Time.time + Mathf.Max(invulnerabilityDuration, 1.5f);
                warded = true;
            }

            DamageTaken?.Invoke(previous - currentHealth);
            Damaged?.Invoke(currentHealth, currentMaxHealth);
            if (playerDamagedChannel != null)
            {
                playerDamagedChannel.Raise(currentHealth, currentMaxHealth);
            }

            if (warded)
            {
                DeathWardUsed?.Invoke();
            }

            if (currentHealth == 0)
            {
                Died?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Starts (or extends) an invulnerability window independent of taking damage, e.g. for a dodge roll.
        /// </summary>
        /// <param name="duration">Seconds of invulnerability from now.</param>
        public void GrantInvulnerability(float duration)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        }

        /// <summary>
        /// Restores up to <paramref name="amount"/> health, capped at <see cref="MaxHealth"/>. Does nothing when dead.
        /// </summary>
        /// <returns>The amount actually restored.</returns>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return 0;
            }

            int before = currentHealth;
            currentHealth = Mathf.Min(currentMaxHealth, currentHealth + amount);
            int restored = currentHealth - before;
            if (restored > 0)
            {
                Healed?.Invoke(currentHealth, currentMaxHealth);
            }

            return restored;
        }

        /// <summary>Restores <paramref name="fraction"/> (0–1) of <see cref="MaxHealth"/>, rounded up.</summary>
        /// <returns>The amount actually restored.</returns>
        public int HealFraction(float fraction)
        {
            return Heal(Mathf.CeilToInt(currentMaxHealth * Mathf.Clamp01(fraction)));
        }

        /// <summary>
        /// Changes the flat max-health bonus by <paramref name="amount"/> (negative to remove). Gaining max health
        /// also heals by the same amount; losing it caps current health. Never kills.
        /// </summary>
        public void AddBonusMaxHealth(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            bonusMaxHealth += amount;
            int newMax = Mathf.Max(1, maxHealth + bonusMaxHealth);
            int delta = newMax - currentMaxHealth;
            currentMaxHealth = newMax;

            if (!IsDead)
            {
                currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0, delta), 1, currentMaxHealth);
            }

            Healed?.Invoke(currentHealth, currentMaxHealth);
        }

        /// <summary>Raises the death-ward count to at least <paramref name="count"/>.</summary>
        public void RefillDeathWards(int count)
        {
            deathWards = Mathf.Max(deathWards, count);
        }

        /// <summary>Removes all death wards.</summary>
        public void ClearDeathWards()
        {
            deathWards = 0;
        }

        /// <summary>
        /// Scales max health from the Inspector base value and refills to the new max. Used for per-stage
        /// difficulty; call after <see cref="ResetHealth"/> (pooled enemies are reset on spawn automatically).
        /// </summary>
        /// <param name="multiplier">Multiplier on the base max health. The result is at least 1.</param>
        public void ApplyMaxHealthMultiplier(float multiplier)
        {
            currentMaxHealth = Mathf.Max(1, Mathf.RoundToInt((maxHealth + bonusMaxHealth) * multiplier));
            currentHealth = currentMaxHealth;
        }

        /// <summary>Restores full base health, removes any multiplier, and clears invulnerability.</summary>
        public void ResetHealth()
        {
            currentMaxHealth = Mathf.Max(1, maxHealth + bonusMaxHealth);
            currentHealth = currentMaxHealth;
            invulnerableUntil = 0f;
            damageRemainder = 0f;
            DamageTakenMultiplier = 1f;
        }

        void IPoolable.OnSpawnFromPool()
        {
            ResetHealth();
        }

        void IPoolable.OnReturnToPool()
        {
        }
    }
}

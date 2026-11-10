using System;
using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>
    /// Multipliers applied to an enemy's base stats when it spawns, used for per-stage difficulty.
    /// </summary>
    [Serializable]
    public struct EnemyModifiers
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Multiplier on the enemy's base max health.")]
        private float healthMultiplier;

        [SerializeField, Min(0.1f)]
        [Tooltip("Multiplier on chase speed, chase acceleration and lunge speed.")]
        private float speedMultiplier;

        [SerializeField, Min(0.1f)]
        [Tooltip("Multiplier on lunge damage.")]
        private float damageMultiplier;

        /// <summary>Creates a modifier set.</summary>
        public EnemyModifiers(float health, float speed, float damage)
        {
            healthMultiplier = health;
            speedMultiplier = speed;
            damageMultiplier = damage;
        }

        /// <summary>No change to base stats.</summary>
        public static EnemyModifiers Default => new EnemyModifiers(1f, 1f, 1f);

        /// <summary>Multiplier on base max health.</summary>
        public float HealthMultiplier => healthMultiplier;

        /// <summary>Multiplier on movement and lunge speed.</summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>Multiplier on lunge damage.</summary>
        public float DamageMultiplier => damageMultiplier;
    }
}

using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>
    /// "Sovereign Echo" (stolen from any boss): you periodically unleash the boss's own bullet ring, as your shots.
    /// </summary>
    [CreateAssetMenu(fileName = "Power_BossEcho", menuName = "Game/Powers/Boss Echo (Auto Bullet Ring)")]
    public sealed class BossEchoPower : StolenPowerDefinition
    {
        [SerializeField, Min(0.5f)]
        [Tooltip("Seconds between rings at level 1.")]
        private float baseInterval = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds taken off the interval per level above 1.")]
        private float intervalReductionPerLevel = 1f;

        [SerializeField, Min(1)]
        private int baseProjectiles = 10;

        [SerializeField, Min(0)]
        private int projectilesPerLevel = 4;

        [SerializeField, Min(0f)]
        private float damageMultiplier = 2f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect(int level)
        {
            float interval = Mathf.Max(0.75f, baseInterval - intervalReductionPerLevel * (level - 1));
            return new Effect(interval, baseProjectiles + projectilesPerLevel * (level - 1), damageMultiplier);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly float interval;
            private readonly int projectiles;
            private readonly float damage;
            private float timer;

            public Effect(float interval, int projectiles, float damage)
            {
                this.interval = interval;
                this.projectiles = projectiles;
                this.damage = damage;
            }

            protected override void OnAttach()
            {
                // Fire straight away so the steal feels immediate.
                timer = interval;
            }

            protected override void OnDetach()
            {
            }

            protected override void OnTick(float deltaTime)
            {
                timer += deltaTime;
                if (timer < interval || Context.Weapon == null)
                {
                    return;
                }

                timer = 0f;
                Context.Weapon.FireRing(Context.Player.transform.position, projectiles, damage);
            }
        }
    }
}

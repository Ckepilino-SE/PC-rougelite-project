using Core.Combat;
using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>"Blink Strike" (stolen from Dashers): your dash recharges faster and hurts enemies you ram.</summary>
    [CreateAssetMenu(fileName = "Power_BlinkStrike", menuName = "Game/Powers/Blink Strike (Dash Damage)")]
    public sealed class BlinkStrikePower : StolenPowerDefinition
    {
        [SerializeField, Min(0)]
        [Tooltip("Damage dealt to an enemy you dash into, per level.")]
        private int damagePerLevel = 2;

        [SerializeField, Range(0f, 0.3f)]
        [Tooltip("Dash cooldown reduction per level. 0.12 = 12% shorter.")]
        private float cooldownReductionPerLevel = 0.12f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect(int level)
        {
            return new Effect(damagePerLevel * level, Mathf.Max(0.2f, 1f - cooldownReductionPerLevel * level));
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly int damage;
            private readonly float cooldownFactor;

            public Effect(int damage, float cooldownFactor)
            {
                this.damage = damage;
                this.cooldownFactor = cooldownFactor;
            }

            protected override void OnAttach()
            {
                if (Context.Dash == null)
                {
                    return;
                }

                Context.Dash.CooldownMultiplier *= cooldownFactor;
                Context.Dash.DashCollided += HandleDashCollided;
            }

            protected override void OnDetach()
            {
                if (Context.Dash == null)
                {
                    return;
                }

                Context.Dash.CooldownMultiplier /= cooldownFactor;
                Context.Dash.DashCollided -= HandleDashCollided;
            }

            private void HandleDashCollided(Collider2D other)
            {
                if (damage > 0 && DamageableLookup.TryGet(other, out IDamageable target))
                {
                    target.TakeDamage(damage);
                }
            }
        }
    }
}

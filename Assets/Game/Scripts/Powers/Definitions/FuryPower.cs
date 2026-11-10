using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>"Fury" (stolen from Grunts): your shots deal more damage per level.</summary>
    [CreateAssetMenu(fileName = "Power_Fury", menuName = "Game/Powers/Fury (Damage)")]
    public sealed class FuryPower : StolenPowerDefinition
    {
        [SerializeField, Min(0f)]
        [Tooltip("Damage bonus per level. 0.2 = +20%.")]
        private float damagePerLevel = 0.2f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect(int level)
        {
            return new Effect(1f + damagePerLevel * level);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly float factor;

            public Effect(float factor)
            {
                this.factor = factor;
            }

            protected override void OnAttach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.DamageMultiplier *= factor;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.DamageMultiplier /= factor;
                }
            }
        }
    }
}

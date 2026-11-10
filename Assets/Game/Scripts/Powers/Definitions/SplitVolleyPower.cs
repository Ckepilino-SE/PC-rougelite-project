using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>"Split Volley" (stolen from Splitters): extra projectiles on every shot.</summary>
    [CreateAssetMenu(fileName = "Power_SplitVolley", menuName = "Game/Powers/Split Volley (Multishot)")]
    public sealed class SplitVolleyPower : StolenPowerDefinition
    {
        [SerializeField, Min(1)]
        private int projectilesPerLevel = 1;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect(int level)
        {
            return new Effect(projectilesPerLevel * level);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly int extra;

            public Effect(int extra)
            {
                this.extra = extra;
            }

            protected override void OnAttach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.ExtraProjectiles += extra;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.ExtraProjectiles -= extra;
                }
            }
        }
    }
}

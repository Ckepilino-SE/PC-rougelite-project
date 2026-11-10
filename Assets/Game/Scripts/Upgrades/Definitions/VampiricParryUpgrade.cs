using Gameplay.Player;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Vampiric Parry": perfect parries heal you. Has a short cooldown so it can't be farmed.</summary>
    [CreateAssetMenu(fileName = "VampiricParry", menuName = "Game/Upgrades/Vampiric Parry (Perfect Parry Heals)")]
    public sealed class VampiricParryUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1)]
        private int healAmount = 1;

        [SerializeField]
        [Tooltip("If ticked, normal (late) parries heal too.")]
        private bool healOnNormalParry;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds between heals.")]
        private float cooldown = 4f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly VampiricParryUpgrade settings;
            private float nextHealTime;

            public Effect(VampiricParryUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (Context.Parry != null)
                {
                    Context.Parry.Parried += HandleParried;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Parry != null)
                {
                    Context.Parry.Parried -= HandleParried;
                }
            }

            private void HandleParried(ParryEvent parry)
            {
                if ((!parry.IsPerfect && !settings.healOnNormalParry) || Time.time < nextHealTime || Context.Health == null)
                {
                    return;
                }

                if (Context.Health.Heal(settings.healAmount) > 0)
                {
                    nextHealTime = Time.time + settings.cooldown;
                }
            }
        }
    }
}

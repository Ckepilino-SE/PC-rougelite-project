using Gameplay.Player;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Momentum": a perfect parry boosts fire rate and move speed for a few seconds.</summary>
    [CreateAssetMenu(fileName = "Momentum", menuName = "Game/Upgrades/Momentum (Perfect Parry Frenzy)")]
    public sealed class MomentumUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1f)]
        private float fireRateMultiplier = 1.6f;

        [SerializeField, Min(1f)]
        private float moveSpeedMultiplier = 1.25f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Seconds the boost lasts. Another perfect parry refreshes it.")]
        private float duration = 3f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly MomentumUpgrade settings;
            private float endTime;
            private bool active;

            public Effect(MomentumUpgrade settings)
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

                SetActive(false);
            }

            protected override void OnTick(float deltaTime)
            {
                if (active && Time.time >= endTime)
                {
                    SetActive(false);
                }
            }

            private void HandleParried(ParryEvent parry)
            {
                if (!parry.IsPerfect)
                {
                    return;
                }

                endTime = Time.time + settings.duration;
                SetActive(true);
            }

            private void SetActive(bool value)
            {
                if (active == value)
                {
                    return;
                }

                active = value;
                if (Context == null)
                {
                    return;
                }

                if (Context.Weapon != null)
                {
                    Context.Weapon.FireRateMultiplier *= value ? settings.fireRateMultiplier : 1f / settings.fireRateMultiplier;
                }

                if (Context.Movement != null)
                {
                    Context.Movement.SpeedMultiplier *= value ? settings.moveSpeedMultiplier : 1f / settings.moveSpeedMultiplier;
                }
            }
        }
    }
}

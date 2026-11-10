using Gameplay.Player;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// "Riposte Burst": every successful parry fires a free shotgun burst toward the cursor. Perfect parries add
    /// extra pellets. Works even when normal firing is blocked (e.g. with Mirror Pact).
    /// </summary>
    [CreateAssetMenu(fileName = "RiposteBurst", menuName = "Game/Upgrades/Riposte Burst (Parry Shotgun)")]
    public sealed class RiposteBurstUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1)]
        private int pellets = 6;

        [SerializeField, Min(0)]
        [Tooltip("Extra pellets on a perfect parry.")]
        private int perfectBonusPellets = 4;

        [SerializeField, Range(0f, 180f)]
        private float spreadDegrees = 50f;

        [SerializeField, Min(0f)]
        private float damageMultiplier = 1f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly RiposteBurstUpgrade settings;

            public Effect(RiposteBurstUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (Context.Parry == null || Context.Weapon == null)
                {
                    Debug.LogWarning("[Riposte Burst] Player needs PlayerParry and WeaponController.");
                    return;
                }

                Context.Parry.Parried += HandleParried;
            }

            protected override void OnDetach()
            {
                if (Context.Parry != null)
                {
                    Context.Parry.Parried -= HandleParried;
                }
            }

            private void HandleParried(ParryEvent parryEvent)
            {
                int count = settings.pellets + (parryEvent.IsPerfect ? settings.perfectBonusPellets : 0);
                Context.Weapon.FireBurst(count, settings.spreadDegrees, settings.damageMultiplier);
            }
        }
    }
}

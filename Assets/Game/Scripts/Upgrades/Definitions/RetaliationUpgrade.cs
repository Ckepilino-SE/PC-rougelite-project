using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Retaliation": when you get hit, you blast a ring of shots around you.</summary>
    [CreateAssetMenu(fileName = "Retaliation", menuName = "Game/Upgrades/Retaliation (Hit Fires Ring)")]
    public sealed class RetaliationUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1)]
        private int projectiles = 12;

        [SerializeField, Min(0f)]
        private float damageMultiplier = 2f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly RetaliationUpgrade settings;

            public Effect(RetaliationUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (Context.Health != null)
                {
                    Context.Health.DamageTaken += HandleDamageTaken;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Health != null)
                {
                    Context.Health.DamageTaken -= HandleDamageTaken;
                }
            }

            private void HandleDamageTaken(int amount)
            {
                if (Context.Weapon != null && !Context.Health.IsDead)
                {
                    Context.Weapon.FireRing(Context.Player.transform.position, settings.projectiles, settings.damageMultiplier);
                }
            }
        }
    }
}

using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Dash Nova": every dash fires a ring of shots around you.</summary>
    [CreateAssetMenu(fileName = "DashNova", menuName = "Game/Upgrades/Dash Nova (Dash Fires Ring)")]
    public sealed class DashNovaUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1)]
        private int projectiles = 8;

        [SerializeField, Min(0f)]
        private float damageMultiplier = 1f;

        [SerializeField]
        [Tooltip("Fire at the end of the dash as well as the start.")]
        private bool alsoOnArrival = true;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly DashNovaUpgrade settings;
            private bool wasDashing;

            public Effect(DashNovaUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (Context.Dash != null)
                {
                    Context.Dash.Dashed += HandleDashed;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Dash != null)
                {
                    Context.Dash.Dashed -= HandleDashed;
                }
            }

            protected override void OnTick(float deltaTime)
            {
                if (Context.Dash == null)
                {
                    return;
                }

                bool dashing = Context.Dash.IsDashing;
                if (wasDashing && !dashing && settings.alsoOnArrival)
                {
                    Fire();
                }

                wasDashing = dashing;
            }

            private void HandleDashed(Vector2 direction)
            {
                wasDashing = true;
                Fire();
            }

            private void Fire()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.FireRing(Context.Player.transform.position, settings.projectiles, settings.damageMultiplier);
                }
            }
        }
    }
}

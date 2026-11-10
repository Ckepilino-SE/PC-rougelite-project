using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// "Mirror Pact": you can no longer fire your gun, but reflected projectiles deal far more damage.
    /// Shotgun bursts from Riposte Burst still fire.
    /// </summary>
    [CreateAssetMenu(fileName = "MirrorPact", menuName = "Game/Upgrades/Mirror Pact (No Gun, Big Reflects)")]
    public sealed class MirrorPactUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1f)]
        private float reflectDamageMultiplier = 4f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(reflectDamageMultiplier);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly float multiplier;

            public Effect(float multiplier)
            {
                this.multiplier = multiplier;
            }

            protected override void OnAttach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.AddFireBlock();
                }

                if (Context.Parry != null)
                {
                    Context.Parry.ReflectDamageMultiplier *= multiplier;
                }
            }

            protected override void OnDetach()
            {
                if (Context.Weapon != null)
                {
                    Context.Weapon.RemoveFireBlock();
                }

                if (Context.Parry != null)
                {
                    Context.Parry.ReflectDamageMultiplier /= multiplier;
                }
            }
        }
    }
}

using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Regeneration": slowly heal over time during stages.</summary>
    [CreateAssetMenu(fileName = "Regeneration", menuName = "Game/Upgrades/Regeneration (Heal Over Time)")]
    public sealed class RegenerationUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1f)]
        [Tooltip("Seconds of gameplay per 1 health restored.")]
        private float secondsPerHeal = 20f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(secondsPerHeal);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly float interval;
            private float timer;

            public Effect(float interval)
            {
                this.interval = interval;
            }

            protected override void OnAttach()
            {
                timer = 0f;
            }

            protected override void OnDetach()
            {
            }

            protected override void OnTick(float deltaTime)
            {
                timer += deltaTime;
                if (timer < interval)
                {
                    return;
                }

                timer -= interval;
                if (Context.Health != null)
                {
                    Context.Health.Heal(1);
                }
            }
        }
    }
}

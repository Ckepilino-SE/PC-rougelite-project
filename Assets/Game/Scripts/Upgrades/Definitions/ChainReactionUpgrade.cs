using Gameplay.Enemies;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Chain Reaction": enemies you kill have a chance to burst into a ring of your shots.</summary>
    [CreateAssetMenu(fileName = "ChainReaction", menuName = "Game/Upgrades/Chain Reaction (Kills Explode)")]
    public sealed class ChainReactionUpgrade : UpgradeDefinition
    {
        [SerializeField, Range(0f, 1f)]
        private float chance = 0.3f;

        [SerializeField, Min(1)]
        private int projectiles = 6;

        [SerializeField, Min(0f)]
        private float damageMultiplier = 1f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly ChainReactionUpgrade settings;

            public Effect(ChainReactionUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                EnemyController.AnyDied += HandleEnemyDied;
            }

            protected override void OnDetach()
            {
                EnemyController.AnyDied -= HandleEnemyDied;
            }

            private void HandleEnemyDied(EnemyController enemy)
            {
                if (Context.Weapon != null && Random.value < settings.chance)
                {
                    Context.Weapon.FireRing(enemy.transform.position, settings.projectiles, settings.damageMultiplier);
                }
            }
        }
    }
}

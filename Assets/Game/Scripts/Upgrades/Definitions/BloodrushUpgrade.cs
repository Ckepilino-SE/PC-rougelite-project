using Gameplay.Enemies;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Bloodrush": every kill shortens your dash cooldown.</summary>
    [CreateAssetMenu(fileName = "Bloodrush", menuName = "Game/Upgrades/Bloodrush (Kills Refund Dash)")]
    public sealed class BloodrushUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(0f)]
        [Tooltip("Seconds taken off the dash cooldown per kill.")]
        private float secondsPerKill = 0.3f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(secondsPerKill);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly float seconds;

            public Effect(float seconds)
            {
                this.seconds = seconds;
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
                if (Context.Dash != null)
                {
                    Context.Dash.ReduceCooldown(seconds);
                }
            }
        }
    }
}

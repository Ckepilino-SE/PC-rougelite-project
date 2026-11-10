using Core.Pooling;
using Gameplay.Combat;
using Gameplay.Enemies;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// "Aftershock": when a stunned enemy recovers, it leaves a damaging hazard zone where it stood.
    /// The zone hurts enemies, not the player.
    /// </summary>
    [CreateAssetMenu(fileName = "Aftershock", menuName = "Game/Upgrades/Aftershock (Stun Hazards)")]
    public sealed class AftershockUpgrade : UpgradeDefinition
    {
        [SerializeField]
        private HazardZone hazardPrefab;

        [SerializeField, Min(1)]
        [Tooltip("Max hazards alive at once.")]
        private int poolCapacity = 16;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly AftershockUpgrade settings;

            public Effect(AftershockUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (settings.hazardPrefab == null)
                {
                    Debug.LogWarning("[Aftershock] No Hazard Prefab assigned on the upgrade asset.");
                    return;
                }

                EnemyController.AnyStunRecovered += HandleStunRecovered;
            }

            protected override void OnDetach()
            {
                EnemyController.AnyStunRecovered -= HandleStunRecovered;
            }

            private void HandleStunRecovered(EnemyController enemy)
            {
                ObjectPool<HazardZone> pool = SharedPools<HazardZone>.Get(settings.hazardPrefab, settings.poolCapacity);
                if (pool != null && pool.TryGet(enemy.transform.position, Quaternion.identity, out HazardZone zone))
                {
                    zone.Activate(pool);
                }
            }
        }
    }
}

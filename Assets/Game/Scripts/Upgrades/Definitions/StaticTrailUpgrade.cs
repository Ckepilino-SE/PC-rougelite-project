using Core.Pooling;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>"Static Trail": dashing leaves damaging hazard zones along your path.</summary>
    [CreateAssetMenu(fileName = "StaticTrail", menuName = "Game/Upgrades/Static Trail (Dash Leaves Hazards)")]
    public sealed class StaticTrailUpgrade : UpgradeDefinition
    {
        [SerializeField]
        [Tooltip("Use the HazardZone prefab (its Damage Mask should be Enemy).")]
        private HazardZone hazardPrefab;

        [SerializeField, Min(1)]
        private int poolCapacity = 16;

        [SerializeField, Min(0.02f)]
        [Tooltip("Seconds between hazards while dashing.")]
        private float spawnInterval = 0.06f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly StaticTrailUpgrade settings;
            private ObjectPool<HazardZone> pool;
            private float nextSpawnTime;

            public Effect(StaticTrailUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (settings.hazardPrefab == null)
                {
                    Debug.LogWarning("[Static Trail] No Hazard Prefab assigned on the upgrade asset.");
                    return;
                }

                // Created here (between stages) so the dash never instantiates.
                pool = SharedPools<HazardZone>.Get(settings.hazardPrefab, settings.poolCapacity);
            }

            protected override void OnDetach()
            {
                pool = null;
            }

            protected override void OnTick(float deltaTime)
            {
                if (pool == null || Context.Dash == null || !Context.Dash.IsDashing || Time.time < nextSpawnTime)
                {
                    return;
                }

                nextSpawnTime = Time.time + settings.spawnInterval;
                if (pool.TryGet(Context.Player.transform.position, Quaternion.identity, out HazardZone zone))
                {
                    zone.Activate(pool);
                }
            }
        }
    }
}

using Core.Combat;
using Gameplay.Enemies;
using Gameplay.Feedback;
using Gameplay.Player;
using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>
    /// "Shockwave" (stolen from Brutes): every parry releases a blast that damages, stuns and knocks back
    /// nearby enemies.
    /// </summary>
    [CreateAssetMenu(fileName = "Power_Shockwave", menuName = "Game/Powers/Shockwave (Parry Blast)")]
    public sealed class ShockwavePower : StolenPowerDefinition
    {
        private const int MaxHits = 32;

        [SerializeField]
        [Tooltip("Layers the blast hits. Set to Enemy.")]
        private LayerMask enemyMask;

        [SerializeField, Min(0.5f)]
        private float baseRadius = 2.5f;

        [SerializeField, Min(0f)]
        private float radiusPerLevel = 0.6f;

        [SerializeField, Min(0)]
        private int damagePerLevel = 2;

        [SerializeField, Min(0f)]
        private float stunDuration = 0.6f;

        [SerializeField, Min(0f)]
        private float knockback = 7f;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect(int level)
        {
            return new Effect(this, level);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly ShockwavePower settings;
            private readonly int level;
            private readonly Collider2D[] hits = new Collider2D[MaxHits];
            private ContactFilter2D filter;

            public Effect(ShockwavePower settings, int level)
            {
                this.settings = settings;
                this.level = level;
                filter = new ContactFilter2D();
                filter.SetLayerMask(settings.enemyMask);
                filter.useTriggers = false;
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
            }

            private void HandleParried(ParryEvent parry)
            {
                Vector2 centre = Context.Player.transform.position;
                float radius = settings.baseRadius + settings.radiusPerLevel * (level - 1);
                int damage = settings.damagePerLevel * level;
                int count = Physics2D.OverlapCircle(centre, radius, filter, hits);

                for (int i = 0; i < count; i++)
                {
                    Collider2D hit = hits[i];
                    hits[i] = null;

                    // Already-stunned enemies (including a staggered boss) keep their longer stun.
                    if (ParryLookup.TryGet(hit, out EnemyController enemy) && enemy.IsAlive && !enemy.IsStunned)
                    {
                        Vector2 away = enemy.Body.position - centre;
                        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
                        enemy.Stun(settings.stunDuration, away * settings.knockback);
                    }

                    if (damage > 0 && DamageableLookup.TryGet(hit, out IDamageable target))
                    {
                        target.TakeDamage(damage);
                    }
                }

                CameraShaker.Shake(0.25f + 0.05f * level);
            }
        }
    }
}

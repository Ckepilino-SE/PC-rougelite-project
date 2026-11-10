using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// Upgrades to power theft: more slots, a chance for normal parries to steal, and powers that survive
    /// into the next stage. One class, configured per asset (Deep Pockets, Pickpocket, Hoarder...).
    /// </summary>
    [CreateAssetMenu(fileName = "ThiefUpgrade", menuName = "Game/Upgrades/Thief Upgrade (Power Theft)")]
    public sealed class ThiefUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(0)]
        [Tooltip("Extra stolen-power slots.")]
        private int extraSlots;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Added chance for normal (non-perfect) parries to steal.")]
        private float normalStealChance;

        [SerializeField, Min(0)]
        [Tooltip("Stolen powers kept when a new stage starts (newest first).")]
        private int keepBetweenStages;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(this);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly ThiefUpgrade settings;

            public Effect(ThiefUpgrade settings)
            {
                this.settings = settings;
            }

            protected override void OnAttach()
            {
                if (Context.Thief == null)
                {
                    Debug.LogWarning("[ThiefUpgrade] The player has no PowerThief component.");
                    return;
                }

                Context.Thief.ExtraSlots += settings.extraSlots;
                Context.Thief.BonusStealChance += settings.normalStealChance;
                Context.Thief.KeepOnStageStart += settings.keepBetweenStages;
            }

            protected override void OnDetach()
            {
                if (Context.Thief == null)
                {
                    return;
                }

                Context.Thief.ExtraSlots -= settings.extraSlots;
                Context.Thief.BonusStealChance -= settings.normalStealChance;
                Context.Thief.KeepOnStageStart -= settings.keepBetweenStages;
            }
        }
    }
}

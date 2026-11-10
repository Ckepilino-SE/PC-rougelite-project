using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// "Second Wind": once per stage, a hit that would kill you leaves you at 1 health instead.
    /// Refreshes each stage (needs the Stage Started channel on PlayerUpgrades).
    /// </summary>
    [CreateAssetMenu(fileName = "SecondWind", menuName = "Game/Upgrades/Second Wind (Cheat Death)")]
    public sealed class SecondWindUpgrade : UpgradeDefinition
    {
        [SerializeField, Min(1)]
        [Tooltip("Lethal hits survived per stage.")]
        private int chargesPerStage = 1;

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(chargesPerStage);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly int charges;

            public Effect(int charges)
            {
                this.charges = charges;
            }

            protected override void OnAttach()
            {
                if (Context.Health != null)
                {
                    Context.Health.RefillDeathWards(charges);
                }
            }

            protected override void OnDetach()
            {
                if (Context.Health != null)
                {
                    Context.Health.ClearDeathWards();
                }
            }

            protected override void OnStageStarted()
            {
                if (Context.Health != null)
                {
                    Context.Health.RefillDeathWards(charges);
                }
            }
        }
    }
}

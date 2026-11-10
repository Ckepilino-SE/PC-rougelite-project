using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>How rare an upgrade is. Rarer upgrades are offered less often, and boss rewards are Rare or better.</summary>
    public enum UpgradeRarity
    {
        /// <summary>Offered often.</summary>
        Common,

        /// <summary>Offered sometimes; guaranteed after bosses.</summary>
        Rare,

        /// <summary>Offered rarely; run-defining.</summary>
        Legendary,
    }

    /// <summary>
    /// Base asset for an upgrade: what the player sees on the card, and a factory for the runtime effect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The asset holds tuning only. All per-run state lives in the <see cref="UpgradeEffect"/> it creates, so
    /// the asset is never modified at runtime and restarting a run is clean.
    /// </para>
    /// <para>
    /// To add an upgrade: subclass this with a <c>[CreateAssetMenu]</c>, subclass <see cref="UpgradeEffect"/>,
    /// subscribe to the events you need in <c>OnAttach</c> and undo everything in <c>OnDetach</c>.
    /// </para>
    /// </remarks>
    public abstract class UpgradeDefinition : ScriptableObject
    {
        [SerializeField]
        private string title = "New Upgrade";

        [SerializeField, TextArea(2, 4)]
        private string description;

        [SerializeField]
        [Tooltip("Optional card art.")]
        private Sprite icon;

        [SerializeField]
        [Tooltip("Card accent colour.")]
        private Color accentColor = Color.white;

        [SerializeField]
        private UpgradeRarity rarity = UpgradeRarity.Common;

        [SerializeField, Min(1)]
        [Tooltip("How many times this upgrade can be picked in one run. Effects stack.")]
        private int maxStacks = 1;

        /// <summary>Card title.</summary>
        public string Title => title;

        /// <summary>Card body text.</summary>
        public string Description => description;

        /// <summary>Optional icon.</summary>
        public Sprite Icon => icon;

        /// <summary>Card accent colour.</summary>
        public Color AccentColor => accentColor;

        /// <summary>Rarity tier.</summary>
        public UpgradeRarity Rarity => rarity;

        /// <summary>Max times this can be picked per run.</summary>
        public int MaxStacks => maxStacks;

        /// <summary>Creates a fresh runtime effect for one run.</summary>
        public abstract UpgradeEffect CreateEffect();
    }

    /// <summary>The runtime half of an upgrade, attached to the player for the rest of the run.</summary>
    public abstract class UpgradeEffect
    {
        /// <summary>The player's components. Valid between attach and detach.</summary>
        protected UpgradeContext Context { get; private set; }

        internal void Attach(UpgradeContext context)
        {
            Context = context;
            OnAttach();
        }

        internal void Detach()
        {
            OnDetach();
            Context = null;
        }

        internal void Tick(float deltaTime)
        {
            OnTick(deltaTime);
        }

        internal void StageStarted()
        {
            OnStageStarted();
        }

        /// <summary>Called every frame while gameplay isn't paused. Override for timers. Must not allocate.</summary>
        protected virtual void OnTick(float deltaTime)
        {
        }

        /// <summary>Called when each stage starts (needs the Stage Started channel on PlayerUpgrades).</summary>
        protected virtual void OnStageStarted()
        {
        }

        /// <summary>Subscribe to events and change player behaviour here.</summary>
        protected abstract void OnAttach();

        /// <summary>Undo everything done in <see cref="OnAttach"/>.</summary>
        protected abstract void OnDetach();
    }
}

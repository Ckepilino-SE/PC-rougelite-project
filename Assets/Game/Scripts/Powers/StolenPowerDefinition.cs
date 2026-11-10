using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>
    /// A power the player can steal from an enemy with a perfect parry. Assign the asset to the enemy's
    /// <c>Stealable Power</c> field. Stealing the same power again levels it up.
    /// </summary>
    /// <remarks>
    /// Like upgrades, the asset holds tuning only and creates a fresh <see cref="UpgradeEffect"/> for each
    /// level, so stolen powers can use the same hooks (weapon, dash, parry, health) as upgrades.
    /// </remarks>
    public abstract class StolenPowerDefinition : ScriptableObject
    {
        [SerializeField]
        private string powerName = "Power";

        [SerializeField, TextArea(2, 3)]
        [Tooltip("Short explanation shown when stolen.")]
        private string description;

        [SerializeField]
        private Sprite icon;

        [SerializeField]
        private Color color = Color.white;

        [SerializeField, Range(1, 5)]
        private int maxLevel = 3;

        /// <summary>Display name.</summary>
        public string PowerName => powerName;

        /// <summary>Explanation.</summary>
        public string Description => description;

        /// <summary>Optional icon.</summary>
        public Sprite Icon => icon;

        /// <summary>HUD colour.</summary>
        public Color Color => color;

        /// <summary>Highest level reachable by stealing it repeatedly.</summary>
        public int MaxLevel => maxLevel;

        /// <summary>Creates the runtime effect for <paramref name="level"/> (1-based).</summary>
        public abstract UpgradeEffect CreateEffect(int level);
    }
}

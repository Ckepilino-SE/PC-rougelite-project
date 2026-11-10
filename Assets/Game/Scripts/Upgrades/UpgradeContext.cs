using Gameplay.Combat;
using Gameplay.Player;
using Gameplay.Powers;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>The player components an upgrade may hook into. Any of them may be null if not on the player.</summary>
    public sealed class UpgradeContext
    {
        /// <summary>Gathers components from <paramref name="player"/> and its children.</summary>
        public UpgradeContext(GameObject player)
        {
            Player = player;
            Movement = player.GetComponentInChildren<PlayerMovement>();
            Dash = player.GetComponentInChildren<PlayerDash>();
            Parry = player.GetComponentInChildren<PlayerParry>();
            Weapon = player.GetComponentInChildren<WeaponController>();
            Health = player.GetComponentInChildren<Health>();
            Thief = player.GetComponentInChildren<PowerThief>();
        }

        /// <summary>The player object.</summary>
        public GameObject Player { get; }

        /// <summary>Movement and aim.</summary>
        public PlayerMovement Movement { get; }

        /// <summary>Dash.</summary>
        public PlayerDash Dash { get; }

        /// <summary>Parry.</summary>
        public PlayerParry Parry { get; }

        /// <summary>Gun.</summary>
        public WeaponController Weapon { get; }

        /// <summary>Hit points.</summary>
        public Health Health { get; }

        /// <summary>Stolen powers.</summary>
        public PowerThief Thief { get; }
    }
}

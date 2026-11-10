using System;
using UnityEngine;

namespace Core.Events
{
    /// <summary>
    /// ScriptableObject event channel broadcast whenever the player's health changes due to damage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create one asset via <c>Assets &gt; Create &gt; Core &gt; Events &gt; Player Damaged Channel</c> and
    /// assign the same asset to the broadcaster (e.g. <c>Health</c>) and to every listener (HUD, camera
    /// shake, audio). Neither side references the other.
    /// </para>
    /// <para>
    /// Listeners should subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>. ScriptableObject
    /// assets outlive scenes (and, with domain reload disabled, play sessions), so a missing unsubscribe
    /// keeps the listener alive and invokes it after it has been destroyed.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Core/Events/Player Damaged Channel", fileName = "PlayerDamagedChannel")]
    public sealed class PlayerDamagedEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// Raised after the player takes damage.
        /// Arguments: <c>current</c> health after the hit, <c>max</c> health.
        /// </summary>
        public event Action<int, int> OnPlayerDamaged;

        /// <summary>
        /// Notifies all listeners that the player was damaged. Allocation-free.
        /// </summary>
        /// <param name="current">The player's health after the damage was applied.</param>
        /// <param name="max">The player's maximum health.</param>
        public void Raise(int current, int max)
        {
            OnPlayerDamaged?.Invoke(current, max);
        }
    }
}

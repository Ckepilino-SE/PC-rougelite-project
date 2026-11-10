using System;
using UnityEngine;

namespace Core.Events
{
    /// <summary>
    /// ScriptableObject event channel broadcast whenever an enemy dies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create one asset via <c>Assets &gt; Create &gt; Core &gt; Events &gt; Enemy Killed Channel</c> and
    /// assign it to every enemy's death broadcaster and to listeners such as the score tracker or
    /// wave manager. Neither side references the other.
    /// </para>
    /// <para>
    /// Listeners should subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>. ScriptableObject
    /// assets outlive scenes (and, with domain reload disabled, play sessions), so a missing unsubscribe
    /// keeps the listener alive and invokes it after it has been destroyed.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Core/Events/Enemy Killed Channel", fileName = "EnemyKilledChannel")]
    public sealed class EnemyKilledEventChannelSO : ScriptableObject
    {
        /// <summary>
        /// Raised when an enemy is killed. Argument: <c>points</c> awarded for the kill.
        /// </summary>
        public event Action<int> OnEnemyKilled;

        /// <summary>
        /// Notifies all listeners that an enemy was killed. Allocation-free.
        /// </summary>
        /// <param name="points">Score awarded for the kill.</param>
        public void Raise(int points)
        {
            OnEnemyKilled?.Invoke(points);
        }
    }
}

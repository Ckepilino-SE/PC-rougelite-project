using System;
using UnityEngine;

namespace Core.Events
{
    /// <summary>Why a run ended.</summary>
    public enum GameOverReason
    {
        /// <summary>The player cleared every stage.</summary>
        AllStagesCleared,

        /// <summary>The player's health reached zero.</summary>
        PlayerDied,
    }

    /// <summary>
    /// ScriptableObject event channel broadcast once when a run ends, win or lose.
    /// </summary>
    /// <remarks>
    /// Create one asset via <c>Assets &gt; Create &gt; Core &gt; Events &gt; Game Over Channel</c>. Listeners
    /// (game-over screen, music, input disabling) subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Core/Events/Game Over Channel", fileName = "GameOverChannel")]
    public sealed class GameOverEventChannelSO : ScriptableObject
    {
        /// <summary>Raised when the run ends. Argument: why it ended.</summary>
        public event Action<GameOverReason> OnGameOver;

        /// <summary>Notifies all listeners that the run has ended. Allocation-free.</summary>
        /// <param name="reason">Why the run ended.</param>
        public void Raise(GameOverReason reason)
        {
            OnGameOver?.Invoke(reason);
        }
    }
}

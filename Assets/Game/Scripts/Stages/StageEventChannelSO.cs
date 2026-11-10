using System;
using UnityEngine;

namespace Gameplay.Stages
{
    /// <summary>
    /// ScriptableObject event channel carrying a <see cref="StageData"/>. Create one asset per event
    /// (e.g. "StageStarted", "StageCompleted").
    /// </summary>
    /// <remarks>
    /// Listeners subscribe in <c>OnEnable</c> and unsubscribe in <c>OnDisable</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Game/Events/Stage Channel", fileName = "StageChannel")]
    public sealed class StageEventChannelSO : ScriptableObject
    {
        /// <summary>Raised with the stage the event refers to.</summary>
        public event Action<StageData> OnEventRaised;

        /// <summary>Notifies all listeners. Allocation-free.</summary>
        public void Raise(StageData stage)
        {
            OnEventRaised?.Invoke(stage);
        }
    }
}

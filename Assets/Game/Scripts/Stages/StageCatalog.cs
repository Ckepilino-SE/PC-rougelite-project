using UnityEngine;

namespace Gameplay.Stages
{
    /// <summary>
    /// Ordered list of the stages in a run.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Stages/Stage Catalog", fileName = "StageCatalog")]
    public sealed class StageCatalog : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Stages in play order.")]
        private StageData[] stages = new StageData[0];

        /// <summary>Number of stages.</summary>
        public int Count => stages != null ? stages.Length : 0;

        /// <summary>Stage at <paramref name="index"/> (0-based); may be null if the slot was left empty.</summary>
        public StageData GetStage(int index)
        {
            return stages[index];
        }
    }
}

using Gameplay.Enemies;
using Gameplay.Waves;
using UnityEngine;

namespace Gameplay.Stages
{
    /// <summary>What kind of stage this is.</summary>
    public enum StageKind
    {
        /// <summary>Regular waves.</summary>
        Normal,

        /// <summary>A boss fight that ends an act. Clearing it fully heals the player and offers rarer upgrades.</summary>
        Boss,

        /// <summary>The last fight. Beating it wins the run immediately.</summary>
        FinalBoss,
    }

    /// <summary>
    /// One stage of a run: its waves and the difficulty applied to every enemy spawned in it.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Stages/Stage Data", fileName = "Stage")]
    public sealed class StageData : ScriptableObject
    {
        [SerializeField, Min(1)]
        [Tooltip("Number shown to the player, e.g. on the Stage Cleared panel.")]
        private int stageNumber = 1;

        [SerializeField]
        private string stageTitle = "New Stage";

        [SerializeField]
        private StageKind kind = StageKind.Normal;

        [SerializeField, Min(1)]
        [Tooltip("Act this stage belongs to (shown as 'Act 2 - Stage 3').")]
        private int act = 1;

        [Header("Boss (Boss / Final Boss stages)")]
        [SerializeField]
        [Tooltip("Spawned when the stage starts. The stage is cleared the moment it dies. Needs a BossController.")]
        private EnemyController bossPrefab;

        [SerializeField]
        [Tooltip("Multipliers applied to the boss only. Waves (escorts) use Enemy Modifiers.")]
        private EnemyModifiers bossModifiers = EnemyModifiers.Default;

        [SerializeField]
        [Tooltip("Waves run in order.")]
        private WaveData[] waves = new WaveData[0];

        [SerializeField, Min(0f)]
        [Tooltip("Pause before each wave, including the first.")]
        private float timeBetweenWaves = 3f;

        [SerializeField]
        [Tooltip("Multipliers applied to every enemy spawned in this stage.")]
        private EnemyModifiers enemyModifiers = EnemyModifiers.Default;

        /// <summary>Number shown to the player.</summary>
        public int StageNumber => stageNumber;

        /// <summary>Display title.</summary>
        public string StageTitle => stageTitle;

        /// <summary>Normal, Boss or Final Boss.</summary>
        public StageKind Kind => kind;

        /// <summary>True for Boss and Final Boss stages.</summary>
        public bool IsBossStage => kind != StageKind.Normal;

        /// <summary>Act number.</summary>
        public int Act => act;

        /// <summary>Boss prefab, or null.</summary>
        public EnemyController BossPrefab => bossPrefab;

        /// <summary>Boss stat multipliers.</summary>
        public EnemyModifiers BossModifiers => bossModifiers;

        /// <summary>Label like "Act 2 - Stage 3", "Act 2 - BOSS" or "FINAL BOSS".</summary>
        public string DisplayLabel
        {
            get
            {
                switch (kind)
                {
                    case StageKind.Boss:
                        return $"Act {act} - BOSS";
                    case StageKind.FinalBoss:
                        return "FINAL BOSS";
                    default:
                        return $"Act {act} - Stage {stageNumber}";
                }
            }
        }

        /// <summary>Number of waves in this stage.</summary>
        public int WaveCount => waves != null ? waves.Length : 0;

        /// <summary>Pause before each wave, in seconds.</summary>
        public float TimeBetweenWaves => timeBetweenWaves;

        /// <summary>Stat multipliers for enemies spawned in this stage.</summary>
        public EnemyModifiers EnemyModifiers => enemyModifiers;

        /// <summary>Wave at <paramref name="index"/>.</summary>
        public WaveData GetWave(int index)
        {
            return waves[index];
        }
    }
}

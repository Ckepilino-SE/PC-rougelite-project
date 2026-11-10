using System;
using Gameplay.Enemies;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// One wave: which enemies can appear, how often, and how many in total.
    /// </summary>
    /// <remarks>
    /// A wave is finished spawning once <see cref="EnemyCount"/> enemies have been spawned. The stage's
    /// time-between-waves then runs before the next wave starts, whether or not this wave's enemies are dead.
    /// </remarks>
    [Serializable]
    public struct WaveData
    {
        [SerializeField]
        [Tooltip("Enemy prefabs this wave picks from at random, one per spawn. Repeat a prefab to weight it.")]
        private EnemyController[] enemyPrefabs;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between spawns.")]
        private float spawnInterval;

        [SerializeField, Min(1)]
        [Tooltip("Total enemies this wave spawns.")]
        private int enemyCount;

        /// <summary>Seconds between spawns.</summary>
        public float SpawnInterval => spawnInterval;

        /// <summary>Total enemies this wave spawns.</summary>
        public int EnemyCount => enemyCount;

        /// <summary>Number of prefab slots in this wave (some may be empty).</summary>
        public int PrefabCount => enemyPrefabs != null ? enemyPrefabs.Length : 0;

        /// <summary>Prefab at <paramref name="index"/>; may be null if the slot was left empty.</summary>
        public EnemyController GetPrefab(int index)
        {
            return enemyPrefabs[index];
        }
    }
}

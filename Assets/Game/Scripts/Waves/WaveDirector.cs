using System;
using System.Collections.Generic;
using Core.Pooling;
using Gameplay.Enemies;
using Gameplay.Enemies.Boss;
using Gameplay.Stages;
using UnityEngine;

namespace Gameplay.Waves
{
    /// <summary>
    /// Runs the waves of one <see cref="StageData"/>: spawns pooled enemies just outside the camera's view and
    /// reports when every wave has spawned and every enemy is dead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Driven by</b> <see cref="StageManager"/>, which calls <see cref="BeginStage"/> and listens for
    /// <see cref="StageCleared"/>. This class knows nothing about stage order, pausing or game over.
    /// </para>
    /// <para>
    /// <b>Flow per stage</b>: Intermission → Spawning (wave 1) → Intermission → Spawning (wave 2) → … →
    /// WaitingForClear → Cleared. A simple state switch in <c>Update</c>, without coroutines.
    /// </para>
    /// <para>
    /// <b>Pools</b>: one <see cref="ObjectPool{T}"/> per distinct prefab, created when a stage that uses it
    /// begins (while the game is between stages) and kept for later stages.
    /// </para>
    /// <para>
    /// <b>Spawn positions</b>: a random point on a rectangle around the camera's visible area on the z = 0
    /// plane, pushed outward by <see cref="offscreenMargin"/>. Works with orthographic and perspective cameras.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WaveDirector : MonoBehaviour
    {
        /// <summary>Director lifecycle within a stage.</summary>
        public enum DirectorState
        {
            /// <summary>No stage running.</summary>
            Idle,

            /// <summary>Pause before the next wave.</summary>
            Intermission,

            /// <summary>A wave is spawning.</summary>
            Spawning,

            /// <summary>All waves have spawned; waiting for the last enemies to die.</summary>
            WaitingForClear,

            /// <summary>Stage finished. Nothing more happens until <see cref="BeginStage"/> is called again.</summary>
            Cleared,
        }

        private const int MaxPlacementAttempts = 8;

        [Header("Pools")]
        [SerializeField, Min(1)]
        [Tooltip("Max alive at once for each distinct enemy prefab. Also the pre-warm count.")]
        private int poolCapacityPerPrefab = 40;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Camera whose view defines 'off-screen'. Defaults to Camera.main.")]
        private Camera targetCamera;

        [SerializeField, Min(0f)]
        [Tooltip("World units outside the camera's view at which enemies appear.")]
        private float offscreenMargin = 1.5f;

        [SerializeField]
        [Tooltip("Layers a spawn point must not overlap (walls, obstacles). Leave as Nothing to skip the check.")]
        private LayerMask blockingMask;

        [SerializeField, Min(0f)]
        [Tooltip("Radius used for the blocking-overlap check. Roughly the enemy collider's radius.")]
        private float clearanceRadius = 0.5f;

        private readonly Dictionary<EnemyController, ObjectPool<EnemyController>> poolsByPrefab =
            new Dictionary<EnemyController, ObjectPool<EnemyController>>();

        private readonly List<ObjectPool<EnemyController>> pools = new List<ObjectPool<EnemyController>>();

        [Header("Boss")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Where the boss appears, as a fraction from the view centre toward the top edge.")]
        private float bossSpawnHeight = 0.6f;

        private StageData stage;
        private EnemyController activeBoss;
        private bool bossPending;
        private float stateEndTime;
        private float nextSpawnTime;
        private int spawnedThisWave;

        /// <summary>Raised when a wave starts spawning. Argument: zero-based wave index.</summary>
        public event Action<int> WaveStarted;

        /// <summary>Raised when a boss stage's boss appears. Argument: the boss enemy.</summary>
        public event Action<EnemyController> BossSpawned;

        /// <summary>
        /// Raised once per stage when every wave has spawned and no enemies remain, or, on a boss stage, the
        /// moment the boss dies (remaining enemies and shots are cleared).
        /// </summary>
        public event Action StageCleared;

        /// <summary>Current lifecycle state.</summary>
        public DirectorState State { get; private set; }

        /// <summary>The stage being run, or null when idle.</summary>
        public StageData CurrentStage => stage;

        /// <summary>Zero-based index of the current (or upcoming, during intermission) wave.</summary>
        public int CurrentWaveIndex { get; private set; }

        /// <summary>Enemies currently alive across all pools.</summary>
        public int AliveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < pools.Count; i++)
                {
                    count += pools[i].CountActive;
                }

                return count;
            }
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < pools.Count; i++)
            {
                pools[i].Dispose();
            }

            pools.Clear();
            poolsByPrefab.Clear();
        }

        /// <summary>
        /// Starts running <paramref name="stageData"/> from its first wave. Creates pools for any enemy prefabs
        /// not seen before, so call this while the game is between stages, not mid-combat.
        /// </summary>
        public void BeginStage(StageData stageData)
        {
            stage = stageData;
            CurrentWaveIndex = 0;
            activeBoss = null;
            bossPending = stage != null && stage.IsBossStage && stage.BossPrefab != null;

            if (stage != null && stage.IsBossStage && stage.BossPrefab == null)
            {
                Debug.LogWarning($"[WaveDirector] Boss stage '{stage.name}' has no Boss Prefab.", this);
            }

            if (stage == null || (stage.WaveCount == 0 && !bossPending))
            {
                Debug.LogWarning($"[WaveDirector] Stage '{(stage != null ? stage.name : "null")}' has no waves; clearing immediately.", this);
                State = DirectorState.WaitingForClear;
                return;
            }

            EnsurePools(stage);
            EnterIntermission();
        }

        /// <summary>Stops spawning. Enemies already alive are left as they are.</summary>
        public void Stop()
        {
            State = DirectorState.Idle;
            stage = null;
            activeBoss = null;
            bossPending = false;
        }

        /// <summary>Returns every live enemy from this director's pools without raising kill events.</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < pools.Count; i++)
            {
                pools[i].ReturnAll();
            }
        }

        private void Update()
        {
            if (activeBoss != null && !activeBoss.IsAlive && State != DirectorState.Idle && State != DirectorState.Cleared)
            {
                HandleBossDefeated();
                return;
            }

            switch (State)
            {
                case DirectorState.Intermission:
                    if (Time.time >= stateEndTime)
                    {
                        BeginWave();
                    }

                    break;

                case DirectorState.Spawning:
                    TickSpawning();
                    break;

                case DirectorState.WaitingForClear:
                    if (AliveCount == 0)
                    {
                        State = DirectorState.Cleared;
                        StageCleared?.Invoke();
                    }

                    break;
            }
        }

        private void EnterIntermission()
        {
            State = DirectorState.Intermission;
            stateEndTime = Time.time + stage.TimeBetweenWaves;
        }

        private void BeginWave()
        {
            if (bossPending)
            {
                bossPending = false;
                SpawnBoss();
                if (stage.WaveCount == 0)
                {
                    State = DirectorState.WaitingForClear;
                    return;
                }
            }

            State = DirectorState.Spawning;
            spawnedThisWave = 0;
            nextSpawnTime = Time.time;
            WaveStarted?.Invoke(CurrentWaveIndex);
        }

        private void TickSpawning()
        {
            WaveData wave = stage.GetWave(CurrentWaveIndex);

            if (Time.time >= nextSpawnTime)
            {
                if (TrySpawn(wave))
                {
                    spawnedThisWave++;
                    nextSpawnTime = Time.time + wave.SpawnInterval;
                }
                else
                {
                    // Pool full or no clear position: retry next frame rather than skipping the enemy.
                    nextSpawnTime = Time.time;
                }
            }

            if (spawnedThisWave < wave.EnemyCount)
            {
                return;
            }

            if (CurrentWaveIndex + 1 < stage.WaveCount)
            {
                CurrentWaveIndex++;
                EnterIntermission();
            }
            else
            {
                State = DirectorState.WaitingForClear;
            }
        }

        /// <summary>Spawns one random enemy from <paramref name="wave"/> off-screen. Allocation-free.</summary>
        private bool TrySpawn(in WaveData wave)
        {
            int prefabCount = wave.PrefabCount;
            if (prefabCount == 0)
            {
                // Misconfigured wave: count it as spawned so the stage can still finish.
                return true;
            }

            EnemyController prefab = wave.GetPrefab(UnityEngine.Random.Range(0, prefabCount));
            if (prefab == null || !poolsByPrefab.TryGetValue(prefab, out ObjectPool<EnemyController> pool))
            {
                return true;
            }

            if (pool.CountActive >= pool.MaxCapacity || !TryFindSpawnPosition(out Vector2 position))
            {
                return false;
            }

            if (!pool.TryGet(position, Quaternion.identity, out EnemyController enemy))
            {
                return false;
            }

            enemy.Initialize(pool, stage.EnemyModifiers);
            return true;
        }

        private void SpawnBoss()
        {
            EnemyController prefab = stage.BossPrefab;
            if (!poolsByPrefab.TryGetValue(prefab, out ObjectPool<EnemyController> pool))
            {
                return;
            }

            Vector2 position = Vector2.zero;
            if (TryGetViewBounds(targetCamera, out Vector2 min, out Vector2 max))
            {
                Vector2 centre = (min + max) * 0.5f;
                float halfHeight = (max.y - min.y) * 0.5f - offscreenMargin;
                position = centre + Vector2.up * (halfHeight * bossSpawnHeight);
            }

            if (pool.TryGet(position, Quaternion.identity, out EnemyController boss))
            {
                boss.Initialize(pool, stage.BossModifiers);
                activeBoss = boss;
                BossSpawned?.Invoke(boss);
            }
        }

        private void HandleBossDefeated()
        {
            activeBoss = null;
            State = DirectorState.Cleared;

            // The fight is over: remove escorts, summons and stray shots so the clear screen is calm.
            PoolRegistry.ReturnAllActive();
            StageCleared?.Invoke();
        }

        private bool TryFindSpawnPosition(out Vector2 position)
        {
            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                if (!TryGetPerimeterPoint(out position))
                {
                    return false;
                }

                if (blockingMask.value == 0 || Physics2D.OverlapCircle(position, clearanceRadius, blockingMask) == null)
                {
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Picks a uniformly random point on the perimeter of the camera's visible rectangle (on the z = 0 plane),
        /// expanded by <see cref="offscreenMargin"/>.
        /// </summary>
        private bool TryGetPerimeterPoint(out Vector2 point)
        {
            if (!TryGetViewBounds(targetCamera, out Vector2 min, out Vector2 max))
            {
                point = default;
                return false;
            }

            float width = max.x - min.x;
            float height = max.y - min.y;
            float t = UnityEngine.Random.value * 2f * (width + height);

            if (t < width)
            {
                point = new Vector2(min.x + t, max.y); // top edge
            }
            else if ((t -= width) < height)
            {
                point = new Vector2(max.x, max.y - t); // right edge
            }
            else if ((t -= height) < width)
            {
                point = new Vector2(max.x - t, min.y); // bottom edge
            }
            else
            {
                point = new Vector2(min.x, min.y + (t - width)); // left edge
            }

            return true;
        }

        private bool TryGetViewBounds(Camera cam, out Vector2 min, out Vector2 max)
        {
            if (cam == null)
            {
                min = max = default;
                return false;
            }

            // Distance from the camera to the gameplay plane (z = 0). Ignored by orthographic cameras.
            float depth = Mathf.Abs(cam.transform.position.z);
            Vector2 margin = new Vector2(offscreenMargin, offscreenMargin);
            min = (Vector2)cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth)) - margin;
            max = (Vector2)cam.ViewportToWorldPoint(new Vector3(1f, 1f, depth)) + margin;
            return true;
        }

        private void EnsurePools(StageData stageData)
        {
            EnemyController bossPrefab = stageData.IsBossStage ? stageData.BossPrefab : null;
            if (bossPrefab != null)
            {
                if (!poolsByPrefab.ContainsKey(bossPrefab))
                {
                    var bossPool = new ObjectPool<EnemyController>(bossPrefab, 1, 1);
                    poolsByPrefab.Add(bossPrefab, bossPool);
                    pools.Add(bossPool);
                }

                if (bossPrefab.TryGetComponent(out BossController bossController))
                {
                    bossController.WarmUpPools();
                }
            }

            for (int w = 0; w < stageData.WaveCount; w++)
            {
                WaveData wave = stageData.GetWave(w);
                for (int p = 0; p < wave.PrefabCount; p++)
                {
                    EnemyController prefab = wave.GetPrefab(p);
                    if (prefab == null)
                    {
                        Debug.LogWarning($"[WaveDirector] '{stageData.name}' wave {w + 1} has an empty prefab slot.", this);
                        continue;
                    }

                    if (poolsByPrefab.ContainsKey(prefab))
                    {
                        continue;
                    }

                    prefab.WarmUpSharedPools();

                    var pool = new ObjectPool<EnemyController>(prefab, poolCapacityPerPrefab, poolCapacityPerPrefab);
                    poolsByPrefab.Add(prefab, pool);
                    pools.Add(pool);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (TryGetViewBounds(cam, out Vector2 min, out Vector2 max))
            {
                Gizmos.color = Color.red;
                Vector2 size = max - min;
                Gizmos.DrawWireCube((Vector3)(min + size * 0.5f), (Vector3)size);
            }
        }
    }
}

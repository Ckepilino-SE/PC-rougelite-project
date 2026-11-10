using Core.Pooling;
using Core.References;
using UnityEngine;

namespace Gameplay.Enemies
{
    /// <summary>
    /// Spawns pooled <see cref="EnemyController"/>s on a fixed interval, either at hand-placed spawn points
    /// or on a ring around the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pool is created and pre-warmed in <c>Awake</c>; spawning afterwards never instantiates.
    /// <see cref="poolCapacity"/> doubles as the cap on enemies alive at once: when it is reached, spawn
    /// attempts are skipped until an enemy dies and returns to the pool.
    /// </para>
    /// <para>
    /// <b>Position</b>: if <see cref="spawnPoints"/> has entries, one is picked at random. Otherwise a point is
    /// picked on a ring between <see cref="minSpawnRadius"/> and <see cref="maxSpawnRadius"/> around the
    /// target in <see cref="targetAnchor"/>. Either way, points overlapping <see cref="blockingMask"/> are
    /// rejected and retried, using the non-allocating <c>Physics2D.OverlapCircle</c> overload.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        private const int MaxPlacementAttempts = 8;

        [Header("Enemy")]
        [SerializeField]
        [Tooltip("Enemy prefab to pool and spawn.")]
        private EnemyController enemyPrefab;

        [Header("Pool")]
        [SerializeField, Min(1)]
        [Tooltip("Hard cap on enemies alive at once.")]
        private int poolCapacity = 30;

        [SerializeField, Min(0)]
        [Tooltip("Enemies instantiated up front during Awake.")]
        private int prewarmCount = 30;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Begin spawning automatically when the scene starts.")]
        private bool spawnOnStart = true;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between spawns.")]
        private float spawnInterval = 1.5f;

        [Header("Placement")]
        [SerializeField]
        [Tooltip("Optional fixed spawn points. If empty, enemies spawn on a ring around the target instead.")]
        private Transform[] spawnPoints = new Transform[0];

        [SerializeField]
        [Tooltip("Anchor the player registers itself in. Used for ring spawning.")]
        private TransformAnchorSO targetAnchor;

        [SerializeField, Min(0f)]
        [Tooltip("Ring spawning: minimum distance from the target. Keep this outside the camera view.")]
        private float minSpawnRadius = 10f;

        [SerializeField, Min(0f)]
        [Tooltip("Ring spawning: maximum distance from the target.")]
        private float maxSpawnRadius = 13f;

        [SerializeField]
        [Tooltip("Layers a spawn point must not overlap (walls, obstacles).")]
        private LayerMask blockingMask;

        [SerializeField, Min(0f)]
        [Tooltip("Radius used for the blocking-overlap check. Roughly the enemy collider's radius.")]
        private float clearanceRadius = 0.5f;

        private ObjectPool<EnemyController> pool;
        private float nextSpawnTime;

        /// <summary>True while the spawner is producing enemies.</summary>
        public bool IsSpawning { get; private set; }

        /// <summary>Number of enemies currently alive from this spawner.</summary>
        public int AliveCount => pool != null ? pool.CountActive : 0;

        private void Awake()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError($"{nameof(EnemySpawner)} on '{name}' has no enemy prefab assigned.", this);
                enabled = false;
                return;
            }

            pool = new ObjectPool<EnemyController>(enemyPrefab, poolCapacity, Mathf.Min(prewarmCount, poolCapacity));
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                StartSpawning();
            }
        }

        private void OnDestroy()
        {
            pool?.Dispose();
        }

        /// <summary>Begins (or resumes) spawning. The first enemy appears immediately.</summary>
        public void StartSpawning()
        {
            IsSpawning = true;
            nextSpawnTime = Time.time;
        }

        /// <summary>Stops spawning. Enemies already alive are unaffected.</summary>
        public void StopSpawning()
        {
            IsSpawning = false;
        }

        /// <summary>Returns every live enemy to the pool without firing kill events (e.g. on run reset).</summary>
        public void DespawnAll()
        {
            pool?.ReturnAll();
        }

        private void Update()
        {
            if (!IsSpawning || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + spawnInterval;
            TrySpawn();
        }

        /// <summary>
        /// Spawns one enemy if the pool has room and a clear position can be found. Allocation-free.
        /// </summary>
        /// <returns><c>true</c> if an enemy was spawned.</returns>
        public bool TrySpawn()
        {
            if (pool == null || pool.CountActive >= pool.MaxCapacity)
            {
                return false;
            }

            if (!TryFindSpawnPosition(out Vector2 position))
            {
                return false;
            }

            if (!pool.TryGet(position, Quaternion.identity, out EnemyController enemy))
            {
                return false;
            }

            enemy.Initialize(pool);
            return true;
        }

        private bool TryFindSpawnPosition(out Vector2 position)
        {
            for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
            {
                if (!TryPickCandidate(out position))
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

        private bool TryPickCandidate(out Vector2 position)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (point != null)
                {
                    position = point.position;
                    return true;
                }
            }
            else if (targetAnchor != null && targetAnchor.IsSet)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float radius = Random.Range(minSpawnRadius, Mathf.Max(minSpawnRadius, maxSpawnRadius));
                position = (Vector2)targetAnchor.Value.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                return true;
            }

            position = default;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Gizmos.color = Color.red;
                foreach (Transform point in spawnPoints)
                {
                    if (point != null)
                    {
                        Gizmos.DrawWireSphere(point.position, clearanceRadius);
                    }
                }

                return;
            }

            if (targetAnchor != null && targetAnchor.IsSet)
            {
                Vector3 center = targetAnchor.Value.position;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(center, minSpawnRadius);
                Gizmos.DrawWireSphere(center, maxSpawnRadius);
            }
        }
    }
}

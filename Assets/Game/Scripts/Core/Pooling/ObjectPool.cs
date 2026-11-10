using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Core.Pooling
{
    /// <summary>
    /// Generic, fixed-capacity pool of <see cref="Component"/> instances cloned from a single prefab.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Allocation:</b> all internal collections are sized to <see cref="MaxCapacity"/> up front, and
    /// <see cref="Get()"/> / <see cref="ReturnToPool"/> perform no managed allocations once the pool is
    /// warm. Instantiation only happens during <see cref="Prewarm"/> or when <see cref="Get()"/> finds the
    /// pool empty while still under capacity — pre-warm to the expected peak to keep combat allocation-free.
    /// </para>
    /// <para>
    /// <b>Capacity:</b> <see cref="MaxCapacity"/> is a hard limit on the total number of instances
    /// (active + inactive) the pool will ever create. When it is reached, <see cref="Get()"/> returns
    /// <c>null</c> (and <see cref="TryGet(out T)"/> returns <c>false</c>) instead of instantiating more.
    /// </para>
    /// <para>
    /// <b>Threading:</b> all pool bookkeeping is guarded by a lock, so counters can be read from any
    /// thread and concurrent callers can never corrupt the pool's state. Unity's Object/Transform API is
    /// main-thread-only, however, so <see cref="Get()"/>, <see cref="ReturnToPool"/>, <see cref="Prewarm"/>
    /// and <see cref="Dispose"/> must be called from the main thread; editor and development builds
    /// assert this.
    /// </para>
    /// <para>
    /// <b>Hierarchy:</b> inactive instances are parked under a hidden, inactive container GameObject.
    /// Because that container is inactive, freshly instantiated clones do not run <c>Awake</c>/<c>OnEnable</c>
    /// until they are first handed out by <see cref="Get()"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Component on the prefab's root that callers interact with.</typeparam>
    public sealed class ObjectPool<T> : IObjectPool, IDisposable where T : Component
    {
        private static readonly IPoolable[] NoPoolables = Array.Empty<IPoolable>();

        private readonly object syncRoot = new object();
        private readonly T prefab;
        private readonly int maxCapacity;
        private readonly Transform storage;
        private readonly int mainThreadId;

        private readonly Stack<T> inactive;
        private readonly HashSet<T> active;
        private readonly Dictionary<T, IPoolable[]> poolablesByInstance;

        // Instances created plus slots reserved for an in-flight instantiation.
        private int totalCount;
        private bool disposed;
        private bool hasWarnedExhausted;

        /// <summary>
        /// Creates a pool for <paramref name="prefab"/>.
        /// </summary>
        /// <param name="prefab">Prefab (or scene template) to clone. Must not be null.</param>
        /// <param name="maxCapacity">Hard cap on total instances this pool may create. Must be at least 1.</param>
        /// <param name="prewarmCount">Number of instances to create immediately. Clamped to <paramref name="maxCapacity"/>.</param>
        /// <param name="parent">
        /// Optional transform the hidden storage container is parented under (e.g. a scene "Pools" root,
        /// or an object marked <c>DontDestroyOnLoad</c>). If null, the container is created at the root
        /// of the active scene and is destroyed with it.
        /// </param>
        public ObjectPool(T prefab, int maxCapacity, int prewarmCount = 0, Transform parent = null)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (maxCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "Max capacity must be at least 1.");
            }

            this.prefab = prefab;
            this.maxCapacity = maxCapacity;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            inactive = new Stack<T>(maxCapacity);
            active = new HashSet<T>(maxCapacity);
            poolablesByInstance = new Dictionary<T, IPoolable[]>(maxCapacity);

            var storageObject = new GameObject($"[Pool] {prefab.name}");
            storageObject.SetActive(false);
            storage = storageObject.transform;
            if (parent != null)
            {
                storage.SetParent(parent, false);
            }

            if (prewarmCount > 0)
            {
                Prewarm(prewarmCount);
            }

            PoolRegistry.Register(this);
        }

        /// <summary>Hard cap on the number of instances this pool will create.</summary>
        public int MaxCapacity => maxCapacity;

        /// <summary>Number of instances currently handed out and not yet returned.</summary>
        public int CountActive
        {
            get { lock (syncRoot) { return active.Count; } }
        }

        /// <summary>Number of instances parked in the pool and ready to be handed out.</summary>
        public int CountInactive
        {
            get { lock (syncRoot) { return inactive.Count; } }
        }

        /// <summary>Total number of instances owned by this pool (active + inactive).</summary>
        public int CountAll
        {
            get { lock (syncRoot) { return totalCount; } }
        }

        /// <summary>
        /// Instantiates up to <paramref name="count"/> additional inactive instances, stopping at
        /// <see cref="MaxCapacity"/>. Call during loading, never during combat.
        /// </summary>
        /// <param name="count">Number of instances to add.</param>
        /// <returns>The number of instances actually created.</returns>
        public int Prewarm(int count)
        {
            AssertMainThread();
            ThrowIfDisposed();

            int created = 0;
            while (created < count && TryReserveSlot())
            {
                T instance = CreateInstance();
                lock (syncRoot)
                {
                    inactive.Push(instance);
                }

                created++;
            }

            return created;
        }

        /// <summary>
        /// Takes an instance from the pool, activates it in place, and invokes
        /// <see cref="IPoolable.OnSpawnFromPool"/> on its poolable components.
        /// </summary>
        /// <returns>The spawned instance, or <c>null</c> if the pool is at <see cref="MaxCapacity"/>.</returns>
        public T Get()
        {
            return TryGet(out T instance) ? instance : null;
        }

        /// <summary>
        /// Takes an instance from the pool, places it at <paramref name="position"/> / <paramref name="rotation"/>
        /// before activation (so <c>OnEnable</c> sees the final pose), and invokes
        /// <see cref="IPoolable.OnSpawnFromPool"/>.
        /// </summary>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="rotation">World-space spawn rotation.</param>
        /// <param name="parent">Optional parent for the active instance; null places it at the scene root.</param>
        /// <returns>The spawned instance, or <c>null</c> if the pool is at <see cref="MaxCapacity"/>.</returns>
        public T Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return TryGet(position, rotation, out T instance, parent) ? instance : null;
        }

        /// <summary>
        /// Non-throwing variant of <see cref="Get()"/>. The instance keeps the local pose it had when it was returned.
        /// </summary>
        /// <param name="instance">The spawned instance, or <c>null</c> on failure.</param>
        /// <returns><c>false</c> if the pool is at <see cref="MaxCapacity"/>.</returns>
        public bool TryGet(out T instance)
        {
            if (!TryAcquire(out instance))
            {
                return false;
            }

            instance.transform.SetParent(null, false);
            Activate(instance);
            return true;
        }

        /// <summary>
        /// Non-throwing variant of <see cref="Get(Vector3, Quaternion, Transform)"/>.
        /// </summary>
        /// <param name="position">World-space spawn position.</param>
        /// <param name="rotation">World-space spawn rotation.</param>
        /// <param name="instance">The spawned instance, or <c>null</c> on failure.</param>
        /// <param name="parent">Optional parent for the active instance; null places it at the scene root.</param>
        /// <returns><c>false</c> if the pool is at <see cref="MaxCapacity"/>.</returns>
        public bool TryGet(Vector3 position, Quaternion rotation, out T instance, Transform parent = null)
        {
            if (!TryAcquire(out instance))
            {
                return false;
            }

            Transform t = instance.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);
            Activate(instance);
            return true;
        }

        /// <summary>
        /// Invokes <see cref="IPoolable.OnReturnToPool"/>, deactivates the instance, and parks it in the pool.
        /// </summary>
        /// <param name="instance">An instance previously obtained from this pool.</param>
        /// <returns>
        /// <c>false</c> if <paramref name="instance"/> is null, was not handed out by this pool, or has
        /// already been returned (double returns are ignored rather than corrupting the pool).
        /// </returns>
        public bool ReturnToPool(T instance)
        {
            AssertMainThread();

            if (ReferenceEquals(instance, null))
            {
                return false;
            }

            if (instance == null)
            {
                // Destroyed externally while active: release its capacity slot so the pool can replace it.
                lock (syncRoot)
                {
                    if (!disposed && active.Remove(instance))
                    {
                        poolablesByInstance.Remove(instance);
                        totalCount--;
                    }
                }

                return false;
            }

            IPoolable[] poolables;
            lock (syncRoot)
            {
                if (disposed || !active.Remove(instance))
                {
                    return false;
                }

                poolables = poolablesByInstance[instance];
            }

            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnReturnToPool();
            }

            instance.gameObject.SetActive(false);
            instance.transform.SetParent(storage, false);

            lock (syncRoot)
            {
                inactive.Push(instance);
            }

            return true;
        }

        /// <summary>
        /// Returns every currently active instance to the pool (e.g. on room clear or run reset).
        /// </summary>
        public void ReturnAll()
        {
            AssertMainThread();

            // Snapshot without allocating: pop active instances one at a time.
            while (true)
            {
                T next = null;
                lock (syncRoot)
                {
                    if (disposed || active.Count == 0)
                    {
                        return;
                    }

                    foreach (T item in active)
                    {
                        next = item;
                        break;
                    }
                }

                // Always removes 'next' from the active set, even if it was destroyed externally.
                ReturnToPool(next);
            }
        }

        /// <summary>
        /// Destroys every instance owned by the pool (active and inactive) and the storage container.
        /// Intended for scene/run teardown only. The pool cannot be used afterwards.
        /// </summary>
        public void Dispose()
        {
            AssertMainThread();

            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                PoolRegistry.Unregister(this);

                foreach (T item in active)
                {
                    if (item != null)
                    {
                        Object.Destroy(item.gameObject);
                    }
                }

                active.Clear();
                inactive.Clear();
                poolablesByInstance.Clear();
                totalCount = 0;
            }

            // Inactive instances are children of the storage container and are destroyed with it.
            if (storage != null)
            {
                Object.Destroy(storage.gameObject);
            }
        }

        /// <summary>
        /// Pops a usable inactive instance, or creates one if under capacity. Does not activate it.
        /// </summary>
        private bool TryAcquire(out T instance)
        {
            AssertMainThread();
            ThrowIfDisposed();

            lock (syncRoot)
            {
                while (inactive.Count > 0)
                {
                    instance = inactive.Pop();

                    // Unity's overloaded null check: the instance may have been destroyed externally
                    // (e.g. scene unload). Release its slot and keep looking.
                    if (instance == null)
                    {
                        poolablesByInstance.Remove(instance);
                        totalCount--;
                        continue;
                    }

                    active.Add(instance);
                    return true;
                }
            }

            if (!TryReserveSlot())
            {
                WarnExhausted();
                instance = null;
                return false;
            }

            instance = CreateInstance();
            lock (syncRoot)
            {
                active.Add(instance);
            }

            return true;
        }

        /// <summary>Atomically claims one unit of capacity for a new instance.</summary>
        private bool TryReserveSlot()
        {
            lock (syncRoot)
            {
                if (disposed || totalCount >= maxCapacity)
                {
                    return false;
                }

                totalCount++;
                return true;
            }
        }

        /// <summary>
        /// Instantiates a clone under the inactive storage container and caches its <see cref="IPoolable"/>s.
        /// The caller must already have reserved a slot via <see cref="TryReserveSlot"/>.
        /// </summary>
        private T CreateInstance()
        {
            T instance = Object.Instantiate(prefab, storage, false);
            instance.name = prefab.name;
            instance.gameObject.SetActive(false);

            IPoolable[] poolables = instance.GetComponentsInChildren<IPoolable>(true);
            if (poolables.Length == 0)
            {
                poolables = NoPoolables;
            }

            lock (syncRoot)
            {
                poolablesByInstance[instance] = poolables;
            }

            return instance;
        }

        /// <summary>Activates an acquired instance and fires its spawn hooks.</summary>
        private void Activate(T instance)
        {
            instance.gameObject.SetActive(true);

            IPoolable[] poolables;
            lock (syncRoot)
            {
                poolables = poolablesByInstance[instance];
            }

            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawnFromPool();
            }
        }

        private void WarnExhausted()
        {
            if (hasWarnedExhausted)
            {
                return;
            }

            hasWarnedExhausted = true;
            Debug.LogWarning($"[ObjectPool] Pool for '{prefab.name}' reached its max capacity of {maxCapacity}. Get() will return null until instances are returned.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException($"ObjectPool<{typeof(T).Name}>");
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private void AssertMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                throw new InvalidOperationException(
                    $"ObjectPool<{typeof(T).Name}> must be used from the main thread because it calls the Unity Object/Transform API.");
            }
        }
    }
}

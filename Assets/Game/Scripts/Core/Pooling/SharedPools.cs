using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Pooling
{
    /// <summary>
    /// One <see cref="ObjectPool{T}"/> per prefab, shared by everything that spawns that prefab
    /// (enemy shots, damage numbers, hazards).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pooled spawners (enemies) can't own a pool each: their spawned objects would be destroyed or orphaned
    /// when the spawner returns to its own pool. Sharing one pool per prefab avoids that and bounds the total.
    /// </para>
    /// <para>
    /// Pools register with <see cref="PoolRegistry"/>, so <c>PoolRegistry.ReturnAllActive</c> clears them between
    /// stages. All shared pools are disposed when a scene unloads, because their storage objects live in it.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Component type on the prefab.</typeparam>
    public static class SharedPools<T> where T : Component
    {
        private static readonly Dictionary<T, ObjectPool<T>> poolsByPrefab = new Dictionary<T, ObjectPool<T>>();
        private static bool isRegistered;

        /// <summary>
        /// Returns the shared pool for <paramref name="prefab"/>, creating it on first use. The first caller's
        /// <paramref name="capacity"/> wins. Half the capacity is pre-warmed.
        /// </summary>
        public static ObjectPool<T> Get(T prefab, int capacity)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!isRegistered)
            {
                SharedPoolsLifetime.Register(DisposeAll, ForgetRegistration);
                isRegistered = true;
            }

            if (!poolsByPrefab.TryGetValue(prefab, out ObjectPool<T> pool))
            {
                int cap = Mathf.Max(1, capacity);
                pool = new ObjectPool<T>(prefab, cap, Mathf.Max(1, cap / 2));
                poolsByPrefab.Add(prefab, pool);
            }

            return pool;
        }

        /// <summary>Destroys every shared pool of this type and its objects.</summary>
        public static void DisposeAll()
        {
            foreach (ObjectPool<T> pool in poolsByPrefab.Values)
            {
                pool.Dispose();
            }

            poolsByPrefab.Clear();
        }

        internal static void ForgetRegistration()
        {
            poolsByPrefab.Clear();
            isRegistered = false;
        }
    }

    /// <summary>Disposes every <see cref="SharedPools{T}"/> when a scene unloads.</summary>
    internal static class SharedPoolsLifetime
    {
        private static readonly List<Action> disposers = new List<Action>();
        private static readonly List<Action> forgetters = new List<Action>();
        private static bool isHooked;

        internal static void Register(Action disposeAll, Action forget)
        {
            disposers.Add(disposeAll);
            forgetters.Add(forget);
            if (!isHooked)
            {
                SceneManager.sceneUnloaded += HandleSceneUnloaded;
                isHooked = true;
            }
        }

        private static void HandleSceneUnloaded(Scene scene)
        {
            for (int i = 0; i < disposers.Count; i++)
            {
                disposers[i]();
            }
        }

        // Static state survives entering Play mode when domain reload is disabled; start clean each time.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Types used last session must re-register; without domain reload their flags would persist.
            for (int i = 0; i < forgetters.Count; i++)
            {
                forgetters[i]();
            }

            disposers.Clear();
            forgetters.Clear();
            if (isHooked)
            {
                SceneManager.sceneUnloaded -= HandleSceneUnloaded;
                isHooked = false;
            }
        }
    }
}

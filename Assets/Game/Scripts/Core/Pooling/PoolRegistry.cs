using System.Collections.Generic;

namespace Core.Pooling
{
    /// <summary>
    /// Non-generic view of an <see cref="ObjectPool{T}"/>, so pools of different types can be managed together.
    /// </summary>
    public interface IObjectPool
    {
        /// <summary>Number of instances currently handed out.</summary>
        int CountActive { get; }

        /// <summary>Returns every active instance to the pool.</summary>
        void ReturnAll();
    }

    /// <summary>
    /// Tracks every live <see cref="ObjectPool{T}"/> so game-flow code can clear the field (enemies, projectiles,
    /// effects) between stages without holding references to the systems that own those pools.
    /// </summary>
    /// <remarks>
    /// Pools register themselves on construction and unregister on <see cref="ObjectPool{T}.Dispose"/>.
    /// Must be used from the main thread, like the pools themselves.
    /// </remarks>
    public static class PoolRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<IObjectPool> Pools = new List<IObjectPool>();

        /// <summary>Number of registered pools.</summary>
        public static int Count
        {
            get { lock (SyncRoot) { return Pools.Count; } }
        }

        /// <summary>Returns every active instance in every registered pool.</summary>
        public static void ReturnAllActive()
        {
            // Index from the end: a ReturnAll callback could, in principle, dispose a pool and shrink the list.
            for (int i = Count - 1; i >= 0; i--)
            {
                IObjectPool pool;
                lock (SyncRoot)
                {
                    if (i >= Pools.Count)
                    {
                        continue;
                    }

                    pool = Pools[i];
                }

                pool.ReturnAll();
            }
        }

        internal static void Register(IObjectPool pool)
        {
            lock (SyncRoot)
            {
                Pools.Add(pool);
            }
        }

        internal static void Unregister(IObjectPool pool)
        {
            lock (SyncRoot)
            {
                Pools.Remove(pool);
            }
        }
    }
}

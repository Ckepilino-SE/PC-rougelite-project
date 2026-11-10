namespace Core.Pooling
{
    /// <summary>
    /// Optional lifecycle hooks for components living on a pooled object.
    /// </summary>
    /// <remarks>
    /// Every <see cref="IPoolable"/> on the pooled object's hierarchy (including inactive children)
    /// is discovered once, when the instance is first created, and cached by the pool.
    /// Use these hooks instead of <c>Awake</c>/<c>OnDestroy</c> to reset per-spawn state
    /// (health, timers, velocity, trail renderers, etc.), since a pooled object is created once
    /// and reused many times.
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>
        /// Called after the object has been positioned and activated by
        /// <see cref="ObjectPool{T}.Get()"/>. Unity's <c>OnEnable</c> has already run at this point.
        /// </summary>
        void OnSpawnFromPool();

        /// <summary>
        /// Called by <see cref="ObjectPool{T}.ReturnToPool"/> while the object is still active,
        /// immediately before it is deactivated and parked in the pool.
        /// </summary>
        void OnReturnToPool();
    }
}

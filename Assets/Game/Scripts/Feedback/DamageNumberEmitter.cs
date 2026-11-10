using Core.Pooling;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// Spawns a <see cref="DamageNumber"/> whenever this object's <see cref="Health"/> takes damage.
    /// Put it on enemy prefabs. All emitters using the same prefab share one pool.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class DamageNumberEmitter : MonoBehaviour
    {
        [SerializeField]
        private DamageNumber damageNumberPrefab;

        [SerializeField, Min(1)]
        [Tooltip("Max numbers on screen at once, shared by every emitter using this prefab.")]
        private int sharedPoolCapacity = 48;

        [SerializeField]
        [Tooltip("Spawn offset from this object's position.")]
        private Vector2 offset = new Vector2(0f, 0.6f);

        private Health health;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.DamageTaken += HandleDamageTaken;
        }

        private void OnDisable()
        {
            health.DamageTaken -= HandleDamageTaken;
        }

        private void HandleDamageTaken(int amount)
        {
            ObjectPool<DamageNumber> pool = SharedPools<DamageNumber>.Get(damageNumberPrefab, sharedPoolCapacity);
            if (pool == null)
            {
                return;
            }

            Vector3 position = transform.position + (Vector3)offset;
            if (pool.TryGet(position, Quaternion.identity, out DamageNumber number))
            {
                number.Show(pool, amount);
            }
        }
    }
}

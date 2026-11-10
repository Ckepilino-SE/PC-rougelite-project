using Core.Pooling;
using Core.References;
using Gameplay.Enemies;
using Gameplay.Enemies.Boss;
using Gameplay.Player;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// Spawns pooled visual effects for combat events: enemy hit, enemy death, parry, perfect parry, stagger,
    /// player hurt and muzzle flash. Put one in the scene (e.g. on GameDirector) and drag effect prefabs
    /// (anything with <see cref="PooledEffect"/>) into the slots. Empty slots are skipped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatEffects : MonoBehaviour
    {
        [SerializeField]
        private TransformAnchorSO playerAnchor;

        [SerializeField, Min(1)]
        [Tooltip("Max of each effect alive at once.")]
        private int capacityPerEffect = 24;

        [Header("Effects")]
        [SerializeField]
        private PooledEffect enemyHit;

        [SerializeField]
        private PooledEffect enemyDeath;

        [SerializeField]
        private PooledEffect parry;

        [SerializeField]
        private PooledEffect perfectParry;

        [SerializeField]
        [Tooltip("Boss or elite posture broken.")]
        private PooledEffect stagger;

        [SerializeField]
        private PooledEffect playerHurt;

        [SerializeField]
        [Tooltip("Small flash at the gun on each shot.")]
        private PooledEffect muzzleFlash;

        [Header("Colours")]
        [SerializeField]
        private Color hitColor = new Color(1f, 0.9f, 0.6f);

        [SerializeField]
        private Color deathColor = new Color(1f, 0.5f, 0.3f);

        [SerializeField]
        private Color parryColor = Color.white;

        [SerializeField]
        private Color perfectColor = new Color(1f, 0.82f, 0.25f);

        [SerializeField]
        private Color staggerColor = new Color(1f, 0.85f, 0.2f);

        [SerializeField]
        private Color hurtColor = new Color(1f, 0.25f, 0.25f);

        private Transform cachedPlayer;
        private PlayerParry playerParry;
        private Combat.Health playerHealth;
        private Combat.WeaponController playerWeapon;

        private void Start()
        {
            // Create every pool up front so nothing is instantiated mid-fight.
            Warm(enemyHit);
            Warm(enemyDeath);
            Warm(parry);
            Warm(perfectParry);
            Warm(stagger);
            Warm(playerHurt);
            Warm(muzzleFlash);
        }

        private void OnEnable()
        {
            EnemyController.AnyDamaged += HandleEnemyDamaged;
            EnemyController.AnyDied += HandleEnemyDied;
            BossController.Staggered += HandleBossStaggered;
            EliteController.AnyStaggered += HandleEliteStaggered;
        }

        private void OnDisable()
        {
            EnemyController.AnyDamaged -= HandleEnemyDamaged;
            EnemyController.AnyDied -= HandleEnemyDied;
            BossController.Staggered -= HandleBossStaggered;
            EliteController.AnyStaggered -= HandleEliteStaggered;
            BindPlayer(null);
        }

        private void Update()
        {
            if (playerAnchor != null && playerAnchor.IsSet && playerAnchor.Value != cachedPlayer)
            {
                BindPlayer(playerAnchor.Value);
            }
        }

        private void BindPlayer(Transform player)
        {
            if (playerParry != null)
            {
                playerParry.Parried -= HandleParried;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandlePlayerHurt;
            }

            if (playerWeapon != null)
            {
                playerWeapon.Fired -= HandleFired;
            }

            cachedPlayer = player;
            playerParry = player != null ? player.GetComponentInChildren<PlayerParry>() : null;
            playerHealth = player != null ? player.GetComponentInChildren<Combat.Health>() : null;
            playerWeapon = player != null ? player.GetComponentInChildren<Combat.WeaponController>() : null;

            if (playerParry != null)
            {
                playerParry.Parried += HandleParried;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken += HandlePlayerHurt;
            }

            if (playerWeapon != null)
            {
                playerWeapon.Fired += HandleFired;
            }
        }

        private void HandleEnemyDamaged(EnemyController enemy, int amount)
        {
            Spawn(enemyHit, enemy.transform.position, hitColor, Mathf.Clamp(0.8f + amount * 0.1f, 0.8f, 1.6f));
        }

        private void HandleEnemyDied(EnemyController enemy)
        {
            Spawn(enemyDeath, enemy.transform.position, deathColor, enemy.transform.localScale.x);
        }

        private void HandleParried(ParryEvent parryEvent)
        {
            // Halfway between the player and what was parried, where the blades "meet".
            Vector2 at = cachedPlayer != null
                ? Vector2.Lerp(cachedPlayer.position, parryEvent.AttackerPosition, 0.5f)
                : parryEvent.AttackerPosition;
            if (parryEvent.IsPerfect && perfectParry != null)
            {
                Spawn(perfectParry, at, perfectColor, 1.4f);
            }
            else
            {
                Spawn(parry, at, parryEvent.IsPerfect ? perfectColor : parryColor, 1f);
            }
        }

        private void HandleBossStaggered(BossController boss)
        {
            Spawn(stagger, boss.transform.position, staggerColor, 2.5f);
        }

        private void HandleEliteStaggered(EliteController elite)
        {
            Spawn(stagger, elite.transform.position, staggerColor, 1.5f);
        }

        private void HandlePlayerHurt(int amount)
        {
            if (cachedPlayer != null)
            {
                Spawn(playerHurt, cachedPlayer.position, hurtColor, 1f);
            }
        }

        private void HandleFired()
        {
            if (playerWeapon != null && playerWeapon.Muzzle != null)
            {
                Spawn(muzzleFlash, playerWeapon.Muzzle.position, Color.white, 1f);
            }
        }

        private void Warm(PooledEffect prefab)
        {
            if (prefab != null)
            {
                SharedPools<PooledEffect>.Get(prefab, capacityPerEffect);
            }
        }

        private void Spawn(PooledEffect prefab, Vector3 position, Color tint, float size)
        {
            if (prefab == null)
            {
                return;
            }

            ObjectPool<PooledEffect> pool = SharedPools<PooledEffect>.Get(prefab, capacityPerEffect);
            if (pool != null && pool.TryGet(position, Quaternion.identity, out PooledEffect effect))
            {
                effect.Play(pool, tint, size);
            }
        }
    }
}

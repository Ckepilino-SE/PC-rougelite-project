using System;
using Core.References;
using Gameplay.Combat;
using Gameplay.Enemies;
using Gameplay.Enemies.Boss;
using Gameplay.Player;
using Gameplay.Powers;
using Gameplay.Tempo;
using UnityEngine;

namespace Gameplay.Feedback
{
    /// <summary>
    /// Plays sound effects for combat events. Put one in the scene (e.g. on GameDirector) and drag clips into
    /// the slots; empty slots are silent. Parry sounds stay on <see cref="ParryFeedback"/>.
    /// </summary>
    /// <remarks>
    /// Owns a small set of AudioSources created at scene start and reused round-robin, so overlapping sounds
    /// never allocate. Each sound has a minimum gap so rapid events (bullets, shotgun hits) don't stack into noise.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CombatAudio : MonoBehaviour
    {
        /// <summary>One sound slot.</summary>
        [Serializable]
        public sealed class Sound
        {
            [SerializeField]
            [Tooltip("Picked at random each time. Several similar clips sound less repetitive.")]
            private AudioClip[] clips = new AudioClip[0];

            [SerializeField, Range(0f, 1f)]
            private float volume = 0.7f;

            [SerializeField, Range(0f, 0.5f)]
            [Tooltip("Random pitch change, +/-.")]
            private float pitchVariation = 0.08f;

            [SerializeField, Min(0f)]
            [Tooltip("Minimum seconds between plays of this sound.")]
            private float minimumGap = 0.03f;

            [NonSerialized]
            internal float NextAllowedTime;

            internal AudioClip[] Clips => clips;

            internal float Volume => volume;

            internal float PitchVariation => pitchVariation;

            internal float MinimumGap => minimumGap;
        }

        [SerializeField]
        private TransformAnchorSO playerAnchor;

        [SerializeField, Range(1, 32)]
        [Tooltip("Sounds that can play at the same time.")]
        private int voices = 12;

        [SerializeField, Range(0f, 1f)]
        private float masterVolume = 1f;

        [Header("Player")]
        [SerializeField]
        private Sound shoot = new Sound();

        [SerializeField]
        private Sound dash = new Sound();

        [SerializeField]
        private Sound playerHurt = new Sound();

        [SerializeField]
        private Sound powerStolen = new Sound();

        [SerializeField]
        private Sound tempoBroken = new Sound();

        [Header("Enemies")]
        [SerializeField]
        private Sound enemyHit = new Sound();

        [SerializeField]
        private Sound enemyDeath = new Sound();

        [SerializeField]
        [Tooltip("Boss or elite posture broken.")]
        private Sound stagger = new Sound();

        [SerializeField]
        private Sound bossPhase = new Sound();

        private AudioSource[] sources;
        private int nextSource;
        private Transform cachedPlayer;
        private WeaponController weapon;
        private PlayerDash playerDash;
        private Health playerHealth;
        private PowerThief thief;
        private TempoMeter tempo;

        private void Awake()
        {
            sources = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var child = new GameObject($"Voice {i}");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
        }

        private void OnEnable()
        {
            EnemyController.AnyDamaged += HandleEnemyDamaged;
            EnemyController.AnyDied += HandleEnemyDied;
            BossController.Staggered += HandleBossStaggered;
            BossController.PhaseChanged += HandleBossPhase;
            EliteController.AnyStaggered += HandleEliteStaggered;
        }

        private void OnDisable()
        {
            EnemyController.AnyDamaged -= HandleEnemyDamaged;
            EnemyController.AnyDied -= HandleEnemyDied;
            BossController.Staggered -= HandleBossStaggered;
            BossController.PhaseChanged -= HandleBossPhase;
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

        /// <summary>Plays <paramref name="sound"/> if its slot has clips and its minimum gap has passed.</summary>
        public void Play(Sound sound)
        {
            if (sound == null || sound.Clips == null || sound.Clips.Length == 0 || sources == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now < sound.NextAllowedTime)
            {
                return;
            }

            AudioClip clip = sound.Clips[UnityEngine.Random.Range(0, sound.Clips.Length)];
            if (clip == null)
            {
                return;
            }

            sound.NextAllowedTime = now + sound.MinimumGap;
            AudioSource source = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            source.pitch = 1f + UnityEngine.Random.Range(-sound.PitchVariation, sound.PitchVariation);
            source.PlayOneShot(clip, sound.Volume * masterVolume);
        }

        private void BindPlayer(Transform player)
        {
            if (weapon != null)
            {
                weapon.Fired -= HandleFired;
            }

            if (playerDash != null)
            {
                playerDash.Dashed -= HandleDashed;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken -= HandlePlayerHurt;
            }

            if (thief != null)
            {
                thief.Stolen -= HandleStolen;
            }

            if (tempo != null)
            {
                tempo.Broken -= HandleTempoBroken;
            }

            cachedPlayer = player;
            weapon = player != null ? player.GetComponentInChildren<WeaponController>() : null;
            playerDash = player != null ? player.GetComponentInChildren<PlayerDash>() : null;
            playerHealth = player != null ? player.GetComponentInChildren<Health>() : null;
            thief = player != null ? player.GetComponentInChildren<PowerThief>() : null;
            tempo = player != null ? player.GetComponentInChildren<TempoMeter>() : null;

            if (weapon != null)
            {
                weapon.Fired += HandleFired;
            }

            if (playerDash != null)
            {
                playerDash.Dashed += HandleDashed;
            }

            if (playerHealth != null)
            {
                playerHealth.DamageTaken += HandlePlayerHurt;
            }

            if (thief != null)
            {
                thief.Stolen += HandleStolen;
            }

            if (tempo != null)
            {
                tempo.Broken += HandleTempoBroken;
            }
        }

        private void HandleFired() => Play(shoot);

        private void HandleDashed(Vector2 direction) => Play(dash);

        private void HandlePlayerHurt(int amount) => Play(playerHurt);

        private void HandleStolen(StolenPowerDefinition power, int level) => Play(powerStolen);

        private void HandleTempoBroken(int lost) => Play(tempoBroken);

        private void HandleEnemyDamaged(EnemyController enemy, int amount) => Play(enemyHit);

        private void HandleEnemyDied(EnemyController enemy) => Play(enemyDeath);

        private void HandleBossStaggered(BossController boss) => Play(stagger);

        private void HandleEliteStaggered(EliteController elite) => Play(stagger);

        private void HandleBossPhase(BossController boss, int phase) => Play(bossPhase);
    }
}

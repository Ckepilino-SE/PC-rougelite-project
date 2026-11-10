using System;
using Gameplay.Combat;
using UnityEngine;

namespace Gameplay.Enemies.Boss
{
    /// <summary>The kinds of attack a boss pattern can perform.</summary>
    public enum BossPatternType
    {
        /// <summary>Fans of shots aimed at the player.</summary>
        AimedSpread,

        /// <summary>Rings of shots in every direction. With a rotation step, the rings spiral.</summary>
        Radial,

        /// <summary>Charges at the player. Touching the player deals lunge damage; it can be parried.</summary>
        Charge,

        /// <summary>Spawns minions around the boss.</summary>
        Summon,

        /// <summary>A straight wall of shots flying at the player, with a gap to slip through.</summary>
        Wall,
    }

    /// <summary>One attack in a boss's repertoire. Inspector-tunable.</summary>
    [Serializable]
    public sealed class BossPattern
    {
        [SerializeField]
        private BossPatternType type = BossPatternType.AimedSpread;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds the boss stands still and flashes before the attack. The player's warning.")]
        private float telegraphDuration = 0.6f;

        [SerializeField, Min(1)]
        [Tooltip("Volleys fired, or charges made, per use of this pattern.")]
        private int repetitions = 3;

        [SerializeField, Min(0.02f)]
        [Tooltip("Seconds between volleys, or the pause between charges.")]
        private float interval = 0.35f;

        [Header("Shots (Aimed Spread / Radial)")]
        [SerializeField]
        [Tooltip("Optional. Defaults to the boss's Default Projectile.")]
        private Projectile projectilePrefab;

        [SerializeField, Min(1)]
        [Tooltip("Shots per volley.")]
        private int projectileCount = 5;

        [SerializeField, Range(0f, 360f)]
        [Tooltip("Aimed Spread: total fan angle.")]
        private float spreadAngle = 45f;

        [SerializeField, Range(-180f, 180f)]
        [Tooltip("Radial: degrees each ring is rotated from the last. Non-zero makes a spiral.")]
        private float rotationStep = 12f;

        [SerializeField]
        [Tooltip("Aimed Spread: re-aim at the player before every volley (otherwise aim is locked at the start).")]
        private bool trackTarget = true;

        [SerializeField, Range(0f, 180f)]
        [Tooltip("Radial: an empty arc this wide (degrees) is left in each ring, centred on a random side. 0 = full ring.")]
        private float gapAngle;

        [Header("Wall")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Distance between shots in the wall.")]
        private float wallSpacing = 0.7f;

        [SerializeField, Min(0)]
        [Tooltip("Shots missing from the wall, as a gap the player must find. Placed randomly each volley.")]
        private int gapSize = 3;

        [Header("Charge")]
        [SerializeField, Min(0f)]
        private float chargeSpeed = 14f;

        [SerializeField, Min(0.05f)]
        private float chargeDuration = 0.45f;

        [Header("Summon")]
        [SerializeField]
        private EnemyController summonPrefab;

        [SerializeField, Min(1)]
        private int summonCount = 3;

        [SerializeField, Min(0f)]
        [Tooltip("Distance from the boss at which minions appear.")]
        private float summonRadius = 2.5f;

        /// <summary>Attack kind.</summary>
        public BossPatternType Type => type;

        /// <summary>Wind-up in seconds.</summary>
        public float TelegraphDuration => telegraphDuration;

        /// <summary>Volleys or charges per use.</summary>
        public int Repetitions => repetitions;

        /// <summary>Seconds between volleys or charges.</summary>
        public float Interval => interval;

        /// <summary>Optional projectile override.</summary>
        public Projectile ProjectilePrefab => projectilePrefab;

        /// <summary>Shots per volley.</summary>
        public int ProjectileCount => projectileCount;

        /// <summary>Fan angle for aimed spreads.</summary>
        public float SpreadAngle => spreadAngle;

        /// <summary>Ring rotation per volley for radial patterns.</summary>
        public float RotationStep => rotationStep;

        /// <summary>Whether aimed spreads re-aim every volley.</summary>
        public bool TrackTarget => trackTarget;

        /// <summary>Empty arc in radial rings.</summary>
        public float GapAngle => gapAngle;

        /// <summary>Spacing of wall shots.</summary>
        public float WallSpacing => wallSpacing;

        /// <summary>Missing shots in a wall.</summary>
        public int GapSize => gapSize;

        /// <summary>Charge speed.</summary>
        public float ChargeSpeed => chargeSpeed;

        /// <summary>Charge length in seconds.</summary>
        public float ChargeDuration => chargeDuration;

        /// <summary>Minion prefab for summons.</summary>
        public EnemyController SummonPrefab => summonPrefab;

        /// <summary>Minions per summon.</summary>
        public int SummonCount => summonCount;

        /// <summary>Spawn ring radius for summons.</summary>
        public float SummonRadius => summonRadius;
    }

    /// <summary>
    /// A boss phase: which patterns it uses and how aggressive it is. A phase becomes active once the boss's
    /// health fraction drops to or below <see cref="HealthThreshold"/>.
    /// </summary>
    [Serializable]
    public sealed class BossPhase
    {
        [SerializeField]
        private string phaseName = "Phase";

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Phase starts when health fraction is at or below this. The first phase should be 1.")]
        private float healthThreshold = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds between the end of one pattern and the start of the next.")]
        private float patternCooldown = 1.2f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Multiplier on the boss's chase speed during this phase.")]
        private float speedMultiplier = 1f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Multiplier on every pattern's telegraph time. Below 1 = less warning.")]
        private float telegraphMultiplier = 1f;

        [SerializeField]
        private BossPattern[] patterns = new BossPattern[0];

        /// <summary>Display name.</summary>
        public string PhaseName => phaseName;

        /// <summary>Health fraction at which this phase starts.</summary>
        public float HealthThreshold => healthThreshold;

        /// <summary>Seconds between patterns.</summary>
        public float PatternCooldown => patternCooldown;

        /// <summary>Chase speed multiplier.</summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>Telegraph time multiplier.</summary>
        public float TelegraphMultiplier => telegraphMultiplier;

        /// <summary>Number of patterns.</summary>
        public int PatternCount => patterns != null ? patterns.Length : 0;

        /// <summary>Pattern at <paramref name="index"/>.</summary>
        public BossPattern GetPattern(int index)
        {
            return patterns[index];
        }
    }
}

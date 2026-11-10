using System;
using Core.Timing;
using Gameplay.Combat;
using Gameplay.Player;
using Gameplay.Powers;
using UnityEngine;

namespace Gameplay.Tempo
{
    /// <summary>
    /// Tempo: chaining parries without getting hit builds stacks. Each stack raises a multiplier on your damage,
    /// reflect and counter damage, and score. At high tempo, a steal grants two levels of the stolen power
    /// instead of one. Getting hit drops tempo to zero. Stacks also drain slowly if you stop parrying.
    /// </summary>
    /// <remarks>
    /// Put it on the player next to <see cref="PlayerParry"/>. It only adds bonuses, so ignoring it never makes
    /// the game harder.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TempoMeter : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int maxStacks = 10;

        [SerializeField, Min(0f)]
        [Tooltip("Multiplier added per stack. 0.08 with 10 stacks = x1.8 at max.")]
        private float bonusPerStack = 0.08f;

        [SerializeField, Min(1)]
        private int stacksPerParry = 1;

        [SerializeField, Min(1)]
        private int stacksPerPerfectParry = 2;

        [SerializeField, Min(0f)]
        [Tooltip("Seconds without a parry before stacks start draining.")]
        private float decayDelay = 6f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds per stack lost while draining.")]
        private float decayInterval = 1.5f;

        [SerializeField, Min(0)]
        [Tooltip("At or above this many stacks, steals grant an extra power level. 0 disables.")]
        private int doubleStealStacks = 6;

        private PlayerParry parry;
        private Health health;
        private WeaponController weapon;
        private PowerThief thief;
        private int stacks;
        private float appliedMultiplier = 1f;
        private float lastGainTime;
        private float nextDecayTime;
        private bool isGrantingBonusSteal;

        /// <summary>Raised when stacks change. Arguments: stacks, multiplier.</summary>
        public event Action<int, float> Changed;

        /// <summary>Raised when a hit wipes out the tempo. Argument: stacks lost.</summary>
        public event Action<int> Broken;

        /// <summary>Current stacks.</summary>
        public int Stacks => stacks;

        /// <summary>Max stacks.</summary>
        public int MaxStacks => maxStacks;

        /// <summary>Current multiplier (1 with no stacks).</summary>
        public float Multiplier => 1f + stacks * bonusPerStack;

        /// <summary>0..1 fill for a meter.</summary>
        public float Fraction => maxStacks > 0 ? (float)stacks / maxStacks : 0f;

        /// <summary>True when steals give a bonus level.</summary>
        public bool IsDoubleStealActive => doubleStealStacks > 0 && stacks >= doubleStealStacks;

        private void Awake()
        {
            parry = GetComponentInChildren<PlayerParry>();
            health = GetComponentInChildren<Health>();
            weapon = GetComponentInChildren<WeaponController>();
            thief = GetComponentInChildren<PowerThief>();
        }

        private void OnEnable()
        {
            if (parry != null)
            {
                parry.Parried += HandleParried;
            }

            if (health != null)
            {
                health.DamageTaken += HandleDamageTaken;
            }

            if (thief != null)
            {
                thief.Stolen += HandleStolen;
            }
        }

        private void OnDisable()
        {
            if (parry != null)
            {
                parry.Parried -= HandleParried;
            }

            if (health != null)
            {
                health.DamageTaken -= HandleDamageTaken;
            }

            if (thief != null)
            {
                thief.Stolen -= HandleStolen;
            }

            SetStacks(0);
        }

        private void Update()
        {
            if (stacks == 0 || GameTime.IsPaused || Time.time - lastGainTime < decayDelay || Time.time < nextDecayTime)
            {
                return;
            }

            nextDecayTime = Time.time + decayInterval;
            SetStacks(stacks - 1);
        }

        /// <summary>Clears tempo without the "broken" effect (e.g. a new run).</summary>
        public void ResetTempo()
        {
            SetStacks(0);
        }

        private void HandleParried(ParryEvent parryEvent)
        {
            lastGainTime = Time.time;
            nextDecayTime = Time.time + decayDelay;
            SetStacks(stacks + (parryEvent.IsPerfect ? stacksPerPerfectParry : stacksPerParry));
        }

        private void HandleDamageTaken(int amount)
        {
            if (stacks == 0)
            {
                return;
            }

            int lost = stacks;
            SetStacks(0);
            Broken?.Invoke(lost);
        }

        private void HandleStolen(StolenPowerDefinition power, int level)
        {
            if (isGrantingBonusSteal || !IsDoubleStealActive || level >= power.MaxLevel)
            {
                return;
            }

            isGrantingBonusSteal = true;
            thief.Steal(power);
            isGrantingBonusSteal = false;
        }

        private void SetStacks(int value)
        {
            value = Mathf.Clamp(value, 0, maxStacks);
            if (value == stacks)
            {
                return;
            }

            stacks = value;
            ApplyMultiplier(Multiplier);
            Changed?.Invoke(stacks, Multiplier);
        }

        /// <summary>Swaps the old multiplier out of the player's damage stats and the new one in.</summary>
        private void ApplyMultiplier(float multiplier)
        {
            float ratio = multiplier / appliedMultiplier;
            appliedMultiplier = multiplier;

            if (weapon != null)
            {
                weapon.DamageMultiplier *= ratio;
            }

            if (parry != null)
            {
                parry.ReflectDamageMultiplier *= ratio;
                parry.CounterDamageMultiplier *= ratio;
            }
        }
    }
}

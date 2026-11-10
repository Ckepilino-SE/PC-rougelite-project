using System;
using System.Collections.Generic;
using Core.Timing;
using Gameplay.Player;
using Gameplay.Stages;
using Gameplay.Upgrades;
using UnityEngine;

namespace Gameplay.Powers
{
    /// <summary>
    /// The player's stolen powers. A perfect parry against an enemy (its lunge, or a shot it fired) steals that
    /// enemy's power into a slot for the rest of the stage. Stealing a power you already hold levels it up.
    /// When the slots are full, the oldest power is pushed out.
    /// </summary>
    /// <remarks>Put it on the player next to <see cref="PlayerParry"/>.</remarks>
    [DisallowMultipleComponent]
    public sealed class PowerThief : MonoBehaviour
    {
        /// <summary>One held power.</summary>
        public sealed class Slot
        {
            internal UpgradeEffect Effect;

            /// <summary>The power.</summary>
            public StolenPowerDefinition Power { get; internal set; }

            /// <summary>Current level (1-based).</summary>
            public int Level { get; internal set; }
        }

        [SerializeField, Min(1)]
        [Tooltip("Powers held at once before upgrades.")]
        private int baseSlots = 2;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance a normal (non-perfect) parry also steals. Perfect parries always steal.")]
        private float baseNormalStealChance;

        [SerializeField]
        [Tooltip("Stolen powers are lost when each stage starts (except any kept by upgrades).")]
        private StageEventChannelSO stageStartedChannel;

        private readonly List<Slot> slots = new List<Slot>();
        private PlayerParry parry;
        private UpgradeContext context;

        /// <summary>Raised after a steal. Arguments: power, its new level.</summary>
        public event Action<StolenPowerDefinition, int> Stolen;

        /// <summary>Raised whenever the held powers change.</summary>
        public event Action Changed;

        /// <summary>Extra slots from upgrades.</summary>
        public int ExtraSlots { get; set; }

        /// <summary>Extra chance for normal parries to steal, from upgrades.</summary>
        public float BonusStealChance { get; set; }

        /// <summary>Powers kept (newest first) when a stage starts, from upgrades.</summary>
        public int KeepOnStageStart { get; set; }

        /// <summary>Total slots.</summary>
        public int SlotCount => Mathf.Max(1, baseSlots + ExtraSlots);

        /// <summary>Held powers, oldest first.</summary>
        public IReadOnlyList<Slot> Slots => slots;

        private void Awake()
        {
            parry = GetComponentInChildren<PlayerParry>();
            context = new UpgradeContext(gameObject);
        }

        private void OnEnable()
        {
            if (parry != null)
            {
                parry.Parried += HandleParried;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }
        }

        private void OnDisable()
        {
            if (parry != null)
            {
                parry.Parried -= HandleParried;
            }

            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised -= HandleStageStarted;
            }
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private void Update()
        {
            if (GameTime.IsPaused)
            {
                return;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Effect?.Tick(dt);
            }
        }

        /// <summary>Steals <paramref name="power"/>: adds it, or levels it up if already held.</summary>
        public void Steal(StolenPowerDefinition power)
        {
            if (power == null)
            {
                return;
            }

            Slot slot = Find(power);
            if (slot != null)
            {
                // Move to newest so it is the last to be pushed out.
                slots.Remove(slot);
                slots.Add(slot);
                if (slot.Level < power.MaxLevel)
                {
                    slot.Effect?.Detach();
                    slot.Level++;
                    slot.Effect = power.CreateEffect(slot.Level);
                    slot.Effect?.Attach(context);
                }
            }
            else
            {
                while (slots.Count >= SlotCount)
                {
                    RemoveAt(0);
                }

                slot = new Slot { Power = power, Level = 1, Effect = power.CreateEffect(1) };
                slots.Add(slot);
                slot.Effect?.Attach(context);
            }

            Debug.Log($"[PowerThief] Stole '{power.PowerName}' (level {slot.Level}).", this);
            Stolen?.Invoke(power, slot.Level);
            Changed?.Invoke();
        }

        /// <summary>Drops every held power.</summary>
        public void ClearAll()
        {
            if (slots.Count == 0)
            {
                return;
            }

            while (slots.Count > 0)
            {
                RemoveAt(0);
            }

            Changed?.Invoke();
        }

        private void HandleParried(ParryEvent parryEvent)
        {
            if (!(parryEvent.Source?.StealablePower is StolenPowerDefinition power))
            {
                return;
            }

            float chance = baseNormalStealChance + BonusStealChance;
            if (parryEvent.IsPerfect || UnityEngine.Random.value < chance)
            {
                Steal(power);
            }
        }

        private void HandleStageStarted(StageData stage)
        {
            int keep = Mathf.Clamp(KeepOnStageStart, 0, slots.Count);
            bool changed = false;
            while (slots.Count > keep)
            {
                RemoveAt(0);
                changed = true;
            }

            // Slots may have shrunk (an upgrade was removed on a new run).
            while (slots.Count > SlotCount)
            {
                RemoveAt(0);
                changed = true;
            }

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        private void RemoveAt(int index)
        {
            slots[index].Effect?.Detach();
            slots.RemoveAt(index);
        }

        private Slot Find(StolenPowerDefinition power)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Power == power)
                {
                    return slots[i];
                }
            }

            return null;
        }
    }
}

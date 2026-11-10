using System;
using System.Collections.Generic;
using Core.Timing;
using Gameplay.Stages;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// Holds the upgrades the player has picked this run and keeps their effects attached.
    /// Put it on the player. <c>StageManager</c> clears it when a new run starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerUpgrades : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional, but needed by per-stage upgrades such as Second Wind.")]
        private StageEventChannelSO stageStartedChannel;

        private readonly List<UpgradeDefinition> owned = new List<UpgradeDefinition>();
        private readonly List<UpgradeEffect> effects = new List<UpgradeEffect>();
        private UpgradeContext context;

        /// <summary>Raised after an upgrade is added or the list is cleared.</summary>
        public event Action Changed;

        /// <summary>Upgrades picked this run, in order.</summary>
        public IReadOnlyList<UpgradeDefinition> Owned => owned;

        private void Awake()
        {
            context = new UpgradeContext(gameObject);
        }

        private void OnEnable()
        {
            if (stageStartedChannel != null)
            {
                stageStartedChannel.OnEventRaised += HandleStageStarted;
            }
        }

        private void OnDisable()
        {
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
            for (int i = 0; i < effects.Count; i++)
            {
                effects[i].Tick(dt);
            }
        }

        private void HandleStageStarted(StageData stage)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                effects[i].StageStarted();
            }
        }

        /// <summary>True if <paramref name="upgrade"/> was already picked this run.</summary>
        public bool Has(UpgradeDefinition upgrade)
        {
            return owned.Contains(upgrade);
        }

        /// <summary>How many times <paramref name="upgrade"/> has been picked this run.</summary>
        public int CountOf(UpgradeDefinition upgrade)
        {
            int count = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] == upgrade)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True if <paramref name="upgrade"/> can still be picked (below its max stacks).</summary>
        public bool CanTake(UpgradeDefinition upgrade)
        {
            return upgrade != null && CountOf(upgrade) < upgrade.MaxStacks;
        }

        /// <summary>Adds an upgrade and attaches its effect immediately.</summary>
        public void Add(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            UpgradeEffect effect = upgrade.CreateEffect();
            owned.Add(upgrade);
            if (effect != null)
            {
                effects.Add(effect);
                effect.Attach(context);
            }

            Debug.Log($"[PlayerUpgrades] Gained '{upgrade.Title}' ({CountOf(upgrade)}/{upgrade.MaxStacks}).", this);
            Changed?.Invoke();
        }

        /// <summary>Detaches every effect and forgets all upgrades (new run).</summary>
        public void ClearAll()
        {
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                effects[i].Detach();
            }

            effects.Clear();
            owned.Clear();
            Changed?.Invoke();
        }
    }
}

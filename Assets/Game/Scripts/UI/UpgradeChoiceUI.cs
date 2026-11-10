using System.Collections.Generic;
using Core.References;
using Gameplay.Stages;
using Gameplay.Upgrades;
using UnityEngine;
using UnityEngine.Events;

namespace Gameplay.UI
{
    /// <summary>
    /// "Pick 1 of 3" upgrade cards on the Stage Clear panel. Fills the cards when a stage is cleared; clicking
    /// one gives it to the player and raises <see cref="onUpgradeChosen"/> (wire it to
    /// <c>StageManager.LoadNextStage</c>).
    /// </summary>
    /// <remarks>
    /// After the final stage, or when the pool has nothing left to offer, the cards are hidden and the
    /// fallback Continue button is shown instead.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class UpgradeChoiceUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField]
        private UpgradePool upgradePool;

        [SerializeField]
        [Tooltip("Anchor the player registers itself in. The player needs a PlayerUpgrades component.")]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        [Tooltip("Optional. Used to skip upgrades after the final stage.")]
        private StageCatalog catalog;

        [SerializeField]
        [Tooltip("Minimum rarity offered after a Boss stage.")]
        private UpgradeRarity bossRewardRarity = UpgradeRarity.Rare;

        [Header("Channels")]
        [SerializeField]
        private StageEventChannelSO stageCompletedChannel;

        [Header("UI")]
        [SerializeField]
        [Tooltip("The cards. Usually three.")]
        private UpgradeOptionView[] options = new UpgradeOptionView[0];

        [SerializeField]
        [Tooltip("Shown instead of the cards when there is nothing to pick.")]
        private GameObject continueButton;

        [Header("Events")]
        [SerializeField]
        [Tooltip("Raised after an upgrade is picked. Wire to StageManager.LoadNextStage.")]
        private UnityEvent onUpgradeChosen = new UnityEvent();

        private readonly List<UpgradeDefinition> choices = new List<UpgradeDefinition>();
        private bool hasChosen;

        private void Awake()
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                {
                    options[i].Chosen += HandleChosen;
                }
            }
        }

        private void OnEnable()
        {
            if (stageCompletedChannel != null)
            {
                stageCompletedChannel.OnEventRaised += HandleStageCompleted;
            }
        }

        private void OnDisable()
        {
            if (stageCompletedChannel != null)
            {
                stageCompletedChannel.OnEventRaised -= HandleStageCompleted;
            }
        }

        private void HandleStageCompleted(StageData stage)
        {
            hasChosen = false;
            PlayerUpgrades player = FindPlayerUpgrades();

            int count = 0;
            if (upgradePool != null && player != null && !IsFinalStage(stage))
            {
                UpgradeRarity minimum = stage != null && stage.IsBossStage ? bossRewardRarity : UpgradeRarity.Common;
                count = upgradePool.PickChoices(player, choices, options.Length, minimum);
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                {
                    UpgradeDefinition upgrade = i < count ? choices[i] : null;
                    options[i].Bind(upgrade, upgrade != null && player != null ? player.CountOf(upgrade) : 0);
                }
            }

            if (continueButton != null)
            {
                continueButton.SetActive(count == 0);
            }
        }

        private void HandleChosen(UpgradeDefinition upgrade)
        {
            if (hasChosen)
            {
                return;
            }

            hasChosen = true;
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                {
                    options[i].SetInteractable(false);
                }
            }

            PlayerUpgrades player = FindPlayerUpgrades();
            if (player != null)
            {
                player.Add(upgrade);
            }

            onUpgradeChosen.Invoke();
        }

        private PlayerUpgrades FindPlayerUpgrades()
        {
            if (playerAnchor == null || !playerAnchor.IsSet)
            {
                return null;
            }

            playerAnchor.Value.TryGetComponent(out PlayerUpgrades upgrades);
            return upgrades;
        }

        private bool IsFinalStage(StageData stage)
        {
            return catalog != null && catalog.Count > 0 && catalog.GetStage(catalog.Count - 1) == stage;
        }
    }
}

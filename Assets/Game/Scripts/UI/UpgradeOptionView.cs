using System;
using Gameplay.Upgrades;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>One upgrade card: a button showing an upgrade's title, description and optional icon.</summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class UpgradeOptionView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text titleText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        [Tooltip("Optional. Shows rarity and stack count, e.g. 'RARE  -  2/3'.")]
        private TMP_Text rarityText;

        [SerializeField]
        private Color commonColor = new Color(0.75f, 0.75f, 0.75f);

        [SerializeField]
        private Color rareColor = new Color(0.3f, 0.6f, 1f);

        [SerializeField]
        private Color legendaryColor = new Color(1f, 0.7f, 0.15f);

        [SerializeField]
        [Tooltip("Optional. Hidden when the upgrade has no icon.")]
        private Image iconImage;

        [SerializeField]
        [Tooltip("Optional. Tinted with the upgrade's accent colour (e.g. a border or header strip).")]
        private Image accentImage;

        private Button button;

        /// <summary>Raised when the card is clicked. Argument: this card's upgrade.</summary>
        public event Action<UpgradeDefinition> Chosen;

        /// <summary>The upgrade currently shown.</summary>
        public UpgradeDefinition Upgrade { get; private set; }

        /// <summary>The card's button.</summary>
        public Button Button => button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(HandleClick);
        }

        /// <summary>Shows <paramref name="upgrade"/>, or hides the card when null.</summary>
        public void Bind(UpgradeDefinition upgrade)
        {
            Bind(upgrade, 0);
        }

        /// <summary>Shows <paramref name="upgrade"/> with how many the player already owns, or hides the card when null.</summary>
        public void Bind(UpgradeDefinition upgrade, int ownedCount)
        {
            Upgrade = upgrade;
            gameObject.SetActive(upgrade != null);
            if (upgrade == null)
            {
                return;
            }

            if (titleText != null)
            {
                titleText.text = upgrade.Title;
            }

            if (descriptionText != null)
            {
                descriptionText.text = upgrade.Description;
            }

            if (rarityText != null)
            {
                Color rarityColor = upgrade.Rarity == UpgradeRarity.Legendary ? legendaryColor
                    : upgrade.Rarity == UpgradeRarity.Rare ? rareColor
                    : commonColor;
                string label = upgrade.Rarity.ToString().ToUpperInvariant();
                rarityText.text = upgrade.MaxStacks > 1 ? $"{label}  -  {ownedCount + 1}/{upgrade.MaxStacks}" : label;
                rarityText.color = rarityColor;
            }

            if (iconImage != null)
            {
                iconImage.sprite = upgrade.Icon;
                iconImage.enabled = upgrade.Icon != null;
            }

            if (accentImage != null)
            {
                accentImage.color = upgrade.AccentColor;
            }

            button.interactable = true;
        }

        /// <summary>Enables or disables clicking.</summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void HandleClick()
        {
            if (Upgrade != null)
            {
                Chosen?.Invoke(Upgrade);
            }
        }
    }
}

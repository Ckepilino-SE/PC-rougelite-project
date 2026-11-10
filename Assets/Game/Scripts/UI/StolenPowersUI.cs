using Core.References;
using Gameplay.Powers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>
    /// Shows the player's stolen powers as a row of slots ("Shockwave II"), and flashes a "STOLEN: ..." toast
    /// whenever a power is taken. Finds the player through its anchor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StolenPowersUI : MonoBehaviour
    {
        private static readonly string[] Numerals = { "", "I", "II", "III", "IV", "V" };

        [SerializeField]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        [Tooltip("Slot backgrounds, left to right. Each needs a TMP text child.")]
        private Image[] slotImages = new Image[0];

        [SerializeField]
        private Color emptySlotColor = new Color(1f, 1f, 1f, 0.12f);

        [Header("Toast")]
        [SerializeField]
        [Tooltip("Optional. 'STOLEN: Shockwave' message. Needs a CanvasGroup.")]
        private CanvasGroup toastGroup;

        [SerializeField]
        private TMP_Text toastText;

        [SerializeField, Min(0.1f)]
        private float toastDuration = 1.6f;

        private PowerThief thief;
        private Transform cachedPlayer;
        private TMP_Text[] slotTexts;
        private bool dirty = true;
        private int shownCapacity = -1;
        private float toastShownAt = -100f;

        private void Awake()
        {
            slotTexts = new TMP_Text[slotImages.Length];
            for (int i = 0; i < slotImages.Length; i++)
            {
                if (slotImages[i] != null)
                {
                    slotTexts[i] = slotImages[i].GetComponentInChildren<TMP_Text>(true);
                }
            }

            if (toastGroup != null)
            {
                toastGroup.alpha = 0f;
            }
        }

        private void OnDisable()
        {
            Bind(null);
        }

        private void Update()
        {
            if (playerAnchor != null && playerAnchor.IsSet && playerAnchor.Value != cachedPlayer)
            {
                cachedPlayer = playerAnchor.Value;
                Bind(cachedPlayer.GetComponentInChildren<PowerThief>());
            }

            // Slot count can change from upgrades without a steal.
            int capacity = thief != null ? thief.SlotCount : 0;
            if (dirty || capacity != shownCapacity)
            {
                dirty = false;
                shownCapacity = capacity;
                Refresh();
            }

            if (toastGroup != null)
            {
                float t = (Time.unscaledTime - toastShownAt) / toastDuration;
                toastGroup.alpha = t < 0.75f ? 1f : Mathf.Clamp01((1f - t) * 4f);
            }
        }

        private void Bind(PowerThief newThief)
        {
            if (thief != null)
            {
                thief.Changed -= MarkDirty;
                thief.Stolen -= HandleStolen;
            }

            thief = newThief;
            if (thief != null)
            {
                thief.Changed += MarkDirty;
                thief.Stolen += HandleStolen;
            }

            dirty = true;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void HandleStolen(StolenPowerDefinition power, int level)
        {
            if (toastText != null)
            {
                toastText.text = level > 1
                    ? $"STOLEN: {power.PowerName} {Numerals[Mathf.Clamp(level, 0, Numerals.Length - 1)]}\n<size=60%>{power.Description}</size>"
                    : $"STOLEN: {power.PowerName}\n<size=60%>{power.Description}</size>";
                toastText.color = power.Color;
            }

            toastShownAt = Time.unscaledTime;
        }

        private void Refresh()
        {
            int capacity = thief != null ? thief.SlotCount : 0;
            for (int i = 0; i < slotImages.Length; i++)
            {
                Image image = slotImages[i];
                if (image == null)
                {
                    continue;
                }

                bool visible = i < capacity;
                image.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                PowerThief.Slot slot = i < thief.Slots.Count ? thief.Slots[i] : null;
                image.color = slot != null ? slot.Power.Color : emptySlotColor;
                if (slotTexts[i] != null)
                {
                    slotTexts[i].text = slot != null
                        ? $"{slot.Power.PowerName} {Numerals[Mathf.Clamp(slot.Level, 0, Numerals.Length - 1)]}"
                        : "-";
                }
            }
        }
    }
}

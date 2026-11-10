using Core.References;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Tempo
{
    /// <summary>
    /// Tempo HUD: "x1.4" text, a fill bar, a glow when double-steal is active, and a red flash + shake when a hit
    /// breaks the chain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TempoUI : MonoBehaviour
    {
        [SerializeField]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        private TMP_Text multiplierText;

        [SerializeField]
        [Tooltip("Image Type = Filled, Horizontal.")]
        private Image fillImage;

        [SerializeField]
        [Tooltip("Optional. Shown when steals give a bonus level.")]
        private TMP_Text bonusText;

        [SerializeField]
        private Color lowColor = new Color(0.4f, 0.75f, 1f);

        [SerializeField]
        private Color highColor = new Color(1f, 0.8f, 0.2f);

        [SerializeField]
        private Color brokenColor = new Color(1f, 0.25f, 0.25f);

        [SerializeField, Min(0f)]
        private float brokenFlashDuration = 0.5f;

        private Transform cachedPlayer;
        private TempoMeter tempo;
        private RectTransform rect;
        private Vector2 basePosition;
        private float brokenUntil = -1f;
        private float pulseUntil = -1f;
        private int shownStacks = -1;

        private void Awake()
        {
            rect = (RectTransform)transform;
            basePosition = rect.anchoredPosition;
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
                Bind(cachedPlayer.GetComponentInChildren<TempoMeter>());
            }

            if (tempo == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            bool broken = now < brokenUntil;
            Color color = broken ? brokenColor : Color.Lerp(lowColor, highColor, tempo.Fraction);

            if (fillImage != null)
            {
                fillImage.fillAmount = tempo.Fraction;
                fillImage.color = color;
            }

            if (multiplierText != null)
            {
                if (tempo.Stacks != shownStacks)
                {
                    shownStacks = tempo.Stacks;
                    multiplierText.SetText("x{0:1}", tempo.Multiplier);
                }

                multiplierText.color = color;
                float pop = now < pulseUntil ? 1f + 0.25f * ((pulseUntil - now) / 0.15f) : 1f;
                multiplierText.rectTransform.localScale = Vector3.one * pop;
            }

            if (bonusText != null && bonusText.enabled != tempo.IsDoubleStealActive)
            {
                bonusText.enabled = tempo.IsDoubleStealActive;
            }

            rect.anchoredPosition = broken
                ? basePosition + Random.insideUnitCircle * 6f * ((brokenUntil - now) / brokenFlashDuration)
                : basePosition;
        }

        private void Bind(TempoMeter meter)
        {
            if (tempo != null)
            {
                tempo.Changed -= HandleChanged;
                tempo.Broken -= HandleBroken;
            }

            tempo = meter;
            shownStacks = -1;
            if (tempo != null)
            {
                tempo.Changed += HandleChanged;
                tempo.Broken += HandleBroken;
            }
        }

        private void HandleChanged(int stacks, float multiplier)
        {
            if (stacks > 0)
            {
                pulseUntil = Time.unscaledTime + 0.15f;
            }
        }

        private void HandleBroken(int lost)
        {
            brokenUntil = Time.unscaledTime + brokenFlashDuration;
        }
    }
}

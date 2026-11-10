using Core.References;
using Gameplay.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>
    /// Player HP bar: a filled image plus "4 / 6" text, with a trail that shows recent damage and a red flash
    /// when hit. Finds the player through its anchor, so it needs no scene reference.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealthBarUI : MonoBehaviour
    {
        [SerializeField]
        private TransformAnchorSO playerAnchor;

        [SerializeField]
        [Tooltip("Image Type = Filled, Fill Method = Horizontal.")]
        private Image fillImage;

        [SerializeField]
        [Tooltip("Optional. Filled image behind the fill that drains slowly after a hit.")]
        private Image damageTrailImage;

        [SerializeField]
        [Tooltip("Optional. Shows 'current / max'.")]
        private TMP_Text valueText;

        [SerializeField]
        private Color healthyColor = new Color(0.3f, 0.85f, 0.35f);

        [SerializeField]
        private Color lowColor = new Color(0.9f, 0.2f, 0.2f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("At or below this fraction the bar turns fully to the low colour and pulses.")]
        private float lowThreshold = 0.34f;

        [SerializeField, Min(0.01f)]
        private float trailSpeed = 0.8f;

        private Transform cachedPlayer;
        private Health health;
        private int shownCurrent = -1;
        private int shownMax = -1;

        private void Update()
        {
            if (!TryGetHealth())
            {
                return;
            }

            int current = health.CurrentHealth;
            int max = Mathf.Max(1, health.MaxHealth);
            float fraction = Mathf.Clamp01((float)current / max);

            if (fillImage != null)
            {
                fillImage.fillAmount = fraction;
                Color color = Color.Lerp(lowColor, healthyColor, Mathf.InverseLerp(lowThreshold, 1f, fraction));
                if (fraction <= lowThreshold && current > 0)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                    color = Color.Lerp(lowColor, Color.white, pulse * 0.35f);
                }

                fillImage.color = color;
            }

            if (damageTrailImage != null)
            {
                damageTrailImage.fillAmount = damageTrailImage.fillAmount > fraction
                    ? Mathf.MoveTowards(damageTrailImage.fillAmount, fraction, trailSpeed * Time.unscaledDeltaTime)
                    : fraction;
            }

            // Only rebuild the text when the numbers change (avoids garbage every frame).
            if (valueText != null && (current != shownCurrent || max != shownMax))
            {
                shownCurrent = current;
                shownMax = max;
                valueText.SetText("{0} / {1}", (float)current, (float)max);
            }
        }

        private bool TryGetHealth()
        {
            if (playerAnchor == null || !playerAnchor.IsSet)
            {
                return false;
            }

            Transform player = playerAnchor.Value;
            if (player != cachedPlayer)
            {
                cachedPlayer = player;
                health = player.GetComponentInChildren<Health>();
            }

            return health != null;
        }
    }
}

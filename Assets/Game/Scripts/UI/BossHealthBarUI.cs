using Gameplay.Enemies.Boss;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.UI
{
    /// <summary>
    /// Boss health bar: appears while a boss is alive, shows its name, current phase and health.
    /// Listens to <see cref="BossController.ActiveChanged"/>, so it needs no reference to the boss.
    /// </summary>
    /// <remarks>
    /// Setup: a panel with a CanvasGroup (this component), a background Image, a fill Image with
    /// Image Type = Filled / Horizontal, and optional name and phase texts.
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    [DisallowMultipleComponent]
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Image with Image Type = Filled, Fill Method = Horizontal.")]
        private Image fillImage;

        [SerializeField]
        [Tooltip("Optional. Trails behind the fill to show recent damage. Also a Filled image.")]
        private Image damageTrailImage;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        [Tooltip("Optional. Shows e.g. 'Phase 2'.")]
        private TMP_Text phaseText;

        [SerializeField]
        [Tooltip("Optional. Posture (stagger) meter, a Filled image. Full = boss staggers.")]
        private Image postureFillImage;

        [SerializeField]
        [Tooltip("Optional. Shown while the boss is staggered.")]
        private TMP_Text staggerText;

        [SerializeField]
        private Color postureColor = new Color(1f, 0.85f, 0.2f);

        [SerializeField]
        private Color staggeredColor = new Color(1f, 1f, 1f);

        [SerializeField, Min(0.01f)]
        [Tooltip("How fast the damage trail catches up, in fill per second.")]
        private float trailSpeed = 0.6f;

        [SerializeField]
        [Tooltip("Fill colour per phase. Leave empty to keep the image colour.")]
        private Color[] phaseColors = { new Color(0.9f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(0.75f, 0.2f, 1f) };

        private CanvasGroup group;
        private BossController boss;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            SetBoss(BossController.Active);
        }

        private void OnEnable()
        {
            BossController.ActiveChanged += SetBoss;
            BossController.PhaseChanged += HandlePhaseChanged;
            SetBoss(BossController.Active);
        }

        private void OnDisable()
        {
            BossController.ActiveChanged -= SetBoss;
            BossController.PhaseChanged -= HandlePhaseChanged;
        }

        private void SetBoss(BossController newBoss)
        {
            boss = newBoss;
            if (group != null)
            {
                group.alpha = boss != null ? 1f : 0f;
            }

            if (boss == null)
            {
                return;
            }

            if (nameText != null)
            {
                nameText.text = boss.BossName;
            }

            if (damageTrailImage != null)
            {
                damageTrailImage.fillAmount = 1f;
            }

            HandlePhaseChanged(boss, 0);
        }

        private void HandlePhaseChanged(BossController changed, int phase)
        {
            if (changed != boss || boss == null)
            {
                return;
            }

            if (phaseText != null)
            {
                phaseText.text = boss.PhaseCount > 1 ? $"Phase {phase + 1}" : string.Empty;
            }

            if (fillImage != null && phaseColors != null && phaseColors.Length > 0)
            {
                fillImage.color = phaseColors[Mathf.Min(phase, phaseColors.Length - 1)];
            }
        }

        private void Update()
        {
            if (boss == null || boss.Health == null)
            {
                return;
            }

            int max = boss.Health.MaxHealth;
            float fraction = max > 0 ? Mathf.Clamp01((float)boss.Health.CurrentHealth / max) : 0f;

            if (fillImage != null)
            {
                fillImage.fillAmount = fraction;
            }

            bool staggered = boss.IsStaggered;
            if (postureFillImage != null)
            {
                postureFillImage.fillAmount = staggered ? boss.StaggerRemainingFraction : boss.PostureFraction;
                postureFillImage.color = staggered ? staggeredColor : postureColor;
            }

            if (staggerText != null && staggerText.enabled != staggered)
            {
                staggerText.enabled = staggered;
            }

            if (damageTrailImage != null)
            {
                damageTrailImage.fillAmount = damageTrailImage.fillAmount > fraction
                    ? Mathf.MoveTowards(damageTrailImage.fillAmount, fraction, trailSpeed * Time.unscaledDeltaTime)
                    : fraction;
            }
        }
    }
}

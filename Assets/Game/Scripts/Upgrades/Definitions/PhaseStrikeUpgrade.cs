using Core.Combat;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>
    /// "Phase Strike": the dash counts as a parry. Dashing into an enemy parries it (stun + counter-hit), and
    /// attacks that land mid-dash are parried. Every dash-parry fires the normal Parried event, so it triggers
    /// other parry upgrades too.
    /// </summary>
    [CreateAssetMenu(fileName = "PhaseStrike", menuName = "Game/Upgrades/Phase Strike (Dash Parries)")]
    public sealed class PhaseStrikeUpgrade : UpgradeDefinition
    {
        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect();
        }

        private sealed class Effect : UpgradeEffect
        {
            private IParryable lastTarget;
            private float lastTime = -1f;

            protected override void OnAttach()
            {
                if (Context.Parry == null || Context.Dash == null)
                {
                    Debug.LogWarning("[Phase Strike] Player needs PlayerParry and PlayerDash.");
                    return;
                }

                Context.Parry.AddParryWhileDashing();
                Context.Dash.DashCollided += HandleDashCollided;
            }

            protected override void OnDetach()
            {
                if (Context.Parry == null || Context.Dash == null)
                {
                    return;
                }

                Context.Parry.RemoveParryWhileDashing();
                Context.Dash.DashCollided -= HandleDashCollided;
            }

            private void HandleDashCollided(Collider2D other)
            {
                if (!ParryLookup.TryGet(other, out IParryable target))
                {
                    return;
                }

                // A lunging enemy may already have been parried this same physics step by its own contact check.
                if (ReferenceEquals(target, lastTarget) && Time.time - lastTime < 0.1f)
                {
                    return;
                }

                lastTarget = target;
                lastTime = Time.time;
                Context.Parry.ParryByDash(target, other.transform.position);
            }
        }
    }
}

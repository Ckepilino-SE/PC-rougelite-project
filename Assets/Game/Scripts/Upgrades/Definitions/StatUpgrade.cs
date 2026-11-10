using System;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>A player stat a <see cref="StatUpgrade"/> can change.</summary>
    public enum PlayerStat
    {
        /// <summary>Percent. 0.1 = +10% move speed.</summary>
        MoveSpeed,

        /// <summary>Percent. 0.15 = +15% shots per second.</summary>
        FireRate,

        /// <summary>Percent. 0.2 = +20% damage on every projectile you fire.</summary>
        Damage,

        /// <summary>Flat. 1 = +1 max health (negative removes).</summary>
        MaxHealth,

        /// <summary>Flat. 1 = one more projectile per shot.</summary>
        ExtraProjectiles,

        /// <summary>Flat. 1 = shots pass through one more enemy.</summary>
        Pierce,

        /// <summary>Percent. -0.15 = dash cooldown 15% shorter.</summary>
        DashCooldown,

        /// <summary>Percent. 0.25 = parry and perfect-parry windows 25% longer.</summary>
        ParryWindow,

        /// <summary>Percent. -0.2 = parry cooldown 20% shorter.</summary>
        ParryCooldown,

        /// <summary>Percent. 0.5 = reflected shots deal 50% more.</summary>
        ReflectDamage,

        /// <summary>Percent. 1 = parried enemies take double counter damage.</summary>
        CounterDamage,

        /// <summary>Percent. 0.5 = parried enemies stay stunned 50% longer.</summary>
        StunDuration,
    }

    /// <summary>One stat change.</summary>
    [Serializable]
    public struct StatModifier
    {
        [SerializeField]
        private PlayerStat stat;

        [SerializeField]
        [Tooltip("Percent stats: 0.1 = +10%, -0.2 = -20%. Flat stats: whole numbers.")]
        private float amount;

        /// <summary>Creates a modifier.</summary>
        public StatModifier(PlayerStat stat, float amount)
        {
            this.stat = stat;
            this.amount = amount;
        }

        /// <summary>The stat.</summary>
        public PlayerStat Stat => stat;

        /// <summary>The change.</summary>
        public float Amount => amount;
    }

    /// <summary>
    /// A simple stat upgrade (e.g. "+15% fire rate"). One class covers many cards: create an asset per card and
    /// list its modifiers. Set Max Stacks above 1 to let it be picked repeatedly.
    /// </summary>
    [CreateAssetMenu(fileName = "StatUpgrade", menuName = "Game/Upgrades/Stat Upgrade")]
    public sealed class StatUpgrade : UpgradeDefinition
    {
        private const float MinimumFactor = 0.1f;

        [SerializeField]
        private StatModifier[] modifiers = new StatModifier[0];

        /// <inheritdoc />
        public override UpgradeEffect CreateEffect()
        {
            return new Effect(modifiers);
        }

        private sealed class Effect : UpgradeEffect
        {
            private readonly StatModifier[] modifiers;

            public Effect(StatModifier[] modifiers)
            {
                this.modifiers = modifiers ?? new StatModifier[0];
            }

            protected override void OnAttach()
            {
                for (int i = 0; i < modifiers.Length; i++)
                {
                    Apply(modifiers[i], +1);
                }
            }

            protected override void OnDetach()
            {
                for (int i = modifiers.Length - 1; i >= 0; i--)
                {
                    Apply(modifiers[i], -1);
                }
            }

            /// <param name="sign">+1 to apply, -1 to undo.</param>
            private void Apply(in StatModifier modifier, int sign)
            {
                float factor = Mathf.Max(MinimumFactor, 1f + modifier.Amount);
                float scale = sign > 0 ? factor : 1f / factor;
                int flat = Mathf.RoundToInt(modifier.Amount) * sign;

                switch (modifier.Stat)
                {
                    case PlayerStat.MoveSpeed:
                        if (Context.Movement != null)
                        {
                            Context.Movement.SpeedMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.FireRate:
                        if (Context.Weapon != null)
                        {
                            Context.Weapon.FireRateMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.Damage:
                        if (Context.Weapon != null)
                        {
                            Context.Weapon.DamageMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.MaxHealth:
                        if (Context.Health != null)
                        {
                            Context.Health.AddBonusMaxHealth(flat);
                        }

                        break;
                    case PlayerStat.ExtraProjectiles:
                        if (Context.Weapon != null)
                        {
                            Context.Weapon.ExtraProjectiles += flat;
                        }

                        break;
                    case PlayerStat.Pierce:
                        if (Context.Weapon != null)
                        {
                            Context.Weapon.Pierce += flat;
                        }

                        break;
                    case PlayerStat.DashCooldown:
                        if (Context.Dash != null)
                        {
                            Context.Dash.CooldownMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.ParryWindow:
                        if (Context.Parry != null)
                        {
                            Context.Parry.WindowMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.ParryCooldown:
                        if (Context.Parry != null)
                        {
                            Context.Parry.CooldownMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.ReflectDamage:
                        if (Context.Parry != null)
                        {
                            Context.Parry.ReflectDamageMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.CounterDamage:
                        if (Context.Parry != null)
                        {
                            Context.Parry.CounterDamageMultiplier *= scale;
                        }

                        break;
                    case PlayerStat.StunDuration:
                        if (Context.Parry != null)
                        {
                            Context.Parry.StunMultiplier *= scale;
                        }

                        break;
                }
            }
        }
    }
}

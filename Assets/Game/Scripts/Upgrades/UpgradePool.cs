using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Upgrades
{
    /// <summary>The set of upgrades that can be offered after a stage, with rarity weights.</summary>
    [CreateAssetMenu(fileName = "UpgradePool", menuName = "Game/Upgrades/Upgrade Pool")]
    public sealed class UpgradePool : ScriptableObject
    {
        [SerializeField]
        private UpgradeDefinition[] upgrades = new UpgradeDefinition[0];

        [Header("Rarity Weights")]
        [SerializeField, Min(0f)]
        private float commonWeight = 60f;

        [SerializeField, Min(0f)]
        private float rareWeight = 30f;

        [SerializeField, Min(0f)]
        private float legendaryWeight = 10f;

        private readonly List<UpgradeDefinition> scratch = new List<UpgradeDefinition>();

        /// <summary>Number of upgrade slots in the pool.</summary>
        public int Count => upgrades != null ? upgrades.Length : 0;

        /// <summary>
        /// Fills <paramref name="results"/> with up to <paramref name="count"/> random, distinct upgrades the player
        /// can still take (below max stacks), weighted by rarity.
        /// </summary>
        /// <param name="minimumRarity">
        /// Prefer upgrades of at least this rarity (e.g. Rare after a boss). If there aren't enough, the rest are
        /// filled from lower rarities.
        /// </param>
        /// <returns>How many were picked (fewer than requested once the pool runs dry).</returns>
        public int PickChoices(PlayerUpgrades player, List<UpgradeDefinition> results, int count,
            UpgradeRarity minimumRarity = UpgradeRarity.Common)
        {
            results.Clear();

            Collect(player, results, minimumRarity, onlyAtLeast: true);
            PickWeighted(results, count);

            if (results.Count < count && minimumRarity > UpgradeRarity.Common)
            {
                Collect(player, results, minimumRarity, onlyAtLeast: false);
                PickWeighted(results, count);
            }

            return results.Count;
        }

        private void Collect(PlayerUpgrades player, List<UpgradeDefinition> exclude, UpgradeRarity minimum, bool onlyAtLeast)
        {
            scratch.Clear();
            for (int i = 0; i < Count; i++)
            {
                UpgradeDefinition upgrade = upgrades[i];
                if (upgrade == null || scratch.Contains(upgrade) || exclude.Contains(upgrade))
                {
                    continue;
                }

                bool rarityOk = onlyAtLeast ? upgrade.Rarity >= minimum : upgrade.Rarity < minimum;
                if (rarityOk && (player == null || player.CanTake(upgrade)))
                {
                    scratch.Add(upgrade);
                }
            }
        }

        /// <summary>Moves weighted-random picks from <see cref="scratch"/> into <paramref name="results"/> without repeats.</summary>
        private void PickWeighted(List<UpgradeDefinition> results, int count)
        {
            while (results.Count < count && scratch.Count > 0)
            {
                float total = 0f;
                for (int i = 0; i < scratch.Count; i++)
                {
                    total += WeightOf(scratch[i]);
                }

                int chosen = scratch.Count - 1;
                if (total > 0f)
                {
                    float roll = Random.value * total;
                    for (int i = 0; i < scratch.Count; i++)
                    {
                        roll -= WeightOf(scratch[i]);
                        if (roll <= 0f)
                        {
                            chosen = i;
                            break;
                        }
                    }
                }
                else
                {
                    chosen = Random.Range(0, scratch.Count);
                }

                results.Add(scratch[chosen]);
                scratch.RemoveAt(chosen);
            }
        }

        private float WeightOf(UpgradeDefinition upgrade)
        {
            switch (upgrade.Rarity)
            {
                case UpgradeRarity.Legendary:
                    return legendaryWeight;
                case UpgradeRarity.Rare:
                    return rareWeight;
                default:
                    return commonWeight;
            }
        }
    }
}

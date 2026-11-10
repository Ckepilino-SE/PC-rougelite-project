using System;
using System.Collections.Generic;
using Gameplay.Enemies;
using Gameplay.Powers;
using Gameplay.Upgrades;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// <b>Tools &gt; Roguelite &gt; Set Up Power Theft</b>: creates the stolen-power assets, gives each enemy prefab
    /// its power, and adds the theft upgrades (Deep Pockets, Pickpocket, Hoarder) to the upgrade pool.
    /// Existing power and upgrade assets are left alone; enemy assignments are always re-applied.
    /// </summary>
    public static class PowerTheftSetup
    {
        private const string PowerFolder = "Assets/Game/Powers";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string BossFolder = "Assets/Prefabs/Bosses";

        [MenuItem("Tools/Roguelite/Set Up Power Theft")]
        public static void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(PowerFolder))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Powers");
            }

            var fury = Power<FuryPower>("Power_Fury", "Fury", "+20% damage per level.", new Color(1f, 0.45f, 0.35f), 3);
            var blink = Power<BlinkStrikePower>("Power_BlinkStrike", "Blink Strike", "Dash recharges faster and hurts what you ram.", new Color(0.4f, 0.85f, 1f), 3);
            var shock = Power<ShockwavePower>("Power_Shockwave", "Shockwave", "Every parry blasts and stuns nearby enemies.", new Color(1f, 0.75f, 0.3f), 3,
                so => so.FindProperty("enemyMask").intValue = LayerMask.GetMask("Enemy"));
            var split = Power<SplitVolleyPower>("Power_SplitVolley", "Split Volley", "+1 projectile per shot per level.", new Color(0.6f, 1f, 0.5f), 2);
            var echo = Power<BossEchoPower>("Power_BossEcho", "Sovereign Echo", "Unleash the boss's bullet ring every few seconds.", new Color(0.85f, 0.5f, 1f), 3);

            int assigned = 0;
            assigned += Assign($"{PrefabFolder}/Grunt.prefab", fury);
            assigned += Assign($"{PrefabFolder}/Dasher.prefab", blink);
            assigned += Assign($"{PrefabFolder}/Brute.prefab", shock);
            assigned += Assign($"{PrefabFolder}/Splitter.prefab", split);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { BossFolder }))
            {
                assigned += Assign(AssetDatabase.GUIDToAssetPath(guid), echo);
            }

            Color common = new Color(0.75f, 0.75f, 0.75f);
            Color rare = new Color(0.3f, 0.6f, 1f);
            Color legendary = new Color(1f, 0.7f, 0.15f);
            var upgrades = new List<UpgradeDefinition>
            {
                RunContentGenerator.Make<ThiefUpgrade>("DeepPockets", "Deep Pockets", "+1 stolen power slot.", UpgradeRarity.Rare, 2, rare,
                    so => so.FindProperty("extraSlots").intValue = 1),
                RunContentGenerator.Make<ThiefUpgrade>("Pickpocket", "Pickpocket", "Normal parries have a 35% chance to steal too.", UpgradeRarity.Common, 2, common,
                    so => so.FindProperty("normalStealChance").floatValue = 0.35f),
                RunContentGenerator.Make<ThiefUpgrade>("Hoarder", "Hoarder", "Keep your newest stolen power into the next stage.", UpgradeRarity.Legendary, 2, legendary,
                    so => so.FindProperty("keepBetweenStages").intValue = 1),
            };
            RunContentGenerator.FillUpgradePool(upgrades);

            AssetDatabase.SaveAssets();
            Debug.Log($"[PowerTheftSetup] Done: 5 powers, {assigned} enemy prefabs given a power, 3 theft upgrades added to the pool.");
        }

        private static T Power<T>(string file, string powerName, string description, Color color, int maxLevel,
            Action<SerializedObject> configure = null) where T : StolenPowerDefinition
        {
            string path = $"{PowerFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("powerName").stringValue = powerName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("maxLevel").intValue = maxLevel;
            configure?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        private static int Assign(string prefabPath, StolenPowerDefinition power)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[PowerTheftSetup] Missing prefab {prefabPath}; skipped.");
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var enemy = root.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    return 0;
                }

                var so = new SerializedObject(enemy);
                so.FindProperty("stealablePower").objectReferenceValue = power;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return 1;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

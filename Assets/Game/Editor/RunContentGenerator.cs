using System;
using System.Collections.Generic;
using System.IO;
using Gameplay.Combat;
using Gameplay.Enemies;
using Gameplay.Enemies.Boss;
using Gameplay.Stages;
using Gameplay.Upgrades;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// One-click generator for the full run: 3 acts of 5 stages + a boss, then a final boss (19 stages),
    /// four boss prefabs, and the full upgrade set. Menu: <b>Tools &gt; Roguelite &gt; Generate Run Content</b>.
    /// </summary>
    /// <remarks>
    /// Non-destructive: assets that already exist are left as they are (delete one and re-run to regenerate
    /// it). The only things updated in place are the Stage Catalog's list, the Upgrade Pool's list, the
    /// rarity of the four original upgrades, and the Splitter's shared projectile pool size.
    /// </remarks>
    public static class RunContentGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string BossFolder = "Assets/Prefabs/Bosses";
        private const string StageFolder = "Assets/Game/Stages/Run";
        internal const string UpgradeFolder = "Assets/Game/Events/Upgrades";
        private const int SharedEnemyShotCapacity = 400;

        private static readonly string[][] StageTitles =
        {
            new[] { "Overgrown Road", "Broken Gate", "Old Market", "Ashen Square", "Warden's Approach" },
            new[] { "Burrow Mouth", "Chitin Halls", "Brood Pits", "Spore Gardens", "Queen's Gate" },
            new[] { "Slag Fields", "Iron Steps", "Furnace Core", "Hammer Hall", "Juggernaut's Forge" },
        };

        private static EnemyController grunt;
        private static EnemyController dasher;
        private static EnemyController brute;
        private static EnemyController splitter;
        private static Projectile enemyShot;
        private static HazardZone hazard;

        [MenuItem("Tools/Roguelite/Generate Run Content")]
        public static void Generate()
        {
            if (!LoadPrefabs())
            {
                return;
            }

            EnsureFolder(BossFolder);
            EnsureFolder(StageFolder);
            EnsureFolder(UpgradeFolder);

            WidenSplitterShotPool();
            List<UpgradeDefinition> upgrades = CreateUpgrades();
            FillUpgradePool(upgrades);
            EnemyController[] bosses = CreateBosses();
            List<StageData> stages = CreateStages(bosses);
            FillCatalog(stages);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RunContentGenerator] Done: {stages.Count} stages, {bosses.Length} bosses, {upgrades.Count} upgrades in the pool.");
        }

        // ------------------------------------------------------------------ prefabs

        private static bool LoadPrefabs()
        {
            grunt = LoadComponent<EnemyController>("Grunt");
            dasher = LoadComponent<EnemyController>("Dasher");
            brute = LoadComponent<EnemyController>("Brute");
            splitter = LoadComponent<EnemyController>("Splitter");
            enemyShot = LoadComponent<Projectile>("EnemyProjectile");
            hazard = LoadComponent<HazardZone>("HazardZone");

            if (grunt == null || brute == null || enemyShot == null)
            {
                Debug.LogError("[RunContentGenerator] Needs at least Grunt, Brute and EnemyProjectile prefabs in Assets/Prefabs.");
                return false;
            }

            dasher = dasher != null ? dasher : grunt;
            splitter = splitter != null ? splitter : grunt;
            return true;
        }

        private static T LoadComponent<T>(string prefabName) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{prefabName}.prefab");
            if (go == null)
            {
                Debug.LogWarning($"[RunContentGenerator] Missing prefab {PrefabFolder}/{prefabName}.prefab.");
                return null;
            }

            return go.GetComponent<T>();
        }

        private static void WidenSplitterShotPool()
        {
            if (splitter == null || splitter == grunt)
            {
                return;
            }

            var so = new SerializedObject(splitter);
            SerializedProperty capacity = so.FindProperty("ranged.sharedPoolCapacity");
            if (capacity != null && capacity.intValue < SharedEnemyShotCapacity)
            {
                // Bosses share this projectile pool; the first user's capacity wins, so make it big enough for both.
                capacity.intValue = SharedEnemyShotCapacity;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(splitter.gameObject);
            }
        }

        // ------------------------------------------------------------------ upgrades

        private static List<UpgradeDefinition> CreateUpgrades()
        {
            var list = new List<UpgradeDefinition>();
            Color common = new Color(0.75f, 0.75f, 0.75f);
            Color rare = new Color(0.3f, 0.6f, 1f);
            Color legendary = new Color(1f, 0.7f, 0.15f);

            // The four originals: just give them a rarity.
            AddExisting(list, "PhaseStrike", UpgradeRarity.Rare);
            AddExisting(list, "RiposteBurst", UpgradeRarity.Rare);
            AddExisting(list, "Aftershock", UpgradeRarity.Rare);
            AddExisting(list, "MirrorPact", UpgradeRarity.Legendary);

            // Stat upgrades (stackable).
            list.Add(Stat("IronSkin", "Iron Skin", "+1 max health.", UpgradeRarity.Common, 5, common, (PlayerStat.MaxHealth, 1)));
            list.Add(Stat("QuickDraw", "Quick Draw", "+15% fire rate.", UpgradeRarity.Common, 4, common, (PlayerStat.FireRate, 0.15f)));
            list.Add(Stat("HeavyRounds", "Heavy Rounds", "+20% damage.", UpgradeRarity.Common, 4, common, (PlayerStat.Damage, 0.2f)));
            list.Add(Stat("FleetFoot", "Fleet Foot", "+10% move speed.", UpgradeRarity.Common, 3, common, (PlayerStat.MoveSpeed, 0.1f)));
            list.Add(Stat("Afterimage", "Afterimage", "Dash cooldown 15% shorter.", UpgradeRarity.Common, 3, common, (PlayerStat.DashCooldown, -0.15f)));
            list.Add(Stat("SteadyHands", "Steady Hands", "Parry and perfect-parry windows 25% longer.", UpgradeRarity.Common, 3, common, (PlayerStat.ParryWindow, 0.25f)));
            list.Add(Stat("QuickGuard", "Quick Guard", "Parry cooldown 20% shorter.", UpgradeRarity.Common, 3, common, (PlayerStat.ParryCooldown, -0.2f)));
            list.Add(Stat("SplitShot", "Split Shot", "+1 projectile per shot.", UpgradeRarity.Rare, 2, rare, (PlayerStat.ExtraProjectiles, 1)));
            list.Add(Stat("PiercingRounds", "Piercing Rounds", "Shots pass through 1 more enemy.", UpgradeRarity.Rare, 2, rare, (PlayerStat.Pierce, 1)));
            list.Add(Stat("MirrorPolish", "Mirror Polish", "Reflected shots deal +50% damage.", UpgradeRarity.Rare, 3, rare, (PlayerStat.ReflectDamage, 0.5f)));
            list.Add(Stat("CrushingCounter", "Crushing Counter", "Parried enemies take double damage and stay stunned 50% longer.", UpgradeRarity.Rare, 2, rare,
                (PlayerStat.CounterDamage, 1f), (PlayerStat.StunDuration, 0.5f)));
            list.Add(Stat("Vitality", "Vitality", "+3 max health.", UpgradeRarity.Rare, 1, rare, (PlayerStat.MaxHealth, 3)));
            list.Add(Stat("GlassCannon", "Glass Cannon", "+75% damage, but -2 max health.", UpgradeRarity.Legendary, 1, legendary,
                (PlayerStat.Damage, 0.75f), (PlayerStat.MaxHealth, -2)));
            list.Add(Stat("TwinFang", "Twin Fang", "+2 projectiles per shot, but -15% damage.", UpgradeRarity.Legendary, 1, legendary,
                (PlayerStat.ExtraProjectiles, 2), (PlayerStat.Damage, -0.15f)));

            // Behaviour upgrades.
            list.Add(Make<VampiricParryUpgrade>("VampiricParry", "Vampiric Parry", "Perfect parries heal 1 health (4s cooldown).", UpgradeRarity.Rare, 1, rare));
            list.Add(Make<SecondWindUpgrade>("SecondWind", "Second Wind", "Once per stage, survive a lethal hit with 1 health.", UpgradeRarity.Legendary, 1, legendary));
            list.Add(Make<DashNovaUpgrade>("DashNova", "Dash Nova", "Dashing fires a ring of 8 shots when you leave and when you land.", UpgradeRarity.Rare, 1, rare));
            list.Add(Make<ChainReactionUpgrade>("ChainReaction", "Chain Reaction", "Killed enemies have a 30% chance to burst into 6 shots.", UpgradeRarity.Rare, 1, rare));
            list.Add(Make<MomentumUpgrade>("Momentum", "Momentum", "Perfect parries give +60% fire rate and +25% speed for 3s.", UpgradeRarity.Rare, 1, rare));
            list.Add(Make<BloodrushUpgrade>("Bloodrush", "Bloodrush", "Each kill takes 0.3s off your dash cooldown.", UpgradeRarity.Common, 1, common));
            list.Add(Make<RetaliationUpgrade>("Retaliation", "Retaliation", "When you're hit, blast 12 double-damage shots around you.", UpgradeRarity.Common, 1, common));
            list.Add(Make<RegenerationUpgrade>("Regeneration", "Regeneration", "Heal 1 health every 20 seconds.", UpgradeRarity.Common, 1, common));

            if (hazard != null)
            {
                list.Add(Make<StaticTrailUpgrade>("StaticTrail", "Static Trail", "Dashing leaves a trail of damaging zones.", UpgradeRarity.Rare, 1, rare,
                    so => so.FindProperty("hazardPrefab").objectReferenceValue = hazard));
            }

            list.RemoveAll(u => u == null);
            return list;
        }

        private static void AddExisting(List<UpgradeDefinition> list, string file, UpgradeRarity rarity)
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>($"{UpgradeFolder}/{file}.asset");
            if (upgrade == null)
            {
                return;
            }

            var so = new SerializedObject(upgrade);
            so.FindProperty("rarity").enumValueIndex = (int)rarity;
            so.ApplyModifiedPropertiesWithoutUndo();
            list.Add(upgrade);
        }

        private static StatUpgrade Stat(string file, string title, string description, UpgradeRarity rarity, int stacks, Color color,
            params (PlayerStat stat, float amount)[] modifiers)
        {
            return Make<StatUpgrade>(file, title, description, rarity, stacks, color, so =>
            {
                SerializedProperty array = so.FindProperty("modifiers");
                array.arraySize = modifiers.Length;
                for (int i = 0; i < modifiers.Length; i++)
                {
                    SerializedProperty element = array.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("stat").enumValueIndex = (int)modifiers[i].stat;
                    element.FindPropertyRelative("amount").floatValue = modifiers[i].amount;
                }
            });
        }

        internal static T Make<T>(string file, string title, string description, UpgradeRarity rarity, int stacks, Color color,
            Action<SerializedObject> configure = null) where T : UpgradeDefinition
        {
            string path = $"{UpgradeFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            var so = new SerializedObject(asset);
            so.FindProperty("title").stringValue = title;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("accentColor").colorValue = color;
            so.FindProperty("rarity").enumValueIndex = (int)rarity;
            so.FindProperty("maxStacks").intValue = stacks;
            configure?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        internal static void FillUpgradePool(List<UpgradeDefinition> upgrades)
        {
            string path = $"{UpgradeFolder}/UpgradePool.asset";
            var pool = AssetDatabase.LoadAssetAtPath<UpgradePool>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<UpgradePool>();
                AssetDatabase.CreateAsset(pool, path);
            }

            var so = new SerializedObject(pool);
            SerializedProperty array = so.FindProperty("upgrades");
            var all = new List<Object>();
            for (int i = 0; i < array.arraySize; i++)
            {
                Object item = array.GetArrayElementAtIndex(i).objectReferenceValue;
                if (item != null && !all.Contains(item))
                {
                    all.Add(item);
                }
            }

            foreach (UpgradeDefinition upgrade in upgrades)
            {
                if (!all.Contains(upgrade))
                {
                    all.Add(upgrade);
                }
            }

            array.arraySize = all.Count;
            for (int i = 0; i < all.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ bosses

        private struct PatternSpec
        {
            public BossPatternType Type;
            public float Telegraph;
            public int Repetitions;
            public float Interval;
            public int Count;
            public float Spread;
            public float Rotation;
            public float GapAngle;
            public int GapSize;
            public float ChargeSpeed;
            public float ChargeDuration;
            public EnemyController Summon;
        }

        private struct PhaseSpec
        {
            public string Name;
            public float Threshold;
            public float Cooldown;
            public float Speed;
            public float Telegraph;
            public PatternSpec[] Patterns;
        }

        private sealed class BossSpec
        {
            public string File;
            public string Name;
            public int Health;
            public float Scale;
            public float Speed;
            public float Posture;
            public float Stagger;
            public Color Tint;
            public PhaseSpec[] Phases;
        }

        private static PatternSpec Aimed(int count, float spread, int volleys, float interval, float telegraph = 0.6f) => new PatternSpec
            { Type = BossPatternType.AimedSpread, Count = count, Spread = spread, Repetitions = volleys, Interval = interval, Telegraph = telegraph };

        private static PatternSpec Radial(int count, float rotation, int volleys, float interval, float gapAngle = 0f, float telegraph = 0.7f) => new PatternSpec
            { Type = BossPatternType.Radial, Count = count, Rotation = rotation, Repetitions = volleys, Interval = interval, GapAngle = gapAngle, Telegraph = telegraph };

        private static PatternSpec Wall(int count, int gap, int volleys, float interval, float telegraph = 0.7f) => new PatternSpec
            { Type = BossPatternType.Wall, Count = count, GapSize = gap, Repetitions = volleys, Interval = interval, Telegraph = telegraph };

        private static PatternSpec Charge(float speed, float duration, int charges, float pause, float telegraph = 0.7f) => new PatternSpec
            { Type = BossPatternType.Charge, ChargeSpeed = speed, ChargeDuration = duration, Repetitions = charges, Interval = pause, Telegraph = telegraph };

        private static PatternSpec Summon(EnemyController prefab, int count, float telegraph = 0.8f) => new PatternSpec
            { Type = BossPatternType.Summon, Summon = prefab, Count = count, Repetitions = 1, Interval = 0.5f, Telegraph = telegraph };

        private static PhaseSpec Phase(string name, float threshold, float cooldown, float speed, float telegraph, params PatternSpec[] patterns) => new PhaseSpec
            { Name = name, Threshold = threshold, Cooldown = cooldown, Speed = speed, Telegraph = telegraph, Patterns = patterns };

        private static BossSpec[] BossSpecs()
        {
            return new[]
            {
                new BossSpec
                {
                    File = "Boss_Warden", Name = "The Warden", Health = 260, Scale = 2.0f, Speed = 2.8f, Posture = 100f, Stagger = 3f,
                    Tint = new Color(0.95f, 0.55f, 0.3f),
                    Phases = new[]
                    {
                        Phase("Guard", 1f, 1.2f, 1f, 1f,
                            Aimed(5, 40f, 3, 0.4f),
                            Charge(15f, 0.45f, 2, 0.45f),
                            Radial(14, 0f, 2, 0.45f, 60f)),
                        Phase("Enraged", 0.6f, 0.9f, 1.25f, 0.85f,
                            Aimed(7, 60f, 4, 0.3f),
                            Charge(17f, 0.4f, 3, 0.4f),
                            Wall(13, 3, 3, 0.55f),
                            Summon(grunt, 3)),
                        Phase("Last Stand", 0.25f, 0.6f, 1.4f, 0.7f,
                            Radial(18, 12f, 4, 0.3f, 50f),
                            Charge(19f, 0.4f, 4, 0.3f),
                            Wall(15, 3, 4, 0.45f),
                            Aimed(9, 90f, 4, 0.25f)),
                    },
                },
                new BossSpec
                {
                    File = "Boss_HiveMother", Name = "Hive Mother", Health = 420, Scale = 2.2f, Speed = 2.4f, Posture = 110f, Stagger = 3f,
                    Tint = new Color(0.55f, 0.9f, 0.35f),
                    Phases = new[]
                    {
                        Phase("Brood", 1f, 1.1f, 1f, 1f,
                            Summon(dasher, 3),
                            Radial(12, 15f, 4, 0.35f),
                            Aimed(3, 20f, 6, 0.22f)),
                        Phase("Swarm", 0.6f, 0.85f, 1.2f, 0.85f,
                            Summon(grunt, 4),
                            Summon(splitter, 2),
                            Radial(8, 22f, 12, 0.11f),
                            Wall(13, 3, 3, 0.5f)),
                        Phase("Frenzy", 0.25f, 0.5f, 1.3f, 0.7f,
                            Radial(10, 18f, 20, 0.08f),
                            Summon(dasher, 5),
                            Aimed(11, 110f, 4, 0.25f),
                            Wall(17, 3, 4, 0.4f)),
                    },
                },
                new BossSpec
                {
                    File = "Boss_Juggernaut", Name = "The Juggernaut", Health = 650, Scale = 2.4f, Speed = 3.2f, Posture = 130f, Stagger = 2.8f,
                    Tint = new Color(0.7f, 0.7f, 0.8f),
                    Phases = new[]
                    {
                        Phase("Steel", 1f, 1f, 1f, 1f,
                            Charge(18f, 0.5f, 3, 0.45f),
                            Radial(16, 0f, 2, 0.5f, 70f),
                            Aimed(5, 30f, 4, 0.3f)),
                        Phase("Overheat", 0.6f, 0.75f, 1.2f, 0.85f,
                            Charge(21f, 0.45f, 4, 0.35f),
                            Radial(20, 9f, 4, 0.25f),
                            Summon(brute, 2),
                            Wall(15, 3, 3, 0.45f)),
                        Phase("Meltdown", 0.25f, 0.55f, 1.4f, 0.7f,
                            Radial(6, 25f, 24, 0.07f),
                            Charge(23f, 0.4f, 5, 0.28f),
                            Aimed(11, 120f, 4, 0.28f),
                            Wall(19, 3, 5, 0.35f)),
                    },
                },
                new BossSpec
                {
                    File = "Boss_Sovereign", Name = "The Sovereign", Health = 1200, Scale = 2.7f, Speed = 3.4f, Posture = 150f, Stagger = 2.5f,
                    Tint = new Color(0.75f, 0.35f, 1f),
                    Phases = new[]
                    {
                        Phase("Regal", 1f, 0.95f, 1f, 1f,
                            Aimed(7, 50f, 4, 0.3f),
                            Radial(16, 12f, 3, 0.35f, 50f),
                            Charge(18f, 0.45f, 2, 0.4f),
                            Wall(13, 3, 3, 0.5f)),
                        Phase("Wrath", 0.7f, 0.75f, 1.2f, 0.85f,
                            Radial(8, 20f, 16, 0.1f),
                            Summon(dasher, 4),
                            Aimed(9, 80f, 5, 0.25f),
                            Charge(20f, 0.4f, 3, 0.35f),
                            Wall(15, 3, 4, 0.42f)),
                        Phase("Ascended", 0.4f, 0.55f, 1.35f, 0.72f,
                            Radial(10, 17f, 24, 0.08f),
                            Radial(24, 7f, 5, 0.22f, 40f),
                            Charge(24f, 0.38f, 5, 0.28f),
                            Summon(splitter, 3),
                            Wall(17, 2, 5, 0.38f)),
                        Phase("Apotheosis", 0.15f, 0.35f, 1.5f, 0.6f,
                            Radial(10, 13f, 30, 0.09f),
                            Wall(21, 2, 6, 0.3f),
                            Aimed(15, 160f, 5, 0.2f),
                            Charge(26f, 0.35f, 6, 0.25f)),
                    },
                },
            };
        }

        private static EnemyController[] CreateBosses()
        {
            BossSpec[] specs = BossSpecs();
            var bosses = new EnemyController[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                bosses[i] = CreateOrUpdateBoss(specs[i], overwrite: false);
            }

            return bosses;
        }

        /// <summary>Re-applies the boss designs above to the boss prefabs, overwriting their stats and patterns.</summary>
        [MenuItem("Tools/Roguelite/Rebalance Bosses (overwrite boss prefabs)")]
        public static void RebalanceBosses()
        {
            if (!LoadPrefabs())
            {
                return;
            }

            EnsureFolder(BossFolder);
            WidenSplitterShotPool();
            foreach (BossSpec spec in BossSpecs())
            {
                CreateOrUpdateBoss(spec, overwrite: true);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[RunContentGenerator] Bosses rebalanced: more health, armour + posture/stagger, extra phases and patterns.");
        }

        private static EnemyController CreateOrUpdateBoss(BossSpec spec, bool overwrite)
        {
            string path = $"{BossFolder}/{spec.File}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null && !overwrite)
            {
                return existing.GetComponent<EnemyController>();
            }

            if (existing != null)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ConfigureBoss(root, spec);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyController>();
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(brute.gameObject);
            try
            {
                instance.name = spec.File;
                ConfigureBoss(instance, spec);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return saved != null ? saved.GetComponent<EnemyController>() : null;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ConfigureBoss(GameObject root, BossSpec spec)
        {
            root.transform.localScale = Vector3.one * spec.Scale;

            var healthSo = new SerializedObject(root.GetComponent<Health>());
            healthSo.FindProperty("maxHealth").intValue = spec.Health;
            healthSo.FindProperty("invulnerabilityDuration").floatValue = 0f;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var enemySo = new SerializedObject(root.GetComponent<EnemyController>());
            enemySo.FindProperty("scoreValue").intValue = 500;
            enemySo.FindProperty("lungeDamage").intValue = 2;
            enemySo.FindProperty("parriedStunDuration").floatValue = 0.6f;
            enemySo.FindProperty("parriedKnockback").floatValue = 3f;
            enemySo.FindProperty("parriedDamage").intValue = 8;
            enemySo.FindProperty("chase.maxSpeed").floatValue = spec.Speed;
            enemySo.FindProperty("chase.attackRange").floatValue = 30f;
            enemySo.FindProperty("chase.preferredDistance").floatValue = 5f;
            enemySo.FindProperty("chase.distanceTolerance").floatValue = 2f;
            enemySo.ApplyModifiedPropertiesWithoutUndo();

            var sprite = root.GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
            {
                var spriteSo = new SerializedObject(sprite);
                spriteSo.FindProperty("m_Color").colorValue = spec.Tint;
                spriteSo.ApplyModifiedPropertiesWithoutUndo();
            }

            BossController boss = root.GetComponent<BossController>();
            if (boss == null)
            {
                boss = root.AddComponent<BossController>();
            }

            var bossSo = new SerializedObject(boss);
            bossSo.FindProperty("bossName").stringValue = spec.Name;
            bossSo.FindProperty("defaultProjectile").objectReferenceValue = enemyShot;
            bossSo.FindProperty("projectilePoolCapacity").intValue = SharedEnemyShotCapacity;
            bossSo.FindProperty("armoredDamageMultiplier").floatValue = 0.5f;
            bossSo.FindProperty("staggeredDamageMultiplier").floatValue = 2.5f;
            bossSo.FindProperty("maxPosture").floatValue = spec.Posture;
            bossSo.FindProperty("posturePerParry").floatValue = 25f;
            bossSo.FindProperty("staggerDuration").floatValue = spec.Stagger;
            WritePhases(bossSo.FindProperty("phases"), spec.Phases);
            bossSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WritePhases(SerializedProperty phasesProperty, PhaseSpec[] phases)
        {
            phasesProperty.arraySize = phases.Length;
            for (int p = 0; p < phases.Length; p++)
            {
                PhaseSpec phase = phases[p];
                SerializedProperty element = phasesProperty.GetArrayElementAtIndex(p);
                element.FindPropertyRelative("phaseName").stringValue = phase.Name;
                element.FindPropertyRelative("healthThreshold").floatValue = phase.Threshold;
                element.FindPropertyRelative("patternCooldown").floatValue = phase.Cooldown;
                element.FindPropertyRelative("speedMultiplier").floatValue = phase.Speed;
                element.FindPropertyRelative("telegraphMultiplier").floatValue = phase.Telegraph;

                SerializedProperty patterns = element.FindPropertyRelative("patterns");
                patterns.arraySize = phase.Patterns.Length;
                for (int i = 0; i < phase.Patterns.Length; i++)
                {
                    PatternSpec spec = phase.Patterns[i];
                    SerializedProperty pattern = patterns.GetArrayElementAtIndex(i);
                    pattern.FindPropertyRelative("type").enumValueIndex = (int)spec.Type;
                    pattern.FindPropertyRelative("telegraphDuration").floatValue = spec.Telegraph;
                    pattern.FindPropertyRelative("repetitions").intValue = Mathf.Max(1, spec.Repetitions);
                    pattern.FindPropertyRelative("interval").floatValue = Mathf.Max(0.02f, spec.Interval);
                    pattern.FindPropertyRelative("projectilePrefab").objectReferenceValue = null;
                    pattern.FindPropertyRelative("projectileCount").intValue = Mathf.Max(1, spec.Count);
                    pattern.FindPropertyRelative("spreadAngle").floatValue = spec.Spread;
                    pattern.FindPropertyRelative("rotationStep").floatValue = spec.Rotation;
                    pattern.FindPropertyRelative("trackTarget").boolValue = true;
                    pattern.FindPropertyRelative("gapAngle").floatValue = spec.GapAngle;
                    pattern.FindPropertyRelative("wallSpacing").floatValue = 0.7f;
                    pattern.FindPropertyRelative("gapSize").intValue = spec.GapSize;
                    pattern.FindPropertyRelative("chargeSpeed").floatValue = spec.ChargeSpeed > 0f ? spec.ChargeSpeed : 14f;
                    pattern.FindPropertyRelative("chargeDuration").floatValue = spec.ChargeDuration > 0f ? spec.ChargeDuration : 0.45f;
                    pattern.FindPropertyRelative("summonPrefab").objectReferenceValue = spec.Summon;
                    pattern.FindPropertyRelative("summonCount").intValue = Mathf.Max(1, spec.Count);
                    pattern.FindPropertyRelative("summonRadius").floatValue = 2.5f;
                }
            }
        }

        // ------------------------------------------------------------------ stages

        private static List<StageData> CreateStages(EnemyController[] bosses)
        {
            var stages = new List<StageData>();
            string[] bossTitles = { "The Warden", "Hive Mother", "The Juggernaut" };

            for (int act = 1; act <= 3; act++)
            {
                for (int s = 1; s <= 5; s++)
                {
                    stages.Add(CreateNormalStage(act, s));
                }

                stages.Add(CreateBossStage($"A{act}_Boss", act, 6, StageKind.Boss, bossTitles[act - 1], bosses[act - 1],
                    new EnemyModifiers(1f, 1f, 1f + (act - 1) * 0.5f)));
            }

            stages.Add(CreateBossStage("Final_Boss", 3, 7, StageKind.FinalBoss, "The Sovereign", bosses[3], new EnemyModifiers(1f, 1f, 2f)));
            stages.RemoveAll(st => st == null);
            return stages;
        }

        private static StageData CreateNormalStage(int act, int stageInAct)
        {
            string path = $"{StageFolder}/A{act}_S{stageInAct}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (existing != null)
            {
                return existing;
            }

            // Difficulty 1..15 across the three acts.
            int d = (act - 1) * 5 + stageInAct;

            var roster = new List<EnemyController> { grunt, grunt };
            if (act >= 2 || stageInAct >= 3)
            {
                roster.Add(dasher);
            }

            if (act >= 2 || stageInAct >= 5)
            {
                roster.Add(splitter);
            }

            if (act >= 3 || (act == 2 && stageInAct >= 3))
            {
                roster.Add(brute);
            }

            if (act >= 3)
            {
                roster.Add(dasher);
                roster.Add(splitter);
            }

            int waveCount = 2 + (stageInAct >= 3 ? 1 : 0) + (act >= 2 ? 1 : 0);

            var stage = ScriptableObject.CreateInstance<StageData>();
            AssetDatabase.CreateAsset(stage, path);
            var so = new SerializedObject(stage);
            so.FindProperty("stageNumber").intValue = stageInAct;
            so.FindProperty("stageTitle").stringValue = StageTitles[act - 1][stageInAct - 1];
            so.FindProperty("kind").enumValueIndex = (int)StageKind.Normal;
            so.FindProperty("act").intValue = act;
            so.FindProperty("timeBetweenWaves").floatValue = Mathf.Max(2f, 3f - d * 0.05f);
            WriteModifiers(so.FindProperty("enemyModifiers"), new EnemyModifiers(1f + 0.12f * (d - 1), 1f + 0.02f * (d - 1), 1f + (act - 1) * 0.5f));

            SerializedProperty waves = so.FindProperty("waves");
            waves.arraySize = waveCount;
            for (int w = 0; w < waveCount; w++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(w);
                wave.FindPropertyRelative("spawnInterval").floatValue = Mathf.Max(0.3f, 1.1f - d * 0.045f - w * 0.05f);
                wave.FindPropertyRelative("enemyCount").intValue = 5 + d + w * 2;

                SerializedProperty prefabs = wave.FindPropertyRelative("enemyPrefabs");
                // Early waves lean on the basic enemy; later waves use the whole roster.
                int rosterSize = w == 0 ? Mathf.Min(roster.Count, 3) : roster.Count;
                prefabs.arraySize = rosterSize;
                for (int i = 0; i < rosterSize; i++)
                {
                    prefabs.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        private static StageData CreateBossStage(string file, int act, int number, StageKind kind, string title, EnemyController boss, EnemyModifiers modifiers)
        {
            string path = $"{StageFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (existing != null)
            {
                return existing;
            }

            var stage = ScriptableObject.CreateInstance<StageData>();
            AssetDatabase.CreateAsset(stage, path);
            var so = new SerializedObject(stage);
            so.FindProperty("stageNumber").intValue = number;
            so.FindProperty("stageTitle").stringValue = title;
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("act").intValue = act;
            so.FindProperty("timeBetweenWaves").floatValue = 2f;
            so.FindProperty("bossPrefab").objectReferenceValue = boss;
            WriteModifiers(so.FindProperty("bossModifiers"), modifiers);
            WriteModifiers(so.FindProperty("enemyModifiers"), modifiers);
            so.FindProperty("waves").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return stage;
        }

        private static void WriteModifiers(SerializedProperty property, EnemyModifiers modifiers)
        {
            property.FindPropertyRelative("healthMultiplier").floatValue = modifiers.HealthMultiplier;
            property.FindPropertyRelative("speedMultiplier").floatValue = modifiers.SpeedMultiplier;
            property.FindPropertyRelative("damageMultiplier").floatValue = modifiers.DamageMultiplier;
        }

        private static void FillCatalog(List<StageData> stages)
        {
            string[] guids = AssetDatabase.FindAssets("t:StageCatalog");
            StageCatalog catalog = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<StageCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StageCatalog>();
                AssetDatabase.CreateAsset(catalog, $"{StageFolder}/StageCatalog.asset");
            }

            var so = new SerializedObject(catalog);
            SerializedProperty array = so.FindProperty("stages");
            array.arraySize = stages.Count;
            for (int i = 0; i < stages.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[RunContentGenerator] Stage Catalog '{AssetDatabase.GetAssetPath(catalog)}' now lists {stages.Count} stages.", catalog);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}

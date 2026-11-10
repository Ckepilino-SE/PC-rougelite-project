using System.Collections.Generic;
using Core.Events;
using Core.References;
using Gameplay.Combat;
using Gameplay.Enemies;
using Gameplay.Player;
using Gameplay.Stages;
using Gameplay.Tempo;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// <b>Tools &gt; Roguelite &gt; Set Up Tempo, Elites &amp; Lean Waves</b> (run with the Main scene open):
    /// <list type="bullet">
    /// <item>New parry-fodder shooters: Gunner and Sprayer (variants of Splitter).</item>
    /// <item>Elite variants (Elite_Gunner, Elite_Brute, Elite_Dasher) with armour and a posture bar.</item>
    /// <item>Rewrites every normal stage's waves: fewer enemies, mostly shooters, elites from Act 1 Stage 4.</item>
    /// <item>Adds TempoMeter to the player, and the Tempo meter, Score and final-score texts to the HUD.</item>
    /// </list>
    /// New prefabs are only created if missing; stage waves are always rewritten.
    /// </summary>
    public static class TempoEliteSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string EliteFolder = "Assets/Prefabs/Elites";
        private const string StageFolder = "Assets/Game/Stages/Run";

        private static Sprite uiSprite;

        [MenuItem("Tools/Roguelite/Set Up Tempo, Elites & Lean Waves")]
        public static void SetUp()
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            EnemyController grunt = Load("Grunt");
            EnemyController dasher = Load("Dasher");
            EnemyController brute = Load("Brute");
            EnemyController splitter = Load("Splitter");
            if (grunt == null || dasher == null || brute == null || splitter == null)
            {
                Debug.LogError("[TempoEliteSetup] Needs Grunt, Dasher, Brute and Splitter prefabs in Assets/Prefabs.");
                return;
            }

            EnsureFolder(EliteFolder);

            // Parryable shooters: the main source of bullets to deflect.
            EnemyController gunner = Variant(splitter, $"{PrefabFolder}/Gunner.prefab", root =>
            {
                var so = new SerializedObject(root.GetComponent<EnemyController>());
                so.FindProperty("ranged.projectilesPerVolley").intValue = 3;
                so.FindProperty("ranged.spreadAngle").floatValue = 24f;
                so.FindProperty("ranged.telegraphDuration").floatValue = 0.6f;
                so.FindProperty("ranged.cooldown").floatValue = 1.8f;
                so.FindProperty("chase.attackRange").floatValue = 9f;
                so.FindProperty("chase.preferredDistance").floatValue = 6.5f;
                so.FindProperty("scoreValue").intValue = 20;
                so.ApplyModifiedPropertiesWithoutUndo();
                Tint(root, new Color(0.45f, 0.8f, 1f));
            });

            EnemyController sprayer = Variant(splitter, $"{PrefabFolder}/Sprayer.prefab", root =>
            {
                var so = new SerializedObject(root.GetComponent<EnemyController>());
                so.FindProperty("ranged.projectilesPerVolley").intValue = 7;
                so.FindProperty("ranged.spreadAngle").floatValue = 80f;
                so.FindProperty("ranged.telegraphDuration").floatValue = 0.9f;
                so.FindProperty("ranged.cooldown").floatValue = 2.6f;
                so.FindProperty("chase.attackRange").floatValue = 8f;
                so.FindProperty("chase.preferredDistance").floatValue = 5f;
                so.FindProperty("scoreValue").intValue = 30;
                var fury = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Game/Powers/Power_Fury.asset");
                if (fury != null)
                {
                    so.FindProperty("stealablePower").objectReferenceValue = fury;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                Tint(root, new Color(1f, 0.5f, 0.85f));
            });

            EnemyController eliteGunner = Elite(gunner, "Elite_Gunner", 4f, 45f);
            EnemyController eliteBrute = Elite(brute, "Elite_Brute", 4f, 50f);
            EnemyController eliteDasher = Elite(dasher, "Elite_Dasher", 4f, 40f);

            int rewritten = RewriteStages(grunt, dasher, brute, splitter, gunner, sprayer, eliteGunner, eliteBrute, eliteDasher);
            bool hud = SetUpSceneObjects();

            AssetDatabase.SaveAssets();
            Debug.Log($"[TempoEliteSetup] Done: Gunner, Sprayer, 3 elites, {rewritten} stages rewritten" +
                      (hud ? ", tempo + score HUD added. Save the scene (Ctrl+S)." : ". Open the Main scene and run again to add the HUD."));
        }

        // ------------------------------------------------------------------ prefabs

        private static EnemyController Load(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
            return go != null ? go.GetComponent<EnemyController>() : null;
        }

        /// <summary>Creates a prefab variant of <paramref name="baseEnemy"/> at <paramref name="path"/> if it doesn't exist.</summary>
        private static EnemyController Variant(EnemyController baseEnemy, string path, System.Action<GameObject> configure)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing.GetComponent<EnemyController>();
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(baseEnemy.gameObject);
            try
            {
                instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
                configure(instance);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                return saved != null ? saved.GetComponent<EnemyController>() : null;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static EnemyController Elite(EnemyController baseEnemy, string name, float healthMultiplier, float posture)
        {
            if (baseEnemy == null)
            {
                return null;
            }

            return Variant(baseEnemy, $"{EliteFolder}/{name}.prefab", root =>
            {
                float scale = root.transform.localScale.x * 1.35f;
                root.transform.localScale = Vector3.one * scale;

                var health = new SerializedObject(root.GetComponent<Health>());
                SerializedProperty max = health.FindProperty("maxHealth");
                max.intValue = Mathf.Max(4, Mathf.RoundToInt(max.intValue * healthMultiplier));
                health.ApplyModifiedPropertiesWithoutUndo();

                var enemy = new SerializedObject(root.GetComponent<EnemyController>());
                SerializedProperty score = enemy.FindProperty("scoreValue");
                score.intValue = Mathf.Max(50, score.intValue * 5);
                enemy.FindProperty("parriedStunDuration").floatValue = 0.8f;
                enemy.ApplyModifiedPropertiesWithoutUndo();

                var sprite = root.GetComponentInChildren<SpriteRenderer>();
                if (sprite != null)
                {
                    Tint(root, Color.Lerp(sprite.color, new Color(1f, 0.8f, 0.2f), 0.55f));
                }

                // Posture bar above the elite, sized in world units regardless of the elite's scale.
                SpriteRenderer background = BarPart(root.transform, "PostureBarBg", new Color(0f, 0f, 0f, 0.75f), scale, 1.3f, 0.18f, 50);
                SpriteRenderer fill = BarPart(root.transform, "PostureBarFill", new Color(1f, 0.85f, 0.2f), scale, 1.2f, 0.1f, 51);

                EliteController elite = root.AddComponent<EliteController>();
                var eliteSo = new SerializedObject(elite);
                eliteSo.FindProperty("maxPosture").floatValue = posture;
                eliteSo.FindProperty("barBackground").objectReferenceValue = background;
                eliteSo.FindProperty("barFill").objectReferenceValue = fill;
                eliteSo.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static SpriteRenderer BarPart(Transform parent, string name, Color color, float parentScale, float width, float height, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            float spriteSize = uiSprite != null ? uiSprite.bounds.size.x : 1f;
            go.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            go.transform.localScale = new Vector3(width / spriteSize / parentScale, height / spriteSize / parentScale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = uiSprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static void Tint(GameObject root, Color color)
        {
            var sprite = root.GetComponentInChildren<SpriteRenderer>();
            if (sprite == null)
            {
                return;
            }

            var so = new SerializedObject(sprite);
            so.FindProperty("m_Color").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ stages

        private static int RewriteStages(EnemyController grunt, EnemyController dasher, EnemyController brute, EnemyController splitter,
            EnemyController gunner, EnemyController sprayer, EnemyController eliteGunner, EnemyController eliteBrute, EnemyController eliteDasher)
        {
            int count = 0;
            for (int act = 1; act <= 3; act++)
            {
                for (int s = 1; s <= 5; s++)
                {
                    var stage = AssetDatabase.LoadAssetAtPath<StageData>($"{StageFolder}/A{act}_S{s}.asset");
                    if (stage == null)
                    {
                        continue;
                    }

                    int d = (act - 1) * 5 + s;
                    var roster = new List<EnemyController>();
                    var elites = new List<EnemyController>();
                    int eliteCount = 0;

                    switch (act)
                    {
                        case 1:
                            roster.AddRange(new[] { grunt, gunner, gunner });
                            if (s >= 3)
                            {
                                roster.Add(dasher);
                            }

                            if (s >= 4)
                            {
                                elites.Add(eliteGunner);
                                eliteCount = 1;
                            }

                            break;
                        case 2:
                            roster.AddRange(new[] { grunt, gunner, gunner, sprayer, dasher });
                            if (s >= 3)
                            {
                                roster.Add(brute);
                            }

                            elites.AddRange(new[] { eliteGunner, eliteBrute });
                            eliteCount = s >= 5 ? 2 : 1;
                            break;
                        default:
                            roster.AddRange(new[] { gunner, gunner, sprayer, sprayer, dasher, brute, splitter });
                            elites.AddRange(new[] { eliteGunner, eliteBrute, eliteDasher });
                            eliteCount = s >= 3 ? 2 : 1;
                            break;
                    }

                    roster.RemoveAll(e => e == null);
                    elites.RemoveAll(e => e == null);

                    int normalWaves = 2 + (s >= 4 ? 1 : 0);
                    bool hasEliteWave = eliteCount > 0 && elites.Count > 0;

                    var so = new SerializedObject(stage);
                    so.FindProperty("timeBetweenWaves").floatValue = 3f;
                    SerializedProperty modifiers = so.FindProperty("enemyModifiers");
                    modifiers.FindPropertyRelative("healthMultiplier").floatValue = 1.2f + 0.12f * (d - 1);
                    modifiers.FindPropertyRelative("speedMultiplier").floatValue = 1f + 0.02f * (d - 1);
                    modifiers.FindPropertyRelative("damageMultiplier").floatValue = 1f + (act - 1) * 0.5f;

                    SerializedProperty waves = so.FindProperty("waves");
                    waves.arraySize = normalWaves + (hasEliteWave ? 1 : 0);
                    for (int w = 0; w < normalWaves; w++)
                    {
                        // Wave 1 eases in with the first three roster slots.
                        int size = w == 0 ? Mathf.Min(3, roster.Count) : roster.Count;
                        WriteWave(waves.GetArrayElementAtIndex(w), roster, size, 3 + d / 3 + w, Mathf.Max(0.8f, 1.6f - d * 0.03f));
                    }

                    if (hasEliteWave)
                    {
                        WriteWave(waves.GetArrayElementAtIndex(normalWaves), elites, elites.Count, eliteCount, 2.5f);
                    }

                    so.ApplyModifiedPropertiesWithoutUndo();
                    count++;
                }
            }

            return count;
        }

        private static void WriteWave(SerializedProperty wave, List<EnemyController> prefabs, int size, int enemyCount, float interval)
        {
            wave.FindPropertyRelative("spawnInterval").floatValue = interval;
            wave.FindPropertyRelative("enemyCount").intValue = Mathf.Max(1, enemyCount);
            SerializedProperty list = wave.FindPropertyRelative("enemyPrefabs");
            list.arraySize = size;
            for (int i = 0; i < size; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
        }

        // ------------------------------------------------------------------ scene

        private static bool SetUpSceneObjects()
        {
            Canvas canvas = null;
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.isRootCanvas)
                {
                    canvas = c;
                    break;
                }
            }

            PlayerParry parry = Object.FindFirstObjectByType<PlayerParry>(FindObjectsInactive.Include);
            if (canvas == null || parry == null)
            {
                return false;
            }

            if (parry.GetComponent<TempoMeter>() == null)
            {
                Undo.AddComponent<TempoMeter>(parry.gameObject);
            }

            var anchor = Find<TransformAnchorSO>("PlayerAnchor");
            Transform root = canvas.transform;

            if (root.Find("TempoMeter") == null)
            {
                BuildTempoUI(root, anchor);
            }

            if (root.Find("Score") == null)
            {
                BuildScore(root, anchor, canvas);
            }

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            return true;
        }

        private static void BuildTempoUI(Transform canvas, TransformAnchorSO anchor)
        {
            RectTransform panel = Rect("TempoMeter", canvas);
            Place(panel, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(300f, 110f));

            TMP_Text label = Text("Label", panel, "TEMPO", 22f, TextAlignmentOptions.Right);
            Stretch(label.rectTransform, new Vector2(0f, 0.72f), Vector2.one);

            TMP_Text multiplier = Text("Multiplier", panel, "x1.0", 48f, TextAlignmentOptions.Right);
            multiplier.fontStyle = FontStyles.Bold;
            Stretch(multiplier.rectTransform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f));

            Image background = Image("Background", panel, new Color(0.08f, 0.08f, 0.1f, 0.9f), false);
            Stretch(background.rectTransform, Vector2.zero, new Vector2(1f, 0.2f));
            Image fill = Image("Fill", background.transform, new Color(0.4f, 0.75f, 1f), true);
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform, Vector2.zero, Vector2.one);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            TMP_Text bonus = Text("DoubleSteal", panel, "DOUBLE STEAL", 18f, TextAlignmentOptions.Left);
            bonus.color = new Color(1f, 0.8f, 0.2f);
            bonus.enabled = false;
            Stretch(bonus.rectTransform, new Vector2(0f, 0.72f), Vector2.one);

            var ui = panel.gameObject.AddComponent<TempoUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("playerAnchor").objectReferenceValue = anchor;
            so.FindProperty("multiplierText").objectReferenceValue = multiplier;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("bonusText").objectReferenceValue = bonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Tempo UI");
        }

        private static void BuildScore(Transform canvas, TransformAnchorSO anchor, Canvas canvasComponent)
        {
            RectTransform panel = Rect("Score", canvas);
            Place(panel, new Vector2(1f, 1f), new Vector2(-30f, -20f), new Vector2(320f, 90f));

            TMP_Text label = Text("Label", panel, "SCORE", 22f, TextAlignmentOptions.Right);
            Stretch(label.rectTransform, new Vector2(0f, 0.6f), Vector2.one);
            TMP_Text value = Text("Value", panel, "0", 46f, TextAlignmentOptions.Right);
            value.fontStyle = FontStyles.Bold;
            Stretch(value.rectTransform, Vector2.zero, new Vector2(1f, 0.65f));

            // Final score line on the Game Over / Victory panel, if there is one.
            TMP_Text finalScore = null;
            Transform gameOver = canvas.Find("GameOverPanel");
            if (gameOver != null)
            {
                finalScore = Text("FinalScore", gameOver, "Score: 0", 36f, TextAlignmentOptions.Center);
                RectTransform rect = finalScore.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 20f);
                rect.sizeDelta = new Vector2(600f, 50f);
            }

            var keeper = panel.gameObject.AddComponent<ScoreKeeper>();
            var so = new SerializedObject(keeper);
            so.FindProperty("enemyKilledChannel").objectReferenceValue = Find<EnemyKilledEventChannelSO>("EnemyKilledChannel");
            so.FindProperty("stageStartedChannel").objectReferenceValue = Find<StageEventChannelSO>("StageStartedChannel");
            so.FindProperty("gameOverChannel").objectReferenceValue = Find<GameOverEventChannelSO>("GameOverChannel");
            so.FindProperty("catalog").objectReferenceValue = Find<StageCatalog>("StageCatalog");
            so.FindProperty("playerAnchor").objectReferenceValue = anchor;
            so.FindProperty("scoreText").objectReferenceValue = value;
            so.FindProperty("finalScoreText").objectReferenceValue = finalScore;
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Score UI");
        }

        // ------------------------------------------------------------------ helpers

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 corner, Vector2 position, Vector2 size)
        {
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var tmp = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static Image Image(string name, Transform parent, Color color, bool filled)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = UnityEngine.UI.Image.Type.Filled;
                image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                image.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            }
            else
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }

            return image;
        }

        private static T Find<T>(string preferredName) where T : Object
        {
            T fallback = null;
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                {
                    continue;
                }

                if (asset.name == preferredName)
                {
                    return asset;
                }

                fallback ??= asset;
            }

            return fallback;
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
            }
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using Core.References;
using Gameplay.Stages;
using Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// Builds the game's UI with every slot wired:
    /// <list type="bullet">
    /// <item><b>Tools &gt; Roguelite &gt; Build Missing Game UI</b> (in the Main scene): boss bar (with posture meter),
    /// stage banner, player HP bar and pause menu. Anything that already exists is skipped.</item>
    /// <item><b>Tools &gt; Roguelite &gt; Create Title Scene</b>: a Title scene with Start / Quit, added first in the build list.</item>
    /// </list>
    /// </summary>
    public static class RunUIBuilder
    {
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private static Sprite uiSprite;

        // ================================================================== Main scene

        [MenuItem("Tools/Roguelite/Build Missing Game UI")]
        public static void Build()
        {
            Canvas canvas = FindRootCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Run UI Builder", "Open the Main scene first (no Canvas found).", "OK");
                return;
            }

            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Transform root = canvas.transform;

            Transform bossBar = root.Find("BossHealthBar");
            if (bossBar == null)
            {
                BuildBossBar(root);
            }
            else
            {
                AddPostureMeter(bossBar.GetComponent<BossHealthBarUI>());
            }

            if (root.Find("StageBanner") == null)
            {
                BuildBanner(root, FindAsset<StageEventChannelSO>("StageStartedChannel"));
            }

            if (root.Find("PlayerHealthBar") == null)
            {
                BuildPlayerHealthBar(root, FindAsset<TransformAnchorSO>("PlayerAnchor"));
            }

            EnsurePowerThief(FindAsset<StageEventChannelSO>("StageStartedChannel"));
            if (root.Find("StolenPowers") == null)
            {
                BuildStolenPowers(root, FindAsset<TransformAnchorSO>("PlayerAnchor"));
            }

            if (root.Find("PauseMenu") == null)
            {
                BuildPauseMenu(root);
            }

            // The pause menu must draw over everything else.
            root.Find("PauseMenu")?.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("[RunUIBuilder] Done. Save the scene (Ctrl+S).", canvas);
        }

        private static void BuildBossBar(Transform canvas)
        {
            RectTransform bar = CreateRect("BossHealthBar", canvas);
            Anchor(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 70f));
            AddHiddenGroup(bar.gameObject);

            TMP_Text nameText = CreateText("BossName", bar, "Boss Name", 26f, TextAlignmentOptions.Left);
            SetRect(nameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), Vector2.zero);

            TMP_Text phaseText = CreateText("Phase", bar, "Phase 1", 20f, TextAlignmentOptions.Right);
            SetRect(phaseText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), Vector2.zero);

            Image background = CreateImage("Background", bar, new Color(0.1f, 0.1f, 0.12f, 0.9f), false);
            SetRect(background.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 28f));

            Image trail = CreateImage("DamageTrail", background.transform, new Color(1f, 0.95f, 0.8f, 0.9f), true);
            SetRect(trail.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            Image fill = CreateImage("Fill", background.transform, new Color(0.9f, 0.2f, 0.2f), true);
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            var ui = bar.gameObject.AddComponent<BossHealthBarUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("damageTrailImage").objectReferenceValue = trail;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("phaseText").objectReferenceValue = phaseText;
            so.ApplyModifiedPropertiesWithoutUndo();

            AddPostureMeter(ui);
            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create Boss Health Bar");
        }

        /// <summary>Adds the posture meter and "STAGGERED!" text under the boss bar, if not already there.</summary>
        private static void AddPostureMeter(BossHealthBarUI ui)
        {
            if (ui == null)
            {
                return;
            }

            var so = new SerializedObject(ui);
            if (so.FindProperty("postureFillImage").objectReferenceValue != null)
            {
                return;
            }

            Transform bar = ui.transform;
            Image background = CreateImage("PostureBackground", bar, new Color(0.1f, 0.1f, 0.12f, 0.9f), false);
            SetRect(background.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0f, -14f), new Vector2(0f, -4f));

            Image fill = CreateImage("PostureFill", background.transform, new Color(1f, 0.85f, 0.2f), true);
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            TMP_Text stagger = CreateText("StaggerText", bar, "STAGGERED!", 24f, TextAlignmentOptions.Center);
            stagger.color = new Color(1f, 0.85f, 0.2f);
            stagger.fontStyle = FontStyles.Bold;
            stagger.enabled = false;
            SetRect(stagger.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -46f), new Vector2(0f, -16f));

            so.FindProperty("postureFillImage").objectReferenceValue = fill;
            so.FindProperty("staggerText").objectReferenceValue = stagger;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ui);
        }

        private static void BuildBanner(Transform canvas, StageEventChannelSO stageStarted)
        {
            RectTransform banner = CreateRect("StageBanner", canvas);
            Anchor(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(900f, 160f));
            AddHiddenGroup(banner.gameObject);

            TMP_Text label = CreateText("Label", banner, "Act 1 - Stage 1", 64f, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            SetRect(label.rectTransform, new Vector2(0f, 0.45f), Vector2.one, Vector2.zero, Vector2.zero);

            TMP_Text title = CreateText("Title", banner, "Stage Title", 34f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, Vector2.zero, new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);

            var ui = banner.gameObject.AddComponent<StageBannerUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("stageStartedChannel").objectReferenceValue = stageStarted;
            so.FindProperty("labelText").objectReferenceValue = label;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(banner.gameObject, "Create Stage Banner");
        }

        private static void BuildPlayerHealthBar(Transform canvas, TransformAnchorSO playerAnchor)
        {
            RectTransform bar = CreateRect("PlayerHealthBar", canvas);
            Anchor(bar, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(420f, 42f));

            Image background = CreateImage("Background", bar, new Color(0.08f, 0.08f, 0.1f, 0.9f), false);
            SetRect(background.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            Image trail = CreateImage("DamageTrail", background.transform, new Color(1f, 1f, 1f, 0.8f), true);
            SetRect(trail.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            Image fill = CreateImage("Fill", background.transform, new Color(0.3f, 0.85f, 0.35f), true);
            SetRect(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

            TMP_Text value = CreateText("Value", bar, "5 / 5", 24f, TextAlignmentOptions.Center);
            value.fontStyle = FontStyles.Bold;
            SetRect(value.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var ui = bar.gameObject.AddComponent<PlayerHealthBarUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            so.FindProperty("fillImage").objectReferenceValue = fill;
            so.FindProperty("damageTrailImage").objectReferenceValue = trail;
            so.FindProperty("valueText").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (playerAnchor == null)
            {
                Debug.LogWarning("[RunUIBuilder] Couldn't find the PlayerAnchor asset; drag it into Player Health Bar UI.", ui);
            }

            Undo.RegisterCreatedObjectUndo(bar.gameObject, "Create Player Health Bar");
        }

        /// <summary>Adds a PowerThief to the player (the object with PlayerParry) if it has none.</summary>
        private static void EnsurePowerThief(StageEventChannelSO stageStarted)
        {
            var parry = Object.FindFirstObjectByType<Gameplay.Player.PlayerParry>(FindObjectsInactive.Include);
            if (parry == null)
            {
                Debug.LogWarning("[RunUIBuilder] No player (PlayerParry) in the scene; add a Power Thief component to the player by hand.");
                return;
            }

            if (parry.GetComponent<Gameplay.Powers.PowerThief>() != null)
            {
                return;
            }

            var thief = Undo.AddComponent<Gameplay.Powers.PowerThief>(parry.gameObject);
            var so = new SerializedObject(thief);
            so.FindProperty("stageStartedChannel").objectReferenceValue = stageStarted;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[RunUIBuilder] Added Power Thief to the player.", thief);
        }

        private static void BuildStolenPowers(Transform canvas, TransformAnchorSO playerAnchor)
        {
            const int slotCount = 4;
            RectTransform row = CreateRect("StolenPowers", canvas);
            Anchor(row, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 84f), new Vector2(slotCount * 160f, 38f));

            var slots = new Image[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                Image slot = CreateImage($"Slot{i + 1}", row, new Color(1f, 1f, 1f, 0.12f), false);
                Anchor(slot.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(i * 160f, 0f), new Vector2(150f, 38f));
                TMP_Text label = CreateText("Label", slot.transform, "-", 20f, TextAlignmentOptions.Center);
                label.color = new Color(0.08f, 0.08f, 0.1f);
                label.fontStyle = FontStyles.Bold;
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
                slots[i] = slot;
            }

            RectTransform toast = CreateRect("StealToast", canvas);
            Anchor(toast, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(1000f, 130f));
            AddHiddenGroup(toast.gameObject);
            TMP_Text toastText = CreateText("Text", toast, "STOLEN: Power", 46f, TextAlignmentOptions.Center);
            toastText.fontStyle = FontStyles.Bold;
            toastText.textWrappingMode = TextWrappingModes.Normal;
            SetRect(toastText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var ui = row.gameObject.AddComponent<StolenPowersUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("playerAnchor").objectReferenceValue = playerAnchor;
            SerializedProperty slotArray = so.FindProperty("slotImages");
            slotArray.arraySize = slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                slotArray.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            }

            so.FindProperty("toastGroup").objectReferenceValue = toast.GetComponent<CanvasGroup>();
            so.FindProperty("toastText").objectReferenceValue = toastText;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(toast.gameObject, "Create Steal Toast");
            Undo.RegisterCreatedObjectUndo(row.gameObject, "Create Stolen Powers HUD");
        }

        private static void BuildPauseMenu(Transform canvas)
        {
            // Full-screen dimmer that also blocks clicks to anything behind it.
            RectTransform menu = CreateRect("PauseMenu", canvas);
            SetRect(menu, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dim = menu.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            AddHiddenGroup(menu.gameObject);

            Image box = CreateImage("Box", menu, new Color(0.12f, 0.14f, 0.2f, 0.95f), false);
            Anchor(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 400f));

            TMP_Text title = CreateText("Title", box.transform, "Paused", 56f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(0f, -20f));

            var ui = menu.gameObject.AddComponent<PauseMenuUI>();
            Button resume = CreateButton("ResumeButton", box.transform, "Resume", new Vector2(0f, 40f));
            Button restart = CreateButton("RestartButton", box.transform, "Restart Run", new Vector2(0f, -40f));
            Button quit = CreateButton("TitleButton", box.transform, "Quit to Title", new Vector2(0f, -120f));

            UnityEventTools.AddPersistentListener(resume.onClick, ui.Resume);
            UnityEventTools.AddPersistentListener(restart.onClick, ui.RestartRun);
            UnityEventTools.AddPersistentListener(quit.onClick, ui.QuitToTitle);

            StageManager stageManager = Object.FindFirstObjectByType<StageManager>();
            FieldInfo restartField = typeof(PauseMenuUI).GetField("onRestart", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stageManager != null && restartField?.GetValue(ui) is UnityEvent onRestart)
            {
                UnityEventTools.AddPersistentListener(onRestart, stageManager.RestartRun);
            }
            else
            {
                Debug.LogWarning("[RunUIBuilder] No StageManager found; wire Pause Menu UI > On Restart to StageManager.RestartRun by hand.", ui);
            }

            var so = new SerializedObject(ui);
            so.FindProperty("firstSelected").objectReferenceValue = resume;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ui);

            menu.SetAsLastSibling();
            Undo.RegisterCreatedObjectUndo(menu.gameObject, "Create Pause Menu");
        }

        // ================================================================== Title scene

        [MenuItem("Tools/Roguelite/Create Title Scene")]
        public static void CreateTitleScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath) != null &&
                !EditorUtility.DisplayDialog("Create Title Scene", "Assets/Scenes/Title.unity already exists. Replace it?", "Replace", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string previousScene = SceneManager.GetActiveScene().path;
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.15f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = canvasObject.transform;

            TMP_Text title = CreateText("GameTitle", canvas, "PARRY ROGUE", 120f, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1400f, 180f));

            TMP_Text subtitle = CreateText("Subtitle", canvas, "Deflect. Stagger. Survive.", 40f, TextAlignmentOptions.Center);
            subtitle.color = new Color(1f, 0.85f, 0.3f);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1200f, 60f));

            var ui = canvasObject.AddComponent<TitleScreenUI>();
            Button start = CreateButton("StartButton", canvas, "Start Run", new Vector2(0f, -40f));
            Button quit = CreateButton("QuitButton", canvas, "Quit", new Vector2(0f, -130f));
            UnityEventTools.AddPersistentListener(start.onClick, ui.StartGame);
            UnityEventTools.AddPersistentListener(quit.onClick, ui.QuitGame);
            eventSystem.GetComponent<EventSystem>().firstSelectedGameObject = start.gameObject;

            EditorSceneManager.SaveScene(scene, TitleScenePath);
            AddScenesToBuild(TitleScenePath, string.IsNullOrEmpty(previousScene) ? "Assets/Scenes/Main.unity" : previousScene);
            Debug.Log("[RunUIBuilder] Title scene created and set as the first scene in the build. Change the title text to your game's name.");
        }

        private static void AddScenesToBuild(string titlePath, string gamePath)
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(titlePath, true) };
            bool hasGame = false;
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == titlePath)
                {
                    continue;
                }

                hasGame |= existing.path == gamePath;
                scenes.Add(existing.path == gamePath ? new EditorBuildSettingsScene(gamePath, true) : existing);
            }

            if (!hasGame && AssetDatabase.LoadAssetAtPath<SceneAsset>(gamePath) != null)
            {
                scenes.Insert(1, new EditorBuildSettingsScene(gamePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ================================================================== helpers

        private static void AddHiddenGroup(GameObject go)
        {
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            return (RectTransform)go.transform;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static Image CreateImage(string name, Transform parent, Color color, bool filled)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.color = color;
            image.raycastTarget = false;
            if (filled)
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = 1f;
            }
            else
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            var tmp = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        /// <summary>A button whose label never blocks clicks (the label has no Button and no raycast target).</summary>
        private static Button CreateButton(string name, Transform parent, string label, Vector2 position)
        {
            RectTransform rect = CreateRect(name, parent);
            Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(320f, 64f));

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(0.95f, 0.95f, 0.95f);

            var button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.4f);
            colors.selectedColor = new Color(1f, 0.85f, 0.4f);
            colors.pressedColor = new Color(0.8f, 0.65f, 0.25f);
            button.colors = colors;

            TMP_Text text = CreateText("Label", rect, label, 30f, TextAlignmentOptions.Center);
            text.color = new Color(0.1f, 0.1f, 0.15f);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Canvas FindRootCanvas()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas)
                {
                    return canvas;
                }
            }

            return null;
        }

        private static T FindAsset<T>(string preferredName) where T : Object
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
    }
}

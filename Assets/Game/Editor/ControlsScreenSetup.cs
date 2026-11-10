using Gameplay.Stages;
using Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// <b>Tools &gt; Roguelite &gt; Add Controls Screens</b>: adds a "How to Play" panel and a Controls button to the
    /// title screen and to the pause menu, plus a controls reminder strip shown at the start of each run.
    /// Edits Assets/Scenes/Title.unity and Assets/Scenes/Main.unity (opening and saving them as needed).
    /// Skips anything already added.
    /// </summary>
    public static class ControlsScreenSetup
    {
        private const string TitlePath = "Assets/Scenes/Title.unity";
        private const string MainPath = "Assets/Scenes/Main.unity";

        private const string ControlsText =
            "<color=#FFD24D>MOVE</color><pos=32%>WASD / Arrow keys\n" +
            "<color=#FFD24D>AIM</color><pos=32%>Mouse\n" +
            "<color=#FFD24D>SHOOT</color><pos=32%>Left mouse button\n" +
            "<color=#FFD24D>DASH</color><pos=32%>Space\n" +
            "<pos=32%><size=75%><color=#AAAAAA>(brief invincibility)</color></size>\n" +
            "<color=#FFD24D>PARRY</color><pos=32%>Right mouse / F\n" +
            "<color=#FFD24D>PAUSE</color><pos=32%>Esc";

        private const string TipsText =
            "<b>Parry</b> just as an attack or bullet reaches you to block it. Bullets bounce back at enemies.\n\n" +
            "<b><color=#FFD24D>Perfect parry</color></b> (very tight timing) <b>steals the enemy's power</b>. Steal it again to level it up.\n\n" +
            "Chain parries without getting hit to build <b><color=#66C2FF>TEMPO</color></b>: more damage and more score.\n\n" +
            "<b>Bosses and elites are armoured.</b> Parry them and hit them with reflected shots to fill their posture bar and <b>STAGGER</b> them.\n\n" +
            "Enemies flash <color=#FF5555>red</color> before a lunge and <color=#FFE04D>yellow</color> before shooting.";

        private const string HintText =
            "<color=#FFD24D>WASD</color> move   <color=#FFD24D>Mouse</color> aim   <color=#FFD24D>Left click</color> shoot   " +
            "<color=#FFD24D>Space</color> dash   <color=#FFD24D>Right click / F</color> parry   <color=#FFD24D>Esc</color> pause";

        private static Sprite uiSprite;

        [MenuItem("Tools/Roguelite/Add Controls Screens")]
        public static void SetUp()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            string title = EditScene(TitlePath, SetUpTitle);
            string main = EditScene(MainPath, SetUpMain);
            Debug.Log($"[ControlsScreenSetup] Title: {title}. Main: {main}.");
        }

        /// <summary>Opens the scene if needed, runs <paramref name="edit"/>, saves, and closes it again if we opened it.</summary>
        private static string EditScene(string path, System.Func<Scene, string> edit)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                return "scene not found";
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.isLoaded;
            if (openedHere)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            string result = edit(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (openedHere)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            return result;
        }

        // ------------------------------------------------------------------ scenes

        private static string SetUpTitle(Scene scene)
        {
            TitleScreenUI title = FindInScene<TitleScreenUI>(scene);
            if (title == null)
            {
                return "no TitleScreenUI found";
            }

            Transform canvas = title.GetComponentInParent<Canvas>().rootCanvas.transform;
            if (canvas.Find("ControlsPanel") != null)
            {
                return "already set up";
            }

            ControlsPanelUI panel = BuildPanel(canvas);

            // Make room: Start stays, Controls goes in the middle, Quit moves down.
            Transform quit = canvas.Find("QuitButton");
            if (quit != null)
            {
                ((RectTransform)quit).anchoredPosition = new Vector2(0f, -220f);
            }

            Button open = CreateButton("ControlsButton", canvas, "How to Play", new Vector2(0f, -130f), new Vector2(320f, 64f));
            UnityEventTools.AddPersistentListener(open.onClick, panel.Open);
            panel.transform.SetAsLastSibling();
            return "controls panel and button added";
        }

        private static string SetUpMain(Scene scene)
        {
            PauseMenuUI pause = FindInScene<PauseMenuUI>(scene);
            if (pause == null)
            {
                return "no PauseMenuUI found (run Build Missing Game UI first)";
            }

            Transform canvas = pause.GetComponentInParent<Canvas>().rootCanvas.transform;
            string result = "already set up";

            if (canvas.Find("ControlsPanel") == null)
            {
                ControlsPanelUI panel = BuildPanel(canvas);

                // Grow the pause box and add a Controls button under the others.
                Transform box = pause.transform.Find("Box");
                Transform parent = box != null ? box : pause.transform;
                if (box != null)
                {
                    ((RectTransform)box).sizeDelta = new Vector2(420f, 500f);
                    Transform titleText = box.Find("Title");
                    if (titleText != null)
                    {
                        var rect = (RectTransform)titleText;
                        rect.offsetMin = new Vector2(0f, -90f);
                        rect.offsetMax = new Vector2(0f, -15f);
                    }

                    Shift(box, "ResumeButton", 70f);
                    Shift(box, "RestartButton", -10f);
                    Shift(box, "TitleButton", -90f);
                }

                Button open = CreateButton("ControlsButton", parent, "Controls", new Vector2(0f, -170f), new Vector2(320f, 64f));
                UnityEventTools.AddPersistentListener(open.onClick, panel.Open);

                // On top of the pause menu.
                panel.transform.SetAsLastSibling();
                result = "controls panel and pause button added";
            }

            if (canvas.Find("ControlsHint") == null)
            {
                BuildHint(canvas);
                result += ", start-of-run hint added";
            }

            return result;
        }

        private static void Shift(Transform box, string child, float y)
        {
            Transform t = box.Find(child);
            if (t != null)
            {
                ((RectTransform)t).anchoredPosition = new Vector2(0f, y);
            }
        }

        // ------------------------------------------------------------------ panel

        private static ControlsPanelUI BuildPanel(Transform canvas)
        {
            RectTransform root = CreateRect("ControlsPanel", canvas);
            Stretch(root, Vector2.zero, Vector2.one);
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image box = CreateImage("Box", root, new Color(0.11f, 0.13f, 0.19f, 0.97f));
            Center(box.rectTransform, Vector2.zero, new Vector2(1240f, 760f));

            TMP_Text heading = CreateText("Heading", box.transform, "HOW TO PLAY", 60f, TextAlignmentOptions.Center);
            heading.fontStyle = FontStyles.Bold;
            Anchor(heading.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -100f), new Vector2(0f, -20f));

            TMP_Text controlsHeading = CreateText("ControlsHeading", box.transform, "CONTROLS", 30f, TextAlignmentOptions.TopLeft);
            controlsHeading.color = new Color(0.7f, 0.75f, 0.85f);
            Anchor(controlsHeading.rectTransform, new Vector2(0f, 1f), new Vector2(0.45f, 1f), new Vector2(60f, -160f), new Vector2(-20f, -115f));

            TMP_Text controls = CreateText("Controls", box.transform, ControlsText, 32f, TextAlignmentOptions.TopLeft);
            controls.lineSpacing = 25f;
            controls.textWrappingMode = TextWrappingModes.Normal;
            Anchor(controls.rectTransform, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(60f, 120f), new Vector2(-20f, -170f));

            TMP_Text tipsHeading = CreateText("TipsHeading", box.transform, "HOW TO WIN", 30f, TextAlignmentOptions.TopLeft);
            tipsHeading.color = new Color(0.7f, 0.75f, 0.85f);
            Anchor(tipsHeading.rectTransform, new Vector2(0.45f, 1f), new Vector2(1f, 1f), new Vector2(20f, -160f), new Vector2(-60f, -115f));

            TMP_Text tips = CreateText("Tips", box.transform, TipsText, 25f, TextAlignmentOptions.TopLeft);
            tips.textWrappingMode = TextWrappingModes.Normal;
            Anchor(tips.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 1f), new Vector2(20f, 120f), new Vector2(-60f, -170f));

            var panel = root.gameObject.AddComponent<ControlsPanelUI>();
            Button back = CreateButton("BackButton", box.transform, "Back", Vector2.zero, new Vector2(280f, 64f));
            var backRect = (RectTransform)back.transform;
            backRect.anchorMin = new Vector2(0.5f, 0f);
            backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 70f);
            UnityEventTools.AddPersistentListener(back.onClick, panel.Close);

            var so = new SerializedObject(panel);
            so.FindProperty("firstSelected").objectReferenceValue = back;
            so.FindProperty("fadeDuration").floatValue = 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static void BuildHint(Transform canvas)
        {
            RectTransform strip = CreateRect("ControlsHint", canvas);
            strip.anchorMin = new Vector2(0.5f, 0f);
            strip.anchorMax = new Vector2(0.5f, 0f);
            strip.pivot = new Vector2(0.5f, 0f);
            strip.anchoredPosition = new Vector2(0f, 28f);
            strip.sizeDelta = new Vector2(1000f, 46f);

            Image background = strip.gameObject.AddComponent<Image>();
            background.sprite = uiSprite;
            background.type = Image.Type.Sliced;
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;

            TMP_Text text = CreateText("Text", strip, HintText, 24f, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);

            var hint = strip.gameObject.AddComponent<ControlsHintUI>();
            var so = new SerializedObject(hint);
            so.FindProperty("stageStartedChannel").objectReferenceValue = FindAsset<StageEventChannelSO>("StageStartedChannel");
            so.FindProperty("catalog").objectReferenceValue = FindAsset<StageCatalog>("StageCatalog");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ helpers

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = uiSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
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
            tmp.richText = true;
            return tmp;
        }

        /// <summary>A button whose label can't block clicks.</summary>
        private static Button CreateButton(string name, Transform parent, string label, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent);
            Center(rect, position, size);
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
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            return button;
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

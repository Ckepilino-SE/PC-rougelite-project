using System.Collections.Generic;
using Core.References;
using Gameplay.Enemies;
using Gameplay.Feedback;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gameplay.EditorTools
{
    /// <summary>
    /// <b>Tools &gt; Roguelite &gt; Set Up Effects, Audio &amp; Facing</b> (run with the Main scene open):
    /// <list type="bullet">
    /// <item>Creates effect prefabs in Assets/Prefabs/Effects, using Kenney Particle Pack sprites if they're in
    /// the project (spark, star, flare, smoke, twirl, muzzle), otherwise a plain circle you can swap later.</item>
    /// <item>Adds CombatEffects and CombatAudio to a "Feedback" object in the scene, with every slot filled.
    /// Kenney Impact Sounds are assigned automatically when found.</item>
    /// <item>Adds FaceTarget to the Grunt prefab, so every enemy variant turns to face the player.</item>
    /// </list>
    /// Safe to run again: existing effect prefabs are kept, and slots are refilled.
    /// </summary>
    public static class FeedbackSetup
    {
        private const string EffectFolder = "Assets/Prefabs/Effects";

        [MenuItem("Tools/Roguelite/Set Up Effects, Audio & Facing")]
        public static void SetUp()
        {
            EnsureFolder(EffectFolder);

            PooledEffect hit = Effect("FX_Hit", new[] { "spark_01", "spark_02", "star_04" }, 0.25f, 0.8f, 0.18f);
            PooledEffect death = Effect("FX_Death", new[] { "smoke_04", "smoke_01", "circle_05" }, 0.6f, 1.8f, 0.45f);
            PooledEffect parry = Effect("FX_Parry", new[] { "flare_01", "light_01", "star_06" }, 0.5f, 1.6f, 0.22f);
            PooledEffect perfect = Effect("FX_PerfectParry", new[] { "star_06", "star_07", "flare_01" }, 0.7f, 2.6f, 0.35f);
            PooledEffect stagger = Effect("FX_Stagger", new[] { "twirl_01", "twirl_02", "circle_03" }, 0.6f, 2.2f, 0.6f);
            PooledEffect hurt = Effect("FX_Hurt", new[] { "slash_02", "scratch_01", "circle_05" }, 0.6f, 1.4f, 0.25f);
            PooledEffect muzzle = Effect("FX_Muzzle", new[] { "muzzle_01", "muzzle_02", "flare_01" }, 0.25f, 0.45f, 0.06f, randomRotation: false);

            AddFaceTarget();
            bool scene = SetUpScene(hit, death, parry, perfect, stagger, hurt, muzzle);

            AssetDatabase.SaveAssets();
            Debug.Log("[FeedbackSetup] Effect prefabs ready in Assets/Prefabs/Effects; FaceTarget added to Grunt." +
                      (scene ? " Feedback object added to the scene. Save the scene (Ctrl+S)." : " Open the Main scene and run again to add the Feedback object."));
        }

        // ------------------------------------------------------------------ effects

        private static PooledEffect Effect(string name, string[] spriteNames, float startSize, float endSize, float lifetime, bool randomRotation = true)
        {
            string path = $"{EffectFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                return existing.GetComponent<PooledEffect>();
            }

            Sprite sprite = FindSprite(spriteNames) ?? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            float spriteSize = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.x) : 1f;

            var go = new GameObject(name);
            try
            {
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 100;

                var effect = go.AddComponent<PooledEffect>();
                var so = new SerializedObject(effect);
                so.FindProperty("lifetime").floatValue = lifetime;
                so.FindProperty("sprite").objectReferenceValue = renderer;
                // Sizes are in world units; divide by the sprite's size so any image works.
                so.FindProperty("startScale").floatValue = startSize / spriteSize;
                so.FindProperty("endScale").floatValue = endSize / spriteSize;
                so.FindProperty("randomRotation").boolValue = randomRotation;
                so.ApplyModifiedPropertiesWithoutUndo();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
                return saved != null ? saved.GetComponent<PooledEffect>() : null;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Finds the first PNG with one of these names and makes sure it imports as a sprite.</summary>
        private static Sprite FindSprite(string[] names)
        {
            foreach (string name in names)
            {
                foreach (string guid in AssetDatabase.FindAssets($"{name} t:Texture2D"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != name)
                    {
                        continue;
                    }

                    if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.SaveAndReimport();
                    }

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                    {
                        return sprite;
                    }
                }
            }

            return null;
        }

        // ------------------------------------------------------------------ facing

        private static void AddFaceTarget()
        {
            const string path = "Assets/Prefabs/Grunt.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning("[FeedbackSetup] Grunt prefab not found; add Face Target to your enemies by hand.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<FaceTarget>() == null)
                {
                    root.AddComponent<FaceTarget>();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ------------------------------------------------------------------ scene

        private static bool SetUpScene(PooledEffect hit, PooledEffect death, PooledEffect parry, PooledEffect perfect,
            PooledEffect stagger, PooledEffect hurt, PooledEffect muzzle)
        {
            if (Object.FindFirstObjectByType<Gameplay.Player.PlayerParry>(FindObjectsInactive.Include) == null)
            {
                return false;
            }

            var anchor = FindAsset<TransformAnchorSO>("PlayerAnchor");

            CombatEffects effects = Object.FindFirstObjectByType<CombatEffects>(FindObjectsInactive.Include);
            CombatAudio audio = Object.FindFirstObjectByType<CombatAudio>(FindObjectsInactive.Include);
            GameObject holder = effects != null ? effects.gameObject : audio != null ? audio.gameObject : null;
            if (holder == null)
            {
                holder = new GameObject("Feedback");
                Undo.RegisterCreatedObjectUndo(holder, "Create Feedback");
            }

            if (effects == null)
            {
                effects = Undo.AddComponent<CombatEffects>(holder);
            }

            if (audio == null)
            {
                audio = Undo.AddComponent<CombatAudio>(holder);
            }

            var fx = new SerializedObject(effects);
            fx.FindProperty("playerAnchor").objectReferenceValue = anchor;
            Fill(fx, "enemyHit", hit);
            Fill(fx, "enemyDeath", death);
            Fill(fx, "parry", parry);
            Fill(fx, "perfectParry", perfect);
            Fill(fx, "stagger", stagger);
            Fill(fx, "playerHurt", hurt);
            Fill(fx, "muzzleFlash", muzzle);
            fx.ApplyModifiedPropertiesWithoutUndo();

            var sfx = new SerializedObject(audio);
            sfx.FindProperty("playerAnchor").objectReferenceValue = anchor;
            Sound(sfx, "enemyHit", "impactPunch_medium", 0.45f, 0.04f);
            Sound(sfx, "enemyDeath", "impactSoft_heavy", 0.7f, 0.05f);
            Sound(sfx, "playerHurt", "impactPunch_heavy", 0.9f, 0.1f);
            Sound(sfx, "stagger", "impactBell_heavy", 0.9f, 0.2f);
            Sound(sfx, "bossPhase", "impactMetal_heavy", 1f, 0.2f);
            Sound(sfx, "powerStolen", "impactTin_medium", 0.8f, 0.1f);
            Sound(sfx, "tempoBroken", "impactGlass_heavy", 0.6f, 0.2f);
            Sound(sfx, "dash", "impactSoft_medium", 0.4f, 0.05f);
            sfx.ApplyModifiedPropertiesWithoutUndo();

            ParryFeedback parryFeedback = Object.FindFirstObjectByType<ParryFeedback>(FindObjectsInactive.Include);
            if (parryFeedback != null)
            {
                var pf = new SerializedObject(parryFeedback);
                SetClip(pf, "parryClip", "impactMetal_medium");
                SetClip(pf, "perfectClip", "impactPlate_heavy");
                pf.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(holder.scene);
            return true;
        }

        private static void Fill(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }

        /// <summary>Fills an empty sound slot with every clip whose name starts with <paramref name="prefix"/>.</summary>
        private static void Sound(SerializedObject so, string field, string prefix, float volume, float gap)
        {
            SerializedProperty sound = so.FindProperty(field);
            SerializedProperty clips = sound.FindPropertyRelative("clips");
            if (clips.arraySize > 0)
            {
                return;
            }

            List<AudioClip> found = FindClips(prefix);
            if (found.Count == 0)
            {
                return;
            }

            clips.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                clips.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }

            sound.FindPropertyRelative("volume").floatValue = volume;
            sound.FindPropertyRelative("minimumGap").floatValue = gap;
        }

        private static void SetClip(SerializedObject so, string field, string prefix)
        {
            List<AudioClip> found = FindClips(prefix);
            if (found.Count > 0)
            {
                so.FindProperty(field).objectReferenceValue = found[0];
            }
        }

        private static List<AudioClip> FindClips(string prefix)
        {
            var result = new List<AudioClip>();
            foreach (string guid in AssetDatabase.FindAssets($"{prefix} t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path).StartsWith(prefix))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null)
                    {
                        result.Add(clip);
                    }
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
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

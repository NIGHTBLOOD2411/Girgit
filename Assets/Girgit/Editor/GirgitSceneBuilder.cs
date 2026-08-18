#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Girgit
{
    /// <summary>
    /// One-click scene authoring. Builds the complete Girgit game as real
    /// GameObjects in the scene — managers, ground, parallax, player and the
    /// full HUD — with every reference wired and a few sample levels filled
    /// in. After building you tune EVERYTHING from the GameManager's Inspector
    /// (levels/colors/times) and drop your sprite-sheet frames onto the
    /// Chameleon's SpriteAnimator component.
    ///
    /// Menu: Girgit > Build Game Into Current Scene / New Scene + Build Game.
    /// </summary>
    public static class GirgitSceneBuilder
    {
        const float OrthoSize = 6f;
        const float RoadHeight = 7f;
        const string ScenePath = "Assets/Girgit/Girgit.unity";
        static readonly Color GrassColor = new Color(0.16f, 0.22f, 0.14f);

        [MenuItem("Girgit/Build Game Into Current Scene")]
        public static void BuildCurrent() => Build(false);

        [MenuItem("Girgit/New Scene + Build Game")]
        public static void BuildNewScene() => Build(true);

        /// <summary>
        /// Non-destructive fix-up for a scene built by an older version of this
        /// tool: fills in any GameManager reference that is still None (e.g. a
        /// panel added to the game after your scene was built) without
        /// touching anything you've already hand-tuned in the Inspector.
        /// </summary>
        [MenuItem("Girgit/Repair Missing References")]
        public static void RepairMissingReferences()
        {
            var gm = Object.FindObjectOfType<GameManager>();
            if (gm == null)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "No GameManager found in this scene — use 'Build Game' instead.", "OK");
                return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "No HUD Canvas found in this scene — can't repair panels.", "OK");
                return;
            }

            var res = new DefaultControls.Resources();
            var font = GirgitFonts.Regular();
            if (font == null) return; // GirgitFonts already showed why (e.g. importing TMP Essential Resources)
            bool changed = false;

            if (gm.ChallengeCompletePanel == null)
            {
                GameObject panel = MakePanel(res, font, canvas.transform,
                    "CONGRATULATIONS!\nYou were matching --%!\nContinue for the next challenge.",
                    "CONTINUE", out Button btn, out TextMeshProUGUI msg);
                panel.SetActive(false);
                gm.ChallengeCompletePanel = panel;
                gm.ChallengeCompleteMessage = msg;
                UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(gm.ContinueToNext));
                changed = true;
            }

            // Levels' shape changed (each Level is now a group of Challenges) —
            // Unity drops fields it can't match on recompile, so a scene built
            // before this change ends up with Levels present but every one of
            // them empty of Challenges. Re-seed with the sample template
            // rather than leaving the game unplayable; re-author from there.
            bool hasAnyChallenges = false;
            if (gm.Levels != null)
                foreach (var lv in gm.Levels)
                    if (lv.Challenges != null && lv.Challenges.Count > 0) { hasAnyChallenges = true; break; }

            if (!hasAnyChallenges)
            {
                gm.Levels = SampleLevels();
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(gm);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorUtility.DisplayDialog("Girgit",
                    "Repaired missing references. Save the scene (Ctrl+S) and press Play.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Girgit", "Nothing to repair — all references are already wired.", "OK");
            }
        }

        static void Build(bool newScene)
        {
            if (newScene)
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            if (Object.FindObjectOfType<GameManager>() != null)
            {
                if (!EditorUtility.DisplayDialog("Girgit",
                    "This scene already has a GameManager. Build another set of objects anyway?",
                    "Build anyway", "Cancel"))
                    return;
            }

            if (GirgitFonts.Regular() == null) return; // GirgitFonts already showed why

            Sprite square = MakeSpriteAsset("white_square", MakeSquareTex(), 4);
            Sprite circle = MakeSpriteAsset("white_circle", MakeCircleTex(64), 64);

            Camera cam = SetupCamera();
            float halfH = OrthoSize;
            float halfW = OrthoSize * Mathf.Max(0.1f, cam.aspect);
            float left = -halfW;
            float roadTop = RoadHeight * 0.5f;

            var root = new GameObject("Girgit Game");
            var scroller = root.AddComponent<WorldScroller>();

            // Ground: a single-color road band (recolored per level at runtime).
            var ground = CreateSprite("Ground", root.transform, square,
                new Color(0.30f, 0.70f, 0.35f), 0,
                new Vector3(0, 0, 0), new Vector3(halfW * 2f + 2f, RoadHeight, 1f));
            var groundSR = ground.GetComponent<SpriteRenderer>();

            BuildParallaxBack(root.transform, circle, halfW, roadTop);
            BuildParallaxFront(root.transform, square, halfW, halfH);

            // Player.
            var playerGO = CreateSprite("Chameleon", root.transform, circle,
                Color.white, 10, new Vector3(left + 3.5f, 0, 0), Vector3.one * 1.5f);
            var player = playerGO.AddComponent<Chameleon>();
            playerGO.AddComponent<SpriteAnimator>(); // drag chameleon frames here

            // HUD + wiring.
            BuildUI(player, out ColorMixerUI mixer, out TextMeshProUGUI levelText, out TextMeshProUGUI timerText,
                    out TextMeshProUGUI matchText, out GameObject overPanel, out GameObject completePanel,
                    out TextMeshProUGUI completeMessage, out GameObject winPanel,
                    out Button overBtn, out Button completeBtn, out Button winBtn);

            var gm = root.AddComponent<GameManager>();
            gm.Player = player;
            gm.Ground = groundSR;
            gm.Mixer = mixer;
            gm.LevelText = levelText;
            gm.TimerText = timerText;
            gm.MatchText = matchText;
            gm.GameOverPanel = overPanel;
            gm.ChallengeCompletePanel = completePanel;
            gm.ChallengeCompleteMessage = completeMessage;
            gm.WinPanel = winPanel;
            gm.Levels = SampleLevels();

            // Persistent button wiring (survives scene save).
            UnityEventTools.AddPersistentListener(overBtn.onClick, new UnityAction(gm.Restart));
            UnityEventTools.AddPersistentListener(completeBtn.onClick, new UnityAction(gm.ContinueToNext));
            UnityEventTools.AddPersistentListener(winBtn.onClick, new UnityAction(gm.Restart));

            EditorUtility.SetDirty(gm);
            Selection.activeGameObject = root;

            if (newScene)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Girgit"))
                    AssetDatabase.CreateFolder("Assets", "Girgit");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
                RegisterScene(ScenePath);
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }

            EditorUtility.DisplayDialog("Girgit",
                "Game built!\n\n" +
                "• Select 'Girgit Game' > GameManager to edit Levels (color + time + match %).\n" +
                "• Drop your sprite-sheet frames onto Chameleon > SpriteAnimator.\n" +
                "• Press Play.", "OK");
        }

        // --------------------------------------------------------------- camera

        static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GrassColor;
            return cam;
        }

        // ------------------------------------------------------------- parallax

        static void BuildParallaxBack(Transform root, Sprite circle, float halfW, float roadTop)
        {
            var go = new GameObject("Parallax_Back");
            go.transform.SetParent(root, false);

            const float spacing = 3.5f;
            int count = Mathf.CeilToInt(halfW * 2f / spacing) + 3;
            for (int i = 0; i < count; i++)
            {
                bool top = (i % 2 == 0);
                float y = top ? roadTop + 1.3f + (i % 3) * 0.2f : -(roadTop + 1.3f + (i % 3) * 0.2f);
                var dot = CreateSprite("Dot", go.transform, circle, new Color(0.14f, 0.26f, 0.13f, 0.9f),
                    -10, new Vector3(-halfW + i * spacing, y, 0f), Vector3.one * (0.7f + 0.2f * (i % 3)));
                var tile = dot.AddComponent<ParallaxTile>();
                tile.MoveAngle = 0f;
                tile.Speed = 1f;
            }
        }

        static void BuildParallaxFront(Transform root, Sprite square, float halfW, float halfH)
        {
            var go = new GameObject("Parallax_Front");
            go.transform.SetParent(root, false);

            const float spacing = 7f;
            int count = Mathf.CeilToInt(halfW * 2f / spacing) + 2;
            for (int i = 0; i < count; i++)
            {
                var bar = CreateSprite("Bar", go.transform, square, new Color(0.05f, 0.05f, 0.08f, 0.35f),
                    20, new Vector3(-halfW + i * spacing, 0f, 0f), new Vector3(0.5f, halfH * 2f, 1f));
                var tile = bar.AddComponent<ParallaxTile>();
                tile.MoveAngle = 0f;
                tile.Speed = 4.5f;
            }
        }

        internal static GameObject CreateSprite(string name, Transform parent, Sprite sprite,
                                       Color color, int order, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.position = pos;
            go.transform.localScale = scale;
            return go;
        }

        // -------------------------------------------------------------------- UI

        static void BuildUI(Chameleon player, out ColorMixerUI mixer, out TextMeshProUGUI levelText,
                            out TextMeshProUGUI timerText, out TextMeshProUGUI matchText, out GameObject overPanel,
                            out GameObject completePanel, out TextMeshProUGUI completeMessage, out GameObject winPanel,
                            out Button overBtn, out Button completeBtn, out Button winBtn)
        {
            var canvasGO = new GameObject("HUD Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var res = new DefaultControls.Resources();
            var font = GirgitFonts.Regular();

            levelText = MakeText(res, font, canvas.transform, "LEVEL 1", 40, TextAnchor.UpperLeft);
            Anchor(levelText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                   new Vector2(220, -50), new Vector2(420, 60));

            matchText = MakeText(res, font, canvas.transform, "MATCH  --", 48, TextAnchor.UpperCenter);
            Anchor(matchText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                   new Vector2(0, -50), new Vector2(600, 70));

            timerText = MakeText(res, font, canvas.transform, "TIME  --", 40, TextAnchor.UpperRight);
            Anchor(timerText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
                   new Vector2(-220, -50), new Vector2(420, 60));

            var sliderSize = new Vector2(700, 34);
            Slider hue = MakeSlider(res, canvas.transform, "HueSlider", sliderSize, new Vector2(20, 170));
            Slider sat = MakeSlider(res, canvas.transform, "SatSlider", sliderSize, new Vector2(20, 110));
            Slider val = MakeSlider(res, canvas.transform, "ValSlider", sliderSize, new Vector2(20, 50));
            hue.value = 0f; sat.value = 0.8f; val.value = 0.8f;
            AddLabel(res, font, canvas.transform, "H", 170);
            AddLabel(res, font, canvas.transform, "S", 110);
            AddLabel(res, font, canvas.transform, "V", 50);

            mixer = canvasGO.AddComponent<ColorMixerUI>();
            mixer.Hue = hue; mixer.Saturation = sat; mixer.Value = val; mixer.Player = player;

            overPanel = MakePanel(res, font, canvas.transform,
                "GAME OVER\nThe eagle got you!", "RESTART", out overBtn, out TextMeshProUGUI _);
            completePanel = MakePanel(res, font, canvas.transform,
                "CONGRATULATIONS!\nYou were matching --%!\nContinue for the next challenge.",
                "CONTINUE", out completeBtn, out completeMessage);
            winPanel = MakePanel(res, font, canvas.transform,
                "YOU WIN!\nAll levels cleared!", "RESTART", out winBtn, out TextMeshProUGUI _2);
            overPanel.SetActive(false);
            completePanel.SetActive(false);
            winPanel.SetActive(false);
        }

        internal static GameObject MakePanel(DefaultControls.Resources res, TMP_FontAsset font, Transform parent,
                                    string message, string buttonLabel, out Button button, out TextMeshProUGUI messageText)
        {
            var panel = DefaultControls.CreatePanel(res);
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.80f);

            messageText = MakeText(res, font, panel.transform, message, 64, TextAnchor.MiddleCenter);
            Anchor(messageText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0, 90), new Vector2(1000, 260));

            var btnGO = DefaultControls.CreateButton(res);
            btnGO.name = buttonLabel.Replace(" ", "") + "Button";
            btnGO.transform.SetParent(panel.transform, false);
            Anchor(btnGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                   new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(320, 90));
            MakeButtonText(btnGO, font, buttonLabel, 36, Color.white);
            button = btnGO.GetComponent<Button>();
            return panel;
        }

        /// <summary>Swaps a DefaultControls button's auto-created legacy Text child for TMP.</summary>
        internal static TextMeshProUGUI MakeButtonText(GameObject btnGO, TMP_FontAsset font, string label, int size, Color color)
        {
            var legacy = btnGO.GetComponentInChildren<Text>();
            GameObject textGO = legacy.gameObject;
            Object.DestroyImmediate(legacy);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = label;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        /// <summary>
        /// Converts any leftover legacy Text component (e.g. from a scene built
        /// by an older, pre-TMP version of this tool) to TMP in place, on the
        /// same GameObject, preserving its content/color/size/alignment.
        /// </summary>
        internal static TextMeshProUGUI ConvertTextToTMP(Text legacy, TMP_FontAsset font)
        {
            GameObject go = legacy.gameObject;
            string content = legacy.text;
            Color color = legacy.color;
            int size = legacy.fontSize;
            TextAlignmentOptions align = ToTMPAlignment(legacy.alignment);

            Object.DestroyImmediate(legacy);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.color = color;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.font = font;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        internal static Slider MakeSlider(DefaultControls.Resources res, Transform parent, string name,
                                 Vector2 size, Vector2 pos)
        {
            var go = DefaultControls.CreateSlider(res);
            go.name = name;
            go.transform.SetParent(parent, false);
            var s = go.GetComponent<Slider>();
            s.minValue = 0f; s.maxValue = 1f;
            Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                   new Vector2(0.5f, 0), pos, size);
            return s;
        }

        static void AddLabel(DefaultControls.Resources res, TMP_FontAsset font, Transform parent, string letter, float y)
        {
            var t = MakeText(res, font, parent, letter, 34, TextAnchor.MiddleCenter);
            Anchor(t.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                   new Vector2(-390, y + 17), new Vector2(60, 40));
        }

        internal static TextMeshProUGUI MakeText(DefaultControls.Resources res, TMP_FontAsset font, Transform parent,
                             string content, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text (TMP)", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.alignment = ToTMPAlignment(anchor);
            t.color = Color.white;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        internal static TextAlignmentOptions ToTMPAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        internal static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        // ----------------------------------------------------------- level data

        /// <summary>
        /// 3 levels x 5 challenges, each level harder than the last (less time,
        /// higher required match) — a starting template; add/remove/edit
        /// freely from the Inspector.
        /// </summary>
        static List<LevelDefinition> SampleLevels() => new List<LevelDefinition>
        {
            new LevelDefinition
            {
                Name = "",
                Challenges = new List<ChallengeDefinition>
                {
                    new ChallengeDefinition { Color = new Color(0.30f, 0.70f, 0.35f), TimeLimit = 20f, RequiredMatch = 88f },
                    new ChallengeDefinition { Color = new Color(0.85f, 0.35f, 0.20f), TimeLimit = 19f, RequiredMatch = 89f },
                    new ChallengeDefinition { Color = new Color(0.20f, 0.45f, 0.85f), TimeLimit = 18f, RequiredMatch = 90f },
                    new ChallengeDefinition { Color = new Color(0.60f, 0.25f, 0.75f), TimeLimit = 17f, RequiredMatch = 91f },
                    new ChallengeDefinition { Color = new Color(0.90f, 0.75f, 0.15f), TimeLimit = 16f, RequiredMatch = 92f },
                }
            },
            new LevelDefinition
            {
                Name = "",
                Challenges = new List<ChallengeDefinition>
                {
                    new ChallengeDefinition { Color = new Color(0.15f, 0.60f, 0.55f), TimeLimit = 15f, RequiredMatch = 93f },
                    new ChallengeDefinition { Color = new Color(0.70f, 0.20f, 0.45f), TimeLimit = 14f, RequiredMatch = 93f },
                    new ChallengeDefinition { Color = new Color(0.35f, 0.35f, 0.80f), TimeLimit = 13f, RequiredMatch = 94f },
                    new ChallengeDefinition { Color = new Color(0.80f, 0.55f, 0.10f), TimeLimit = 13f, RequiredMatch = 94f },
                    new ChallengeDefinition { Color = new Color(0.25f, 0.75f, 0.20f), TimeLimit = 12f, RequiredMatch = 95f },
                }
            },
            new LevelDefinition
            {
                Name = "",
                Challenges = new List<ChallengeDefinition>
                {
                    new ChallengeDefinition { Color = new Color(0.90f, 0.20f, 0.20f), TimeLimit = 11f, RequiredMatch = 95f },
                    new ChallengeDefinition { Color = new Color(0.20f, 0.30f, 0.65f), TimeLimit = 11f, RequiredMatch = 96f },
                    new ChallengeDefinition { Color = new Color(0.65f, 0.45f, 0.85f), TimeLimit = 10f, RequiredMatch = 96f },
                    new ChallengeDefinition { Color = new Color(0.95f, 0.60f, 0.05f), TimeLimit = 10f, RequiredMatch = 97f },
                    new ChallengeDefinition { Color = new Color(0.10f, 0.55f, 0.40f), TimeLimit = 9f,  RequiredMatch = 97f },
                }
            },
        };

        // ------------------------------------------------------- sprite assets

        static Sprite MakeSpriteAsset(string fileName, Texture2D tex, int ppu)
        {
            const string dir = "Assets/Girgit/Art";
            if (!AssetDatabase.IsValidFolder("Assets/Girgit"))
                AssetDatabase.CreateFolder("Assets", "Girgit");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/Girgit", "Art");

            string path = $"{dir}/{fileName}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D MakeSquareTex()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Texture2D MakeCircleTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r, dy = y + 0.5f - r;
                    bool inside = dx * dx + dy * dy <= r * r;
                    px[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void RegisterScene(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == path))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
#endif

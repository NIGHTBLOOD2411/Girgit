#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Girgit
{
    /// <summary>
    /// Non-destructive patch on top of GirgitSceneBuilder's output: wraps the
    /// existing gameplay HUD into a GameplayPanel, adds SplashPanel + MenuPanel
    /// (matching the reference mockups), restyles the timer into a
    /// "SURVIVE TIME / big number / SEC" cluster, and re-skins the 3 mixer
    /// sliders with the art in Assets/Girgit/Art/Environment. Everything is
    /// TextMeshPro — any leftover legacy Text from an older build is swept to
    /// TMP first. Safe to run once on an already-tuned scene — it only
    /// touches what it adds, and leaves your Levels list, chameleon animation
    /// frames, etc. untouched.
    ///
    /// Menu: Girgit > Add Splash + Menu Screens.
    /// </summary>
    public static class GirgitScreensBuilder
    {
        static readonly Color BrandPurple = new Color(0.290f, 0.227f, 0.710f);
        static readonly Color BrandYellow = new Color(1f, 0.831f, 0f);
        static readonly Color BrandGreen = new Color(0.043f, 0.902f, 0.463f);

        const string ArtDir = "Assets/Girgit/Art/Environment";

        [MenuItem("Girgit/Add Splash + Menu Screens")]
        public static void AddScreens()
        {
            var gm = Object.FindObjectOfType<GameManager>();
            var mixer = Object.FindObjectOfType<ColorMixerUI>();
            var canvas = Object.FindObjectOfType<Canvas>();
            if (gm == null || mixer == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "Need an existing built game (GameManager / ColorMixerUI / Canvas) first — run 'Build Game' before this.", "OK");
                return;
            }

            TMP_FontAsset font = GirgitFonts.Regular();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Girgit", "Could not load/create the Jaro TMP font asset.", "OK");
                return;
            }

            // Sweep any leftover legacy Text (from an older, pre-TMP build) to
            // TMP first, re-wiring GameManager's Level/Timer/Match references
            // by their original top-row anchor (left/center/right) — the
            // TextMeshProUGUI field type change nulls out any old Text ref.
            ConvertLegacyTextAndRewire(canvas, gm, font);

            // Always re-run the optical-size pass too (even on an already-built
            // scene) — cheap, idempotent, and the only way an existing scene
            // picks up newly added Jaro size variants.
            ApplyOpticalSizeFonts(canvas.transform);

            // Backfill the victory-transition wiring (HudGroup CanvasGroup +
            // EnvironmentRoots) onto an already-built scene too — same
            // idempotent-pass pattern as the two calls above.
            Transform existingGameplayPanel = canvas.transform.Find("GameplayPanel");
            if (existingGameplayPanel != null)
                WireVictoryTransition(gm, mixer, existingGameplayPanel);

            // Backfill the 3 HSV sliders too if they're missing (e.g. lost to
            // a scene-repair pass) — same idempotent-pass pattern.
            if (existingGameplayPanel != null)
                RebuildSlidersIfMissing(gm, mixer, canvas.transform, existingGameplayPanel);

            if (existingGameplayPanel != null)
            {
                EditorUtility.DisplayDialog("Girgit", "Screens already added (GameplayPanel already exists).", "OK");
                return;
            }

            EnsureBorder($"{ArtDir}/bg_pannel.png", new Vector4(20, 20, 20, 20));
            EnsureBorder($"{ArtDir}/Mask group.png", new Vector4(13, 0, 13, 0));
            Sprite bgPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/bg_pannel.png");
            Sprite maskGroupSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Mask group.png");
            Sprite ellipseSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Ellipse.png");

            var res = new DefaultControls.Resources();

            // 1) Wrap everything currently in the canvas into a GameplayPanel.
            var gameplayPanel = new GameObject("GameplayPanel", typeof(RectTransform));
            gameplayPanel.transform.SetParent(canvas.transform, false);
            StretchFull(gameplayPanel.GetComponent<RectTransform>());

            var existing = new List<Transform>();
            foreach (Transform child in canvas.transform)
                if (child != gameplayPanel.transform) existing.Add(child);
            foreach (var child in existing)
                child.SetParent(gameplayPanel.transform, true);
            gameplayPanel.transform.SetAsFirstSibling();

            RemoveSliderLetterLabels(gameplayPanel.transform);
            RestyleTimerCluster(gm, gameplayPanel.transform, res, font);
            BuildSliderPanel(gameplayPanel.transform, mixer, bgPanelSprite, maskGroupSprite, ellipseSprite);

            // 2) Splash + Menu screens.
            GameObject splash = BuildSplashPanel(canvas.transform, res, font);
            GameObject menu = BuildMenuPanel(canvas.transform, res, font, out Button singleBtn, out Button multiBtn);

            // 3) Screen flow wiring.
            var flow = canvas.gameObject.AddComponent<ScreenFlow>();
            flow.SplashPanel = splash;
            flow.MenuPanel = menu;
            flow.GameplayPanel = gameplayPanel;
            flow.Game = gm;
            UnityEventTools.AddPersistentListener(singleBtn.onClick, new UnityAction(flow.PlaySinglePlayer));
            multiBtn.interactable = false;

            // 4) Victory-transition wiring — HudGroup CanvasGroup (for fading
            // the in-round HUD) + EnvironmentRoots (parallax layers to fade
            // with the player). Must run before the panels get hidden below,
            // since it reparents based on gm.GameOverPanel/ChallengeCompletePanel/
            // WinPanel identity, not visibility.
            WireVictoryTransition(gm, mixer, gameplayPanel.transform);

            gameplayPanel.SetActive(false);
            menu.SetActive(false);
            splash.SetActive(true);

            // 5) Every piece of text (gameplay HUD + splash + menu) gets Jaro's
            // optical-size instance closest to its own font size, not one
            // instance stretched across every size.
            ApplyOpticalSizeFonts(canvas.transform);

            EditorUtility.SetDirty(canvas.gameObject);
            EditorUtility.SetDirty(gm);
            EditorUtility.SetDirty(mixer);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Girgit",
                "Splash + Menu screens added, HUD restyled (all TMP).\nSave the scene (Ctrl+S) and press Play.", "OK");
        }

        // --------------------------------------------------------- legacy sweep

        static void ConvertLegacyTextAndRewire(Canvas canvas, GameManager gm, TMP_FontAsset font)
        {
            var legacyTexts = new List<Text>(canvas.GetComponentsInChildren<Text>(true));
            foreach (var legacy in legacyTexts)
            {
                RectTransform rt = legacy.rectTransform;
                bool isTopRow = Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f);
                bool isLeft = isTopRow && Mathf.Approximately(rt.anchorMin.x, 0f);
                bool isRight = isTopRow && Mathf.Approximately(rt.anchorMin.x, 1f);
                bool isCenter = isTopRow && Mathf.Approximately(rt.anchorMin.x, 0.5f);

                var tmp = GirgitSceneBuilder.ConvertTextToTMP(legacy, font);

                if (isLeft) gm.LevelText = tmp;
                else if (isRight) gm.TimerText = tmp;
                else if (isCenter) gm.MatchText = tmp;
            }
        }

        // ------------------------------------------------------------- gameplay HUD

        static void RemoveSliderLetterLabels(Transform root)
        {
            var toRemove = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.text == "H" || t.text == "S" || t.text == "V")
                    toRemove.Add(t.gameObject);
            foreach (var go in toRemove) Object.DestroyImmediate(go);
        }

        static void RestyleTimerCluster(GameManager gm, Transform gameplayPanel, DefaultControls.Resources res, TMP_FontAsset font)
        {
            if (gm.LevelText != null) gm.LevelText.gameObject.SetActive(false);
            if (gm.MatchText != null) gm.MatchText.gameObject.SetActive(false);

            if (gm.TimerText != null)
            {
                var t = gm.TimerText;
                t.fontSize = 140;
                t.alignment = TextAlignmentOptions.Center;
                t.color = BrandGreen;
                t.text = "--";
                GirgitSceneBuilder.Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(420, 180));
            }

            var label = GirgitSceneBuilder.MakeText(res, font, gameplayPanel, "SURVIVE TIME", 38, TextAnchor.MiddleCenter);
            label.color = Color.white;
            GirgitSceneBuilder.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(600, 60));

            var unit = GirgitSceneBuilder.MakeText(res, font, gameplayPanel, "SEC", 34, TextAnchor.MiddleCenter);
            unit.color = Color.white;
            GirgitSceneBuilder.Anchor(unit.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(150, -270), new Vector2(120, 60));
        }

        static void BuildSliderPanel(Transform gameplayPanel, ColorMixerUI mixer, Sprite bgPanelSprite,
                                     Sprite maskGroupSprite, Sprite ellipseSprite)
        {
            var panelGO = new GameObject("SliderPanel", typeof(RectTransform));
            panelGO.transform.SetParent(gameplayPanel, false);
            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 40);
            rt.sizeDelta = new Vector2(760, 320);

            var img = panelGO.AddComponent<Image>();
            img.sprite = bgPanelSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            Image hueBg = RestyleSlider(mixer.Hue, panelGO.transform, new Vector2(0, 110), maskGroupSprite, ellipseSprite);
            Image satBg = RestyleSlider(mixer.Saturation, panelGO.transform, new Vector2(0, 40), null, ellipseSprite);
            Image valBg = RestyleSlider(mixer.Value, panelGO.transform, new Vector2(0, -30), null, ellipseSprite);

            mixer.SaturationTrack = satBg;
            mixer.ValueTrack = valBg;
        }

        static Image RestyleSlider(Slider slider, Transform newParent, Vector2 anchoredPos, Sprite bgSprite, Sprite handleSprite)
        {
            slider.transform.SetParent(newParent, false);
            var rt = slider.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(660, 40);

            Image bg = null;
            Transform bgT = slider.transform.Find("Background");
            if (bgT != null)
            {
                bg = bgT.GetComponent<Image>();
                if (bg != null)
                {
                    if (bgSprite != null) bg.sprite = bgSprite;
                    bg.type = Image.Type.Sliced;
                    bg.color = Color.white;
                }
            }

            if (slider.fillRect != null) slider.fillRect.gameObject.SetActive(false);

            if (slider.handleRect != null)
            {
                var handle = slider.handleRect.GetComponent<Image>();
                if (handle != null)
                {
                    handle.sprite = handleSprite;
                    handle.color = Color.white;
                }
            }

            return bg;
        }

        /// <summary>
        /// If ColorMixerUI lost its Hue/Saturation/Value slider references
        /// (e.g. a scene-repair pass destroyed an empty duplicate "SliderPanel"
        /// that turned out to be the real one), rebuild the 3 sliders from
        /// scratch and re-style/re-parent them exactly like a fresh build
        /// does. Also sweeps up any leftover empty "SliderPanel" shells so
        /// they don't linger as cruft. No-ops if the sliders are already wired.
        /// </summary>
        internal static void RebuildSlidersIfMissing(GameManager gm, ColorMixerUI mixer, Transform canvasT, Transform gameplayPanel)
        {
            if (mixer.Hue != null && mixer.Saturation != null && mixer.Value != null) return;

            foreach (var panel in gameplayPanel.GetComponentsInChildren<Transform>(true))
            {
                if (panel.name != "SliderPanel" || panel == gameplayPanel) continue;
                if (panel.GetComponentsInChildren<Slider>(true).Length == 0)
                    Undo.DestroyObjectImmediate(panel.gameObject);
            }

            EnsureBorder($"{ArtDir}/bg_pannel.png", new Vector4(20, 20, 20, 20));
            EnsureBorder($"{ArtDir}/Mask group.png", new Vector4(13, 0, 13, 0));
            Sprite bgPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/bg_pannel.png");
            Sprite maskGroupSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Mask group.png");
            Sprite ellipseSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/Ellipse.png");

            var res = new DefaultControls.Resources();
            var sliderSize = new Vector2(700, 34);
            Slider hue = GirgitSceneBuilder.MakeSlider(res, canvasT, "HueSlider", sliderSize, new Vector2(20, 170));
            Slider sat = GirgitSceneBuilder.MakeSlider(res, canvasT, "SatSlider", sliderSize, new Vector2(20, 110));
            Slider val = GirgitSceneBuilder.MakeSlider(res, canvasT, "ValSlider", sliderSize, new Vector2(20, 50));
            hue.value = 0f; sat.value = 0.8f; val.value = 0.8f;

            mixer.Hue = hue; mixer.Saturation = sat; mixer.Value = val;

            BuildSliderPanel(gameplayPanel, mixer, bgPanelSprite, maskGroupSprite, ellipseSprite);

            // Match the nested-in-HudGroup layout WireVictoryTransition would
            // have produced, since that pass already ran and won't run again.
            if (gm.HudFade != null)
            {
                Transform newPanel = gameplayPanel.Find("SliderPanel");
                if (newPanel != null)
                {
                    newPanel.SetParent(gm.HudFade.transform, false);
                    newPanel.SetAsFirstSibling();
                }
            }
        }

        /// <summary>
        /// Wires GameManager.HudFade (a new CanvasGroup wrapping everything
        /// under gameplayPanel that ISN'T one of the 3 end-of-round panels —
        /// sliders, timer/match/level text) and GameManager.EnvironmentRoots
        /// (Parallax_Back / Parallax_Front) so the victory-transition sequence
        /// has something to fade. Idempotent — skips whichever half is already
        /// wired, so it's safe to call on every AddScreens() run.
        /// </summary>
        static void WireVictoryTransition(GameManager gm, ColorMixerUI mixer, Transform gameplayPanel)
        {
            if (gm.HudFade == null)
            {
                var hudGroupGO = new GameObject("HudGroup", typeof(RectTransform));
                hudGroupGO.transform.SetParent(gameplayPanel, false);
                StretchFull(hudGroupGO.GetComponent<RectTransform>());
                hudGroupGO.transform.SetAsFirstSibling(); // stay behind the 3 end panels

                var toMove = new List<Transform>();
                foreach (Transform child in gameplayPanel)
                {
                    if (child == hudGroupGO.transform) continue;
                    if (gm.GameOverPanel != null && child.gameObject == gm.GameOverPanel) continue;
                    if (gm.ChallengeCompletePanel != null && child.gameObject == gm.ChallengeCompletePanel) continue;
                    if (gm.WinPanel != null && child.gameObject == gm.WinPanel) continue;
                    toMove.Add(child);
                }
                foreach (var child in toMove)
                    child.SetParent(hudGroupGO.transform, true);

                gm.HudFade = hudGroupGO.AddComponent<CanvasGroup>();
            }

            if (gm.EnvironmentRoots == null || gm.EnvironmentRoots.Length == 0)
            {
                var roots = new List<Transform>();
                GameObject back = GameObject.Find("Parallax_Back");
                GameObject front = GameObject.Find("Parallax_Front");
                if (back != null) roots.Add(back.transform);
                if (front != null) roots.Add(front.transform);
                gm.EnvironmentRoots = roots.ToArray();
            }
        }

        static void ApplyOpticalSizeFonts(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var f = GirgitFonts.ForSize(t.fontSize);
                if (f != null) t.font = f;
            }
        }

        // -------------------------------------------------------------- splash/menu

        static GameObject BuildSplashPanel(Transform canvasT, DefaultControls.Resources res, TMP_FontAsset font)
        {
            var panel = new GameObject("SplashPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasT, false);
            StretchFull(panel.GetComponent<RectTransform>());
            panel.AddComponent<Image>().color = BrandPurple;

            var logo = GirgitSceneBuilder.MakeText(res, font, panel.transform, "GIRGIT", 110, TextAnchor.MiddleCenter);
            logo.color = BrandYellow;
            GirgitSceneBuilder.Anchor(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 160));

            var sub = GirgitSceneBuilder.MakeText(res, font, panel.transform, "COLOR MATCHER", 30, TextAnchor.MiddleCenter);
            sub.color = Color.white;
            GirgitSceneBuilder.Anchor(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(700, 60));

            return panel;
        }

        static GameObject BuildMenuPanel(Transform canvasT, DefaultControls.Resources res, TMP_FontAsset font,
                                         out Button singleBtn, out Button multiBtn)
        {
            var panel = new GameObject("MenuPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasT, false);
            StretchFull(panel.GetComponent<RectTransform>());
            panel.AddComponent<Image>().color = BrandPurple;

            var logo = GirgitSceneBuilder.MakeText(res, font, panel.transform, "GIRGIT", 80, TextAnchor.MiddleCenter);
            logo.color = BrandGreen;
            GirgitSceneBuilder.Anchor(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -140), new Vector2(700, 120));

            var sub = GirgitSceneBuilder.MakeText(res, font, panel.transform, "COLOR MATCHER", 24, TextAnchor.MiddleCenter);
            sub.color = BrandGreen;
            GirgitSceneBuilder.Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -215), new Vector2(700, 50));

            singleBtn = MakeTextButton(res, font, panel.transform, "SINGLE PLAYER", 46, BrandGreen, new Vector2(0, 40));
            multiBtn = MakeTextButton(res, font, panel.transform, "MULTI PLAYER", 46, new Color(0.55f, 0.55f, 0.55f), new Vector2(0, -60));

            return panel;
        }

        static Button MakeTextButton(DefaultControls.Resources res, TMP_FontAsset font, Transform parent,
                                     string label, int size, Color color, Vector2 anchoredPos)
        {
            GameObject btnGO = DefaultControls.CreateButton(res);
            btnGO.name = label.Replace(" ", "") + "Button";
            btnGO.transform.SetParent(parent, false);
            GirgitSceneBuilder.Anchor(btnGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(600, 80));

            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);

            GirgitSceneBuilder.MakeButtonText(btnGO, font, label, size, color);

            return btnGO.GetComponent<Button>();
        }

        // ------------------------------------------------------------------- utils

        internal static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void EnsureBorder(string path, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null || imp.spriteBorder == border) return;
            imp.spriteBorder = border;
            imp.SaveAndReimport();
        }
    }
}
#endif

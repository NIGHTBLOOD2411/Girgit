#if UNITY_EDITOR
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
    /// Adds the pre-challenge countdown, the reworked no-eagle failure
    /// sequence (blackout -> GameOver1 title card -> GameOver2 Animator
    /// sprite-sheet -> stats popup with Retry + Main Menu), a Main Menu
    /// button on WinPanel too, and an AudioManager — on top of a scene
    /// already built via GirgitSceneBuilder + GirgitScreensBuilder. Idempotent
    /// — safe to re-run; only fills in whatever isn't wired yet.
    ///
    /// Menu: Girgit > Add Countdown + Game Over Rework + Audio.
    /// </summary>
    public static class GirgitGameplayUpgrade
    {
        [MenuItem("Girgit/Add Countdown + Game Over Rework + Audio")]
        public static void Upgrade()
        {
            var gm = Object.FindObjectOfType<GameManager>();
            var flow = Object.FindObjectOfType<ScreenFlow>();
            var canvas = Object.FindObjectOfType<Canvas>();
            if (gm == null || flow == null || canvas == null)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "Need an existing built game (GameManager / ScreenFlow / Canvas) first — run 'Build Game' then 'Add Splash + Menu Screens' before this.", "OK");
                return;
            }

            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "No GameplayPanel found — run 'Add Splash + Menu Screens' first.", "OK");
                return;
            }

            TMP_FontAsset font = GirgitFonts.Regular();
            if (font == null)
            {
                EditorUtility.DisplayDialog("Girgit", "Could not load/create the Jaro TMP font asset.", "OK");
                return;
            }
            var res = new DefaultControls.Resources();

            if (gm.Flow == null) gm.Flow = flow;

            EnsureDimOverlay(gm, gameplayPanel);
            EnsureCountdownText(gm, gameplayPanel, res, font);
            EnsureGameOver1(gm, gameplayPanel, res, font);
            EnsureGameOver2(gm, gameplayPanel);
            ReworkGameOverPanel(gm, font);
            AddMainMenuToWinPanel(gm, font);
            EnsureAudioManager();

            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Girgit",
                "Countdown, failure sequence (blackout -> GameOver1 -> GameOver2 -> popup), and AudioManager added.\n\n" +
                "GameOver2Panel's Animator has no Controller assigned yet — import your sprite-sheet, build an Animator Controller with one NON-LOOPING state animating the Image's sprite through the frames, and assign it there.\n\n" +
                "Also assign your own audio clips on the AudioManager component, then save the scene (Ctrl+S).",
                "OK");
        }

        /// <summary>Every overlay element (dim/countdown/GameOver1/GameOver2) is inserted right after HudGroup (index 0) — their order relative to EACH OTHER never matters since only one is ever visible at a time, they just all need to render above the world/HUD and stay below the end panels.</summary>
        static void EnsureCountdownText(GameManager gm, Transform gameplayPanel, DefaultControls.Resources res, TMP_FontAsset font)
        {
            if (gm.CountdownText != null) return;

            var countdown = GirgitSceneBuilder.MakeText(res, font, gameplayPanel, "3", 160, TextAnchor.MiddleCenter);
            countdown.color = Color.white;
            GirgitSceneBuilder.Anchor(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 220));
            countdown.transform.SetSiblingIndex(1);
            countdown.gameObject.SetActive(false);

            gm.CountdownText = countdown;
        }

        static void EnsureDimOverlay(GameManager gm, Transform gameplayPanel)
        {
            if (gm.DimOverlay != null) return;

            var dimGO = new GameObject("DimOverlay", typeof(RectTransform));
            dimGO.transform.SetParent(gameplayPanel, false);
            GirgitScreensBuilder.StretchFull(dimGO.GetComponent<RectTransform>());
            dimGO.transform.SetSiblingIndex(1);

            var img = dimGO.AddComponent<Image>();
            img.color = Color.black;

            var group = dimGO.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            gm.DimOverlay = group;
        }

        /// <summary>GameOver1: a simple "GAME OVER" title card that fades in, holds, fades out over the black backdrop.</summary>
        static void EnsureGameOver1(GameManager gm, Transform gameplayPanel, DefaultControls.Resources res, TMP_FontAsset font)
        {
            if (gm.GameOver1Fade != null) return;

            var go = new GameObject("GameOver1", typeof(RectTransform));
            go.transform.SetParent(gameplayPanel, false);
            GirgitScreensBuilder.StretchFull(go.GetComponent<RectTransform>());
            go.transform.SetSiblingIndex(1);

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var title = GirgitSceneBuilder.MakeText(res, font, go.transform, "GAME OVER", 90, TextAnchor.MiddleCenter);
            title.color = Color.white;
            GirgitSceneBuilder.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 200));

            gm.GameOver1Fade = group;
        }

        /// <summary>
        /// GameOver2: a full-screen Image + Animator for your sprite-sheet
        /// sequence. The Animator Controller is left unassigned — build one
        /// with a single non-looping state that keyframes the Image's sprite
        /// through your frames, then drag it onto this Animator.
        /// </summary>
        static void EnsureGameOver2(GameManager gm, Transform gameplayPanel)
        {
            if (gm.GameOver2Panel != null) return;

            var go = new GameObject("GameOver2", typeof(RectTransform));
            go.transform.SetParent(gameplayPanel, false);
            GirgitScreensBuilder.StretchFull(go.GetComponent<RectTransform>());
            go.transform.SetSiblingIndex(1);

            var img = go.AddComponent<Image>();
            img.color = Color.white; // tint neutral; the sprite-sheet frames supply the actual art
            img.preserveAspect = true;

            var animator = go.AddComponent<Animator>(); // no Controller assigned — set up your sprite-sheet animation and assign it

            go.SetActive(false);

            gm.GameOver2Panel = go;
            gm.GameOver2Animator = animator;
        }

        /// <summary>Relabels the existing button "RETRY" (its Restart listener is already correctly wired — untouched) and adds a second "MAIN MENU" button + captures the panel's message text.</summary>
        static void ReworkGameOverPanel(GameManager gm, TMP_FontAsset font)
        {
            if (gm.GameOverPanel == null) return;

            if (gm.GameOverMessage == null)
                gm.GameOverMessage = FindPanelMessageText(gm.GameOverPanel);

            var buttons = gm.GameOverPanel.GetComponentsInChildren<Button>(true);
            if (buttons.Length >= 1)
            {
                var label = buttons[0].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "RETRY";
            }

            if (buttons.Length < 2)
            {
                var mainMenuBtn = AddSecondButton(gm.GameOverPanel, font, "MAIN MENU", new Vector2(0, -220));
                UnityEventTools.AddPersistentListener(mainMenuBtn.onClick, new UnityAction(gm.GoToMainMenu));
            }
        }

        static void AddMainMenuToWinPanel(GameManager gm, TMP_FontAsset font)
        {
            if (gm.WinPanel == null) return;
            if (gm.WinPanel.GetComponentsInChildren<Button>(true).Length >= 2) return;

            var mainMenuBtn = AddSecondButton(gm.WinPanel, font, "MAIN MENU", new Vector2(0, -220));
            UnityEventTools.AddPersistentListener(mainMenuBtn.onClick, new UnityAction(gm.GoToMainMenu));
        }

        internal static Button AddSecondButton(GameObject panel, TMP_FontAsset font, string label, Vector2 anchoredPos)
        {
            var res = new DefaultControls.Resources();
            var btnGO = DefaultControls.CreateButton(res);
            btnGO.name = label.Replace(" ", "") + "Button";
            btnGO.transform.SetParent(panel.transform, false);
            GirgitSceneBuilder.Anchor(btnGO.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(320, 90));
            GirgitSceneBuilder.MakeButtonText(btnGO, font, label, 36, Color.white);
            return btnGO.GetComponent<Button>();
        }

        /// <summary>The panel's own message text is a DIRECT child of the panel; each button's label is a child of the button instead.</summary>
        internal static TextMeshProUGUI FindPanelMessageText(GameObject panel)
        {
            foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.transform.parent == panel.transform) return t;
            return null;
        }

        static void EnsureAudioManager()
        {
            if (Object.FindObjectOfType<AudioManager>() != null) return;
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }
    }
}
#endif

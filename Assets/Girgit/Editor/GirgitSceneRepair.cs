#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
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
    /// One-shot repair for scene corruption caused by a build/upgrade menu
    /// command re-running its fresh-build path on top of an already-built
    /// scene (this happens if GameplayPanel briefly wasn't detectable as a
    /// direct child of the Canvas — e.g. during manual Hierarchy
    /// reorganization — so the "already exists?" check missed it).
    ///
    /// Removes: the duplicate ScreenFlow component stacked on HUD Canvas,
    /// root-level orphan buttons sitting outside any Canvas, duplicate
    /// SplashPanel/MenuPanel copies nested inside GameplayPanel, empty
    /// leftover "SliderPanel" shells (keeping whichever copy actually has
    /// the 3 Slider components), the vestigial Eagle object (Eagle.cs no
    /// longer exists), and a stray unreferenced "Game Over 1" duplicate.
    /// Rebuilds GameOverPanel/ChallengeCompletePanel from scratch if they're
    /// gutted/miswired (mirroring the known-good WinPanel structure), and
    /// rebuilds the 3 HSV sliders from scratch if ColorMixerUI lost its
    /// references to them entirely.
    ///
    /// Every removal goes through Undo.DestroyObjectImmediate, so Ctrl+Z
    /// immediately after running this can reverse it if something looks
    /// wrong.
    ///
    /// Menu: Girgit > Repair Scene Corruption (Duplicate ScreenFlow etc).
    /// </summary>
    public static class GirgitSceneRepair
    {
        [MenuItem("Girgit/Repair Scene Corruption (Duplicate ScreenFlow etc)")]
        public static void Repair()
        {
            var canvas = Object.FindObjectOfType<Canvas>(true);
            var gm = Object.FindObjectOfType<GameManager>();
            if (canvas == null || gm == null)
            {
                EditorUtility.DisplayDialog("Girgit", "No Canvas/GameManager found in this scene — nothing to repair.", "OK");
                return;
            }

            var log = new StringBuilder();

            RemoveDuplicateScreenFlow(canvas.gameObject, log);
            RemoveRootOrphanButtons(log);
            RemoveDuplicatesNestedInGameplayPanel(canvas, log);
            RemoveEmptySliderPanelDuplicates(canvas, log);
            RebuildGutteredEndPanels(gm, canvas, log);
            RemoveStrayGameOverObjects(canvas, log);
            RemoveEagle(log);
            RebuildSlidersIfMissing(gm, canvas, log);

            if (log.Length == 0)
                log.AppendLine("Nothing found to repair — scene already looks clean.");

            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Girgit",
                "Repair complete:\n\n" + log +
                "\nSave the scene (Ctrl+S). If anything looks wrong, Ctrl+Z should undo these changes (do it now, before saving).",
                "OK");
        }

        /// <summary>Keeps whichever ScreenFlow instance has a real GameplayPanel assigned; destroys any others on the same GameObject.</summary>
        static void RemoveDuplicateScreenFlow(GameObject canvasGO, StringBuilder log)
        {
            var flows = canvasGO.GetComponents<ScreenFlow>();
            if (flows.Length <= 1) return;

            ScreenFlow keep = null;
            foreach (var f in flows)
                if (f.GameplayPanel != null) { keep = f; break; }
            if (keep == null) keep = flows[0];

            foreach (var f in flows)
            {
                if (f == keep) continue;
                Undo.DestroyObjectImmediate(f);
                log.AppendLine("- Removed a duplicate ScreenFlow component from HUD Canvas (it had no GameplayPanel assigned — the one kept does).");
            }
        }

        static readonly string[] OrphanCandidateNames = { "SINGLEPLAYERButton", "MULTIPLAYERButton", "MAINMENUButton" };

        /// <summary>Root-level (no parent at all) copies of these buttons can never render or receive clicks — a UI Graphic needs a Canvas ancestor.</summary>
        static void RemoveRootOrphanButtons(StringBuilder log)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var toRemove = new List<GameObject>();
            foreach (var root in roots)
                foreach (var name in OrphanCandidateNames)
                    if (root.name == name) toRemove.Add(root);

            foreach (var go in toRemove)
            {
                string n = go.name;
                Undo.DestroyObjectImmediate(go);
                log.AppendLine($"- Removed root-level orphan '{n}' (sat outside any Canvas, could never render or receive clicks).");
            }
        }

        /// <summary>SplashPanel/MenuPanel should never be direct children of GameplayPanel by design — the real ones live elsewhere. Any found here are duplicates from a re-run. (SliderPanel is NOT included here — by design it legitimately IS built as a direct child of GameplayPanel by GirgitScreensBuilder.BuildSliderPanel; see RemoveEmptySliderPanelDuplicates for its dedup rule instead.)</summary>
        static void RemoveDuplicatesNestedInGameplayPanel(Canvas canvas, StringBuilder log)
        {
            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null) return;

            var toRemove = new List<Transform>();
            foreach (Transform child in gameplayPanel)
                if (child.name == "SplashPanel" || child.name == "MenuPanel")
                    toRemove.Add(child);

            foreach (var t in toRemove)
            {
                string n = t.name;
                Undo.DestroyObjectImmediate(t.gameObject);
                log.AppendLine($"- Removed duplicate '{n}' that had been nested inside GameplayPanel (the real one lives elsewhere in the hierarchy).");
            }
        }

        /// <summary>Unlike SplashPanel/MenuPanel, "SliderPanel" duplicates can't be told apart by parentage — the real one can legitimately sit either as a direct GameplayPanel child (right after BuildSliderPanel runs) or nested inside HudGroup (after WireVictoryTransition absorbs it). The real one is whichever copy actually contains Slider components; any other "SliderPanel" is an empty leftover shell.</summary>
        static void RemoveEmptySliderPanelDuplicates(Canvas canvas, StringBuilder log)
        {
            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null) return;

            var panels = new List<Transform>();
            foreach (var t in gameplayPanel.GetComponentsInChildren<Transform>(true))
                if (t.name == "SliderPanel") panels.Add(t);
            if (panels.Count <= 1) return;

            bool keptOne = false;
            foreach (var p in panels)
            {
                bool hasSliders = p.GetComponentsInChildren<Slider>(true).Length > 0;
                if (hasSliders && !keptOne) { keptOne = true; continue; }
                if (hasSliders) continue; // extremely unlikely (2 populated copies) — keep both rather than guess wrong
                Undo.DestroyObjectImmediate(p.gameObject);
                log.AppendLine("- Removed an empty duplicate 'SliderPanel' shell (no Slider components inside — the real one elsewhere still has its 3 sliders).");
            }
        }

        /// <summary>If GameOverPanel/ChallengeCompletePanel are missing their message text or don't have enough buttons, they're gutted/misassigned — destroy and rebuild fresh, mirroring the known-good WinPanel.</summary>
        static void RebuildGutteredEndPanels(GameManager gm, Canvas canvas, StringBuilder log)
        {
            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null) return;

            TMP_FontAsset font = GirgitFonts.Regular();
            if (font == null)
            {
                log.AppendLine("- Could NOT rebuild GameOverPanel/ChallengeCompletePanel: Jaro TMP font unavailable (run a Build/Add Screens command first to generate it).");
                return;
            }
            var res = new DefaultControls.Resources();

            bool gameOverOk = gm.GameOverPanel != null && gm.GameOverMessage != null
                && gm.GameOverPanel.GetComponentsInChildren<Button>(true).Length >= 2;
            if (!gameOverOk)
            {
                if (gm.GameOverPanel != null)
                {
                    string oldName = gm.GameOverPanel.name;
                    Undo.DestroyObjectImmediate(gm.GameOverPanel);
                    log.AppendLine($"- Removed gutted GameOverPanel target ('{oldName}' — missing its message text and/or a button).");
                }

                GameObject newGameOver = GirgitSceneBuilder.MakePanel(res, font, gameplayPanel,
                    "GAME OVER\n\nMatch: --%   Time: --s", "RETRY", out Button retryBtn, out TextMeshProUGUI overMsg);
                newGameOver.SetActive(false);
                UnityEventTools.AddPersistentListener(retryBtn.onClick, new UnityAction(gm.Restart));
                Button mainMenuBtn = GirgitGameplayUpgrade.AddSecondButton(newGameOver, font, "MAIN MENU", new Vector2(0, -220));
                UnityEventTools.AddPersistentListener(mainMenuBtn.onClick, new UnityAction(gm.GoToMainMenu));
                gm.GameOverPanel = newGameOver;
                gm.GameOverMessage = overMsg;
                log.AppendLine("- Rebuilt GameOverPanel fresh: dynamic message + RETRY + MAIN MENU, correctly wired.");
            }

            bool completeOk = gm.ChallengeCompletePanel != null && gm.ChallengeCompleteMessage != null
                && gm.ChallengeCompletePanel.GetComponentsInChildren<Button>(true).Length >= 1;
            if (!completeOk)
            {
                if (gm.ChallengeCompletePanel != null)
                {
                    string oldName = gm.ChallengeCompletePanel.name;
                    Undo.DestroyObjectImmediate(gm.ChallengeCompletePanel);
                    log.AppendLine($"- Removed gutted ChallengeCompletePanel target ('{oldName}' — missing its message text and/or Continue button).");
                }

                GameObject newComplete = GirgitSceneBuilder.MakePanel(res, font, gameplayPanel,
                    "CONGRATULATIONS!\nYou were matching --%!\nContinue for the next challenge.",
                    "CONTINUE", out Button continueBtn, out TextMeshProUGUI completeMsg);
                newComplete.SetActive(false);
                UnityEventTools.AddPersistentListener(continueBtn.onClick, new UnityAction(gm.ContinueToNext));
                gm.ChallengeCompletePanel = newComplete;
                gm.ChallengeCompleteMessage = completeMsg;
                log.AppendLine("- Rebuilt ChallengeCompletePanel fresh: dynamic message + CONTINUE, correctly wired.");
            }
        }

        /// <summary>"Game Over 1" (with a space) is a stray duplicate distinct from the wired "GameOver1" title card — not referenced by any GameManager field.</summary>
        static void RemoveStrayGameOverObjects(Canvas canvas, StringBuilder log)
        {
            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null) return;

            Transform stray = gameplayPanel.Find("Game Over 1");
            if (stray != null)
            {
                Undo.DestroyObjectImmediate(stray.gameObject);
                log.AppendLine("- Removed stray unreferenced 'Game Over 1' object (distinct from the wired 'GameOver1' title card — not pointed to by any field).");
            }
        }

        /// <summary>If ColorMixerUI's Hue/Saturation/Value slider references are null (e.g. an earlier repair run destroyed the wrong "SliderPanel" copy), rebuild the 3 sliders fresh via the same path a first-time build uses.</summary>
        static void RebuildSlidersIfMissing(GameManager gm, Canvas canvas, StringBuilder log)
        {
            var mixer = Object.FindObjectOfType<ColorMixerUI>();
            if (mixer == null) return;
            if (mixer.Hue != null && mixer.Saturation != null && mixer.Value != null) return;

            Transform gameplayPanel = canvas.transform.Find("GameplayPanel");
            if (gameplayPanel == null) return;

            GirgitScreensBuilder.RebuildSlidersIfMissing(gm, mixer, canvas.transform, gameplayPanel);
            log.AppendLine("- Rebuilt the 3 HSV mixer sliders (Hue/Saturation/Value) — ColorMixerUI had lost its references to them.");
        }

        /// <summary>Eagle.cs no longer exists in the project; this GameObject is inactive vestige with a missing-script component.</summary>
        static void RemoveEagle(StringBuilder log)
        {
            GameObject girgitGameRoot = GameObject.Find("Girgit Game");
            if (girgitGameRoot == null) return;

            Transform eagle = girgitGameRoot.transform.Find("Eagle");
            if (eagle == null) return;

            Undo.DestroyObjectImmediate(eagle.gameObject);
            log.AppendLine("- Removed the leftover 'Eagle' GameObject (Eagle.cs was deleted from the project; it had a missing-script component).");
        }
    }
}
#endif

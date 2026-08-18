using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Girgit
{
    /// <summary>
    /// The whole game, controlled from this one Inspector. Define the ordered
    /// list of Levels; each Level is 4-5 Challenges (a color + time limit +
    /// required match) cleared in order — make later Levels harder by giving
    /// their Challenges less TimeLimit and/or a higher RequiredMatch.
    ///
    /// Flow: every challenge (including the very first) opens with a
    /// "3, 2, 1, GO!" countdown before the timer starts, and the HSV sliders
    /// are jumped to a random position first — so the player can never coast
    /// in on a leftover slider value from the previous round.
    ///
    /// Reach a challenge's required match and it PAUSES — gameplay fades out,
    /// the ChallengeCompletePanel congratulates the player and shows the
    /// match % they hit, holds for MatchHoldDuration (or until "Continue" is
    /// pressed), then the ground LERPS to the next challenge's color and
    /// everything fades back in before the next countdown. Clearing the FINAL
    /// challenge of the FINAL level shows WinPanel instead — no next round to
    /// transition into.
    ///
    /// Running out of time before matching: the whole gameplay view fades to
    /// black (DimOverlay), a GameOver1 title card fades in/holds/fades out,
    /// then GameOver2's Animator-driven sprite-sheet plays once, and finally
    /// GameOverPanel appears showing the match % reached and that challenge's
    /// time limit, with Retry and Main Menu buttons.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("LEVELS — edit the whole game here")]
        [Tooltip("Each level is a sequence of 4-5 Challenges (colors) cleared in order.")]
        public List<LevelDefinition> Levels = new List<LevelDefinition>();

        [Header("Scene references")]
        public Chameleon Player;
        public SpriteRenderer Ground;      // tinted to the current challenge's color
        public ColorMixerUI Mixer;
        public ScreenFlow Flow;            // for the "Main Menu" buttons

        [Header("HUD references")]
        public TextMeshProUGUI LevelText;      // e.g. "LEVEL 2  •  CHALLENGE 3/5"
        public TextMeshProUGUI TimerText;
        public TextMeshProUGUI MatchText;
        public TextMeshProUGUI CountdownText;  // "3", "2", "1", "GO!" before each challenge
        public GameObject matchStartPanel;
        bool hasShownMatchStart = false;

        [Header("Timer display")]
        [Tooltip("Timer text color while time remaining is above both thresholds below.")]
        public Color TimerNormalColor = new Color(0.043f, 0.902f, 0.463f); // matches the editor tool's BrandGreen
        [Tooltip("Timer text color once the displayed seconds drop below TimerWarningThreshold.")]
        public Color TimerWarningColor = Color.yellow;
        [Tooltip("Seconds remaining below which the timer switches to TimerWarningColor.")]
        [Min(0f)] public float TimerWarningThreshold = 11f;
        [Tooltip("Timer text color once the displayed seconds drop below TimerDangerThreshold.")]
        public Color TimerDangerColor = Color.red;
        [Tooltip("Seconds remaining below which the timer switches to TimerDangerColor.")]
        [Min(0f)] public float TimerDangerThreshold = 5f;

        [Header("Failure sequence")]
        [Tooltip("Fades the whole gameplay view to black before GameOver1.")]
        public CanvasGroup DimOverlay;
        [Tooltip("Title-card panel (e.g. 'GAME OVER' text) — fades in, holds, fades out.")]
        public CanvasGroup GameOver1Fade;
        [Tooltip("Seconds GameOver1Fade holds fully visible before fading out.")]
        [Min(0f)] public float GameOver1HoldDuration = 1.5f;
        [Tooltip("Panel holding the Animator-driven sprite-sheet sequence — shown, played once, then hidden.")]
        public GameObject GameOver2Panel;
        [Tooltip("Animator driving GameOver2Panel's sprite-sheet — must NOT loop; polled for completion.")]
        public Animator GameOver2Animator;

        [Header("End-of-round panels")]
        public GameObject GameOverPanel;             // failure sequence complete -> Retry / Main Menu
        public TextMeshProUGUI GameOverMessage;      // dynamic "Match X%, Time Ys" text
        public GameObject ChallengeCompletePanel;    // success mid-game -> Continue -> next challenge
        public TextMeshProUGUI ChallengeCompleteMessage; // dynamic "You matched X%!" text
        public GameObject WinPanel;                  // success on the final challenge of the final level

        [Header("Round transition (on challenge success)")]
        [Tooltip("CanvasGroup wrapping the in-game HUD (sliders, timer/match text) — NOT the end-of-round panels. Faded out/in around the victory pause.")]
        public CanvasGroup HudFade;
        [Tooltip("Root(s) of decorative environment sprites (e.g. Parallax_Back, Parallax_Front) faded with the player.")]
        public Transform[] EnvironmentRoots;
        [Tooltip("Seconds for HUD/player/environment to fade out, and separately to fade back in (also used for the dim overlay on failure).")]
        [Min(0.01f)] public float FadeDuration = 0.5f;
        [Tooltip("Seconds the victory page stays up before auto-advancing (Continue button skips the remaining wait).")]
        [Min(0f)] public float PanelHoldDuration = 2.5f;
        [Tooltip("Seconds for the ground to lerp from the old challenge's color into the new one.")]
        [Min(0.01f)] public float GroundLerpDuration = 1f;

        [Header("Countdown")]
        [Tooltip("Seconds each countdown step (3/2/1/GO!) stays up before every challenge starts.")]
        [Min(0.1f)] public float CountdownStepDuration = 0.8f;

        [Header("Options")]
        [Tooltip("If on, reaching the required match at any moment pauses the challenge immediately. If off, you must be matched exactly when time runs out.")]
        public bool WinWhenThresholdReached = true;

        [Tooltip("Seconds the match must stay above RequiredMatch before it counts as cleared — prevents an accidental instant pass while still dragging the sliders.")]
        [Min(0f)] public float MatchHoldDuration = 0.4f;

        [Header("Match weighting (Hue/Saturation/Value)")]
        [Tooltip("How much Hue counts toward the match %.")]
        [Range(0f, 1f)] public float HueWeight = ColorMatch.DefaultHueWeight;
        [Tooltip("How much Saturation counts toward the match %.")]
        [Range(0f, 1f)] public float SaturationWeight = ColorMatch.DefaultSaturationWeight;
        [Tooltip("How much Value (brightness) counts toward the match %.")]
        [Range(0f, 1f)] public float ValueWeight = ColorMatch.DefaultValueWeight;

        int _levelIndex;
        int _challengeIndex;
        float _timeLeft;
        bool _playing;
        float _lastMatch;
        float _matchHoldTimer;
        bool _skipRequested;
        SpriteRenderer[] _environmentSprites;
        float[] _environmentBaseAlpha; // each sprite's authored alpha (e.g. dots .9, bars .35), captured once

        void Awake()
        {
            Instance = this;

            var sprites = new List<SpriteRenderer>();
            if (EnvironmentRoots != null)
                foreach (var root in EnvironmentRoots)
                    if (root != null) sprites.AddRange(root.GetComponentsInChildren<SpriteRenderer>(true));
            _environmentSprites = sprites.ToArray();

            _environmentBaseAlpha = new float[_environmentSprites.Length];
            for (int i = 0; i < _environmentSprites.Length; i++)
                _environmentBaseAlpha[i] = _environmentSprites[i] != null ? _environmentSprites[i].color.a : 1f;
        }

        /// <summary>
        /// Soft restart — no scene reload needed. Wired to the Restart buttons,
        /// and to ScreenFlow.PlaySinglePlayer (gameplay only begins once the
        /// player presses Single Player, not automatically on scene load). No
        /// fade ceremony — a clean, instant reset.
        /// </summary>
        public void ResetGame()
        {
            StopAllCoroutines();
            if (GameOverPanel != null) GameOverPanel.SetActive(false);

            if (ChallengeCompletePanel != null) ChallengeCompletePanel.SetActive(false);

            if (WinPanel != null) WinPanel.SetActive(false);

            if (matchStartPanel != null) matchStartPanel.SetActive(false);

            if (GameOver2Panel != null) GameOver2Panel.SetActive(false);

            if (Player != null) Player.gameObject.SetActive(true);

            // In case a previous run was mid-fade/mid-sequence when interrupted, snap everything back.
            ApplyFadeAlpha(Player != null ? Player.GetComponent<SpriteRenderer>() : null, 1f);

            if (HudFade != null) { HudFade.interactable = true; HudFade.blocksRaycasts = true; }

            if (DimOverlay != null) { DimOverlay.alpha = 0f; DimOverlay.blocksRaycasts = false; }

            if (GameOver1Fade != null) { GameOver1Fade.alpha = 0f; GameOver1Fade.blocksRaycasts = false; }

            _levelIndex = 0;
            _challengeIndex = 0;
            LoadChallenge();
        }

        /// <summary>Wired to the ChallengeCompletePanel's "Continue" button — skips the rest of the hold wait.</summary>
        public void ContinueToNext()
        {
            AudioManager.Instance?.PlayButtonTap();
            _skipRequested = true;
        }

        /// <summary>Button hook for GameOverPanel / WinPanel's Retry button.</summary>
        public void Restart()
        {
            AudioManager.Instance?.PlayButtonTap();
            ResetGame();
        }

        /// <summary>Button hook for GameOverPanel / WinPanel's Main Menu button.</summary>
        public void GoToMainMenu()
        {
            AudioManager.Instance?.PlayButtonTap();
            StopAllCoroutines();
            if (GameOverPanel != null) GameOverPanel.SetActive(false);
            if (WinPanel != null) WinPanel.SetActive(false);
            if (Flow != null) Flow.BackToMenu();
        }

        void LoadChallenge()
        {
            if (!HasChallenges())
            {
                _playing = false;
                if (LevelText != null) LevelText.text = "NO LEVELS SET";
                return;
            }

            _levelIndex = Mathf.Clamp(_levelIndex, 0, Levels.Count - 1);
            LevelDefinition level = Levels[_levelIndex];

            if (level.Challenges == null || level.Challenges.Count == 0)
            {
                _playing = false;
                if (LevelText != null) LevelText.text = $"LEVEL {_levelIndex + 1} HAS NO CHALLENGES";
                return;
            }

            _challengeIndex = Mathf.Clamp(_challengeIndex, 0, level.Challenges.Count - 1);
            ChallengeDefinition ch = level.Challenges[_challengeIndex];

            if (Ground != null) Ground.color = ch.Color; // instant — no previous challenge to lerp from
            BeginChallengeTimer(ch, level);
            if (Mixer != null) Mixer.RandomizeSliders();

            if (!hasShownMatchStart)
            {
                hasShownMatchStart = true;
                StartCoroutine(BeginPlayAfterCountdown());
            }
            else
            {
                // Countdown only ever plays once; every later LoadChallenge()
                // call (Restart, replay from menu) must still resume gameplay
                // itself, since skipping the coroutine above used to also skip
                // the _playing = true it sets — leaving the game stuck forever.
                _playing = true;
            }
        }

        void BeginChallengeTimer(ChallengeDefinition ch, LevelDefinition level)
        {
            _timeLeft = ch.TimeLimit;
            _matchHoldTimer = 0f;

            if (TimerText != null)
            {
                TimerText.text = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft)).ToString("00");
                TimerText.color = TimerNormalColor;
            }

            if (LevelText != null)
            {
                string levelName = string.IsNullOrEmpty(level.Name) ? $"LEVEL {_levelIndex + 1}" : level.Name;
                LevelText.text = $"{levelName}  •  CHALLENGE {_challengeIndex + 1}/{level.Challenges.Count}";
            }
        }

        bool HasChallenges()
        {
            if (Levels == null || Levels.Count == 0) return false;
            foreach (var lv in Levels)
                if (lv.Challenges != null && lv.Challenges.Count > 0) return true;
            return false;
        }

        void Update()
        {
            if (!_playing || !HasChallenges()) return;
            ChallengeDefinition ch = Levels[_levelIndex].Challenges[_challengeIndex];

            float match = Player != null
                ? ColorMatch.Percent(Player.CurrentColor, ch.Color, HueWeight, SaturationWeight, ValueWeight)
                : 00f;
            bool matched = match >= ch.RequiredMatch;
            _lastMatch = match;

            _matchHoldTimer = matched ? _matchHoldTimer + Time.deltaTime : 00f;

            if (MatchText != null)
            {
                MatchText.text = $"MATCH  {match:00}%";
                MatchText.color = matched ? Color.green : Color.white;
            }

            _timeLeft -= Time.deltaTime;
            if (TimerText != null)
            {
                int displaySeconds = Mathf.CeilToInt(Mathf.Max(0f, _timeLeft));
                TimerText.text = displaySeconds.ToString("00");
                TimerText.color = displaySeconds < TimerDangerThreshold ? TimerDangerColor
                    : displaySeconds < TimerWarningThreshold ? TimerWarningColor
                    : TimerNormalColor;
            }

            if (WinWhenThresholdReached && matched && _matchHoldTimer >= MatchHoldDuration)
            {
                Succeed();
                return;
            }

            if (_timeLeft <= 0f)
            {
                if (matched) Succeed();
                else Fail();
            }
        }

        void Succeed()
        {
            _playing = false;
            AudioManager.Instance?.PlayVictory();
            bool isLastChallengeOfLevel = _challengeIndex + 1 >= Levels[_levelIndex].Challenges.Count;
            bool isLastLevel = _levelIndex + 1 >= Levels.Count;

            if (isLastChallengeOfLevel && isLastLevel)
            {
                // The whole game is complete — no next round to transition into.
                if (WinPanel != null) WinPanel.SetActive(true);
            }
            else
            {
                StartCoroutine(ChallengeCompleteSequence());
            }
        }

        IEnumerator ChallengeCompleteSequence()
        {
            yield return Fade(1f, 0f);

            if (ChallengeCompleteMessage != null)
                ChallengeCompleteMessage.text = $"CONGRATULATIONS!\nYou were matching {_lastMatch:0}%!\nContinue for the next challenge.";
            if (ChallengeCompletePanel != null) ChallengeCompletePanel.SetActive(true);

            _skipRequested = false;
            float held = 0f;
            while (held < PanelHoldDuration && !_skipRequested)
            {
                held += Time.deltaTime;
                yield return null;
            }

            if (ChallengeCompletePanel != null) ChallengeCompletePanel.SetActive(false);

            LevelDefinition curLevel = Levels[_levelIndex];
            if (_challengeIndex + 1 < curLevel.Challenges.Count) _challengeIndex++;
            else { _levelIndex++; _challengeIndex = 0; }

            LevelDefinition newLevel = Levels[_levelIndex];
            ChallengeDefinition newCh = newLevel.Challenges[_challengeIndex];

            if (Ground != null)
                yield return LerpGroundColor(Ground.color, newCh.Color, GroundLerpDuration);

            BeginChallengeTimer(newCh, newLevel);
            if (Mixer != null) Mixer.RandomizeSliders();

            yield return Fade(0f, 1f);

            if (!hasShownMatchStart)
            {
                hasShownMatchStart = true;
                yield return Countdown();
            }

            _playing = true;
        }

        IEnumerator BeginPlayAfterCountdown()
        {
            yield return Countdown();
            _playing = true;
        }

        /// <summary>Plays "3", "2", "1", "GO!" on CountdownText, one step every CountdownStepDuration.</summary>
        IEnumerator Countdown()
        {
            if (matchStartPanel == null) yield break;

            if (CountdownText == null) yield break;

            if (matchStartPanel != null) matchStartPanel.SetActive(true);

            string[] steps = { "3", "2", "1", "GO!" };
            foreach (var step in steps)
            {
                CountdownText.text = step;
                if (step == "GO!") AudioManager.Instance?.PlayCountdownGo();
                else AudioManager.Instance?.PlayCountdownTick();

                float t = 0f;
                while (t < CountdownStepDuration)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            if (matchStartPanel != null) matchStartPanel.SetActive(false);
        }

        IEnumerator LerpGroundColor(Color from, Color to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Ground.color = Color.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            Ground.color = to;
        }

        /// <summary>
        /// Fades the HUD (via CanvasGroup), the player, and the environment
        /// sprites together, from one alpha multiplier to another, over
        /// FadeDuration. Each sprite's own authored alpha (e.g. the parallax
        /// dots/bars are semi-transparent by design) is preserved — this
        /// scales relative to it rather than forcing full opacity.
        /// </summary>
        IEnumerator Fade(float from, float to)
        {
            var playerSR = Player != null ? Player.GetComponent<SpriteRenderer>() : null;

            if (HudFade != null) { HudFade.interactable = false; HudFade.blocksRaycasts = false; }

            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.deltaTime;
                ApplyFadeAlpha(playerSR, Mathf.Lerp(from, to, Mathf.Clamp01(t / FadeDuration)));
                yield return null;
            }

            ApplyFadeAlpha(playerSR, to);

            if (HudFade != null && to >= 0.999f) { HudFade.interactable = true; HudFade.blocksRaycasts = true; }
        }

        /// <summary>
        /// Sets HUD/player/environment to a given alpha multiplier m (0..1).
        /// The player's mixed color is always fully opaque, so m alone is its
        /// target alpha; each environment sprite scales relative to its OWN
        /// authored alpha (captured once in Awake), not forced to full opacity.
        /// </summary>
        void ApplyFadeAlpha(SpriteRenderer playerSR, float m)
        {
            if (HudFade != null) HudFade.alpha = m;

            if (playerSR != null)
            {
                var c = playerSR.color;
                c.a = m;
                playerSR.color = c;
            }

            for (int i = 0; i < _environmentSprites.Length; i++)
            {
                var sr = _environmentSprites[i];
                if (sr == null) continue;
                var c = sr.color;
                c.a = _environmentBaseAlpha[i] * m;
                sr.color = c;
            }
        }

        void Fail()
        {
            _playing = false;
            StartCoroutine(FailSequence());
        }

        /// <summary>
        /// Blackout -> GameOver1 title card (fade in / hold / fade out) ->
        /// GameOver2's Animator-driven sprite-sheet (played once, polled for
        /// completion) -> the final stats popup (GameOverPanel).
        /// </summary>
        IEnumerator FailSequence()
        {
            yield return FadeCanvasGroup(DimOverlay, 0f, 1f);

            AudioManager.Instance?.PlayGameOver1();
            yield return FadeCanvasGroup(GameOver1Fade, 0f, 1f);
            if (GameOver1Fade != null) yield return new WaitForSeconds(GameOver1HoldDuration);
            yield return FadeCanvasGroup(GameOver1Fade, 1f, 0f);

            if (GameOver2Panel != null) GameOver2Panel.SetActive(true);
            AudioManager.Instance?.PlayGameOverAnimation();
            yield return PlayGameOver2Animation();
            if (GameOver2Panel != null) GameOver2Panel.SetActive(false);

            if (DimOverlay != null) { DimOverlay.alpha = 0f; DimOverlay.blocksRaycasts = false; }
            ShowGameOver();
        }

        /// <summary>Fades a CanvasGroup's alpha over FadeDuration. No-ops gracefully if group is null.</summary>
        IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to)
        {
            if (group == null) yield break;

            group.blocksRaycasts = to > 0.01f;
            float t = 0f;
            while (t < FadeDuration)
            {
                t += Time.deltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / FadeDuration));
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>
        /// Plays GameOver2Animator's current state from the start and waits
        /// for one full playthrough (normalizedTime reaching 1) — works
        /// whether or not the clip is set to loop, since that's still the
        /// first-completion point either way.
        /// </summary>
        IEnumerator PlayGameOver2Animation()
        {
            if (GameOver2Animator == null) yield break;

            GameOver2Animator.Play(0, 0, 0f);
            yield return null; // let the Animator update once so state info reflects the new state

            while (GameOver2Animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f
                   || GameOver2Animator.IsInTransition(0))
            {
                yield return null;
            }
        }

        void ShowGameOver()
        {
            if (GameOverMessage != null)
            {
                float timeLimit = 0f;
                if (HasChallenges())
                {
                    var level = Levels[Mathf.Clamp(_levelIndex, 0, Levels.Count - 1)];
                    if (level.Challenges != null && level.Challenges.Count > 0)
                        timeLimit = level.Challenges[Mathf.Clamp(_challengeIndex, 0, level.Challenges.Count - 1)].TimeLimit;
                }
                GameOverMessage.text = $"GAME OVER\n\nMatch: {_lastMatch:0}%   Time: {timeLimit:0}s";
            }
            if (GameOverPanel != null) GameOverPanel.SetActive(true);
            AudioManager.Instance?.PlayGameOver();
        }
    }
}

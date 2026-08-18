using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// App-level screen flow: Splash -> Menu -> Gameplay, as three sibling
    /// panels in the same scene (no scene loads, matching the "everything in
    /// one scene" setup). The gameplay world keeps quietly running behind an
    /// opaque Splash/Menu panel; PlaySinglePlayer reveals it and (re)starts
    /// the level sequence from the GameManager.
    /// </summary>
    public class ScreenFlow : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject SplashPanel;
        public GameObject MenuPanel;
        public GameObject GameplayPanel;

        [Header("Splash")]
        [Tooltip("Seconds the splash screen stays up before the menu appears. 0 = wait for a tap/click instead.")]
        [Min(0f)] public float SplashDuration = 1.5f;

        public GameManager Game;

        float _splashTimer;
        bool _splashDone;

        void Start() => ShowOnly(SplashPanel);

        void Update()
        {
            if (_splashDone || SplashPanel == null || !SplashPanel.activeSelf) return;

            if (SplashDuration <= 0f)
            {
                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) GoToMenu();
                return;
            }

            _splashTimer += Time.deltaTime;
            if (_splashTimer >= SplashDuration) GoToMenu();
        }

        void GoToMenu()
        {
            _splashDone = true;
            ShowOnly(MenuPanel);
        }

        /// <summary>Wired to the Single Player button.</summary>
        public void PlaySinglePlayer()
        {
            AudioManager.Instance?.PlayButtonTap();
            ShowOnly(GameplayPanel);
            if (Game != null) Game.ResetGame();
        }

        /// <summary>Wired (via GameManager) to the "MAIN MENU" buttons on GameOverPanel/WinPanel.</summary>
        public void BackToMenu()
        {
            AudioManager.Instance?.PlayButtonTap();
            ShowOnly(MenuPanel);
        }

        void ShowOnly(GameObject panel)
        {
            if (SplashPanel != null) SplashPanel.SetActive(panel == SplashPanel);
            if (MenuPanel != null) MenuPanel.SetActive(panel == MenuPanel);
            if (GameplayPanel != null) GameplayPanel.SetActive(panel == GameplayPanel);

            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (panel == SplashPanel) audio.PlayMusic(audio.SplashMusic);
            else if (panel == MenuPanel) audio.PlayMusic(audio.MenuMusic);
            else if (panel == GameplayPanel) audio.PlayMusic(audio.GameplayMusic);
        }
    }
}

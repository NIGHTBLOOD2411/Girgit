using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Global audio singleton — one looping BGM source, one one-shot SFX
    /// source. Assign clips in the Inspector; call the Play* methods from
    /// anywhere (ScreenFlow, GameManager, buttons) to trigger them. No clips
    /// are provided out of the box — drop your own AudioClip assets onto the
    /// fields below.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music (looping) — one plays at a time")]
        public AudioClip SplashMusic;
        public AudioClip MenuMusic;
        public AudioClip GameplayMusic;
        [Range(0f, 1f)] public float MusicVolume = 0.6f;

        [Header("Sound effects (one-shot)")]
        public AudioClip ButtonTap;
        public AudioClip Victory;
        public AudioClip GameOver;
        public AudioClip GameOver1; // plays as GameOver1's title card fades in
        public AudioClip GameOverAnimation; // plays as GameOver2's sprite-sheet sequence starts
        public AudioClip CountdownTick;   // each of 3, 2, 1
        public AudioClip CountdownGo;     // "GO!"
        [Range(0f, 1f)] public float SfxVolume = 0.8f;

        AudioSource _music;
        AudioSource _sfx;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _music = gameObject.AddComponent<AudioSource>();
            _music.loop = true;
            _music.playOnAwake = false;

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.loop = false;
            _sfx.playOnAwake = false;
        }

        /// <summary>Switches looping BGM. No-ops if this clip is already playing.</summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || _music.clip == clip) return;
            _music.clip = clip;
            _music.volume = MusicVolume;
            _music.Play();
        }

        public void StopMusic() => _music.Stop();

        public void PlaySfx(AudioClip clip)
        {
            if (clip == null) return;
            _sfx.PlayOneShot(clip, SfxVolume);
        }

        public void PlayButtonTap() => PlaySfx(ButtonTap);
        public void PlayVictory() => PlaySfx(Victory);
        public void PlayGameOver() => PlaySfx(GameOver);
        public void PlayGameOver1() => PlaySfx(GameOver1);
        public void PlayGameOverAnimation() => PlaySfx(GameOverAnimation);
        public void PlayCountdownTick() => PlaySfx(CountdownTick);
        public void PlayCountdownGo() => PlaySfx(CountdownGo);
    }
}

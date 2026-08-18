using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Plays a sprite-sheet animation by cycling a list of frames on a
    /// SpriteRenderer. Slice your sheet in the Sprite Editor (Sprite Mode:
    /// Multiple), then drag the resulting frames into <see cref="Frames"/> and
    /// set the speed — all from the Inspector. Tinting still works (the color
    /// on the SpriteRenderer multiplies each frame), so the chameleon's matched
    /// color shows over its animation.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [Tooltip("Ordered animation frames (slice your sprite sheet, drag them here).")]
        public Sprite[] Frames;

        [Min(0.01f)] public float FramesPerSecond = 8f;
        public bool Loop = true;
        [Tooltip("Start playing automatically when the object is enabled.")]
        public bool PlayOnEnable = true;

        SpriteRenderer _sr;
        int _index;
        float _timer;
        bool _playing;

        void Awake() => _sr = GetComponent<SpriteRenderer>();

        void OnEnable()
        {
            if (PlayOnEnable) Play();
        }

        public void Play()
        {
            if (Frames == null || Frames.Length == 0) return;
            _playing = true;
            _index = 0;
            _timer = 0f;
            _sr.sprite = Frames[0];
        }

        public void Stop() => _playing = false;

        void Update()
        {
            if (!_playing || Frames == null || Frames.Length == 0) return;

            _timer += Time.deltaTime * FramesPerSecond;
            while (_timer >= 1f)
            {
                _timer -= 1f;
                _index++;
                if (_index >= Frames.Length)
                {
                    if (Loop) _index = 0;
                    else { _index = Frames.Length - 1; _playing = false; }
                }
                _sr.sprite = Frames[_index];
            }
        }
    }
}

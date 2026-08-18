using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Fully self-contained scrolling tile — no parent/manager script needed.
    /// Attach directly to a bar/dot sprite. It moves in its own MoveAngle
    /// direction at its own Speed, optionally shows a separate visual
    /// SpriteRotation, and wraps to the opposite side of the camera view when
    /// it exits — X and Y wrap independently, so this works correctly for any
    /// movement angle, not just horizontal.
    /// </summary>
    public class ParallaxTile : MonoBehaviour
    {
        [Header("Movement (this tile only)")]
        [Tooltip("Direction this tile scrolls, in degrees. 0 = straight left, 90 = up, 180 = right, -90 = down.")]
        [Range(-180f, 180f)] public float MoveAngle = 0f;

        [Tooltip("Scroll speed, world units/second, for this tile.")]
        public float Speed = 3f;

        [Header("Visual")]
        [Tooltip("Visual tilt of the sprite itself, in degrees — independent of MoveAngle.")]
        public float SpriteRotation = 0f;

        [Header("Screen wrap")]
        [Tooltip("Extra margin (world units) beyond the camera edge before wrapping, so the tile fully leaves view before it reappears.")]
        [Min(0f)] public float WrapMargin = 1f;

        [Header("Randomize (right-click this component -> Randomize, or Girgit > Randomize Parallax Bars and Circles for every tile)")]
        public float RandomAngleMin = -30f;
        public float RandomAngleMax = 30f;
        public float RandomSpeedMin = 1.5f;
        public float RandomSpeedMax = 5f;
        public bool RandomizeSpriteRotationToo = true;

        Camera _cam;

        void Start()
        {
            _cam = Camera.main;
            ApplyRotation();
        }

        void OnValidate() => ApplyRotation();

        public void ApplyRotation() => transform.rotation = Quaternion.Euler(0f, 0f, SpriteRotation);

        void Update()
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            float rad = MoveAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            transform.position += dir * Speed * Time.deltaTime;

            WrapAroundScreen();
        }

        /// <summary>Independently wraps X and Y around the camera's view, so any movement angle loops correctly.</summary>
        void WrapAroundScreen()
        {
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;
            Vector3 camPos = _cam.transform.position;

            float minX = camPos.x - halfW - WrapMargin;
            float maxX = camPos.x + halfW + WrapMargin;
            float minY = camPos.y - halfH - WrapMargin;
            float maxY = camPos.y + halfH + WrapMargin;

            Vector3 p = transform.position;
            if (p.x < minX) p.x = maxX;
            else if (p.x > maxX) p.x = minX;
            if (p.y < minY) p.y = maxY;
            else if (p.y > maxY) p.y = minY;
            transform.position = p;
        }

        [ContextMenu("Randomize")]
        public void Randomize()
        {
            MoveAngle = Random.Range(RandomAngleMin, RandomAngleMax);
            Speed = Random.Range(RandomSpeedMin, RandomSpeedMax);
            SpriteRotation = RandomizeSpriteRotationToo ? MoveAngle : 0f;
            ApplyRotation();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}

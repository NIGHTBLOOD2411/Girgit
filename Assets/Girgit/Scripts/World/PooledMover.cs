using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Moves in a fixed direction at a fixed speed, both assigned by
    /// whichever Spawner most recently activated this instance. Despawn
    /// only arms once this has actually been on-screen at least once since
    /// spawning — the spawn area itself often sits off-screen by design, so
    /// a fresh spawn must never be mistaken for "exited the screen".
    /// </summary>
    public class PooledMover : MonoBehaviour
    {
        Spawner _spawner;
        Vector3 _direction;
        float _speed;
        float _despawnDelay;
        float _offScreenTimer;
        bool _hasEnteredScreen;

        public void Init(Spawner spawner, Vector3 direction, float speed, float despawnDelay)
        {
            _spawner = spawner;
            _direction = direction;
            _speed = speed;
            _despawnDelay = despawnDelay;
            _offScreenTimer = 0f;
            _hasEnteredScreen = false;
        }

        void Update()
        {
            transform.position += _direction * _speed * Time.deltaTime;

            bool onScreen = IsOnScreen();
            if (onScreen) _hasEnteredScreen = true;

            if (_hasEnteredScreen && !onScreen)
            {
                _offScreenTimer += Time.deltaTime;
                if (_offScreenTimer >= _despawnDelay) _spawner.Recycle(this);
            }
            else
            {
                _offScreenTimer = 0f;
            }
        }

        bool IsOnScreen()
        {
            Camera cam = Camera.main;
            if (cam == null) return true;
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            return vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f && vp.z > 0f;
        }
    }
}

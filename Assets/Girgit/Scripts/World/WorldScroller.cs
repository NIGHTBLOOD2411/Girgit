using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Single source of truth for parallax scroll speed (world units/sec).
    /// Purely decorative now that levels are single-color — the world scrolls
    /// for atmosphere. Set Acceleration &gt; 0 if you want it to speed up.
    /// </summary>
    public class WorldScroller : MonoBehaviour
    {
        public static WorldScroller Instance { get; private set; }

        [Min(0f)] public float Speed = 3f;
        [Min(0f)] public float Acceleration = 0f;
        [Min(0f)] public float MaxSpeed = 9f;

        void Awake() => Instance = this;

        void Update()
        {
            if (Acceleration > 0f && Speed < MaxSpeed)
                Speed = Mathf.Min(MaxSpeed, Speed + Acceleration * Time.deltaTime);
        }
    }
}

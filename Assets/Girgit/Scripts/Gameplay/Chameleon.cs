using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// The player. Holds the current mixed color and pushes it to its sprite.
    /// Position is fixed; the world scrolls past it.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Chameleon : MonoBehaviour
    {
        public Color CurrentColor { get; private set; } = Color.white;

        SpriteRenderer _sr;

        void Awake() => _sr = GetComponent<SpriteRenderer>();

        public void SetColor(Color c)
        {
            CurrentColor = c;
            if (_sr != null) _sr.color = c;
        }
    }
}

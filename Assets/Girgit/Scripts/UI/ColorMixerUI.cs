using UnityEngine;
using UnityEngine.UI;

namespace Girgit
{
    /// <summary>
    /// The 3 HSV mixing sliders. Reads Hue/Saturation/Value (each 0..1) and
    /// pushes the resulting color to the chameleon. Sliders respond to both
    /// mouse and touch out of the box, so this works on PC and mobile.
    ///
    /// The Hue track uses a fixed rainbow sprite (assigned once in the editor
    /// tool). Saturation (white -> current hue) and Value (black -> current
    /// hue) are painted here at runtime, since their gradient depends on the
    /// live Hue/Saturation values — like a standard HSV color picker.
    /// </summary>
    public class ColorMixerUI : MonoBehaviour
    {
        public Slider Hue;
        public Slider Saturation;
        public Slider Value;

        [Tooltip("Background Image of the Saturation slider — repainted white -> hue.")]
        public Image SaturationTrack;
        [Tooltip("Background Image of the Value slider — repainted black -> hue.")]
        public Image ValueTrack;

        public Chameleon Player;

        const int TrackTexWidth = 128;
        const int TrackTexHeight = 32;

        Texture2D _satTex;
        Texture2D _valTex;
        float _lastHue = -1f;
        float _lastSat = -1f;

        public Color Current => Color.HSVToRGB(Hue.value, Saturation.value, Value.value);

        /// <summary>
        /// Jumps all 3 sliders to a random position (fires Apply() naturally
        /// via onValueChanged). Called at the start of every challenge so the
        /// player can never coast in on a leftover value from the previous
        /// round — all three axes need genuine adjustment each time.
        /// </summary>
        public void RandomizeSliders()
        {
            Hue.value = Random.value;
            Saturation.value = Random.value;
            Value.value = Random.value;
        }

        void Start()
        {
            if (SaturationTrack != null)
            {
                _satTex = CreatePillTexture();
                SaturationTrack.sprite = ToSprite(_satTex);
                SaturationTrack.type = Image.Type.Sliced;
            }
            if (ValueTrack != null)
            {
                _valTex = CreatePillTexture();
                ValueTrack.sprite = ToSprite(_valTex);
                ValueTrack.type = Image.Type.Sliced;
            }

            Hue.onValueChanged.AddListener(_ => Apply());
            Saturation.onValueChanged.AddListener(_ => Apply());
            Value.onValueChanged.AddListener(_ => Apply());
            Apply();
        }

        void Apply()
        {
            bool hueChanged = !Mathf.Approximately(_lastHue, Hue.value);
            bool satChanged = !Mathf.Approximately(_lastSat, Saturation.value);
            _lastHue = Hue.value;
            _lastSat = Saturation.value;

            if (hueChanged && _satTex != null)
                PaintGradient(_satTex, Color.white, Color.HSVToRGB(_lastHue, 1f, 1f));

            if ((hueChanged || satChanged) && _valTex != null)
                PaintGradient(_valTex, Color.black, Color.HSVToRGB(_lastHue, _lastSat, 1f));

            if (Player != null) Player.SetColor(Current);
        }

        static Texture2D CreatePillTexture()
        {
            var tex = new Texture2D(TrackTexWidth, TrackTexHeight, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        static Sprite ToSprite(Texture2D tex)
        {
            float r = tex.height * 0.5f;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, 0, r, 0));
        }

        /// <summary>Paints a left-to-right gradient clipped to a stadium/pill shape (rounded ends).</summary>
        static void PaintGradient(Texture2D tex, Color left, Color right)
        {
            int w = tex.width, h = tex.height;
            float r = h * 0.5f;
            var px = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                float cy = y + 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f;
                    bool inside;
                    if (cx < r)
                    {
                        float dx = cx - r, dy = cy - r;
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    else if (cx > w - r)
                    {
                        float dx = cx - (w - r), dy = cy - r;
                        inside = dx * dx + dy * dy <= r * r;
                    }
                    else inside = true;

                    Color c = Color.Lerp(left, right, x / (float)(w - 1));
                    c.a = inside ? 1f : 0f;
                    px[y * w + x] = c;
                }
            }

            tex.SetPixels(px);
            tex.Apply();
        }
    }
}

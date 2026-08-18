using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Perceptual-ish color match. Works in HSV instead of raw RGB so the
    /// "% match" tracks how close two colors LOOK, not how close their bytes are.
    /// Weights are passed in (GameManager exposes them in the Inspector) rather
    /// than fixed here, defaulting to a balance where Hue, Saturation, and Value
    /// are each meaningfully load-bearing — nailing Hue alone should not be
    /// enough to pass. Hue's weight is scaled by the TARGET's saturation only,
    /// so a near-gray target doesn't demand an exact hue (a low-saturation
    /// color has no well-defined hue) — it must be the target's saturation,
    /// not the player's current slider position, or the player could inflate
    /// or deflate hue's weight just by where they leave their own slider.
    /// </summary>
    public static class ColorMatch
    {
        public const float DefaultHueWeight = 0.40f;
        public const float DefaultSaturationWeight = 0.32f;
        public const float DefaultValueWeight = 0.28f;

        /// <summary>0..100 percentage. 100 = identical. a = player's color, b = target color.</summary>
        public static float Percent(Color a, Color b,
            float hueWeight = DefaultHueWeight, float saturationWeight = DefaultSaturationWeight, float valueWeight = DefaultValueWeight)
        {
            Color.RGBToHSV(a, out float ha, out float sa, out float va);
            Color.RGBToHSV(b, out float hb, out float sb, out float vb);

            float dh = HueDistance(ha, hb);          // 0..1 (circular)
            float ds = Mathf.Abs(sa - sb);           // 0..1
            float dv = Mathf.Abs(va - vb);           // 0..1

            // If the TARGET is near-gray, hue barely matters — fold its
            // weight into saturation/value so gray targets stay matchable.
            float satFactor = sb;                    // 0..1, target's saturation only
            float wHue = hueWeight * satFactor;
            float leftover = hueWeight - wHue;       // redistribute
            float wSat = saturationWeight + leftover * (saturationWeight / (saturationWeight + valueWeight));
            float wVal = valueWeight + leftover * (valueWeight / (saturationWeight + valueWeight));

            float dist = wHue * dh + wSat * ds + wVal * dv; // 0..1
            return Mathf.Clamp01(1f - dist) * 100f;
        }

        /// <summary>Circular hue distance normalized to 0..1.</summary>
        static float HueDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b);
            return Mathf.Min(d, 1f - d) * 2f; // *2 so the max (0.5) maps to 1
        }
    }
}

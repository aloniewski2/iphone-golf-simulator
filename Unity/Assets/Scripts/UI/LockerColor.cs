using UnityEngine;

namespace GolfArcade.UI
{
    /// The locker's colour ranges (after Adnan's LockerColors.swift): every colour a player picks is a point on
    /// a continuous range, not one of a few names. Skin and natural hair walk a ramp of real tones; the
    /// outfit pieces, the cap and hair dye are hue and shade (shade -1 is near black, 0 the vivid colour,
    /// +1 pastel). The look stores the exact hex, so a slider is set back where its colour sits on the range.
    public static class LockerColor
    {
        /// Very fair to deep.
        public static readonly string[] SkinStops = { "FFEBDA", "FFE0C4", "F2C9A5", "E6AE7E", "D19062", "B7744A", "96593A", "74432C", "55301F", "3E2317" };
        /// Natural hair: black, browns, auburn, copper, blonds, platinum, silver.
        public static readonly string[] HairStops = { "141010", "2B1D17", "4A2F20", "6B4128", "8E4A25", "B35E2E", "C98B4C", "DDB468", "ECD59C", "F3EBD7", "C9CDD3" };
        /// The quick swatches (positions on the ramps), and names for the hair ones.
        public static readonly float[] SkinPresets = { 0.05f, 0.2f, 0.36f, 0.52f, 0.68f, 0.84f, 0.98f };
        public static readonly (string name, float t)[] HairPresets =
            { ("Black", 0f), ("Dark brown", 0.14f), ("Brown", 0.28f), ("Auburn", 0.42f), ("Copper", 0.52f), ("Honey", 0.63f), ("Blond", 0.72f), ("Platinum", 0.9f), ("Silver", 1f) };

        static Color Rgb(string hex) => UiKit.Hex(hex);

        /// The colour at t (0..1) along a ramp of hex stops.
        public static Color Ramp(string[] stops, float t)
        {
            float x = Mathf.Clamp01(t) * (stops.Length - 1);
            int i = Mathf.Min(stops.Length - 2, (int)x);
            return Color.Lerp(Rgb(stops[i]), Rgb(stops[i + 1]), x - i);
        }

        /// Where on a ramp a colour sits (the nearest point, found by walking it).
        public static float RampPosition(string[] stops, Color c)
        {
            float best = 0, bestD = float.MaxValue;
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f;
                var r = Ramp(stops, t);
                float d = (r.r - c.r) * (r.r - c.r) + (r.g - c.g) * (r.g - c.g) + (r.b - c.b) * (r.b - c.b);
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }

        /// Hue 0..1 and shade -1..1 to a colour.
        public static Color HueShade(float hue, float shade)
        {
            hue -= Mathf.Floor(hue);
            shade = Mathf.Clamp(shade, -1f, 1f);
            float sat = shade > 0 ? 0.86f * (1 - shade * 0.92f) : 0.86f;
            float val = shade < 0 ? 0.97f + shade * 0.84f : 0.97f;
            return Color.HSVToRGB(hue, sat, val);
        }

        /// The hue and shade that come nearest a colour (for setting the sliders to a palette colour).
        public static (float hue, float shade) HueShadeOf(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.9f) return (h, Mathf.Max(-1f, (v - 0.97f) / 0.84f));
            return (h, Mathf.Min(1f, Mathf.Max(0f, (1 - s / 0.86f) / 0.92f)));
        }

        /// A word for a colour (the row under a slider).
        public static string Describe(Color c)
        {
            var (h, s) = HueShadeOf(c);
            float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            float sat = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
            if (sat < 0.1f) return lum > 0.85f ? "White" : lum < 0.18f ? "Black" : "Grey";
            string[] names = { "Red", "Orange", "Gold", "Lime", "Green", "Teal", "Sky", "Blue", "Violet", "Pink", "Red" };
            string basic = names[Mathf.RoundToInt(h * 10) % names.Length];
            return s < -0.4f ? "Deep " + basic.ToLowerInvariant() : s > 0.45f ? "Pastel " + basic.ToLowerInvariant() : basic;
        }
    }
}

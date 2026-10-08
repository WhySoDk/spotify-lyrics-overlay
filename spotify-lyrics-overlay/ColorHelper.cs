using System.Globalization;
using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    internal static class ColorHelper
    {
        //#RRGGBB, an alpha part from older configs (#RRGGBBAA) is ignored
        public static bool IsValidHex(string value)
        {
            return Regex.IsMatch(value ?? "", @"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
        }

        public static Color FromHex(string value, Color fallback)
        {
            if (!IsValidHex(value)) return fallback;

            string hex = value.TrimStart('#');
            int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);

            return Color.FromArgb(r, g, b);
        }

        public static string ToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        //hue 0-360, saturation and value 0-1
        public static Color FromHsv(float hue, float saturation, float value)
        {
            float c = value * saturation;
            float h = (hue % 360f + 360f) % 360f / 60f;
            float x = c * (1 - Math.Abs(h % 2 - 1));
            float m = value - c;

            (float r, float g, float b) = (int)h switch
            {
                0 => (c, x, 0f),
                1 => (x, c, 0f),
                2 => (0f, c, x),
                3 => (0f, x, c),
                4 => (x, 0f, c),
                _ => (c, 0f, x),
            };

            return Color.FromArgb(toByte(r + m), toByte(g + m), toByte(b + m));
        }

        public static (float hue, float saturation, float value) ToHsv(Color color)
        {
            float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float hue = 0f;
            if (delta > 0)
            {
                if (max == r) hue = 60f * ((g - b) / delta % 6);
                else if (max == g) hue = 60f * ((b - r) / delta + 2);
                else hue = 60f * ((r - g) / delta + 4);
            }
            if (hue < 0) hue += 360f;

            return (hue, max == 0 ? 0f : delta / max, max);
        }

        private static int toByte(float value) => (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255);

        //opacity in percent (0-100)
        public static Color WithOpacity(Color color, int opacity)
        {
            int alpha = (int)Math.Round(Math.Clamp(opacity, 0, 100) * 255 / 100.0);
            return Color.FromArgb(alpha, color);
        }
    }
}

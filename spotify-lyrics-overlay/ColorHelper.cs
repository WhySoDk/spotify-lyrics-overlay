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

        //opacity in percent (0-100)
        public static Color WithOpacity(Color color, int opacity)
        {
            int alpha = (int)Math.Round(Math.Clamp(opacity, 0, 100) * 255 / 100.0);
            return Color.FromArgb(alpha, color);
        }
    }
}

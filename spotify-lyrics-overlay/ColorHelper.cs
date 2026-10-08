using System.Globalization;
using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    internal static class ColorHelper
    {
        //#RRGGBB or #RRGGBBAA
        public static bool IsValidRgbaHex(string value)
        {
            return Regex.IsMatch(value ?? "", @"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");
        }

        public static Color FromRgbaHex(string value, Color fallback)
        {
            if (!IsValidRgbaHex(value)) return fallback;

            string hex = value.TrimStart('#');
            int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            int a = hex.Length == 8 ? int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) : 255;

            return Color.FromArgb(a, r, g, b);
        }

        public static string ToRgbaHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
        }
    }
}

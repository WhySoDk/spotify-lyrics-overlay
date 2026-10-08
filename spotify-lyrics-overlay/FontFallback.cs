using System.Runtime.InteropServices;

namespace spotify_lyrics_overlay
{
    //picks the secondary font for lines the main font has no glyphs for (e.g. Thai lyrics with an English font).
    //the whole line switches font so it is measured and drawn with one font
    internal static class FontFallback
    {
        private static Font? secondary;
        private static (string primary, string secondary) fontNames = ("", "");
        //per line, true when it uses the secondary font
        private static readonly Dictionary<string, bool> useSecondary = new();
        private static readonly Dictionary<(string family, char c), bool> glyphCache = new();

        //set by the overlay for every render (the fonts are recreated each time), null turns the secondary font off
        public static void Configure(Font primary, Font? secondaryFont)
        {
            var names = (primary.Name, secondaryFont?.Name ?? "");
            if (names != fontNames)
            {
                fontNames = names;
                useSecondary.Clear();
            }
            secondary = secondaryFont;
        }

        public static Font Pick(Font primary, string text)
        {
            if (secondary == null || string.IsNullOrWhiteSpace(text)) return primary;

            if (!useSecondary.TryGetValue(text, out bool use))
            {
                // only switch when the secondary font actually has something the main one is missing
                var missing = text.Where(c => !char.IsWhiteSpace(c) && !char.IsSurrogate(c) && !hasGlyph(primary, c)).ToList();
                use = missing.Count > 0 && missing.Any(c => hasGlyph(secondary, c));

                if (useSecondary.Count > 500) useSecondary.Clear();
                useSecondary[text] = use;
            }
            return use ? secondary : primary;
        }

        private static bool hasGlyph(Font font, char c)
        {
            var key = (font.Name, c);
            if (glyphCache.TryGetValue(key, out bool has)) return has;

            IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hFont = font.ToHfont();
            IntPtr oldFont = SelectObject(dc, hFont);
            try
            {
                var indices = new ushort[1];
                GetGlyphIndicesW(dc, c.ToString(), 1, indices, GGI_MARK_NONEXISTING_GLYPHS);
                has = indices[0] != 0xFFFF;
            }
            finally
            {
                SelectObject(dc, oldFont);
                DeleteObject(hFont);
                DeleteDC(dc);
            }

            glyphCache[key] = has;
            return has;
        }

        private const uint GGI_MARK_NONEXISTING_GLYPHS = 1;

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetGlyphIndicesW(IntPtr hdc, string text, int count, [Out] ushort[] indices, uint flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
    }
}

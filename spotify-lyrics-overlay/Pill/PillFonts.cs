using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace spotify_lyrics_overlay.Pill
{
    //Prompt, bundled in the exe (Fonts folder, SIL Open Font License) so Thai titles look right on every machine
    internal static class PillFonts
    {
        //one collection per file, GDI+ draws every style with the same face when they share a collection
        private static readonly Lazy<PrivateFontCollection?> regular = new(() => load("Prompt-Regular.ttf"));
        private static readonly Lazy<PrivateFontCollection?> bold = new(() => load("Prompt-Bold.ttf"));

        public static bool HasThai(string text) => text.Any(c => c >= '\u0E00' && c <= '\u0E7F');

        //Prompt for Thai text, the regular pill font otherwise (also when Prompt couldn't be loaded)
        public static Font Create(string text, float size, FontStyle style)
        {
            if (HasThai(text))
            {
                var family = (style.HasFlag(FontStyle.Bold) ? bold : regular).Value?.Families.FirstOrDefault();
                if (family != null && family.IsStyleAvailable(style)) return new Font(family, size, style);
            }
            return new Font(PillStyle.FontName, size, style);
        }

        private static PrivateFontCollection? load(string fileName)
        {
            var assembly = typeof(PillFonts).Assembly;
            string? name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
            if (name == null) return null;

            try
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
                // GDI+ reads the font from this memory for as long as it's used, so it's never freed
                IntPtr memory = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                var fonts = new PrivateFontCollection();
                fonts.AddMemoryFont(memory, bytes.Length);
                return fonts;
            }
            catch (Exception ex)
            {
                Log.Write($"Error loading font {fileName}: {ex.Message}");
                return null;
            }
        }
    }
}

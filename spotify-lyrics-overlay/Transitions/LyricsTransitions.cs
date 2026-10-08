namespace spotify_lyrics_overlay.Transitions
{
    //registry of the transition modes shown in the "Line transition" dropdown
    internal static class LyricsTransitions
    {
        public record Mode(string Id, string DisplayName, Func<ILyricsTransition> Create);

        // Id is stored in config.json, the first mode is the default
        public static readonly IReadOnlyList<Mode> All = new[]
        {
            new Mode("Legacy", "Legacy", () => new LegacyTransition()),
            new Mode("SlideUp", "Slide up", () => new SlideUpTransition()),
            new Mode("Crossfade", "Crossfade", () => new CrossfadeTransition()),
            new Mode("Scroll", "Scroll", () => new ScrollTransition()),
            new Mode("Pop", "Pop", () => new PopTransition()),
            new Mode("Karaoke", "Karaoke fill", () => new KaraokeTransition()),
            new Mode("Push", "Push sideways", () => new PushTransition()),
            new Mode("Typewriter", "Typewriter", () => new TypewriterTransition()),
        };

        public static Mode Find(string id)
        {
            return All.FirstOrDefault(mode => mode.Id == id) ?? All[0];
        }

        //status text and plain lines, every line full size and stacked around the center
        public static List<TextItem> StackLines(Graphics g, Font font, float centerY, IEnumerable<string> lines)
        {
            var items = lines
                .Select(line => new TextItem { Text = line, Size = g.MeasureString(line, font) })
                .ToList();

            float y = centerY - items.Sum(item => item.Size.Height) / 2f;
            foreach (var item in items)
            {
                item.Y = y;
                y += item.Size.Height;
            }
            return items;
        }
    }
}

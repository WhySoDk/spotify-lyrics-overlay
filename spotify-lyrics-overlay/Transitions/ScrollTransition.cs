namespace spotify_lyrics_overlay.Transitions
{
    //previous line above and next line below the current one, both smaller and dimmer.
    //on a line change the whole stack scrolls up by one row
    internal class ScrollTransition : LineTransition
    {
        private const float SideLineScale = 0.75f;
        private const float SideLineOpacity = 0.5f;

        private record Slot(float Y, float Scale, float Opacity);

        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            var items = new List<TextItem>();

            float rowHeight = g.MeasureString("Ag", font).Height;
            float sideHeight = rowHeight * SideLineScale;
            float currentY = centerY - rowHeight / 2f;

            // rows from -2 (just left at the top) to 2 (about to come in at the bottom), 0 is the current line
            var slots = new[]
            {
                new Slot(currentY - sideHeight * 1.5f, SideLineScale, 0f),
                new Slot(currentY - sideHeight, SideLineScale, SideLineOpacity),
                new Slot(currentY, 1f, 1f),
                new Slot(currentY + rowHeight, SideLineScale, SideLineOpacity),
                new Slot(currentY + rowHeight + sideHeight * 0.5f, SideLineScale, 0f),
            };

            // line index + k moves from row k + 1 up to row k
            for (int k = -2; k <= 1; k++)
            {
                // after the last lyric only the lines scrolling out are left
                if (Ended && (k >= 0 || p >= 1f)) continue;
                if (k == -2 && p >= 1f) continue;
                if (LineAt(index + k) is not string text) continue;

                var from = slots[k + 3];
                var to = slots[k + 2];
                items.Add(MakeItem(g, font, text,
                    Lerp(from.Scale, to.Scale, p), Lerp(from.Opacity, to.Opacity, p), Lerp(from.Y, to.Y, p)));
            }

            return items;
        }
    }
}

namespace spotify_lyrics_overlay.Transitions
{
    //current line on the first row, smaller and dimmer next line on the second row.
    //on a line change the second row moves up and grows, the old first row moves up and fades out
    internal class SlideUpTransition : LineTransition
    {
        private const float NextLineScale = 0.75f;
        private const float NextLineOpacity = 0.5f;

        //karaoke fill of the current line, null for none
        protected virtual float? CurrentLineFill => null;

        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            var items = new List<TextItem>();

            float row1Height = g.MeasureString("Ag", font).Height;
            float row2Height = row1Height * NextLineScale;
            float row1Y = centerY - (row1Height + row2Height) / 2f;
            float row2Y = row1Y + row1Height;

            // previous line leaving the first row
            if (p < 1f && LineAt(index - 1) is string previous)
            {
                items.Add(MakeItem(g, font, previous, 1f, 1f - p, row1Y - row1Height * 0.5f * p));
            }

            if (Ended) return items;

            // current line, coming up from the second row
            if (LineAt(index) is string current)
            {
                var item = MakeItem(g, font, current,
                    Lerp(NextLineScale, 1f, p), Lerp(NextLineOpacity, 1f, p), Lerp(row2Y, row1Y, p));
                item.Fill = CurrentLineFill;
                items.Add(item);
            }

            // upcoming line fading in on the second row
            if (LineAt(index + 1) is string next)
            {
                items.Add(MakeItem(g, font, next,
                    NextLineScale, NextLineOpacity * p, row2Y + row2Height * 0.5f * (1f - p)));
            }

            return items;
        }
    }
}

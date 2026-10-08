namespace spotify_lyrics_overlay.Transitions
{
    //only the current line, on a line change the old one fades out while the new one fades in
    internal class CrossfadeTransition : LineTransition
    {
        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            var items = new List<TextItem>();
            float y = centerY - g.MeasureString("Ag", font).Height / 2f;

            if (p < 1f && LineAt(index - 1) is string previous)
            {
                items.Add(MakeItem(g, font, previous, 1f, 1f - p, y));
            }

            if (!Ended && LineAt(index) is string current)
            {
                items.Add(MakeItem(g, font, current, 1f, p, y));
            }

            return items;
        }
    }
}

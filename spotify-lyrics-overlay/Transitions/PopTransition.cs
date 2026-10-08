namespace spotify_lyrics_overlay.Transitions
{
    //only the current line, on a line change the new one grows into place while fading in
    //and the old one keeps growing while fading out
    internal class PopTransition : LineTransition
    {
        protected override TimeSpan Duration => TimeSpan.FromMilliseconds(250);

        private const float IncomingScale = 0.85f;
        private const float OutgoingScale = 1.1f;

        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            var items = new List<TextItem>();

            // scaled around the vertical center of the row
            TextItem makeCentered(string text, float scale, float opacity)
            {
                var item = MakeItem(g, font, text, scale, opacity, 0f);
                item.Y = centerY - item.Size.Height / 2f;
                return item;
            }

            if (p < 1f && LineAt(index - 1) is string previous)
            {
                items.Add(makeCentered(previous, Lerp(1f, OutgoingScale, p), 1f - p));
            }

            if (!Ended && LineAt(index) is string current)
            {
                items.Add(makeCentered(current, Lerp(IncomingScale, 1f, p), p));
            }

            return items;
        }
    }
}

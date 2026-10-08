namespace spotify_lyrics_overlay.Transitions
{
    //only the current line, on a line change the new one comes in from the right
    //and pushes the old one out to the left, side by side
    internal class PushTransition : LineTransition
    {
        // space between the two lines while they move, in line heights
        private const float Gap = 1f;

        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            var items = new List<TextItem>();
            float height = g.MeasureString("Ag", font).Height;
            float y = centerY - height / 2f;

            var previous = p < 1f && LineAt(index - 1) is string previousText ? MakeItem(g, font, previousText, 1f, 1f - p, y) : null;
            var current = !Ended && LineAt(index) is string currentText ? MakeItem(g, font, currentText, 1f, p, y) : null;

            // distance between the centers of the two lines when they sit next to each other
            float distance = ((previous?.Size.Width ?? 0f) + (current?.Size.Width ?? 0f)) / 2f + height * Gap;

            if (previous != null)
            {
                previous.OffsetX = -distance * p;
                items.Add(previous);
            }

            if (current != null)
            {
                current.OffsetX = distance * (1f - p);
                items.Add(current);
            }

            return items;
        }
    }
}

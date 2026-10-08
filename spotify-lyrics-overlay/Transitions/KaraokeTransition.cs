namespace spotify_lyrics_overlay.Transitions
{
    //slide up layout, the current line fills with the text color from left to right while it is sung.
    //lyrics are only timed per line, so the fill runs at a steady pace until the next line starts
    internal class KaraokeTransition : SlideUpTransition
    {
        // long gaps (e.g. instrumental breaks) must not stretch the fill, cap it by the line length
        private const double MinFillSeconds = 0.5;
        private const double MaxSecondsPerChar = 0.12;

        private float fill;

        protected override float? CurrentLineFill => fill;

        public override bool Update(LyricsView view)
        {
            bool changed = base.Update(view);

            float newFill = lineFill(view.CurrentTime);
            // skip redraws for changes too small to see
            if (Math.Abs(newFill - fill) >= 0.002f || (newFill != fill && (newFill == 0f || newFill == 1f)))
            {
                fill = newFill;
                changed = true;
            }

            return changed;
        }

        private float lineFill(double time)
        {
            if (lines == null || index < 0 || Ended) return 0f;

            var line = lines[index];
            double end = index + 1 < lines.Count ? lines[index + 1].Time : double.PositiveInfinity;
            double duration = Math.Min(end - line.Time, MinFillSeconds + line.Text.Length * MaxSecondsPerChar);
            if (duration <= 0) return 1f;

            return Math.Clamp((float)((time - line.Time) / duration), 0f, 1f);
        }
    }
}

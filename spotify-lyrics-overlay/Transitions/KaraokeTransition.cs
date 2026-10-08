namespace spotify_lyrics_overlay.Transitions
{
    //slide up layout, the current line fills with the text color from left to right while it is sung.
    //with word timings the fill follows each word, otherwise it runs at a steady pace until the line ends
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
            if (line.Words is { Count: > 0 } words) return wordsFill(line, words, time);

            double duration;
            if (line.EndTime is double lineEnd)
            {
                duration = lineEnd - line.Time;
            }
            else
            {
                double end = index + 1 < lines.Count ? lines[index + 1].Time : double.PositiveInfinity;
                duration = Math.Min(end - line.Time, MinFillSeconds + line.Text.Length * MaxSecondsPerChar);
            }
            return fillBetween(line.Time, duration, time);
        }

        //share of the line's characters sung so far, each word fills during its own time
        private static float wordsFill(LyricLine line, List<LyricWord> words, double time)
        {
            int total = words.Sum(w => w.Text.Length);
            if (total == 0) return 0f;

            double sung = 0;
            for (int i = 0; i < words.Count && words[i].Time <= time; i++)
            {
                var word = words[i];
                double end = word.EndTime
                    ?? (i + 1 < words.Count ? words[i + 1].Time : line.EndTime ?? word.Time + MinFillSeconds + word.Text.Length * MaxSecondsPerChar);
                sung += word.Text.Length * fillBetween(word.Time, end - word.Time, time);
            }
            return Math.Clamp((float)(sung / total), 0f, 1f);
        }

        private static float fillBetween(double start, double duration, double time)
        {
            if (duration <= 0) return 1f;
            return Math.Clamp((float)((time - start) / duration), 0f, 1f);
        }
    }
}

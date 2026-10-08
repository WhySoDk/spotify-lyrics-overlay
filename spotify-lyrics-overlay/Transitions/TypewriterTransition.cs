using System.Globalization;

namespace spotify_lyrics_overlay.Transitions
{
    //only the current line, typed out character by character when it starts.
    //follows the song time, so pausing or seeking stops or skips the typing too
    internal class TypewriterTransition : LineTransition
    {
        private const double SecondsPerChar = 0.035;
        // short lines must be fully typed well before the next one starts
        private const double MaxShareOfLine = 0.6;

        private int shownChars;

        public override bool Update(LyricsView view)
        {
            bool changed = base.Update(view);

            int newShownChars = visibleChars(view.CurrentTime);
            if (newShownChars != shownChars)
            {
                shownChars = newShownChars;
                changed = true;
            }

            return changed;
        }

        protected override List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p)
        {
            return layoutCurrent(g, font, centerY, shownChars);
        }

        // the background box is sized for the whole line right away
        protected override List<TextItem> LayoutTargetLines(Graphics g, Font font, float centerY)
        {
            return layoutCurrent(g, font, centerY, int.MaxValue);
        }

        private List<TextItem> layoutCurrent(Graphics g, Font font, float centerY, int chars)
        {
            var items = new List<TextItem>();
            if (Ended || LineAt(index) is not string text) return items;

            // characters are counted as text elements so combining marks stay with their letter
            var info = new StringInfo(text);
            string typed = chars >= info.LengthInTextElements ? text : info.SubstringByTextElements(0, chars);

            float y = centerY - g.MeasureString("Ag", font).Height / 2f;
            var item = MakeItem(g, font, typed, 1f, 1f, y);

            // keep the left edge where the whole line will start instead of re-centering every character
            item.OffsetX = (item.Size.Width - g.MeasureString(text, font).Width) / 2f;
            items.Add(item);
            return items;
        }

        private int visibleChars(double time)
        {
            if (lines == null || index < 0 || Ended) return 0;

            var line = lines[index];
            int length = new StringInfo(line.Text).LengthInTextElements;
            double duration = length * SecondsPerChar;
            if (index + 1 < lines.Count)
            {
                duration = Math.Min(duration, (lines[index + 1].Time - line.Time) * MaxShareOfLine);
            }
            if (duration <= 0) return length;

            double share = Math.Clamp((time - line.Time) / duration, 0, 1);
            return (int)Math.Ceiling(share * length);
        }
    }
}

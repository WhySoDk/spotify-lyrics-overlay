using System.Diagnostics;

namespace spotify_lyrics_overlay.Transitions
{
    //base for modes that animate from one synced line to the next.
    //tracks the current line, shows status text as-is and runs the animation clock
    internal abstract class LineTransition : ILyricsTransition
    {
        protected virtual TimeSpan Duration => TimeSpan.FromMilliseconds(350);

        protected string? message;
        protected List<LyricLine>? lines;
        protected int index = -1;
        private bool animating;
        private readonly Stopwatch clock = new();

        public virtual bool Update(LyricsView view)
        {
            if (view.Lines == null)
            {
                // status text, shown without animation
                bool messageChanged = view.Message != message || lines != null;
                message = view.Message;
                lines = null;
                animating = false;
                return messageChanged;
            }

            bool changed = message != null || !ReferenceEquals(view.Lines, lines) || view.CurrentIndex != index;
            if (changed)
            {
                // animate when moving on to the next line, jump straight there on seek or song change
                animating = message == null && ReferenceEquals(view.Lines, lines) && view.CurrentIndex == index + 1;
                clock.Restart();

                message = null;
                lines = view.Lines;
                index = view.CurrentIndex;
            }

            return changed || animating;
        }

        public List<TextItem> Layout(Graphics g, Font font, float centerY)
        {
            if (lines == null) return layoutMessage(g, font, centerY);
            return LayoutLines(g, font, centerY, getProgress());
        }

        public List<TextItem> LayoutTarget(Graphics g, Font font, float centerY)
        {
            if (lines == null) return layoutMessage(g, font, centerY);
            return LayoutTargetLines(g, font, centerY);
        }

        private List<TextItem> layoutMessage(Graphics g, Font font, float centerY)
        {
            return LyricsTransitions.StackLines(g, font, centerY, (message ?? "").Split('\n'));
        }

        //synced lines for the current frame.
        //p: animation progress, 0 = previous line still in place, 1 = done
        protected abstract List<TextItem> LayoutLines(Graphics g, Font font, float centerY, float p);

        //synced lines once the running animation is done
        protected virtual List<TextItem> LayoutTargetLines(Graphics g, Font font, float centerY)
        {
            return LayoutLines(g, font, centerY, 1f);
        }

        //true when only empty lines are left, nothing is shown then
        protected bool Ended => lines != null && index >= 0 && LyricsFactory.IsLyricsEnded(lines, index);

        //line text at i, null when out of range
        protected string? LineAt(int i) => lines != null && i >= 0 && i < lines.Count ? lines[i].Text : null;

        protected static TextItem MakeItem(Graphics g, Font font, string text, float scale, float opacity, float y) => new TextItem
        {
            Text = text,
            Scale = scale,
            Opacity = opacity,
            Y = y,
            Size = g.MeasureString(text, font) * scale
        };

        //0 to 1, eased, 1 when there is no running animation
        private float getProgress()
        {
            if (!animating) return 1f;

            double t = clock.Elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
            if (t >= 1)
            {
                animating = false;
                return 1f;
            }
            return 1f - (float)Math.Pow(1 - t, 3);
        }

        protected static float Lerp(float from, float to, float t) => from + (to - from) * t;
    }
}

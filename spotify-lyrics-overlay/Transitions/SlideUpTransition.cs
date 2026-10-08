using System.Diagnostics;

namespace spotify_lyrics_overlay.Transitions
{
    //current line on the first row, smaller and dimmer next line on the second row.
    //on a line change the second row moves up and grows, the old first row moves up and fades out
    internal class SlideUpTransition : ILyricsTransition
    {
        private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(350);
        private const float NextLineScale = 0.75f;
        private const float NextLineOpacity = 0.5f;

        private string? message;
        private List<LyricLine>? lines;
        private int index = -1;
        private bool animating;
        private readonly Stopwatch clock = new();

        public bool Update(LyricsView view)
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
            return layout(g, font, centerY, getProgress());
        }

        public List<TextItem> LayoutTarget(Graphics g, Font font, float centerY)
        {
            return layout(g, font, centerY, 1f);
        }

        //p: animation progress, 0 = previous line still on the first row, 1 = done
        private List<TextItem> layout(Graphics g, Font font, float centerY, float p)
        {
            if (lines == null)
            {
                return LyricsTransitions.StackLines(g, font, centerY, (message ?? "").Split('\n'));
            }

            var items = new List<TextItem>();

            float row1Height = g.MeasureString("Ag", font).Height;
            float row2Height = row1Height * NextLineScale;
            float row1Y = centerY - (row1Height + row2Height) / 2f;
            float row2Y = row1Y + row1Height;

            TextItem makeItem(string text, float scale, float opacity, float y) => new TextItem
            {
                Text = text,
                Scale = scale,
                Opacity = opacity,
                Y = y,
                Size = g.MeasureString(text, font) * scale
            };

            bool ended = index >= 0 && LyricsFactory.IsLyricsEnded(lines, index);

            // previous line leaving the first row
            if (p < 1f && index - 1 >= 0)
            {
                items.Add(makeItem(lines[index - 1].Text, 1f, 1f - p, row1Y - row1Height * 0.5f * p));
            }

            // current line, coming up from the second row
            if (index >= 0 && !ended)
            {
                items.Add(makeItem(lines[index].Text,
                    lerp(NextLineScale, 1f, p), lerp(NextLineOpacity, 1f, p), lerp(row2Y, row1Y, p)));
            }

            // upcoming line fading in on the second row
            if (index + 1 < lines.Count && !ended)
            {
                items.Add(makeItem(lines[index + 1].Text,
                    NextLineScale, NextLineOpacity * p, row2Y + row2Height * 0.5f * (1f - p)));
            }

            return items;
        }

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

        private static float lerp(float from, float to, float t) => from + (to - from) * t;
    }
}

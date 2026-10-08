namespace spotify_lyrics_overlay.Transitions
{
    //two rows, lines alternate between top and bottom, the current one is marked with ">"
    internal class LegacyTransition : ILyricsTransition
    {
        private string? text;

        public bool Update(LyricsView view)
        {
            string newText = view.Message ?? LyricsFactory.GetKaraokeLines(view.Lines!, view.CurrentTime);

            //re-render if text actually changed
            if (newText == text) return false;
            text = newText;
            return true;
        }

        public List<TextItem> Layout(Graphics g, Font font, float centerY)
        {
            return LyricsTransitions.StackLines(g, font, centerY, (text ?? "").Split('\n'));
        }
    }
}

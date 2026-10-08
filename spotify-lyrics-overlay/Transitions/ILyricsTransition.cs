namespace spotify_lyrics_overlay.Transitions
{
    //one line of text to draw. Y is relative to the top of the screen,
    //X is filled in by the overlay to center the line
    internal class TextItem
    {
        public string Text = "";
        public float Scale = 1f;
        public float Opacity = 1f;
        //karaoke, share of the line from the left drawn in full color and the rest dimmed,
        //null draws the whole line normally
        public float? Fill;
        public float Y;
        public float X;
        //horizontal shift from the centered position
        public float OffsetX;
        public SizeF Size;
        //font for this line, the secondary font when the main one is missing glyphs (null uses the main font)
        public Font? Font;
    }

    //a lyrics line transition mode, add new modes to LyricsTransitions.All
    internal interface ILyricsTransition
    {
        //take the latest lyrics state, return true when the overlay has to redraw
        //(keep returning true while an animation is running)
        bool Update(LyricsView view);

        //lines to draw for the current frame, vertically centered on centerY
        List<TextItem> Layout(Graphics g, Font font, float centerY);

        //lines as they will be once the running animation is done,
        //the background box glides toward this layout instead of following every frame
        List<TextItem> LayoutTarget(Graphics g, Font font, float centerY) => Layout(g, font, centerY);
    }
}

namespace spotify_lyrics_overlay
{
    public record ScreenLayout(int fontSize, int xOffset, int yOffset);

    public class AppConfig
    {
        public Boolean newlyGenerated { get; set; }
        public String fontName { get; set; } = "";
        //used for lines the main font has no glyphs for, empty leaves it to Windows
        public String secondaryFontName { get; set; } = "";
        public int fontSize { get; set; }
        public Boolean bold { get; set; }
        public Boolean italic { get; set; }
        public Boolean dropShadow { get; set; }
        public String fontColorHex { get; set; } = "";
        public Boolean albumColor { get; set; } = true;
        public String screenName { get; set; } = "";
        public int xOffset { get; set; }
        public int yOffset { get; set; }
        // font size and offsets last used on each screen size, keyed like "1920x1080"
        public Dictionary<String, ScreenLayout> screenLayouts { get; set; } = new();
        public String apiKey { get; set; } = "";
        public Boolean rememberApiKey { get; set; }
        // start the overlay on launch when it was running the last time the app closed
        public Boolean rememberRunState { get; set; }
        public Boolean wasStarted { get; set; }
        public Boolean backgroundEnabled { get; set; }
        public String backgroundColorHex { get; set; } = "#000000";
        public int backgroundOpacity { get; set; } = 70;
        public int backgroundSpread { get; set; } = 12;
        public String transitionMode { get; set; } = Transitions.LyricsTransitions.DefaultId;
        // pill with the cover, title, artist and lyrics status at the top of the screen when the song changes
        public Boolean songPill { get; set; } = true;
        // show the pill from the Windows media info right away instead of waiting for Spotify's answer
        public Boolean songPillWindowsMedia { get; set; }
        // small box above the lyrics telling which lookup step is running
        public Boolean debugEnabled { get; set; }
        public Boolean debugShowCacheHits { get; set; } = true;

        public override string ToString()
        {
            return $"AppConfig(newlyGenerated: {newlyGenerated}, fontName: {fontName}, secondaryFontName: {secondaryFontName}, fontSize: {fontSize}, " +
                   $"bold: {bold}, italic: {italic}, dropShadow: {dropShadow}, fontColorHex: {fontColorHex}, albumColor: {albumColor}, " +
                   $"screenName: {screenName}, xOffset: {xOffset}, yOffset: {yOffset}, screenLayouts: {screenLayouts.Count}, apiKey: {apiKey}, rememberApiKey: {rememberApiKey}, " +
                   $"rememberRunState: {rememberRunState}, wasStarted: {wasStarted}, " +
                   $"backgroundEnabled: {backgroundEnabled}, backgroundColorHex: {backgroundColorHex}, backgroundOpacity: {backgroundOpacity}, backgroundSpread: {backgroundSpread}, transitionMode: {transitionMode}, songPill: {songPill}, songPillWindowsMedia: {songPillWindowsMedia}, " +
                   $"debugEnabled: {debugEnabled}, debugShowCacheHits: {debugShowCacheHits})";
        }

    }
}

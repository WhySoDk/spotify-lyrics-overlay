using System.Text.Json;

namespace spotify_lyrics_overlay
{
    public class LyricsResult
    {
        public string? SyncLyrics { get; set; }
        public string? PlainLyrics { get; set; }
    }

    public class LrcLibLyricsProvider
    {
        private static readonly Lazy<LrcLibLyricsProvider> lazy = new(() => new LrcLibLyricsProvider());
        public static LrcLibLyricsProvider Instance => lazy.Value;

        private readonly HttpClient httpClient = new();
        private string? lastTrackName;
        private string? lastArtistName;
        private LyricsResult? cachedLyrics;

        private LrcLibLyricsProvider()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "SpotifyLyricsOverlay v1.0.0 (https://github.com/WhySoDk/spotify-lyrics-overlay)"
            );
        }


        public async Task<LyricsResult?> GetLyricsAsync(string trackName, string artistName, int durationSeconds)
        {
            if (string.Equals(trackName, lastTrackName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(artistName, lastArtistName, StringComparison.OrdinalIgnoreCase))
            {
                return cachedLyrics;
            }

            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}&duration={durationSeconds}";
            try
            {
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;

                var result = new LyricsResult
                {
                    SyncLyrics = doc.TryGetProperty("syncedLyrics", out var sync) ? sync.GetString() : null,
                    PlainLyrics = doc.TryGetProperty("plainLyrics", out var plain) ? plain.GetString() : null
                };

                lastTrackName = trackName;
                lastArtistName = artistName;
                cachedLyrics = result;

                System.Diagnostics.Debug.WriteLine($"Fetched lyrics for {trackName} by {artistName}: {result.SyncLyrics ?? "No synced lyrics"}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching lyrics: {ex.Message}");
                return null;
            }
        }
    }
}

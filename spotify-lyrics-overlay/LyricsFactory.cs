using System.Text.Json;
using SpotifyAPI.Web;
using System.Globalization;
using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    internal class LyricLine
    {
        public double Time { get; set; }
        public string Text { get; set; }

        public LyricLine(double time, string text)
        {
            Time = time;
            Text = text;
        }
    }

    internal class PlaybackState
    {
        public string? TrackName { get; set; }
        public string? TrackArtists { get; set; }
        public int TrackLength { get; set; }
        public double CurrentTime { get; set; }
        public bool IsPlaying { get; set; }
    }

    internal class LyricsFactory
    {
        private SpotifyClient? spotify;
        private string? parsedLyricsSource;
        private List<LyricLine> parsedLyrics = new();

        public LyricsFactory()
        {
        }
        private async Task<PlaybackState?> GetPlaybackStateAsync()
        {
            spotify ??= await SpotifyConnector.GetClientAsync();
            var playback = await spotify.Player.GetCurrentPlayback();

            if (playback?.Item is FullTrack track)
            {
                string trackName = track.Name;
                string trackArtists = string.Join(", ", track.Artists.Select(a => a.Name));
                int trackLength = track.DurationMs / 1000;
                double currentTime = (double)playback.ProgressMs / 1000;
                bool isPlaying = playback.IsPlaying;

                var state = new PlaybackState
                {
                    TrackName = trackName,
                    TrackArtists = trackArtists,
                    TrackLength = trackLength,
                    CurrentTime = currentTime,
                    IsPlaying = isPlaying
                };

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                System.Diagnostics.Debug.WriteLine($"Playback info: {json}");

                return state;
            }

            return null;
        }



        public async Task<string> getLyricsAsync()
        {

            var playBackState = await GetPlaybackStateAsync();

            if (playBackState == null)
            {
                return "Play smt on Spotify";
            }
            
            if (playBackState.IsPlaying == false)
            {
                return "";
            }

            var lyrics = await LrcLibLyricsProvider.Instance.GetLyricsAsync(
             playBackState.TrackName, playBackState.TrackArtists, playBackState.TrackLength
            );

            if (lyrics == null )
            {
                if (playBackState.CurrentTime < 5)
                {
                    return "No lyrics found";
                }
                else
                {
                    return "";
                }
                
            }
            if (string.IsNullOrEmpty(lyrics.SyncLyrics))
            {
                return "";
            }

            if (!ReferenceEquals(parsedLyricsSource, lyrics.SyncLyrics))
            {
                parsedLyrics = ParseLyrics(lyrics.SyncLyrics);
                parsedLyricsSource = lyrics.SyncLyrics;
            }

            return GetKaraokeLines(parsedLyrics, playBackState.CurrentTime);
        }

        private static readonly Regex TimestampRegex = new(@"^\[(\d+):(\d+(?:[.:]\d+)?)\]");
        private static readonly Regex WordTimestampRegex = new(@"<\d+:\d+(?:[.:]\d+)?>");

        public static List<LyricLine> ParseLyrics(string rawLyrics)
        {
            var lines = new List<LyricLine>();

            foreach (string rawLine in rawLyrics.Split('\n'))
            {
                // a line can have several timestamps, e.g. "[00:12.00][01:30.00]chorus"
                var times = new List<double>();
                string rest = rawLine.Trim();
                Match match;
                while ((match = TimestampRegex.Match(rest)).Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    double seconds = double.Parse(match.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                    times.Add(minutes * 60 + seconds);
                    rest = rest.Substring(match.Length);
                }

                string text = WordTimestampRegex.Replace(rest, "").Trim();
                foreach (double time in times)
                {
                    lines.Add(new LyricLine(time, text));
                }
            }

            // stable sort so lines with equal timestamps keep their order
            return lines.OrderBy(l => l.Time).ToList();
        }


        public static string GetKaraokeLines(List<LyricLine> lyrics, double currentTime)
        {
            if (lyrics == null || lyrics.Count == 0)
                return "";

            // last line that has already started
            int currentLineIndex = -1;
            for (int i = 0; i < lyrics.Count && lyrics[i].Time <= currentTime; i++)
            {
                currentLineIndex = i;
            }

            if (currentLineIndex == -1)
            {
                // song has not reached the first line yet, preview the first two
                string first = lyrics[0].Text;
                string second = lyrics.Count >= 2 ? lyrics[1].Text : "";
                return $"{first}\n{second}";
            }

            // lines alternate between the top (even index) and bottom (odd index) slot,
            // the other slot previews the upcoming line, or keeps the previous one at the end
            string current = lyrics[currentLineIndex].Text;
            string highlighted = string.IsNullOrWhiteSpace(current) ? "" : ">" + current;

            int otherIndex = currentLineIndex + 1 < lyrics.Count ? currentLineIndex + 1 : currentLineIndex - 1;
            string other = otherIndex >= 0 ? lyrics[otherIndex].Text : "";

            return currentLineIndex % 2 == 0
                ? $"{highlighted}\n{other}"
                : $"{other}\n{highlighted}";
        }

    }
}

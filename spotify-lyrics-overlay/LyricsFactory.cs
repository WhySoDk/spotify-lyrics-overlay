using System.Text.Json;
using SpotifyAPI.Web;
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

            return GetKaraokeLines(ParseLyrics(lyrics.SyncLyrics), playBackState.CurrentTime);
        }

        public static List<LyricLine> ParseLyrics(string rawLyrics)
        {
            var lines = new List<LyricLine>();
            var regex = new Regex(@"\[(\d+):(\d+\.\d+)]\s*(.*)");

            foreach (string rawLine in rawLyrics.Split('\n'))
            {
                var match = regex.Match(rawLine);
                if (match.Success)
                {
                    int minutes = int.Parse(match.Groups[1].Value);
                    double seconds = double.Parse(match.Groups[2].Value);
                    string text = match.Groups[3].Value.Trim();

                    lines.Add(new LyricLine(minutes * 60 + seconds, text));
                }
            }

            return lines;
        }


        public static string GetKaraokeLines(List<LyricLine> lyrics, double currentTime)
        {
            if (lyrics == null || lyrics.Count == 0)
                return "";

            int currentLineIndex = -1;
            for (int i = 0; i < lyrics.Count; i++)
            {
                if (lyrics[i].Time <= currentTime)
                    currentLineIndex = i;
                else
                    break;
            }

            if (currentLineIndex == -1)
            {
                if (lyrics.Count >= 2)
                    return $"{lyrics[0].Text}\n{lyrics[1].Text}";
                else if (lyrics.Count == 1)
                    return lyrics[0].Text;
                else
                    return "";
            }

            bool isOddLine = currentLineIndex % 2 == 0;

            string oddLineText = "";
            string evenLineText = "";

            if (isOddLine)
            {
                if (lyrics[currentLineIndex].Text != "")
                {
                    oddLineText = ">" + lyrics[currentLineIndex].Text;
                }
                
                int nextEvenIndex = currentLineIndex + 1;
                if (nextEvenIndex < lyrics.Count)
                    evenLineText = lyrics[nextEvenIndex].Text;
            }
            else
            {
                if (lyrics[currentLineIndex].Text != "")
                {
                    evenLineText = ">" + lyrics[currentLineIndex].Text;
                }

                int nextOddIndex = currentLineIndex + 1;
                if (nextOddIndex < lyrics.Count)
                    oddLineText = lyrics[nextOddIndex].Text;
                else
                {
                    int prevOddIndex = currentLineIndex - 1;
                    if (prevOddIndex >= 0)
                        oddLineText = lyrics[prevOddIndex].Text;
                }
            }

            return $"{oddLineText}\n{evenLineText}";
        }

    }
}

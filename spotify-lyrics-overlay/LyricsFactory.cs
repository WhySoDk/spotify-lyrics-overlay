using System.Diagnostics;
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
        public string? TrackId { get; set; }
        public string? TrackName { get; set; }
        public string? TrackArtists { get; set; }
        public int TrackLength { get; set; }
        public double CurrentTime { get; set; }
        public bool IsPlaying { get; set; }
    }

    //what the overlay should show right now
    internal class LyricsView
    {
        // status text shown as-is (can be empty), null when synced lyrics are available
        public string? Message { get; set; }
        public List<LyricLine>? Lines { get; set; }
        public int CurrentIndex { get; set; } = -1;
        public double CurrentTime { get; set; }
    }

    internal class LyricsFactory
    {
        private static readonly TimeSpan PlayingPollInterval = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan InactiveCheckInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MinPollInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private SpotifyClient? spotify;

        // last playback state from Spotify and when we received it,
        // the position in between polls is estimated locally
        private PlaybackState? playbackState;
        private bool hasPolled;
        private readonly Stopwatch sincePoll = new();

        private string? lyricsTrackId;
        private bool lyricsLoaded;
        private bool lyricsLoading;
        private LyricsResult? lyrics;
        private string? parsedLyricsSource;
        private List<LyricLine> parsedLyrics = new();

        public LyricsFactory()
        {
        }

        //single polling loop, only one request to Spotify is in flight at a time
        public async Task RunPollingAsync(Func<bool> isActive, CancellationToken cancellationToken)
        {
            TimeSpan backoff = TimeSpan.Zero;

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay;

                if (!isActive())
                {
                    // force a fresh poll when the overlay is started again
                    hasPolled = false;
                    delay = InactiveCheckInterval;
                }
                else
                {
                    try
                    {
                        await PollPlaybackAsync(cancellationToken);
                        backoff = TimeSpan.Zero;
                        delay = GetNextPollDelay();
                    }
                    catch (APITooManyRequestsException ex)
                    {
                        delay = ex.RetryAfter + TimeSpan.FromSeconds(1);
                        Debug.WriteLine($"Spotify rate limit hit, retrying in {delay.TotalSeconds}s");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // timeouts, network errors, failed login: back off exponentially
                        backoff = backoff == TimeSpan.Zero
                            ? TimeSpan.FromSeconds(2)
                            : TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                        delay = backoff;
                        Debug.WriteLine($"Error polling Spotify, retrying in {delay.TotalSeconds}s: {ex.Message}");
                    }
                }

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task PollPlaybackAsync(CancellationToken cancellationToken)
        {
            spotify ??= await SpotifyConnector.GetClientAsync();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);
            var playback = await spotify.Player.GetCurrentPlayback(timeout.Token);

            PlaybackState? state = null;
            if (playback?.Item is FullTrack track)
            {
                state = new PlaybackState
                {
                    TrackId = track.Id ?? track.Uri,
                    TrackName = track.Name,
                    TrackArtists = string.Join(", ", track.Artists.Select(a => a.Name)),
                    TrackLength = track.DurationMs / 1000,
                    CurrentTime = (double)playback.ProgressMs / 1000,
                    IsPlaying = playback.IsPlaying
                };

                if (state.TrackId != playbackState?.TrackId)
                {
                    Debug.WriteLine($"Now playing: {state.TrackName} by {state.TrackArtists}");
                }
            }

            playbackState = state;
            hasPolled = true;
            sincePoll.Restart();

            if (state != null)
            {
                UpdateLyricsForTrack(state);
            }
        }

        private TimeSpan GetNextPollDelay()
        {
            var state = playbackState;
            if (state == null || !state.IsPlaying)
            {
                return IdlePollInterval;
            }

            // poll right after the song ends so the next song is picked up quickly
            var untilTrackEnd = TimeSpan.FromSeconds(state.TrackLength - state.CurrentTime) + TimeSpan.FromMilliseconds(300);
            if (untilTrackEnd < PlayingPollInterval)
            {
                return untilTrackEnd < MinPollInterval ? MinPollInterval : untilTrackEnd;
            }
            return PlayingPollInterval;
        }

        private void UpdateLyricsForTrack(PlaybackState state)
        {
            if (state.TrackId != lyricsTrackId)
            {
                lyricsTrackId = state.TrackId;
                lyricsLoaded = false;
                lyrics = null;
            }

            if (lyricsLoaded || lyricsLoading) return;

            _ = LoadLyricsAsync(state);
        }

        private async Task LoadLyricsAsync(PlaybackState state)
        {
            lyricsLoading = true;
            try
            {
                var result = await LrcLibLyricsProvider.Instance.GetLyricsAsync(
                    state.TrackName ?? "", state.TrackArtists ?? "", state.TrackLength
                );

                // ignore the result if the song changed while loading
                if (state.TrackId == lyricsTrackId)
                {
                    lyrics = result;
                    lyricsLoaded = true;
                }
            }
            finally
            {
                lyricsLoading = false;
            }
        }

        //estimated playback position, without asking Spotify
        private double GetEstimatedTime(PlaybackState state)
        {
            double time = state.CurrentTime;
            if (state.IsPlaying)
            {
                time += sincePoll.Elapsed.TotalSeconds;
            }
            return Math.Min(time, state.TrackLength);
        }

        public LyricsView getLyricsView()
        {
            if (!hasPolled)
            {
                return new LyricsView { Message = "" };
            }

            var playBackState = playbackState;

            if (playBackState == null)
            {
                return new LyricsView { Message = "Play smt on Spotify" };
            }

            if (playBackState.IsPlaying == false)
            {
                return new LyricsView { Message = "" };
            }

            double currentTime = GetEstimatedTime(playBackState);

            if (!lyricsLoaded)
            {
                return new LyricsView { Message = "" };
            }

            if (lyrics == null)
            {
                if (currentTime < 5)
                {
                    return new LyricsView { Message = "No lyrics found" };
                }
                else
                {
                    return new LyricsView { Message = "" };
                }

            }
            if (string.IsNullOrEmpty(lyrics.SyncLyrics))
            {
                return new LyricsView { Message = "" };
            }

            if (!ReferenceEquals(parsedLyricsSource, lyrics.SyncLyrics))
            {
                parsedLyrics = ParseLyrics(lyrics.SyncLyrics);
                parsedLyricsSource = lyrics.SyncLyrics;
            }

            return new LyricsView
            {
                Lines = parsedLyrics,
                CurrentIndex = FindCurrentLineIndex(parsedLyrics, currentTime),
                CurrentTime = currentTime
            };
        }

        //legacy two line karaoke text
        public string getLyrics()
        {
            var view = getLyricsView();
            return view.Message ?? GetKaraokeLines(view.Lines!, view.CurrentTime);
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

            int currentLineIndex = FindCurrentLineIndex(lyrics, currentTime);

            if (currentLineIndex == -1)
            {
                // song has not reached the first line yet, preview the first two
                string first = lyrics[0].Text;
                string second = lyrics.Count >= 2 ? lyrics[1].Text : "";
                return $"{first}\n{second}";
            }

            // lyrics are over, e.g. an empty end marker followed by an instrumental outro
            if (IsLyricsEnded(lyrics, currentLineIndex))
                return "";

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

        //last line that has already started, -1 before the first line
        public static int FindCurrentLineIndex(List<LyricLine> lyrics, double currentTime)
        {
            int currentLineIndex = -1;
            for (int i = 0; i < lyrics.Count && lyrics[i].Time <= currentTime; i++)
            {
                currentLineIndex = i;
            }
            return currentLineIndex;
        }

        //true when the current line is empty and no lyrics come after it
        public static bool IsLyricsEnded(List<LyricLine> lyrics, int currentLineIndex)
        {
            for (int i = currentLineIndex; i < lyrics.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(lyrics[i].Text))
                    return false;
            }
            return true;
        }

    }
}

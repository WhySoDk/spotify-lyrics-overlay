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
        // only known from a Lyricsfile, null otherwise
        public double? EndTime { get; set; }
        public List<LyricWord>? Words { get; set; }

        public LyricLine(double time, string text)
        {
            Time = time;
            Text = text;
        }
    }

    internal record LyricWord(double Time, double? EndTime, string Text);

    internal class PlaybackState
    {
        public string? TrackId { get; set; }
        public string? TrackName { get; set; }
        public string? TrackArtists { get; set; }
        public string? TrackArtist { get; set; }
        public int TrackLength { get; set; }
        public double CurrentTime { get; set; }
        public bool IsPlaying { get; set; }
        public string? AlbumImageUrl { get; set; }
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

    //lyrics of a song as the song pill shows them
    internal enum SongLyricsStatus { Loading, Synced, Plain, Instrumental, NotFound, Failed }

    internal class LyricsFactory
    {
        private static readonly TimeSpan PlayingPollInterval = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan InactiveCheckInterval = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan MinPollInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan StatusDuration = TimeSpan.FromSeconds(5);
        // after Windows says the song changed, Spotify's API can still have the old one for a moment
        private static readonly TimeSpan NewTrackRetryInterval = TimeSpan.FromMilliseconds(300);
        private const int NewTrackRetries = 3;

        private SpotifyClient? spotify;

        // last playback state from Spotify and when we received it,
        // the position in between polls is estimated locally
        private PlaybackState? playbackState;
        private bool hasPolled;
        private bool spotifyFailed;
        private readonly Stopwatch sincePoll = new();

        // Spotify's Windows media info, wakes the polling loop as soon as the song changes or pauses
        private readonly SpotifyMediaWatcher mediaWatcher = new();
        private readonly SemaphoreSlim pollNow = new(0, 1);
        private int newTrackRetriesLeft;
        private string? trackIdBeforeChange;

        private string? lyricsTrackId;
        private bool lyricsLoaded;
        private bool lyricsLoading;
        private bool lyricsFailed;

        // status messages are shown for a few seconds, once per song
        private string? statusKey;
        private readonly Stopwatch statusClock = new();
        private LyricsResult? lyrics;
        private LyricsResult? parsedLyricsSource;
        private List<LyricLine> parsedLyrics = new();

        // lyrics color from the album cover, only loaded while the option is on
        private string? albumColorTrackId;
        // song whose color is being picked, a new song doesn't wait for the previous one
        private string? albumColorLoadingTrackId;
        public Color? AlbumColor { get; private set; }

        //true while the album color for the playing song is still being picked
        public bool AlbumColorPending => playbackState?.TrackId is string trackId && albumColorTrackId != trackId;

        //song from the last Spotify poll, null when nothing is playing
        public PlaybackState? CurrentSong => playbackState;

        //a different song started, not raised again when the same song repeats
        public event Action<PlaybackState>? SongChanged;
        // last song SongChanged was raised for, kept while nothing is playing so a paused song doesn't count as new
        private string? announcedTrackId;

        public LyricsFactory()
        {
        }

        //single polling loop, only one request to Spotify is in flight at a time
        public async Task RunPollingAsync(Func<bool> isActive, CancellationToken cancellationToken)
        {
            TimeSpan backoff = TimeSpan.Zero;

            mediaWatcher.TrackChanged += track => RequestPoll(newTrack: true);
            mediaWatcher.PlaybackChanged += () => RequestPoll(newTrack: false);
            await mediaWatcher.StartAsync();

            while (!cancellationToken.IsCancellationRequested)
            {
                TimeSpan delay;

                if (!isActive())
                {
                    // force a fresh poll when the overlay is started again
                    hasPolled = false;
                    statusKey = null;
                    announcedTrackId = null;
                    delay = InactiveCheckInterval;
                }
                else
                {
                    try
                    {
                        await PollPlaybackAsync(cancellationToken);
                        spotifyFailed = false;
                        backoff = TimeSpan.Zero;
                        delay = GetNextPollDelay();

                        if (newTrackRetriesLeft > 0)
                        {
                            if (playbackState?.TrackId == trackIdBeforeChange)
                            {
                                newTrackRetriesLeft--;
                                delay = NewTrackRetryInterval;
                            }
                            else
                            {
                                newTrackRetriesLeft = 0;
                            }
                        }
                    }
                    catch (APITooManyRequestsException ex)
                    {
                        delay = ex.RetryAfter + TimeSpan.FromSeconds(1);
                        Log.Write($"Spotify rate limit hit, retrying in {delay.TotalSeconds}s");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // timeouts, network errors, failed login: back off exponentially
                        spotifyFailed = true;
                        backoff = backoff == TimeSpan.Zero
                            ? TimeSpan.FromSeconds(2)
                            : TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                        delay = backoff;
                        Log.Write($"Error polling Spotify, retrying in {delay.TotalSeconds}s: {ex.GetType().Name}: {ex.Message}");

                        // the login was revoked or the refresh token stopped working,
                        // connect from scratch so the user is asked to log in again
                        if (IsLoginRejected(ex))
                        {
                            Log.Write("Spotify rejected the login, connecting again");
                            spotify = null;
                            SpotifyConnector.Reset();
                        }
                    }
                }

                try
                {
                    // RequestPoll ends the wait early
                    await pollNow.WaitAsync(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        //poll Spotify now instead of waiting for the next poll, newTrack when the song changed
        private void RequestPoll(bool newTrack)
        {
            if (newTrack)
            {
                newTrackRetriesLeft = NewTrackRetries;
                trackIdBeforeChange = playbackState?.TrackId;
            }
            if (pollNow.CurrentCount == 0) pollNow.Release();
        }

        //401 from the API, or 400 (invalid_grant) when the token refresh is refused
        private static bool IsLoginRejected(Exception ex)
        {
            return ex is APIUnauthorizedException
                || (ex is APIException api && api.Response?.StatusCode == System.Net.HttpStatusCode.BadRequest);
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
                    TrackArtist = track.Artists.FirstOrDefault()?.Name,
                    TrackLength = track.DurationMs / 1000,
                    CurrentTime = (double)playback.ProgressMs / 1000,
                    IsPlaying = playback.IsPlaying,
                    // smallest cover (64px) is enough for picking a color and the song pill
                    AlbumImageUrl = track.Album?.Images?.OrderBy(image => image.Width).FirstOrDefault()?.Url
                };

                if (state.TrackId != playbackState?.TrackId)
                {
                    Log.Write($"Now playing: {state.TrackName} by {state.TrackArtists}");
                }
            }

            playbackState = state;
            hasPolled = true;
            sincePoll.Restart();

            if (state != null)
            {
                UpdateLyricsForTrack(state);

                if (state.TrackId != announcedTrackId)
                {
                    announcedTrackId = state.TrackId;
                    SongChanged?.Invoke(state);
                }
            }
        }

        //lyrics status of a song, Loading while its lyrics aren't looked up yet
        public SongLyricsStatus GetLyricsStatus(string? trackId)
        {
            if (trackId == null || trackId != lyricsTrackId) return SongLyricsStatus.Loading;
            if (!lyricsLoaded) return lyricsFailed ? SongLyricsStatus.Failed : SongLyricsStatus.Loading;
            if (lyrics == null) return SongLyricsStatus.NotFound;

            return GetParsedLyrics().Count > 0 ? SongLyricsStatus.Synced
                : !string.IsNullOrWhiteSpace(lyrics.PlainLyrics) ? SongLyricsStatus.Plain
                : lyrics.Instrumental ? SongLyricsStatus.Instrumental
                : SongLyricsStatus.NotFound;
        }

        //true when the lyrics of the song were set by hand (a lrclib link, pasted or marked instrumental)
        public bool IsSetByHand(string? trackId)
        {
            return trackId != null && trackId == lyricsTrackId && lyrics?.Manual == true;
        }

        //mark the song as instrumental, or forget that and look its lyrics up again
        public void SetInstrumental(PlaybackState song, bool instrumental)
        {
            if (instrumental)
                LrcLibLyricsProvider.Instance.SetInstrumental(song.TrackName ?? "", song.TrackArtists ?? "", song.TrackLength);
            else
                LrcLibLyricsProvider.Instance.ClearCache(song.TrackName ?? "", song.TrackArtists ?? "", song.TrackLength);
            ReloadSong(song.TrackId);
        }

        //synced lines of the loaded lyrics, parsed once per lyrics
        private List<LyricLine> GetParsedLyrics()
        {
            if (!ReferenceEquals(parsedLyricsSource, lyrics))
            {
                // the Lyricsfile has more timing detail, the LRC lyrics are the fallback (and all older cached songs have)
                parsedLyrics = lyrics == null ? new List<LyricLine>()
                    : Lyricsfile.ParseLines(lyrics.Lyricsfile) is { Count: > 0 } lines ? lines
                    : !string.IsNullOrEmpty(lyrics.SyncLyrics) ? ParseLyrics(lyrics.SyncLyrics)
                    : new List<LyricLine>();
                parsedLyricsSource = lyrics;
            }
            return parsedLyrics;
        }

        //look up the lyrics and album color of the playing song again, skipping every cache.
        //false when nothing is playing
        public bool RefetchCurrentSong()
        {
            var state = playbackState;
            if (state == null) return false;

            LrcLibLyricsProvider.Instance.ClearCache(state.TrackName ?? "", state.TrackArtists ?? "", state.TrackLength);
            if (state.AlbumImageUrl != null) spotify_lyrics_overlay.AlbumColor.Forget(state.AlbumImageUrl);
            ReloadSong(state.TrackId);
            return true;
        }

        //load the lyrics and album color of the song again if it's still playing, from the cache when there is one
        public void ReloadSong(string? trackId)
        {
            var state = playbackState;
            if (state == null || state.TrackId != trackId) return;

            // a different id makes UpdateLyricsForTrack start over
            lyricsTrackId = null;
            statusKey = null;
            UpdateLyricsForTrack(state);
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
                lyricsFailed = false;
                lyrics = null;
                AlbumColor = null;
                albumColorTrackId = null;
            }

            UpdateAlbumColorForTrack(state);

            if (lyricsLoaded || lyricsLoading) return;

            _ = LoadLyricsAsync(state);
        }

        private async Task LoadLyricsAsync(PlaybackState state)
        {
            lyricsLoading = true;
            try
            {
                var lookup = await LrcLibLyricsProvider.Instance.GetLyricsAsync(
                    state.TrackName ?? "", state.TrackArtists ?? "", state.TrackArtist ?? "", state.TrackLength
                );

                // ignore the result if the song changed while loading,
                // a failed request is tried again on a later poll
                if (state.TrackId == lyricsTrackId)
                {
                    lyricsFailed = lookup.Failed;
                    if (!lookup.Failed)
                    {
                        lyrics = lookup.Result;
                        lyricsLoaded = true;
                    }
                }
            }
            finally
            {
                lyricsLoading = false;
            }
        }

        private void UpdateAlbumColorForTrack(PlaybackState state)
        {
            if (!ConfigManager.Instance.LoadConfig().albumColor) return;

            // the cover is known right away, so the color is picked alongside the lyrics lookup
            if (state.TrackId != lyricsTrackId || albumColorLoadingTrackId == state.TrackId || albumColorTrackId == state.TrackId) return;

            _ = LoadAlbumColorAsync(state);
        }

        private async Task LoadAlbumColorAsync(PlaybackState state)
        {
            albumColorLoadingTrackId = state.TrackId;
            try
            {
                Color? color = state.AlbumImageUrl != null
                    ? await spotify_lyrics_overlay.AlbumColor.FromImageUrlAsync(state.AlbumImageUrl)
                    : null;

                // ignore the result if the song changed while loading
                if (state.TrackId == lyricsTrackId)
                {
                    AlbumColor = color;
                    albumColorTrackId = state.TrackId;
                }
            }
            finally
            {
                if (albumColorLoadingTrackId == state.TrackId) albumColorLoadingTrackId = null;
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
                return new LyricsView { Message = spotifyFailed ? briefly("spotify", "(Can't connect to Spotify)") : "" };
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
            string track = playBackState.TrackId ?? "";

            if (!lyricsLoaded)
            {
                return new LyricsView { Message = lyricsFailed ? briefly(track + ":failed", "(Couldn't load lyrics)") : "" };
            }

            if (lyrics == null)
            {
                return new LyricsView { Message = briefly(track + ":none", "No lyrics found") };
            }

            var syncedLines = GetParsedLyrics();
            if (syncedLines.Count == 0)
            {
                string message = !string.IsNullOrWhiteSpace(lyrics.SyncLyrics) ? "(Can't parse lyrics format)"
                    : !string.IsNullOrWhiteSpace(lyrics.PlainLyrics) ? "(Lyrics not sync)"
                    : lyrics.Instrumental ? "(Instrumental)"
                    : "No lyrics found";
                return new LyricsView { Message = briefly(track + ":" + message, message) };
            }

            return new LyricsView
            {
                Lines = syncedLines,
                CurrentIndex = FindCurrentLineIndex(syncedLines, currentTime),
                CurrentTime = currentTime
            };
        }

        //text for the first few seconds a status (key) is shown, empty after that
        private string briefly(string key, string text)
        {
            if (key != statusKey)
            {
                statusKey = key;
                statusClock.Restart();
            }
            return statusClock.Elapsed < StatusDuration ? text : "";
        }

        //legacy two line karaoke text
        public string getLyrics()
        {
            var view = getLyricsView();
            return view.Message ?? GetKaraokeLines(view.Lines!, view.CurrentTime);
        }

        private static readonly Regex TimestampRegex = new(@"^\[(\d+):(\d+(?:[.:]\d+)?)\]");
        private static readonly Regex WordTimestampRegex = new(@"<\d+:\d+(?:[.:]\d+)?>");
        // [offset:+500] in milliseconds, a positive offset shows the lyrics earlier
        private static readonly Regex OffsetTagRegex = new(@"^\[offset:\s*([+-]?\d{1,9})\s*\]", RegexOptions.IgnoreCase);

        public static List<LyricLine> ParseLyrics(string rawLyrics)
        {
            var lines = new List<LyricLine>();
            double offset = 0;

            // Trim() leaves the byte order mark of a pasted file in front of the first timestamp
            foreach (string rawLine in rawLyrics.TrimStart('﻿').Split('\n'))
            {
                // a line can have several timestamps, e.g. "[00:12.00][01:30.00]chorus"
                var times = new List<double>();
                string rest = rawLine.Trim();

                var offsetMatch = OffsetTagRegex.Match(rest);
                if (offsetMatch.Success)
                {
                    offset = int.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture) / 1000.0;
                    continue;
                }

                Match match;
                while ((match = TimestampRegex.Match(rest)).Success)
                {
                    // a time too big to parse ends the timestamps
                    if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int minutes)
                        || !double.TryParse(match.Groups[2].Value.Replace(':', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds))
                        break;
                    times.Add(minutes * 60 + seconds);
                    rest = rest.Substring(match.Length);
                }

                string text = WordTimestampRegex.Replace(rest, "").Trim();
                foreach (double time in times)
                {
                    lines.Add(new LyricLine(time, text));
                }
            }

            foreach (var line in lines)
            {
                line.Time = Math.Max(0, line.Time - offset);
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

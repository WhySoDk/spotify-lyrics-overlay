using Windows.Media.Control;

namespace spotify_lyrics_overlay
{
    //title and artist as the Windows media info has them
    internal record MediaTrack(string Title, string Artist);

    //watches Spotify's Windows media session (the one the media keys popup shows), every other player is ignored.
    //Windows tells right away when the song changes or pauses, the Spotify API is only polled every 1.5s.
    //read only, when it isn't available the app just keeps polling
    internal sealed class SpotifyMediaWatcher
    {
        //raised on the thread that called StartAsync (the UI thread)
        public event Action<MediaTrack>? TrackChanged;
        public event Action? PlaybackChanged;

        // Windows sends the title, artist and cover in separate updates, wait for them to settle
        private const int SettleMs = 150;

        private SynchronizationContext? context;
        private GlobalSystemMediaTransportControlsSessionManager? manager;
        private GlobalSystemMediaTransportControlsSession? session;
        private readonly object sync = new();
        private MediaTrack? lastTrack;
        private int trackVersion;
        private GlobalSystemMediaTransportControlsSessionPlaybackStatus? lastStatus;

        public async Task StartAsync()
        {
            context = SynchronizationContext.Current;
            try
            {
                manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                manager.SessionsChanged += (s, e) => attach();
                attach();
            }
            catch (Exception ex)
            {
                Log.Write($"Windows media info unavailable, only polling Spotify: {ex.Message}");
            }
        }

        //desktop app is "Spotify.exe", the Microsoft Store one "SpotifyAB.SpotifyMusic_...!Spotify"
        private static bool isSpotify(GlobalSystemMediaTransportControlsSession s) =>
            s.SourceAppUserModelId?.Contains("Spotify", StringComparison.OrdinalIgnoreCase) == true;

        //follow Spotify's session as apps open and close
        private void attach()
        {
            try
            {
                var spotify = manager?.GetSessions().FirstOrDefault(isSpotify);
                lock (sync)
                {
                    if (spotify?.SourceAppUserModelId == session?.SourceAppUserModelId) return;

                    if (session != null)
                    {
                        session.MediaPropertiesChanged -= onMediaPropertiesChanged;
                        session.PlaybackInfoChanged -= onPlaybackInfoChanged;
                    }
                    session = spotify;
                    lastTrack = null;
                    lastStatus = null;
                    if (session == null) return;

                    session.MediaPropertiesChanged += onMediaPropertiesChanged;
                    session.PlaybackInfoChanged += onPlaybackInfoChanged;
                }
                Log.Write($"Watching Windows media info of {spotify!.SourceAppUserModelId}");
                onMediaPropertiesChanged(spotify, null);
            }
            catch (Exception ex)
            {
                Log.Write($"Error finding Spotify's media session: {ex.Message}");
            }
        }

        private async void onMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs? args)
        {
            int version = Interlocked.Increment(ref trackVersion);
            try
            {
                await Task.Delay(SettleMs);
                if (version != Volatile.Read(ref trackVersion)) return;

                var properties = await sender.TryGetMediaPropertiesAsync();
                if (properties == null || string.IsNullOrWhiteSpace(properties.Title)) return;

                var track = new MediaTrack(properties.Title, properties.Artist ?? "");
                lock (sync)
                {
                    if (session == null || track == lastTrack) return;
                    lastTrack = track;
                }
                post(() => TrackChanged?.Invoke(track));
            }
            catch (Exception ex)
            {
                Log.Write($"Error reading Windows media info: {ex.Message}");
            }
        }

        private void onPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            try
            {
                var status = sender.GetPlaybackInfo()?.PlaybackStatus;
                lock (sync)
                {
                    if (session == null || status == lastStatus) return;
                    lastStatus = status;
                }
                post(() => PlaybackChanged?.Invoke());
            }
            catch (Exception ex)
            {
                Log.Write($"Error reading Windows playback info: {ex.Message}");
            }
        }

        private void post(Action action)
        {
            if (context != null) context.Post(_ => action(), null);
            else action();
        }
    }
}

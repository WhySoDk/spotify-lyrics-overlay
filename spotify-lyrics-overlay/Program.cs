namespace spotify_lyrics_overlay
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // config.json, token.json, the cache and the logs live next to the exe,
            // Windows starts apps at sign in from System32
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            // a second copy would fight over token.json and the login callback port
            using var instanceMutex = new Mutex(true, @"Local\SpotifyLyricsOverlay", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                MessageBox.Show("Spotify Lyrics Overlay is already running.", "Spotify Lyrics Overlay",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var app = new System.Windows.Application();
            Log.CatchUnhandled(app);
            Log.Write($"Starting v{typeof(Program).Assembly.GetName().Version?.ToString(3)}");
#pragma warning disable WPF0001
            app.ThemeMode = System.Windows.ThemeMode.Dark;
#pragma warning restore WPF0001
            app.Run(new SettingsWindow());
        }
    }
}

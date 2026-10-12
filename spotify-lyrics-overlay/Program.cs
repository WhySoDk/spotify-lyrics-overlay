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

            // a second copy would fight over token.json and the login callback port,
            // it opens the settings of the running one instead
            using var instanceMutex = new Mutex(true, @"Local\SpotifyLyricsOverlay", out bool isFirstInstance);
            using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SpotifyLyricsOverlay.Show");
            if (!isFirstInstance)
            {
                showEvent.Set();
                return;
            }

            var app = new System.Windows.Application();
            Log.CatchUnhandled(app);
            Log.Write($"Starting v{typeof(Program).Assembly.GetName().Version?.ToString(3)}");
#pragma warning disable WPF0001
            app.ThemeMode = System.Windows.ThemeMode.Dark;
#pragma warning restore WPF0001
            // the app lives in the tray, closing the window doesn't end it
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

            var window = new SettingsWindow();
            ThreadPool.RegisterWaitForSingleObject(showEvent,
                (_, _) => window.Dispatcher.BeginInvoke(window.ShowFromTray), null, Timeout.Infinite, executeOnlyOnce: false);
            window.Show();
            app.Run();
        }
    }
}

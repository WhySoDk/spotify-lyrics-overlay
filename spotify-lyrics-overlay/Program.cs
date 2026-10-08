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

            var app = new System.Windows.Application();
#pragma warning disable WPF0001
            app.ThemeMode = System.Windows.ThemeMode.Dark;
#pragma warning restore WPF0001
            app.Run(new SettingsWindow());
        }
    }
}

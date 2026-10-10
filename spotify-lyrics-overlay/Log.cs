using System.Diagnostics;

namespace spotify_lyrics_overlay
{
    //text log in logs/app.log, the previous one is kept as app.old.log once it gets too big
    internal static class Log
    {
        private const string LogDirectory = "logs";
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "app.log");
        private static readonly string OldLogFilePath = Path.Combine(LogDirectory, "app.old.log");
        private const long MaxLength = 1_000_000;
        private static readonly object sync = new();

        public static void Write(string message)
        {
            Debug.WriteLine(message);

            lock (sync)
            {
                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    var file = new FileInfo(LogFilePath);
                    if (file.Exists && file.Length > MaxLength)
                    {
                        File.Move(LogFilePath, OldLogFilePath, overwrite: true);
                    }
                    File.AppendAllText(LogFilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
                catch
                {
                    // logging must never take the app down
                }
            }
        }

        public static void Error(string context, Exception ex)
        {
            Write($"{context}: {ex}");
        }

        //log crashes and exceptions nobody waited for
        public static void CatchUnhandled(System.Windows.Application app)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write($"Unhandled exception (terminating: {e.IsTerminating}): {e.ExceptionObject}");
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Error("Unobserved task exception", e.Exception);
                e.SetObserved();
            };
            app.DispatcherUnhandledException += (_, e) => Error("Unhandled UI exception", e.Exception);
        }
    }
}

using System.Diagnostics;

namespace spotify_lyrics_overlay
{
    //latest lyrics lookup step, shown above the lyrics for a few seconds when debug info is on
    internal static class DebugStatus
    {
        private static readonly TimeSpan ShowDuration = TimeSpan.FromSeconds(5);
        private static readonly Stopwatch clock = new();
        private static readonly object sync = new();
        private static string? text;
        private static bool isCacheHit;

        //cacheHit messages can be hidden on their own in the settings
        public static void Show(string message, bool cacheHit = false)
        {
            lock (sync)
            {
                text = message;
                isCacheHit = cacheHit;
                clock.Restart();
            }
            Debug.WriteLine($"[lyrics] {message}");
        }

        //null once the message has been shown long enough
        public static string? Current(bool showCacheHits)
        {
            lock (sync)
            {
                if (text == null || clock.Elapsed >= ShowDuration || (isCacheHit && !showCacheHits)) return null;
                return text;
            }
        }

        //"1d 5h", "3h", "under 1h"
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.FromHours(1)) return "under 1h";
            return duration.Days > 0 ? $"{duration.Days}d {duration.Hours}h" : $"{duration.Hours}h";
        }
    }
}

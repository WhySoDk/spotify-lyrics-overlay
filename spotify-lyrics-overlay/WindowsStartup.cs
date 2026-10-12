using Microsoft.Win32;

namespace spotify_lyrics_overlay
{
    //starts the app in the tray when the user signs in, through the user's Run key
    internal static class WindowsStartup
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SpotifyLyricsOverlay";
        public const string TrayArgument = "--tray";

        private static string Command => $"\"{Environment.ProcessPath}\" {TrayArgument}";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                    return key?.GetValue(ValueName) != null;
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to read the startup entry", ex);
                    return false;
                }
            }
        }

        public static void Set(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (enabled) key.SetValue(ValueName, Command);
                else key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to change the startup entry", ex);
            }
        }

        //the exe may have moved since the entry was written
        public static void Refresh()
        {
            if (IsEnabled) Set(true);
        }
    }
}

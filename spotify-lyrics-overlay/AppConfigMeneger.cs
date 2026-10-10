using System.Text.Json;

namespace spotify_lyrics_overlay
{
    public class ConfigManager
    {
        private const string ConfigFilePath = "config.json";
        private static readonly Lazy<ConfigManager> instance = new(() => new ConfigManager());

        public static ConfigManager Instance => instance.Value;

        public AppConfig Config { get; private set; }

        private ConfigManager()
        {
            if (File.Exists(ConfigFilePath) && ReadConfig() is AppConfig config)
            {
                Config = config;
            }
            else
            {
                Config = new AppConfig
                {
                    newlyGenerated = true,
                    fontName = "Helvetica",
                    secondaryFontName = "",
                    fontSize = 12,
                    bold = false,
                    italic = false,
                    dropShadow = false,
                    fontColorHex = "#f3ce32",
                    albumColor = true,
                    screenName = "",
                    xOffset = 0,
                    yOffset = 540,
                    apiKey = "",
                    rememberApiKey = false,
                    rememberRunState = false,
                    wasStarted = false,
                    backgroundEnabled = false,
                    backgroundColorHex = "#000000",
                    backgroundOpacity = 70,
                    backgroundSpread = 12,
                    transitionMode = Transitions.LyricsTransitions.DefaultId,
                    songPill = true,
                    songPillWindowsMedia = false,
                    debugEnabled = false,
                    debugShowCacheHits = true
                };

                //System.Diagnostics.Debug.WriteLine("Check read json" + Config.ToString());
                SaveConfig();
            }
        }

        //null when the file can't be read, it is kept as config.json.broken so the settings aren't lost for good
        private static AppConfig? ReadConfig()
        {
            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new JsonException("config is null");
            }
            catch (Exception ex)
            {
                Log.Error("Config unreadable, starting with the defaults", ex);
                try
                {
                    File.Copy(ConfigFilePath, ConfigFilePath + ".broken", overwrite: true);
                }
                catch (Exception copyEx)
                {
                    Log.Error("Failed to keep the broken config", copyEx);
                }
                return null;
            }
        }

        public AppConfig LoadConfig() => Config;

        public void SaveConfig()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(Config, options);
            AtomicFile.WriteAllText(ConfigFilePath, json);
        }
    }
}

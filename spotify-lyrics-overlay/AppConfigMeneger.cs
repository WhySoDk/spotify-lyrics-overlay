using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;

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
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                Config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })!;
            }
            else
            {
                Config = new AppConfig
                {
                    newlyGenerated = true,
                    fontName = "Helvetica",
                    fontSize = 12,
                    bold = false,
                    italic = false,
                    dropShadow = false,
                    fontColorHex = "#f3ce32",
                    screenName = "",
                    xOffset = 0,
                    yOffset = 540,
                    apiKey = "",
                    rememberApiKey = false
                };

                //System.Diagnostics.Debug.WriteLine("Check read json" + Config.ToString());
                SaveConfig();
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
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}

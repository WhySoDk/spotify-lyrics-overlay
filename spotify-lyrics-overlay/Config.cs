using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace spotify_lyrics_overlay
{
    public class AppConfig
    {
        public Boolean newlyGenerated { get; set; }
        public String fontName { get; set; } = "";
        public int fontSize { get; set; }
        public Boolean bold { get; set; }
        public Boolean italic { get; set; }
        public Boolean dropShadow { get; set; }
        public String fontColorHex { get; set; } = "";
        public String screenName { get; set; } = "";
        public int xOffset { get; set; }
        public int yOffset { get; set; }
        public String apiKey { get; set; } = "";
        public Boolean rememberApiKey { get; set; }

        public override string ToString()
        {
            return $"AppConfig(newlyGenerated: {newlyGenerated}, fontName: {fontName}, fontSize: {fontSize}, " +
                   $"bold: {bold}, italic: {italic}, dropShadow: {dropShadow}, fontColorHex: {fontColorHex}, " +
                   $"screenName: {screenName}, xOffset: {xOffset}, yOffset: {yOffset}, apiKey: {apiKey}, rememberApiKey: {rememberApiKey})";
        }

    }
}

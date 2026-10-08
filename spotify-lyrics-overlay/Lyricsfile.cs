using System.Globalization;
using YamlDotNet.RepresentationModel;

namespace spotify_lyrics_overlay
{
    //reader for the Lyricsfile 1.0 draft (https://github.com/tranxuanthang/lyricsfile), the YAML lyrics format lrclib returns
    internal static class Lyricsfile
    {
        // a whole song is a few KB, anything much bigger is not lyrics
        private const int MaxLength = 1_000_000;

        //synced lines sorted by start time, null when the document isn't a valid version 1.0 Lyricsfile
        public static List<LyricLine>? ParseLines(string? yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml) || yaml.Length > MaxLength) return null;

            try
            {
                var stream = new YamlStream();
                // throws on duplicate keys
                stream.Load(new StringReader(yaml));
                if (stream.Documents.Count != 1 || stream.Documents[0].RootNode is not YamlMappingNode root) return null;

                // unknown versions must not be read as 1.0
                if (GetString(root, "version") != "1.0") return null;

                var lines = new List<LyricLine>();
                if (Get(root, "lines") is YamlSequenceNode lineNodes)
                {
                    foreach (var node in lineNodes)
                    {
                        if (node is YamlMappingNode lineNode && ReadLine(lineNode) is LyricLine line)
                            lines.Add(line);
                    }
                }

                // stable sort so lines with equal start times keep their order
                return lines.OrderBy(l => l.Time).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing Lyricsfile: {ex.Message}");
                return null;
            }
        }

        //null when the line is missing its text or start time
        private static LyricLine? ReadLine(YamlMappingNode node)
        {
            string? text = GetString(node, "text");
            if (text == null || GetMs(node, "start_ms") is not int start) return null;

            var line = new LyricLine(start / 1000.0, text.Trim()) { EndTime = GetEnd(node, start) };

            if (Get(node, "words") is YamlSequenceNode wordNodes)
            {
                var words = new List<LyricWord>();
                foreach (var wordNode in wordNodes.OfType<YamlMappingNode>())
                {
                    string? wordText = GetString(wordNode, "text");
                    if (wordText == null || GetMs(wordNode, "start_ms") is not int wordStart) continue;
                    words.Add(new LyricWord(wordStart / 1000.0, GetEnd(wordNode, wordStart), wordText));
                }
                if (words.Count > 0) line.Words = words.OrderBy(w => w.Time).ToList();
            }

            return line;
        }

        //end time in seconds, ignored when it's before the start
        private static double? GetEnd(YamlMappingNode node, int start)
        {
            return GetMs(node, "end_ms") is int end && end >= start ? end / 1000.0 : null;
        }

        private static int? GetMs(YamlMappingNode node, string key)
        {
            return GetString(node, key) is string value
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int ms) ? ms : null;
        }

        private static string? GetString(YamlMappingNode node, string key)
        {
            return Get(node, key) is YamlScalarNode scalar ? scalar.Value : null;
        }

        private static YamlNode? Get(YamlMappingNode node, string key)
        {
            return node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;
        }
    }
}

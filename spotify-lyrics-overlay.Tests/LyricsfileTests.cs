namespace spotify_lyrics_overlay.Tests
{
    public class LyricsfileTests
    {
        [Fact]
        public void ParsesLinesAndWords()
        {
            var lines = Lyricsfile.ParseLines("""
                version: "1.0"
                lines:
                  - text: second
                    start_ms: 5000
                  - text: " first "
                    start_ms: 1000
                    end_ms: 4000
                    words:
                      - text: fir
                        start_ms: 1000
                        end_ms: 2000
                      - text: st
                        start_ms: 2000
                """);

            Assert.NotNull(lines);
            Assert.Equal(new[] { "first", "second" }, lines!.Select(l => l.Text));
            Assert.Equal(1, lines[0].Time, 3);
            Assert.Equal(4, lines[0].EndTime);
            Assert.Null(lines[1].EndTime);
            Assert.Equal(new[] { "fir", "st" }, lines[0].Words!.Select(w => w.Text));
            Assert.Null(lines[0].Words![1].EndTime);
        }

        [Fact]
        public void RejectsOtherVersions()
        {
            Assert.Null(Lyricsfile.ParseLines("version: \"2.0\"\nlines: []"));
        }

        [Fact]
        public void RejectsInvalidYaml()
        {
            Assert.Null(Lyricsfile.ParseLines("version: \"1.0\"\nversion: \"1.0\""));
            Assert.Null(Lyricsfile.ParseLines("[not, a, mapping]"));
            Assert.Null(Lyricsfile.ParseLines(""));
            Assert.Null(Lyricsfile.ParseLines(null));
        }

        [Fact]
        public void SkipsLinesWithoutTextOrStart()
        {
            var lines = Lyricsfile.ParseLines("""
                version: "1.0"
                lines:
                  - text: no start
                  - start_ms: 1000
                  - text: bad start
                    start_ms: -5
                  - text: kept
                    start_ms: 2000
                """);

            Assert.Equal("kept", Assert.Single(lines!).Text);
        }

        [Fact]
        public void IgnoresEndBeforeStart()
        {
            var lines = Lyricsfile.ParseLines("""
                version: "1.0"
                lines:
                  - text: line
                    start_ms: 3000
                    end_ms: 1000
                """);

            Assert.Null(Assert.Single(lines!).EndTime);
        }
    }
}

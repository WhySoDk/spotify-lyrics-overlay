namespace spotify_lyrics_overlay.Tests
{
    public class LrcParserTests
    {
        [Fact]
        public void ParsesTimestampsAndText()
        {
            var lines = LyricsFactory.ParseLyrics("[00:12.50]first\n[01:02.00]second");

            Assert.Equal(2, lines.Count);
            Assert.Equal(12.5, lines[0].Time, 3);
            Assert.Equal("first", lines[0].Text);
            Assert.Equal(62, lines[1].Time, 3);
            Assert.Equal("second", lines[1].Text);
        }

        [Fact]
        public void AcceptsColonFractionAndNoFraction()
        {
            var lines = LyricsFactory.ParseLyrics("[00:12:34]colon\n[00:20]whole");

            Assert.Equal(12.34, lines[0].Time, 3);
            Assert.Equal(20, lines[1].Time, 3);
        }

        [Fact]
        public void RepeatsLinesWithSeveralTimestamps()
        {
            var lines = LyricsFactory.ParseLyrics("[00:12.00][01:30.00]chorus\n[00:40.00]verse");

            Assert.Equal(new[] { 12.0, 40.0, 90.0 }, lines.Select(l => l.Time));
            Assert.Equal(new[] { "chorus", "verse", "chorus" }, lines.Select(l => l.Text));
        }

        [Fact]
        public void HandlesCrlf()
        {
            var lines = LyricsFactory.ParseLyrics("[00:01.00]one\r\n[00:02.00]two\r\n");

            Assert.Equal(new[] { "one", "two" }, lines.Select(l => l.Text));
        }

        [Fact]
        public void IgnoresLeadingBom()
        {
            var lines = LyricsFactory.ParseLyrics("﻿[00:01.00]one\n[00:02.00]two");

            Assert.Equal(2, lines.Count);
            Assert.Equal(1, lines[0].Time, 3);
        }

        [Fact]
        public void SkipsTagsAndUntimedLines()
        {
            var lines = LyricsFactory.ParseLyrics("[ar: Artist]\n[ti: Title]\nno time here\n[00:05.00]sung");

            var line = Assert.Single(lines);
            Assert.Equal("sung", line.Text);
        }

        [Fact]
        public void PositiveOffsetShowsLyricsEarlier()
        {
            var lines = LyricsFactory.ParseLyrics("[offset:+500]\n[00:10.00]line");

            Assert.Equal(9.5, Assert.Single(lines).Time, 3);
        }

        [Fact]
        public void NegativeOffsetShowsLyricsLater()
        {
            var lines = LyricsFactory.ParseLyrics("[00:10.00]line\n[offset:-1500]");

            Assert.Equal(11.5, Assert.Single(lines).Time, 3);
        }

        [Fact]
        public void OffsetNeverMovesLinesBeforeZero()
        {
            var lines = LyricsFactory.ParseLyrics("[offset:2000]\n[00:01.00]line");

            Assert.Equal(0, Assert.Single(lines).Time, 3);
        }

        [Fact]
        public void TimestampTooBigToParseDoesNotThrow()
        {
            var lines = LyricsFactory.ParseLyrics("[99999999999:00.00]huge\n[00:01.00]fine");

            Assert.Equal("fine", Assert.Single(lines).Text);
        }

        [Fact]
        public void StripsWordTimestamps()
        {
            var lines = LyricsFactory.ParseLyrics("[00:01.00]<00:01.00>hello <00:01.50>world");

            Assert.Equal("hello world", Assert.Single(lines).Text);
        }

        [Fact]
        public void SortsByTimeKeepingOrderOfEqualTimes()
        {
            var lines = LyricsFactory.ParseLyrics("[00:05.00]b\n[00:01.00]a\n[00:05.00]c");

            Assert.Equal(new[] { "a", "b", "c" }, lines.Select(l => l.Text));
        }

        [Fact]
        public void FindsCurrentLine()
        {
            var lines = LyricsFactory.ParseLyrics("[00:01.00]a\n[00:05.00]b");

            Assert.Equal(-1, LyricsFactory.FindCurrentLineIndex(lines, 0.5));
            Assert.Equal(0, LyricsFactory.FindCurrentLineIndex(lines, 1));
            Assert.Equal(1, LyricsFactory.FindCurrentLineIndex(lines, 10));
        }
    }
}

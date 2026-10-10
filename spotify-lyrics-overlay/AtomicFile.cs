namespace spotify_lyrics_overlay
{
    internal static class AtomicFile
    {
        //write to a temp file first and swap it in, a crash mid-write leaves the old file intact
        public static void WriteAllText(string path, string contents)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        }
    }
}

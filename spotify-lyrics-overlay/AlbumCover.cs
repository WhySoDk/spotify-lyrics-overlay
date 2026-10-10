namespace spotify_lyrics_overlay
{
    //album cover images, downloaded once and kept in CacheDb.
    //the album color and the song pill ask for the same cover at the same time, they share one download
    internal static class AlbumCover
    {
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly Dictionary<string, Task<byte[]>> pending = new();

        //throws when the cover can't be downloaded
        public static Task<byte[]> GetBytesAsync(string url)
        {
            lock (pending)
            {
                if (pending.TryGetValue(url, out var task)) return task;
                task = loadAsync(url);
                pending[url] = task;
                return task;
            }
        }

        //decoded cover, null when it can't be loaded
        public static async Task<Bitmap?> LoadAsync(string url)
        {
            try
            {
                var bytes = await GetBytesAsync(url);
                return await Task.Run(() =>
                {
                    using var stream = new MemoryStream(bytes);
                    using var image = Image.FromStream(stream);
                    // copy so the bitmap doesn't depend on the stream
                    return new Bitmap(image);
                });
            }
            catch (Exception ex)
            {
                Log.Write($"Error loading album cover: {ex.Message}");
                return null;
            }
        }

        private static async Task<byte[]> loadAsync(string url)
        {
            try
            {
                byte[]? cached = null;
                try
                {
                    cached = await Task.Run(() => CacheDb.ReadCover(url));
                }
                catch (Exception ex)
                {
                    Log.Write($"Error reading album cover cache: {ex.Message}");
                }
                if (cached != null) return cached;

                var bytes = await httpClient.GetByteArrayAsync(url);
                try
                {
                    await Task.Run(() => CacheDb.WriteCover(url, bytes));
                }
                catch (Exception ex)
                {
                    Log.Write($"Error writing album cover cache: {ex.Message}");
                }
                return bytes;
            }
            finally
            {
                lock (pending) pending.Remove(url);
            }
        }
    }
}

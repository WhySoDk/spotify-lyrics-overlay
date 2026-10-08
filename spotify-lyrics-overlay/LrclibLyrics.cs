using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace spotify_lyrics_overlay
{
    public class LyricsResult
    {
        public string? SyncLyrics { get; set; }
        public string? PlainLyrics { get; set; }
        // lyrics color picked from the album cover, cached together with the lyrics
        public string? AlbumColorHex { get; set; }
    }

    public class LrcLibLyricsProvider
    {
        private static readonly Lazy<LrcLibLyricsProvider> lazy = new(() => new LrcLibLyricsProvider());
        public static LrcLibLyricsProvider Instance => lazy.Value;

        private const string CacheDirectory = "lyrics_cache";
        private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromSeconds(30);

        private readonly HttpClient httpClient;

        // null value = lyrics not found on lrclib
        private readonly Dictionary<string, LyricsResult?> memoryCache = new();
        private readonly Dictionary<string, Task<LyricsResult?>> pendingRequests = new();
        private readonly Dictionary<string, DateTime> failedRequests = new();

        private LrcLibLyricsProvider()
        {
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "SpotifyLyricsOverlay v1.1.0 (https://github.com/WhySoDk/spotify-lyrics-overlay)"
            );
        }


        private static string GetKey(string trackName, string artistName, int durationSeconds)
        {
            return $"{trackName}\n{artistName}\n{durationSeconds}".ToLowerInvariant();
        }

        public Task<LyricsResult?> GetLyricsAsync(string trackName, string artistName, int durationSeconds)
        {
            string key = GetKey(trackName, artistName, durationSeconds);

            lock (memoryCache)
            {
                if (memoryCache.TryGetValue(key, out var cached))
                    return Task.FromResult(cached);

                if (pendingRequests.TryGetValue(key, out var pending))
                    return pending;

                if (failedRequests.TryGetValue(key, out var failedAt) && DateTime.UtcNow - failedAt < FailedRetryDelay)
                    return Task.FromResult<LyricsResult?>(null);

                var task = LoadLyricsAsync(key, trackName, artistName, durationSeconds);
                pendingRequests[key] = task;
                return task;
            }
        }

        private async Task<LyricsResult?> LoadLyricsAsync(string key, string trackName, string artistName, int durationSeconds)
        {
            bool found = true;
            LyricsResult? result = ReadDiskCache(key);

            try
            {
                if (result == null)
                {
                    (found, result) = await FetchLyricsAsync(trackName, artistName, durationSeconds);
                    if (result != null) WriteDiskCache(key, result);
                }

                lock (memoryCache)
                {
                    if (found) memoryCache[key] = result;
                    else failedRequests[key] = DateTime.UtcNow;
                }
                return result;
            }
            finally
            {
                lock (memoryCache) pendingRequests.Remove(key);
            }
        }

        // returns found = false when the request failed and should be retried later
        private async Task<(bool found, LyricsResult? result)> FetchLyricsAsync(string trackName, string artistName, int durationSeconds)
        {
            string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artistName)}&duration={durationSeconds}";
            try
            {
                var response = await httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (true, null);
                }
                if (!response.IsSuccessStatusCode)
                {
                    return (false, null);
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;

                var result = new LyricsResult
                {
                    SyncLyrics = doc.TryGetProperty("syncedLyrics", out var sync) ? sync.GetString() : null,
                    PlainLyrics = doc.TryGetProperty("plainLyrics", out var plain) ? plain.GetString() : null
                };

                System.Diagnostics.Debug.WriteLine($"Fetched lyrics for {trackName} by {artistName}: {result.SyncLyrics ?? "No synced lyrics"}");
                return (true, result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching lyrics: {ex.Message}");
                return (false, null);
            }
        }

        //write back a result from GetLyricsAsync after changing it, e.g. adding the album color
        public void UpdateCache(string trackName, string artistName, int durationSeconds, LyricsResult result)
        {
            WriteDiskCache(GetKey(trackName, artistName, durationSeconds), result);
        }

        private static string GetCacheFilePath(string key)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
            return Path.Combine(CacheDirectory, hash + ".json");
        }

        private static LyricsResult? ReadDiskCache(string key)
        {
            try
            {
                string path = GetCacheFilePath(key);
                if (!File.Exists(path)) return null;
                return JsonSerializer.Deserialize<LyricsResult>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading lyrics cache: {ex.Message}");
                return null;
            }
        }

        private static void WriteDiskCache(string key, LyricsResult result)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);
                File.WriteAllText(GetCacheFilePath(key), JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing lyrics cache: {ex.Message}");
            }
        }
    }
}

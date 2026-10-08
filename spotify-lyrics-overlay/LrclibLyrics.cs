using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace spotify_lyrics_overlay
{
    public class LyricsResult
    {
        public string? SyncLyrics { get; set; }
        public string? PlainLyrics { get; set; }
        // Lyricsfile YAML, has line end times and word timings that the LRC lyrics don't
        public string? Lyricsfile { get; set; }
        // lyrics color picked from the album cover, cached together with the lyrics
        public string? AlbumColorHex { get; set; }
        public bool Instrumental { get; set; }
    }

    //Result is null when lrclib has no lyrics for the song, Failed when the request didn't go through
    public record LyricsLookup(LyricsResult? Result, bool Failed);

    public class LrcLibLyricsProvider
    {
        private static readonly Lazy<LrcLibLyricsProvider> lazy = new(() => new LrcLibLyricsProvider());
        public static LrcLibLyricsProvider Instance => lazy.Value;

        private const string CacheDirectory = "lyrics_cache";
        private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(5);
        private const int MaxAttempts = 3;
        // lrclib's own matching allows ±2 seconds
        private const double SearchDurationTolerance = 2;

        private readonly HttpClient httpClient;

        // null value = lyrics not found on lrclib
        private readonly Dictionary<string, LyricsResult?> memoryCache = new();
        private readonly Dictionary<string, Task<LyricsLookup>> pendingRequests = new();
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

        //artistName is every artist of the track, primaryArtist the first one
        public Task<LyricsLookup> GetLyricsAsync(string trackName, string artistName, string primaryArtist, int durationSeconds)
        {
            string key = GetKey(trackName, artistName, durationSeconds);

            lock (memoryCache)
            {
                if (memoryCache.TryGetValue(key, out var cached))
                    return Task.FromResult(new LyricsLookup(cached, false));

                if (pendingRequests.TryGetValue(key, out var pending))
                    return pending;

                if (failedRequests.TryGetValue(key, out var failedAt) && DateTime.UtcNow - failedAt < FailedRetryDelay)
                    return Task.FromResult(new LyricsLookup(null, true));

                var task = LoadLyricsAsync(key, trackName, artistName, primaryArtist, durationSeconds);
                pendingRequests[key] = task;
                return task;
            }
        }

        private async Task<LyricsLookup> LoadLyricsAsync(string key, string trackName, string artistName, string primaryArtist, int durationSeconds)
        {
            bool found = true;
            LyricsResult? result = ReadDiskCache(key);

            try
            {
                if (result == null)
                {
                    (found, result) = await FetchLyricsAsync(trackName, artistName, primaryArtist, durationSeconds);
                    if (result != null) WriteDiskCache(key, result);
                }

                lock (memoryCache)
                {
                    if (found) memoryCache[key] = result;
                    else failedRequests[key] = DateTime.UtcNow;
                }
                return new LyricsLookup(result, !found);
            }
            finally
            {
                lock (memoryCache) pendingRequests.Remove(key);
            }
        }

        // returns found = false when the request failed and should be retried later
        private async Task<(bool found, LyricsResult? result)> FetchLyricsAsync(string trackName, string artistName, string primaryArtist, int durationSeconds)
        {
            try
            {
                // Spotify lists every artist ("LONG:D, Kim Do Yeon") but lrclib usually only has the main one,
                // and /api/get needs the artist to match
                var artists = new List<string> { artistName };
                if (!string.IsNullOrWhiteSpace(primaryArtist) && !primaryArtist.Equals(artistName, StringComparison.OrdinalIgnoreCase))
                    artists.Add(primaryArtist);

                // lrclib often answers 503 instead of 404 when the artist doesn't match,
                // so a failed step doesn't stop the next one, it only matters if nothing is found
                bool failed = false;

                foreach (string artist in artists)
                {
                    string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artist)}&duration={durationSeconds}";
                    var (ok, json) = await GetJsonAsync(url);
                    failed |= !ok;
                    if (json != null)
                    {
                        var result = ReadRecord(JsonDocument.Parse(json).RootElement);
                        System.Diagnostics.Debug.WriteLine($"Fetched lyrics for {trackName} by {artist}: {result.SyncLyrics ?? "No synced lyrics"}");
                        return (true, result);
                    }
                }

                // the title can differ too, e.g. "(Feat. Kim Do Yeon)" vs "(Feat. Kim Doyeon of Weki Meki)",
                // search by the title without the featured artists and pick a record with the same duration
                string searchUrl = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(StripFeaturing(trackName))}&artist_name={Uri.EscapeDataString(artists[^1])}";
                var (searchOk, searchJson) = await GetJsonAsync(searchUrl);
                failed |= !searchOk;
                if (searchJson == null) return (!failed, null);

                var match = JsonDocument.Parse(searchJson).RootElement.EnumerateArray()
                    .Where(r => r.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                        && Math.Abs(d.GetDouble() - durationSeconds) <= SearchDurationTolerance)
                    .Select(ReadRecord)
                    .OrderByDescending(r => !string.IsNullOrWhiteSpace(r.SyncLyrics))
                    .FirstOrDefault();

                System.Diagnostics.Debug.WriteLine($"Searched lyrics for {trackName} by {artistName}: {match?.SyncLyrics ?? "No synced lyrics"}");
                return (match != null || !failed, match);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching lyrics: {ex.Message}");
                return (false, null);
            }
        }

        // ok = false when the request failed, json = null when lrclib has no match (404)
        private async Task<(bool ok, string? json)> GetJsonAsync(string url)
        {
            for (int attempt = 1; ; attempt++)
            {
                using var response = await httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return (true, null);
                if (response.IsSuccessStatusCode)
                    return (true, await response.Content.ReadAsStringAsync());

                // lrclib answers 503 when busy and 429 when rate limited, both say when to retry
                bool retryable = response.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable or System.Net.HttpStatusCode.TooManyRequests;
                if (!retryable || attempt >= MaxAttempts)
                    return (false, null);

                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                await Task.Delay(retryAfter < MaxRetryAfter ? retryAfter : MaxRetryAfter);
            }
        }

        private static LyricsResult ReadRecord(JsonElement doc)
        {
            return new LyricsResult
            {
                SyncLyrics = doc.TryGetProperty("syncedLyrics", out var sync) && sync.ValueKind == JsonValueKind.String ? sync.GetString() : null,
                PlainLyrics = doc.TryGetProperty("plainLyrics", out var plain) && plain.ValueKind == JsonValueKind.String ? plain.GetString() : null,
                Lyricsfile = doc.TryGetProperty("lyricsfile", out var lyricsfile) && lyricsfile.ValueKind == JsonValueKind.String ? lyricsfile.GetString() : null,
                Instrumental = doc.TryGetProperty("instrumental", out var instrumental) && instrumental.ValueKind == JsonValueKind.True
            };
        }

        private static readonly Regex FeaturingRegex = new(@"\s*[\(\[]\s*(feat\.?|ft\.?|featuring|with)\s[^\)\]]*[\)\]]", RegexOptions.IgnoreCase);

        //"All night (Feat. Kim Do Yeon)" -> "All night"
        private static string StripFeaturing(string trackName)
        {
            string stripped = FeaturingRegex.Replace(trackName, "").Trim();
            return stripped.Length > 0 ? stripped : trackName;
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

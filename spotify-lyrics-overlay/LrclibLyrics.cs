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
        public bool Instrumental { get; set; }
        // disk cache only: when lrclib was asked, results without synced lyrics are asked again after a while
        public DateTime? CheckedAt { get; set; }
        // disk cache only: lrclib had no lyrics for the song
        public bool NotFound { get; set; }
        // disk cache only: lyrics set by hand (a lrclib link or pasted lyrics), never looked up again
        public bool Manual { get; set; }
    }

    //Result is null when lrclib has no lyrics for the song, Failed when the request didn't go through
    public record LyricsLookup(LyricsResult? Result, bool Failed);

    public class LrcLibLyricsProvider
    {
        private static readonly Lazy<LrcLibLyricsProvider> lazy = new(() => new LrcLibLyricsProvider());
        public static LrcLibLyricsProvider Instance => lazy.Value;

        private const string CacheDirectory = "lyrics_cache";
        private static readonly TimeSpan FailedRetryDelay = TimeSpan.FromSeconds(30);
        // synced lyrics are kept forever, no lyrics or plain lyrics only are looked up again after this
        private static readonly TimeSpan IncompleteCacheDuration = TimeSpan.FromDays(7);
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
                $"SpotifyLyricsOverlay v{typeof(LrcLibLyricsProvider).Assembly.GetName().Version?.ToString(3)} (https://github.com/WhySoDk/spotify-lyrics-overlay)"
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
                {
                    DebugStatus.Show($"Cache hit (memory): {trackName} — {artistName}", cacheHit: true);
                    return Task.FromResult(new LyricsLookup(cached, false));
                }

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
            LyricsResult? cached = ReadDiskCache(key);
            LyricsResult? result = cached?.NotFound == true ? null : cached;

            try
            {
                if (cached != null && !IsExpired(cached))
                {
                    if (cached.Manual)
                        DebugStatus.Show($"Cache hit (set by hand): {trackName} — {artistName}, {Describe(result)}", cacheHit: true);
                    else if (IsComplete(cached))
                        DebugStatus.Show($"Cache hit: {trackName} — {artistName}", cacheHit: true);
                    else
                        DebugStatus.Show($"Cache: {Describe(result)}, expires in {DebugStatus.FormatDuration(cached.CheckedAt!.Value + IncompleteCacheDuration - DateTime.UtcNow)}");
                }
                else
                {
                    (found, result) = await FetchLyricsAsync(trackName, artistName, primaryArtist, durationSeconds);

                    if (!found)
                    {
                        // lrclib can't be reached, the expired entry is still better than nothing
                        if (cached != null)
                        {
                            found = true;
                            result = cached.NotFound ? null : cached;
                            DebugStatus.Show($"Lookup failed, using the expired cache: {Describe(result)}");
                        }
                        else
                        {
                            DebugStatus.Show($"Lookup failed, trying again in {FailedRetryDelay.TotalSeconds:0}s");
                        }
                    }
                    else
                    {
                        // keep the plain lyrics if they're gone now
                        if (result == null && cached?.NotFound == false) result = cached;

                        var entry = result ?? new LyricsResult { NotFound = true };
                        entry.CheckedAt = IsComplete(entry) ? null : DateTime.UtcNow;
                        WriteDiskCache(key, entry);
                        DebugStatus.Show(IsComplete(entry)
                            ? $"Found {Describe(result)}"
                            : $"Found {Describe(result)}, cached for {DebugStatus.FormatDuration(IncompleteCacheDuration)}");
                    }
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
                LyricsResult? best = null;
                int steps = artists.Count + 1;

                for (int i = 0; i < artists.Count; i++)
                {
                    string artist = artists[i];
                    string url = $"https://lrclib.net/api/get?track_name={Uri.EscapeDataString(trackName)}&artist_name={Uri.EscapeDataString(artist)}&duration={durationSeconds}";
                    var (ok, json) = await GetJsonAsync(url, $"{i + 1}/{steps} {(i == 0 ? "Simple query" : "Main artist query")}");
                    failed |= !ok;
                    if (json != null)
                    {
                        best = ReadRecord(JsonDocument.Parse(json).RootElement);
                        System.Diagnostics.Debug.WriteLine($"Fetched lyrics for {trackName} by {artist}: {best.SyncLyrics ?? "No synced lyrics"}");
                        if (HasSyncedLyrics(best) || best.Instrumental) return (true, best);
                        // lrclib can pick a record with only plain lyrics when another one of the same song is synced
                        break;
                    }
                }

                // the title can differ too, e.g. "(Feat. Kim Do Yeon)" vs "(Feat. Kim Doyeon of Weki Meki)"
                // or "- from the series Arcane" vs "(from the series Arcane)",
                // search by the base title and pick a record with the same duration
                string title = BaseTitle(trackName);
                string searchUrl = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}&artist_name={Uri.EscapeDataString(artists[^1])}";
                var (searchOk, searchJson) = await GetJsonAsync(searchUrl, $"{steps}/{steps} Search by title");
                failed |= !searchOk;

                var match = searchJson == null ? null : JsonDocument.Parse(searchJson).RootElement.EnumerateArray()
                    .Where(r => r.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                        && Math.Abs(d.GetDouble() - durationSeconds) <= SearchDurationTolerance
                        && r.TryGetProperty("trackName", out var name) && name.ValueKind == JsonValueKind.String
                        && BaseTitle(name.GetString()!).Equals(title, StringComparison.OrdinalIgnoreCase))
                    .Select(r => (record: ReadRecord(r),
                        sameTitle: r.GetProperty("trackName").GetString()!.Equals(trackName, StringComparison.OrdinalIgnoreCase),
                        durationOff: Math.Abs(r.GetProperty("duration").GetDouble() - durationSeconds)))
                    .OrderByDescending(c => HasSyncedLyrics(c.record))
                    .ThenByDescending(c => c.sameTitle)
                    .ThenBy(c => c.durationOff)
                    .Select(c => c.record)
                    .FirstOrDefault();

                if (match != null && (best == null || HasSyncedLyrics(match))) best = match;

                System.Diagnostics.Debug.WriteLine($"Searched lyrics for {trackName} by {artistName}: {best?.SyncLyrics ?? "No synced lyrics"}");
                return (best != null || !failed, best);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching lyrics: {ex.Message}");
                return (false, null);
            }
        }

        // ok = false when the request failed, json = null when lrclib has no match (404),
        // step names the request in the debug info
        private async Task<(bool ok, string? json)> GetJsonAsync(string url, string step)
        {
            for (int attempt = 1; ; attempt++)
            {
                DebugStatus.Show(attempt == 1 ? step : $"{step} (lrclib busy, try {attempt}/{MaxAttempts})");
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

        //what a lookup found, for the debug info
        private static string Describe(LyricsResult? result)
        {
            return result == null ? "not found"
                : HasSyncedLyrics(result) ? "synced lyrics"
                : result.Instrumental ? "instrumental"
                : !string.IsNullOrWhiteSpace(result.PlainLyrics) ? "plain lyrics only"
                : "no lyrics";
        }

        private static bool HasSyncedLyrics(LyricsResult result)
        {
            return !string.IsNullOrWhiteSpace(result.SyncLyrics);
        }

        //synced or instrumental, nothing better to find on lrclib
        private static bool IsComplete(LyricsResult result)
        {
            return !result.NotFound && (HasSyncedLyrics(result) || result.Instrumental);
        }

        //entries cached before CheckedAt existed count as expired
        private static bool IsExpired(LyricsResult cached)
        {
            return !cached.Manual && !IsComplete(cached) && (cached.CheckedAt is not DateTime checkedAt || DateTime.UtcNow - checkedAt > IncompleteCacheDuration);
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

        private static readonly Regex BracketsRegex = new(@"\([^)]*\)|\[[^\]]*\]");
        private static readonly Regex SuffixRegex = new(@"\s[-–—]\s.*$");
        private static readonly Regex SpacesRegex = new(@"\s+");

        //title without the parts that differ between releases, the duration tells the versions apart:
        //"All night (Feat. Kim Do Yeon)" -> "All night", "Ma Meilleure Ennemie - from the series Arcane" -> "Ma Meilleure Ennemie"
        private static string BaseTitle(string trackName)
        {
            string stripped = BracketsRegex.Replace(trackName, " ");
            stripped = SpacesRegex.Replace(SuffixRegex.Replace(stripped, ""), " ").Trim();
            return stripped.Length > 0 ? stripped : trackName.Trim();
        }

        private static readonly Regex RecordLinkRegex = new(@"^(?:(?:https?://)?(?:www\.)?lrclib\.net/(?:tracks|api/get)/)?(\d+)/?(?:[?#].*)?$", RegexOptions.IgnoreCase);

        //use the lrclib record of a link (or just its id) as the lyrics of the song,
        //returns an error message or null when it was saved
        public async Task<string?> SetFromLinkAsync(string trackName, string artistName, int durationSeconds, string link)
        {
            var match = RecordLinkRegex.Match(link.Trim());
            if (!match.Success || !long.TryParse(match.Groups[1].Value, out long id))
                return "That isn't a lrclib link, it should look like https://lrclib.net/tracks/36084871";

            string? json;
            try
            {
                bool ok;
                (ok, json) = await GetJsonAsync($"https://lrclib.net/api/get/{id}", $"Loading lrclib #{id}");
                if (!ok) return "Couldn't reach lrclib, try again in a moment";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading lrclib record: {ex.Message}");
                return "Couldn't reach lrclib, try again in a moment";
            }
            if (json == null) return $"lrclib has no record #{id}";

            var result = ReadRecord(JsonDocument.Parse(json).RootElement);
            SaveManual(trackName, artistName, durationSeconds, result);
            DebugStatus.Show($"Lyrics set from lrclib #{id}: {Describe(result)}");
            return null;
        }

        //use pasted LRC lyrics (e.g. from lyricsify) as the lyrics of the song,
        //returns an error message or null when they were saved
        public string? SetSyncedLyrics(string trackName, string artistName, int durationSeconds, string lrc)
        {
            lrc = lrc.Replace("\r\n", "\n").Trim();
            // tags like [ar: Artist] have no time and are skipped
            var lines = LyricsFactory.ParseLyrics(lrc);
            if (lines.Count == 0)
                return "No timed lines found, each line should start with a time like [00:20.50]";

            var result = new LyricsResult
            {
                SyncLyrics = lrc,
                PlainLyrics = string.Join("\n", lines.Select(l => l.Text)).Trim()
            };
            SaveManual(trackName, artistName, durationSeconds, result);
            DebugStatus.Show($"Pasted lyrics saved: {lines.Count(l => l.Text.Length > 0)} lines");
            return null;
        }

        private void SaveManual(string trackName, string artistName, int durationSeconds, LyricsResult result)
        {
            result.Manual = true;

            string key = GetKey(trackName, artistName, durationSeconds);
            WriteDiskCache(key, result);
            lock (memoryCache)
            {
                memoryCache[key] = result;
                failedRequests.Remove(key);
            }
        }

        //forget everything cached for the song, the next lookup asks lrclib again
        public void ClearCache(string trackName, string artistName, int durationSeconds)
        {
            string key = GetKey(trackName, artistName, durationSeconds);
            lock (memoryCache)
            {
                memoryCache.Remove(key);
                failedRequests.Remove(key);
            }

            try
            {
                File.Delete(GetCacheFilePath(key));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting lyrics cache: {ex.Message}");
            }
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

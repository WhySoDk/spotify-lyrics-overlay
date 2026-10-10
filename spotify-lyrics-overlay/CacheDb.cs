using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace spotify_lyrics_overlay
{
    //local cache database (lyrics_cache/cache.db) for lyrics, album colors and album covers.
    //the old cache (one json file per song) is imported the first time it's opened
    internal static class CacheDb
    {
        private const string CacheDirectory = "lyrics_cache";
        private static readonly string DbPath = Path.Combine(CacheDirectory, "cache.db");
        private static readonly string OldAlbumColorsPath = Path.Combine(CacheDirectory, "album_colors.json");
        // old cache files are moved here once imported, not deleted
        private static readonly string ImportedDirectory = Path.Combine(CacheDirectory, "imported_json");

        private static readonly Lazy<string> connectionString = new(open);

        private static SqliteConnection connect()
        {
            var connection = new SqliteConnection(connectionString.Value);
            connection.Open();
            return connection;
        }

        private static string open()
        {
            Directory.CreateDirectory(CacheDirectory);
            string cs = new SqliteConnectionStringBuilder { DataSource = DbPath }.ToString();

            using var connection = new SqliteConnection(cs);
            connection.Open();
            execute(connection, """
                PRAGMA journal_mode = WAL;

                -- key is the sha256 of "track\nartist\nduration" (lower case), the name of the old cache file.
                -- imported songs have no names until they're played again
                CREATE TABLE IF NOT EXISTS lyrics (
                    key TEXT PRIMARY KEY,
                    track_name TEXT,
                    artist_name TEXT,
                    duration INTEGER,
                    synced_lyrics TEXT,
                    plain_lyrics TEXT,
                    lyricsfile TEXT,
                    instrumental INTEGER NOT NULL DEFAULT 0,
                    not_found INTEGER NOT NULL DEFAULT 0,
                    manual INTEGER NOT NULL DEFAULT 0,
                    checked_at TEXT,
                    updated_at TEXT NOT NULL
                );

                -- cover url -> picked color as hex, "" when the cover has no usable color
                CREATE TABLE IF NOT EXISTS album_colors (
                    url TEXT PRIMARY KEY,
                    color TEXT NOT NULL
                );

                -- cover image file by url, Spotify cover urls never change their image
                CREATE TABLE IF NOT EXISTS covers (
                    url TEXT PRIMARY KEY,
                    image BLOB NOT NULL
                );
                """);

            importOldCache(connection);
            migrate(connection);
            return cs;
        }

        //one time changes to the cache, the step reached is kept in user_version
        private static void migrate(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";
            long version = (long)command.ExecuteScalar()!;

            // colors picked before the blue bias was removed are picked again
            if (version < 1) execute(connection, "DELETE FROM album_colors; PRAGMA user_version = 1;");
        }

        // ---------- lyrics ----------

        //also fills in the song names of an entry imported from the old cache
        public static LyricsResult? ReadLyrics(string key, string trackName, string artistName, int durationSeconds)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT synced_lyrics, plain_lyrics, lyricsfile, instrumental, not_found, manual, checked_at, track_name
                FROM lyrics WHERE key = $key
                """;
            command.Parameters.AddWithValue("$key", key);

            LyricsResult result;
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return null;
                result = new LyricsResult
                {
                    SyncLyrics = reader.IsDBNull(0) ? null : reader.GetString(0),
                    PlainLyrics = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Lyricsfile = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Instrumental = reader.GetBoolean(3),
                    NotFound = reader.GetBoolean(4),
                    Manual = reader.GetBoolean(5),
                    CheckedAt = reader.IsDBNull(6) ? null : DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
                };
                if (!reader.IsDBNull(7)) return result;
            }

            using var names = connection.CreateCommand();
            names.CommandText = "UPDATE lyrics SET track_name = $track, artist_name = $artist, duration = $duration WHERE key = $key";
            names.Parameters.AddWithValue("$key", key);
            names.Parameters.AddWithValue("$track", trackName);
            names.Parameters.AddWithValue("$artist", artistName);
            names.Parameters.AddWithValue("$duration", durationSeconds);
            names.ExecuteNonQuery();
            return result;
        }

        public static void WriteLyrics(string key, string trackName, string artistName, int durationSeconds, LyricsResult result)
        {
            using var connection = connect();
            writeLyrics(connection, key, trackName, artistName, durationSeconds, result, replace: true);
        }

        public static void DeleteLyrics(string key)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM lyrics WHERE key = $key";
            command.Parameters.AddWithValue("$key", key);
            command.ExecuteNonQuery();
        }

        //replace = false keeps an entry that is already there
        private static void writeLyrics(SqliteConnection connection, string key, string? trackName, string? artistName, int? durationSeconds,
            LyricsResult result, bool replace, SqliteTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT OR {(replace ? "REPLACE" : "IGNORE")} INTO lyrics
                    (key, track_name, artist_name, duration, synced_lyrics, plain_lyrics, lyricsfile, instrumental, not_found, manual, checked_at, updated_at)
                VALUES ($key, $track, $artist, $duration, $synced, $plain, $lyricsfile, $instrumental, $notFound, $manual, $checkedAt, $updatedAt)
                """;
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$track", (object?)trackName ?? DBNull.Value);
            command.Parameters.AddWithValue("$artist", (object?)artistName ?? DBNull.Value);
            command.Parameters.AddWithValue("$duration", (object?)durationSeconds ?? DBNull.Value);
            command.Parameters.AddWithValue("$synced", (object?)result.SyncLyrics ?? DBNull.Value);
            command.Parameters.AddWithValue("$plain", (object?)result.PlainLyrics ?? DBNull.Value);
            command.Parameters.AddWithValue("$lyricsfile", (object?)result.Lyricsfile ?? DBNull.Value);
            command.Parameters.AddWithValue("$instrumental", result.Instrumental);
            command.Parameters.AddWithValue("$notFound", result.NotFound);
            command.Parameters.AddWithValue("$manual", result.Manual);
            command.Parameters.AddWithValue("$checkedAt", (object?)result.CheckedAt?.ToUniversalTime() ?? DBNull.Value);
            command.Parameters.AddWithValue("$updatedAt", DateTime.UtcNow);
            command.ExecuteNonQuery();
        }

        // ---------- album colors ----------

        //null when the cover isn't cached
        public static string? ReadAlbumColor(string url)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT color FROM album_colors WHERE url = $url";
            command.Parameters.AddWithValue("$url", url);
            return command.ExecuteScalar() as string;
        }

        public static void WriteAlbumColor(string url, string color)
        {
            using var connection = connect();
            writeAlbumColor(connection, url, color, replace: true);
        }

        public static void DeleteAlbumColor(string url)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM album_colors WHERE url = $url";
            command.Parameters.AddWithValue("$url", url);
            command.ExecuteNonQuery();
        }

        private static void writeAlbumColor(SqliteConnection connection, string url, string color, bool replace, SqliteTransaction? transaction = null)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT OR {(replace ? "REPLACE" : "IGNORE")} INTO album_colors (url, color) VALUES ($url, $color)";
            command.Parameters.AddWithValue("$url", url);
            command.Parameters.AddWithValue("$color", color);
            command.ExecuteNonQuery();
        }

        // ---------- album covers ----------

        //null when the cover isn't cached
        public static byte[]? ReadCover(string url)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT image FROM covers WHERE url = $url";
            command.Parameters.AddWithValue("$url", url);
            return command.ExecuteScalar() as byte[];
        }

        public static void WriteCover(string url, byte[] image)
        {
            using var connection = connect();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO covers (url, image) VALUES ($url, $image)";
            command.Parameters.AddWithValue("$url", url);
            command.Parameters.AddWithValue("$image", image);
            command.ExecuteNonQuery();
        }

        // ---------- old cache import ----------

        //json files left in lyrics_cache are imported and moved to imported_json,
        //entries already in the database win
        private static void importOldCache(SqliteConnection connection)
        {
            var lyricsFiles = Directory.GetFiles(CacheDirectory, "*.json")
                .Where(path => !Path.GetFullPath(path).Equals(Path.GetFullPath(OldAlbumColorsPath), StringComparison.OrdinalIgnoreCase))
                .ToList();
            bool hasColors = File.Exists(OldAlbumColorsPath);
            if (lyricsFiles.Count == 0 && !hasColors) return;

            int imported = 0;
            var done = new List<string>();
            using (var transaction = connection.BeginTransaction())
            {
                foreach (var path in lyricsFiles)
                {
                    try
                    {
                        var result = JsonSerializer.Deserialize<LyricsResult>(File.ReadAllText(path));
                        if (result != null)
                        {
                            writeLyrics(connection, Path.GetFileNameWithoutExtension(path), null, null, null, result, replace: false, transaction);
                            imported++;
                        }
                        done.Add(path);
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"Skipped old lyrics cache file {Path.GetFileName(path)}: {ex.Message}");
                    }
                }

                if (hasColors)
                {
                    try
                    {
                        var colors = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(OldAlbumColorsPath));
                        foreach (var (url, color) in colors ?? new())
                        {
                            writeAlbumColor(connection, url, color, replace: false, transaction);
                        }
                        done.Add(OldAlbumColorsPath);
                    }
                    catch (Exception ex)
                    {
                        Log.Write($"Skipped old album color cache: {ex.Message}");
                    }
                }

                transaction.Commit();
            }

            Directory.CreateDirectory(ImportedDirectory);
            foreach (var path in done)
            {
                try
                {
                    File.Move(path, Path.Combine(ImportedDirectory, Path.GetFileName(path)), overwrite: true);
                }
                catch (Exception ex)
                {
                    Log.Write($"Error moving imported cache file {Path.GetFileName(path)}: {ex.Message}");
                }
            }
            Log.Write($"Imported {imported} songs from the old lyrics cache");
        }

        private static void execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}

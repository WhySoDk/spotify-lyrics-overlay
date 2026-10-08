using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using spotify_lyrics_overlay;
using SpotifyAPI.Web;

public class SpotifyConnector
{

    private const string RedirectUri = "http://127.0.0.1:5543/callback";
    private const string TokenCacheFilePath = "token.json";
    private static string ClientId = "";

    private static readonly object sync = new();
    private static Task<SpotifyClient>? clientTask;
    private static DateTime lastFailure = DateTime.MinValue;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    private class TokenCache
    {
        public string ClientId { get; set; } = "";
        public PKCETokenResponse? Token { get; set; }
    }

    public static Task<SpotifyClient> GetClientAsync()
    {
        lock (sync)
        {
            // retry the connection if the previous attempt failed, but not too often
            bool failed = clientTask != null && (clientTask.IsFaulted || clientTask.IsCanceled);
            if (clientTask == null || (failed && DateTime.UtcNow - lastFailure > RetryDelay))
            {
                clientTask = ConnectAsyncInternal();
            }
            return clientTask;
        }
    }

    private static async Task<SpotifyClient> ConnectAsyncInternal()
    {
        try
        {
            return await ConnectAsync();
        }
        catch
        {
            lastFailure = DateTime.UtcNow;
            throw;
        }
    }

    private static async Task<SpotifyClient> ConnectAsync()
    {
        var userConfig = ConfigManager.Instance.LoadConfig();
        ClientId = userConfig.apiKey;

        var token = await TryRefreshCachedTokenAsync() ?? await LoginAsync();
        SaveToken(token);

        var authenticator = new PKCEAuthenticator(ClientId, token);
        authenticator.TokenRefreshed += (_, refreshed) => SaveToken(refreshed);

        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
        return new SpotifyClient(config);
    }

    private static async Task<PKCETokenResponse?> TryRefreshCachedTokenAsync()
    {
        try
        {
            if (!File.Exists(TokenCacheFilePath)) return null;

            var cache = JsonSerializer.Deserialize<TokenCache>(File.ReadAllText(TokenCacheFilePath));
            if (cache?.Token?.RefreshToken == null || cache.ClientId != ClientId) return null;

            return await new OAuthClient().RequestToken(
                new PKCETokenRefreshRequest(ClientId, cache.Token.RefreshToken)
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cached token unusable, logging in again: {ex.Message}");
            return null;
        }
    }

    private static async Task<PKCETokenResponse> LoginAsync()
    {
        var (verifier, challenge) = PKCEUtil.GenerateCodes();

        var loginRequest = new LoginRequest(
            new Uri(RedirectUri),
            ClientId,
            LoginRequest.ResponseType.Code
        )
        {
            CodeChallengeMethod = "S256",
            CodeChallenge = challenge,
            Scope = new[] {
                Scopes.UserReadPlaybackState,
            }
        };

        var loginUri = loginRequest.ToUri();
        OpenBrowser(loginUri.ToString());

        string code = await WaitForCodeAsync();

        return await new OAuthClient().RequestToken(
            new PKCETokenRequest(ClientId, code, new Uri(RedirectUri), verifier)
        );
    }

    private static void SaveToken(PKCETokenResponse token)
    {
        try
        {
            var cache = new TokenCache { ClientId = ClientId, Token = token };
            File.WriteAllText(TokenCacheFilePath, JsonSerializer.Serialize(cache));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save token cache: {ex.Message}");
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open browser: {ex.Message}");
        }
    }

    private static async Task<string> WaitForCodeAsync()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:5543/callback/");
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync();
            var response = context.Response;

            string? code = context.Request.QueryString["code"];
            string responseString = code != null
                ? "<html><body>You can close this window.</body></html>"
                : "<html><body>Login failed. You can close this window.</body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();

            return code ?? throw new InvalidOperationException(
                $"Spotify login failed: {context.Request.QueryString["error"] ?? "no code returned"}"
            );
        }
        finally
        {
            listener.Stop();
        }
    }
}

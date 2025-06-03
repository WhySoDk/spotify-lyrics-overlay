using System.Diagnostics;
using System.Net;
using System.Text;
using spotify_lyrics_overlay;
using SpotifyAPI.Web;

public class SpotifyConnector
{

    private const string RedirectUri = "http://127.0.0.1:5543/callback";
    private static string ClientId = "";
    private static string? _verifier;
    private static string? _refreshToken;
    private static SpotifyClient? _client;

    private static readonly Lazy<Task<SpotifyClient>> lazyInstance = new(() => ConnectAsyncInternal());

    public static Task<SpotifyClient> GetClientAsync()
    {
        return lazyInstance.Value;
    }

    private static async Task<SpotifyClient> ConnectAsyncInternal()
    {
        var userConfig = ConfigManager.Instance.LoadConfig();
        ClientId = userConfig.apiKey;

        var (verifier, challenge) = PKCEUtil.GenerateCodes();
        _verifier = verifier;

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

        var tokenResponse = await new OAuthClient().RequestToken(
            new PKCETokenRequest(ClientId, code, new Uri(RedirectUri), _verifier)
        );

        _refreshToken = tokenResponse.RefreshToken;
        _client = new SpotifyClient(tokenResponse.AccessToken);

        return _client;
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

        var context = await listener.GetContextAsync();
        var response = context.Response;

        string code = context.Request.QueryString["code"]!;
        string responseString = "<html><body>You can close this window.</body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);

        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        response.OutputStream.Close();
        listener.Stop();

        return code;
    }
}

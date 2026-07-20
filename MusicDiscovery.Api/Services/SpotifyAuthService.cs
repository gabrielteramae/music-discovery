using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace MusicDiscovery.Api.Services;

public class SpotifyOptions
{
    public const string SectionName = "Spotify";
    public string ClientId { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string[] Scopes { get; set; } =
    [
        "playlist-read-private",
        "playlist-modify-private",
        "user-library-read",
        "user-top-read"
    ];
}

public class SpotifyAuthService : ISpotifyAuthService
{
    private const string AuthorizeEndpoint = "https://accounts.spotify.com/authorize";
    private const string TokenEndpoint = "https://accounts.spotify.com/api/token";

    private readonly HttpClient _http;
    private readonly SpotifyOptions _options;

    public SpotifyAuthService(HttpClient http, IOptions<SpotifyOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string BuildAuthorizationUrl(string state, string codeChallenge)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state,
            ["scope"] = string.Join(' ', _options.Scopes),
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = codeChallenge
        };

        var queryString = string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        return $"{AuthorizeEndpoint}?{queryString}";
    }

    public async Task<SpotifyTokenResponse> ExchangeCodeAsync(string code, string codeVerifier)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = codeVerifier
        };

        return await PostTokenRequestAsync(form);
    }

    public async Task<SpotifyTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _options.ClientId
        };

        return await PostTokenRequestAsync(form);
    }

    private async Task<SpotifyTokenResponse> PostTokenRequestAsync(Dictionary<string, string> form)
    {
        using var response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SpotifyTokenPayload>()
            ?? throw new InvalidOperationException("Resposta de token vazia da Spotify.");

        return new SpotifyTokenResponse(
            payload.AccessToken,
            payload.RefreshToken ?? string.Empty,
            DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn));
    }

    private class SpotifyTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = default!;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

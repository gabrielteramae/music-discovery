using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicDiscovery.Api.Data;
using MusicDiscovery.Api.Models;
using MusicDiscovery.Api.Services;

namespace MusicDiscovery.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISpotifyAuthService _spotifyAuth;
    private readonly ISyncService _sync;
    private readonly IAppTokenService _appToken;
    private readonly ITokenProtector _tokenProtector;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(
        ISpotifyAuthService spotifyAuth,
        ISyncService sync,
        IAppTokenService appToken,
        ITokenProtector tokenProtector,
        AppDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _spotifyAuth = spotifyAuth;
        _sync = sync;
        _appToken = appToken;
        _tokenProtector = tokenProtector;
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    // Passo 1: gera code_verifier/challenge (PKCE) e redireciona pro login da Spotify
    [HttpGet("login")]
    public IActionResult Login()
    {
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        // Em produção: guardar codeVerifier/state em cookie de sessão assinado (não em memória do processo)
        HttpContext.Session.SetString("spotify_code_verifier", codeVerifier);
        HttpContext.Session.SetString("spotify_state", state);

        var url = _spotifyAuth.BuildAuthorizationUrl(state, codeChallenge);
        return Redirect(url);
    }

    // Passo 2: callback da Spotify com o "code"
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var expectedState = HttpContext.Session.GetString("spotify_state");
        if (state != expectedState)
            return BadRequest("State inválido — possível CSRF.");

        var codeVerifier = HttpContext.Session.GetString("spotify_code_verifier")
            ?? throw new InvalidOperationException("Code verifier não encontrado na sessão.");

        var tokens = await _spotifyAuth.ExchangeCodeAsync(code, codeVerifier);
        var profile = await GetSpotifyProfileAsync(tokens.AccessToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SpotifyUserId == profile.Id);
        if (user is null)
        {
            user = new AppUser { SpotifyUserId = profile.Id };
            _db.Users.Add(user);
        }

        user.DisplayName = profile.DisplayName ?? profile.Id;
        user.Email = profile.Email;
        user.AccessTokenEncrypted = _tokenProtector.Protect(tokens.AccessToken);
        user.RefreshTokenEncrypted = _tokenProtector.Protect(tokens.RefreshToken);
        user.TokenExpiresAt = tokens.ExpiresAt;

        await _db.SaveChangesAsync(); // garante user.Id antes do sync/JWT

        // Primeira sincronização acontece já no login — próximas execuções ficam a cargo
        // de um endpoint/worker de refresh (fora do escopo deste MVP).
        var syncResult = await _sync.SyncUserLibraryAsync(user.Id, tokens.AccessToken);

        var jwt = _appToken.IssueToken(user.Id, user.SpotifyUserId, user.DisplayName);

        return Ok(new
        {
            token = jwt,
            user = new { user.Id, user.DisplayName },
            sync = syncResult
        });
    }

    private async Task<SpotifyProfileResponse> GetSpotifyProfileAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SpotifyProfileResponse>()
            ?? throw new InvalidOperationException("Resposta vazia ao buscar perfil do usuário na Spotify.");
    }

    private record SpotifyProfileResponse(string Id, string? DisplayName, string? Email);

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

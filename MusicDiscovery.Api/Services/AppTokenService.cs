using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MusicDiscovery.Api.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = default!;
    public string Issuer { get; set; } = "MusicDiscovery.Api";
    public string Audience { get; set; } = "MusicDiscovery.Web";
    public int ExpiryMinutes { get; set; } = 60;
}

public interface IAppTokenService
{
    /// <summary>Emite o JWT próprio da API, usado pelo Blazor pra autenticar chamadas depois do login Spotify.</summary>
    string IssueToken(int userId, string spotifyUserId, string displayName);
}

public class AppTokenService : IAppTokenService
{
    private readonly JwtOptions _options;

    public AppTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string IssueToken(int userId, string spotifyUserId, string displayName)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("spotify_user_id", spotifyUserId),
            new Claim(ClaimTypes.Name, displayName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Criptografa os tokens da Spotify (access/refresh) antes de salvar no banco, usando
/// o Data Protection API nativo do ASP.NET Core — não inventa cripto própria.
/// </summary>
public interface ITokenProtector
{
    string Protect(string rawToken);
    string Unprotect(string protectedToken);
}

public class TokenProtector : ITokenProtector
{
    private const string Purpose = "MusicDiscovery.SpotifyTokens.v1";
    private readonly IDataProtector _protector;

    public TokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string rawToken) => _protector.Protect(rawToken);
    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}

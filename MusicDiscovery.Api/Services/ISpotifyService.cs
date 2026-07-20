using MusicDiscovery.Api.Models;

namespace MusicDiscovery.Api.Services;

public interface ISpotifyAuthService
{
    /// <summary>Monta a URL de autorização (Authorization Code + PKCE).</summary>
    string BuildAuthorizationUrl(string state, string codeChallenge);

    /// <summary>Troca o "code" recebido no callback por access + refresh token.</summary>
    Task<SpotifyTokenResponse> ExchangeCodeAsync(string code, string codeVerifier);

    /// <summary>Renova o access token usando o refresh token salvo.</summary>
    Task<SpotifyTokenResponse> RefreshTokenAsync(string refreshToken);
}

public interface ISpotifyApiService
{
    Task<IReadOnlyList<SpotifyPlaylistDto>> GetUserPlaylistsAsync(string accessToken);
    Task<IReadOnlyList<SpotifyTrackDto>> GetPlaylistTracksAsync(string accessToken, string playlistId);
    Task<IReadOnlyList<SpotifyTrackDto>> GetLikedSongsAsync(string accessToken);
    Task<IReadOnlyDictionary<string, SpotifyAudioFeaturesDto>> GetAudioFeaturesAsync(string accessToken, IEnumerable<string> trackIds);
    Task<IReadOnlyList<SpotifyTrackDto>> GetRecommendationsAsync(string accessToken, SpotifyRecommendationSeed seed);
}

public record SpotifyTokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public record SpotifyPlaylistDto(string Id, string Name, string? Description, int TrackCount);

public record SpotifyTrackDto(string Id, string Name, string ArtistName, string? AlbumName, int DurationMs, string? PreviewUrl);

public record SpotifyAudioFeaturesDto(
    float Danceability, float Energy, float Valence, float Tempo,
    float Acousticness, float Instrumentalness, float Speechiness,
    float Loudness, int Key, int Mode);

// Sementes pro endpoint de recomendação: até 5 no total entre tracks/artistas/gêneros,
// mais os alvos opcionais de audio features (ex: "quero recomendações mais dançantes que a média")
public record SpotifyRecommendationSeed(
    IReadOnlyList<string> SeedTrackIds,
    float? TargetDanceability = null,
    float? TargetEnergy = null,
    float? TargetValence = null);

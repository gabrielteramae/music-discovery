using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MusicDiscovery.Api.Services;

public class SpotifyApiService : ISpotifyApiService
{
    private const string BaseUrl = "https://api.spotify.com/v1";
    private readonly HttpClient _http;

    public SpotifyApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<SpotifyPlaylistDto>> GetUserPlaylistsAsync(string accessToken)
    {
        var results = new List<SpotifyPlaylistDto>();
        var url = $"{BaseUrl}/me/playlists?limit=50";

        while (url is not null)
        {
            var page = await GetAsync<SpotifyPagedResponse<SpotifyPlaylistItem>>(accessToken, url);
            results.AddRange(page.Items.Select(p =>
                new SpotifyPlaylistDto(p.Id, p.Name, p.Description, p.Tracks.Total)));
            url = page.Next;
        }

        return results;
    }

    public async Task<IReadOnlyList<SpotifyTrackDto>> GetPlaylistTracksAsync(string accessToken, string playlistId)
    {
        var results = new List<SpotifyTrackDto>();
        var url = $"{BaseUrl}/playlists/{playlistId}/tracks?limit=100";

        while (url is not null)
        {
            var page = await GetAsync<SpotifyPagedResponse<SpotifyPlaylistTrackItem>>(accessToken, url);
            results.AddRange(page.Items
                .Where(i => i.Track is not null)
                .Select(i => MapTrack(i.Track!)));
            url = page.Next;
        }

        return results;
    }

    public async Task<IReadOnlyList<SpotifyTrackDto>> GetLikedSongsAsync(string accessToken)
    {
        var results = new List<SpotifyTrackDto>();
        var url = $"{BaseUrl}/me/tracks?limit=50";

        while (url is not null)
        {
            var page = await GetAsync<SpotifyPagedResponse<SpotifySavedTrackItem>>(accessToken, url);
            results.AddRange(page.Items.Select(i => MapTrack(i.Track)));
            url = page.Next;
        }

        return results;
    }

    public async Task<IReadOnlyDictionary<string, SpotifyAudioFeaturesDto>> GetAudioFeaturesAsync(
        string accessToken, IEnumerable<string> trackIds)
    {
        var result = new Dictionary<string, SpotifyAudioFeaturesDto>();

        // A API só aceita até 100 ids por chamada
        foreach (var chunk in trackIds.Chunk(100))
        {
            var ids = string.Join(',', chunk);
            var response = await GetAsync<SpotifyAudioFeaturesResponse>(
                accessToken, $"{BaseUrl}/audio-features?ids={ids}");

            foreach (var feature in response.AudioFeatures.Where(f => f is not null))
            {
                result[feature!.Id] = new SpotifyAudioFeaturesDto(
                    feature.Danceability, feature.Energy, feature.Valence, feature.Tempo,
                    feature.Acousticness, feature.Instrumentalness, feature.Speechiness,
                    feature.Loudness, feature.Key, feature.Mode);
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<SpotifyTrackDto>> GetRecommendationsAsync(string accessToken, SpotifyRecommendationSeed seed)
    {
        var seeds = seed.SeedTrackIds.Take(5); // limite da API: 5 seeds no total
        var query = $"seed_tracks={string.Join(',', seeds)}&limit=20";

        if (seed.TargetDanceability is { } danceability)
            query += $"&target_danceability={danceability.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (seed.TargetEnergy is { } energy)
            query += $"&target_energy={energy.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (seed.TargetValence is { } valence)
            query += $"&target_valence={valence.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

        var response = await GetAsync<SpotifyRecommendationsResponse>(accessToken, $"{BaseUrl}/recommendations?{query}");
        return response.Tracks.Select(MapTrack).ToList();
    }

    private async Task<T> GetAsync<T>(string accessToken, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException($"Resposta vazia da Spotify em {url}");
    }

    private static SpotifyTrackDto MapTrack(SpotifyTrackItem t) =>
        new(t.Id, t.Name, t.Artists.FirstOrDefault()?.Name ?? "Desconhecido", t.Album?.Name, t.DurationMs, t.PreviewUrl);

    // --- DTOs internos que espelham o shape cru da resposta da Spotify ---
    private record SpotifyPagedResponse<T>(List<T> Items, string? Next);
    private record SpotifyPlaylistItem(string Id, string Name, string? Description, SpotifyTrackCount Tracks);
    private record SpotifyTrackCount(int Total);
    private record SpotifyPlaylistTrackItem(SpotifyTrackItem? Track);
    private record SpotifySavedTrackItem(SpotifyTrackItem Track);
    private record SpotifyTrackItem(string Id, string Name, int DurationMs, string? PreviewUrl, List<SpotifyArtistItem> Artists, SpotifyAlbumItem? Album);
    private record SpotifyArtistItem(string Name);
    private record SpotifyAlbumItem(string Name);
    private record SpotifyAudioFeaturesResponse(List<SpotifyAudioFeatureItem?> AudioFeatures);
    private record SpotifyAudioFeatureItem(
        string Id, float Danceability, float Energy, float Valence, float Tempo,
        float Acousticness, float Instrumentalness, float Speechiness, float Loudness, int Key, int Mode);
    private record SpotifyRecommendationsResponse(List<SpotifyTrackItem> Tracks);
}

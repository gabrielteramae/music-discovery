using Microsoft.EntityFrameworkCore;
using MusicDiscovery.Api.Data;
using MusicDiscovery.Api.Models;

namespace MusicDiscovery.Api.Services;

public interface ISyncService
{
    /// <summary>
    /// Sincroniza playlists, liked songs e audio features de um usuário.
    /// Idempotente: pode rodar de novo sem duplicar dados (upsert por SpotifyId).
    /// </summary>
    Task<SyncResult> SyncUserLibraryAsync(int userId, string accessToken);
}

public record SyncResult(int PlaylistsSynced, int TracksSynced, int AudioFeaturesSynced);

public class SyncService : ISyncService
{
    private readonly AppDbContext _db;
    private readonly ISpotifyApiService _spotify;

    public SyncService(AppDbContext db, ISpotifyApiService spotify)
    {
        _db = db;
        _spotify = spotify;
    }

    public async Task<SyncResult> SyncUserLibraryAsync(int userId, string accessToken)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"Usuário {userId} não encontrado.");

        var tracksSynced = 0;
        var playlistsSynced = 0;

        // 1. Liked Songs — não é uma "playlist" na Spotify, mas tratamos como conjunto de tracks soltas
        var likedSongs = await _spotify.GetLikedSongsAsync(accessToken);
        tracksSynced += await UpsertTracksAsync(likedSongs);

        // 2. Playlists do usuário + faixas de cada uma
        var spotifyPlaylists = await _spotify.GetUserPlaylistsAsync(accessToken);

        foreach (var sp in spotifyPlaylists)
        {
            var playlist = await _db.Playlists
                .FirstOrDefaultAsync(p => p.SpotifyPlaylistId == sp.Id && p.UserId == userId);

            if (playlist is null)
            {
                playlist = new Playlist
                {
                    SpotifyPlaylistId = sp.Id,
                    UserId = userId,
                    Name = sp.Name,
                    Description = sp.Description
                };
                _db.Playlists.Add(playlist);
            }
            else
            {
                playlist.Name = sp.Name;
                playlist.Description = sp.Description;
            }
            playlist.LastSyncedAt = DateTimeOffset.UtcNow;

            var playlistTracks = await _spotify.GetPlaylistTracksAsync(accessToken, sp.Id);
            tracksSynced += await UpsertTracksAsync(playlistTracks);

            await _db.SaveChangesAsync(); // garante playlist.Id e track.Id gerados antes de linkar

            await LinkPlaylistTracksAsync(playlist.Id, playlistTracks);
            playlistsSynced++;
        }

        await _db.SaveChangesAsync();

        // 3. Audio features de tudo que ainda não tem features salvas — cobre liked songs + faixas
        // de todas as playlists sincronizadas acima, sem precisar re-somar as listas em memória.
        var trackIdsNeedingFeatures = await _db.Tracks
            .Where(t => t.AudioFeatures == null)
            .Select(t => t.SpotifyTrackId)
            .ToListAsync();

        var featuresSynced = await UpsertAudioFeaturesAsync(accessToken, trackIdsNeedingFeatures);

        return new SyncResult(playlistsSynced, tracksSynced, featuresSynced);
    }

    private async Task<int> UpsertTracksAsync(IReadOnlyList<SpotifyTrackDto> tracks)
    {
        var incomingIds = tracks.Select(t => t.Id).ToList();
        var existingIds = await _db.Tracks
            .Where(t => incomingIds.Contains(t.SpotifyTrackId))
            .Select(t => t.SpotifyTrackId)
            .ToListAsync();

        var newTracks = tracks
            .Where(t => !existingIds.Contains(t.Id))
            .Select(t => new Track
            {
                SpotifyTrackId = t.Id,
                Name = t.Name,
                ArtistName = t.ArtistName,
                AlbumName = t.AlbumName,
                DurationMs = t.DurationMs,
                PreviewUrl = t.PreviewUrl
            })
            .ToList();

        if (newTracks.Count > 0)
        {
            _db.Tracks.AddRange(newTracks);
            await _db.SaveChangesAsync();
        }

        return newTracks.Count;
    }

    private async Task LinkPlaylistTracksAsync(int playlistId, IReadOnlyList<SpotifyTrackDto> tracks)
    {
        var spotifyIds = tracks.Select(t => t.Id).ToList();
        var trackIdMap = await _db.Tracks
            .Where(t => spotifyIds.Contains(t.SpotifyTrackId))
            .ToDictionaryAsync(t => t.SpotifyTrackId, t => t.Id);

        var existingLinks = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .Select(pt => pt.TrackId)
            .ToListAsync();

        var position = 0;
        foreach (var track in tracks)
        {
            var trackId = trackIdMap[track.Id];
            if (!existingLinks.Contains(trackId))
            {
                _db.PlaylistTracks.Add(new PlaylistTrack
                {
                    PlaylistId = playlistId,
                    TrackId = trackId,
                    Position = position,
                    AddedAt = DateTimeOffset.UtcNow
                });
            }
            position++;
        }

        await _db.SaveChangesAsync();
    }

    private async Task<int> UpsertAudioFeaturesAsync(string accessToken, IReadOnlyList<string> spotifyTrackIds)
    {
        if (spotifyTrackIds.Count == 0) return 0;

        var featuresBySpotifyId = await _spotify.GetAudioFeaturesAsync(accessToken, spotifyTrackIds);
        var tracks = await _db.Tracks
            .Where(t => spotifyTrackIds.Contains(t.SpotifyTrackId))
            .ToListAsync();

        var count = 0;
        foreach (var track in tracks)
        {
            if (!featuresBySpotifyId.TryGetValue(track.SpotifyTrackId, out var f)) continue;

            _db.AudioFeatures.Add(new Models.AudioFeatures
            {
                TrackId = track.Id,
                Danceability = f.Danceability,
                Energy = f.Energy,
                Valence = f.Valence,
                Tempo = f.Tempo,
                Acousticness = f.Acousticness,
                Instrumentalness = f.Instrumentalness,
                Speechiness = f.Speechiness,
                Loudness = f.Loudness,
                Key = f.Key,
                Mode = f.Mode
            });
            count++;
        }

        await _db.SaveChangesAsync();
        return count;
    }
}

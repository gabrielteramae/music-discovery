namespace MusicDiscovery.Api.Models;

public class AppUser
{
    public int Id { get; set; }
    public string SpotifyUserId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Email { get; set; }

    // Tokens ficam guardados criptografados (ver ITokenProtector em Services)
    public string AccessTokenEncrypted { get; set; } = default!;
    public string RefreshTokenEncrypted { get; set; } = default!;
    public DateTimeOffset TokenExpiresAt { get; set; }

    public ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();
}

public class Playlist
{
    public int Id { get; set; }
    public string SpotifyPlaylistId { get; set; } = default!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}

public class Track
{
    public int Id { get; set; }
    public string SpotifyTrackId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string ArtistName { get; set; } = default!;
    public string? AlbumName { get; set; }
    public int DurationMs { get; set; }
    public string? PreviewUrl { get; set; }

    public AudioFeatures? AudioFeatures { get; set; }
    public ICollection<PlaylistTrack> PlaylistTracks { get; set; } = new List<PlaylistTrack>();
}

// Join entity: uma track pode estar em várias playlists, com posição e data própria em cada uma
public class PlaylistTrack
{
    public int PlaylistId { get; set; }
    public Playlist Playlist { get; set; } = default!;

    public int TrackId { get; set; }
    public Track Track { get; set; } = default!;

    public int Position { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

// Espelha o objeto "Audio Features" da Spotify Web API — é a base do motor de recomendação
public class AudioFeatures
{
    public int TrackId { get; set; }
    public Track Track { get; set; } = default!;

    public float Danceability { get; set; }
    public float Energy { get; set; }
    public float Valence { get; set; }       // "positividade" da faixa (0 = triste/negativa, 1 = eufórica)
    public float Tempo { get; set; }         // BPM
    public float Acousticness { get; set; }
    public float Instrumentalness { get; set; }
    public float Speechiness { get; set; }
    public float Loudness { get; set; }      // dB
    public int Key { get; set; }             // 0-11, notação de classe de altura (pitch class)
    public int Mode { get; set; }            // 0 = menor, 1 = maior
}

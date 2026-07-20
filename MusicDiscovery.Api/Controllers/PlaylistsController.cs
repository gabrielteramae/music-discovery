using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicDiscovery.Api.Data;
using MusicDiscovery.Api.Services;

namespace MusicDiscovery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/playlists")]
public class PlaylistsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly IPlaylistOrganizerService _organizer;

    public PlaylistsController(
        AppDbContext db,
        IRecommendationEngine recommendationEngine,
        IPlaylistOrganizerService organizer)
    {
        _db = db;
        _recommendationEngine = recommendationEngine;
        _organizer = organizer;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token sem claim de usuário."));

    [HttpGet]
    public async Task<IActionResult> GetPlaylists()
    {
        var playlists = await _db.Playlists
            .Where(p => p.UserId == CurrentUserId)
            .Select(p => new { p.Id, p.Name, TrackCount = p.PlaylistTracks.Count, p.LastSyncedAt })
            .ToListAsync();

        return Ok(playlists);
    }

    // Rankeia candidatos (ex: Liked Songs ainda não adicionadas) por similaridade com uma playlist
    [HttpGet("{playlistId:int}/recommendations")]
    public async Task<IActionResult> GetRecommendations(int playlistId, [FromQuery] int take = 15)
    {
        var playlistOwnedByUser = await _db.Playlists
            .AnyAsync(p => p.Id == playlistId && p.UserId == CurrentUserId);
        if (!playlistOwnedByUser)
            return NotFound("Playlist não encontrada para este usuário.");

        var playlistTracks = await _db.PlaylistTracks
            .Where(pt => pt.PlaylistId == playlistId)
            .Include(pt => pt.Track).ThenInclude(t => t.AudioFeatures)
            .Select(pt => pt.Track)
            .ToListAsync();

        if (playlistTracks.Count == 0)
            return NotFound("Playlist vazia ou não sincronizada ainda.");

        var alreadyInPlaylist = playlistTracks.Select(t => t.Id).ToHashSet();

        var candidates = await _db.Tracks
            .Include(t => t.AudioFeatures)
            .Where(t => !alreadyInPlaylist.Contains(t.Id) && t.AudioFeatures != null)
            .ToListAsync();

        var ranked = _recommendationEngine.RankBySimilarity(playlistTracks, candidates);

        return Ok(ranked.Take(take).Select(r => new
        {
            r.Track.Id,
            r.Track.Name,
            r.Track.ArtistName,
            Score = Math.Round(r.Score, 3)
        }));
    }

    // Analisa as "Liked Songs" (ou qualquer conjunto de faixas soltas) e sugere como dividir em playlists
    //
    // LIMITAÇÃO CONHECIDA: Track ainda não tem vínculo direto com usuário (só Playlist tem UserId).
    // Com um único usuário rodando localmente isso não é problema, mas pra suportar múltiplos usuários
    // de verdade precisa de uma entidade tipo "LikedTrack(UserId, TrackId)" pra isolar os dados.
    [HttpGet("suggest-organization")]
    public async Task<IActionResult> SuggestOrganization([FromQuery] int clusters = 4)
    {
        var likedTracks = await _db.Tracks
            .Include(t => t.AudioFeatures)
            .Where(t => t.AudioFeatures != null)
            .ToListAsync();

        var suggestions = _organizer.SuggestGroupings(likedTracks, clusters);

        return Ok(suggestions.Select(s => new
        {
            s.SuggestedName,
            TrackCount = s.Tracks.Count,
            s.Profile,
            SampleTracks = s.Tracks.Take(5).Select(t => new { t.Name, t.ArtistName })
        }));
    }
}
